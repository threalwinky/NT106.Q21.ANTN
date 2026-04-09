from __future__ import annotations

import json
import os
import sys
from pathlib import Path
from typing import Any
from urllib.error import URLError
from urllib.request import urlopen

from fastapi import FastAPI, Header, HTTPException

ROOT_DIR = Path(__file__).resolve().parents[1]
sys.path.append(str(ROOT_DIR / "shared" / "py"))

from netrix_shared.config import load_jwt_config  # noqa: E402
from netrix_shared.security import decode_access_token  # noqa: E402


app = FastAPI(title="Netrix Load Balancer", version="1.0.0")
JWT_CONFIG = load_jwt_config()
REGISTRY_PATH = Path(__file__).resolve().with_name("servers.json")


def default_registry() -> list[dict[str, str]]:
    return [
        {
            "name": "main-server-1",
            "health_url": os.getenv("NETRIX_MAIN_HEALTH_URL", "http://127.0.0.1:8000/health"),
        }
    ]


def load_registry() -> list[dict[str, str]]:
    if not REGISTRY_PATH.exists():
        REGISTRY_PATH.write_text(json.dumps(default_registry(), indent=2), encoding="utf-8")
    return json.loads(REGISTRY_PATH.read_text(encoding="utf-8"))


def fetch_health(server: dict[str, str]) -> dict[str, Any]:
    try:
        with urlopen(server["health_url"], timeout=2) as response:  # noqa: S310
            payload = json.loads(response.read().decode("utf-8"))
            payload["server_name"] = server["name"]
            payload["health_url"] = server["health_url"]
            return payload
    except (URLError, TimeoutError, json.JSONDecodeError) as exc:
        return {
            "status": "down",
            "active_rooms": 10**9,
            "active_connections": 10**9,
            "ws_url": "",
            "server_name": server["name"],
            "health_url": server["health_url"],
            "error": str(exc),
        }


def choose_server(servers: list[dict[str, Any]]) -> dict[str, Any]:
    healthy_servers = [server for server in servers if server.get("status") == "ok" and server.get("ws_url")]
    if not healthy_servers:
        raise HTTPException(status_code=503, detail="No healthy main server is available")
    return min(
        healthy_servers,
        key=lambda server: (int(server.get("active_connections", 0)), int(server.get("active_rooms", 0))),
    )


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.get("/servers")
def servers() -> dict[str, Any]:
    registry = load_registry()
    snapshots = [fetch_health(server) for server in registry]
    return {"servers": snapshots}


@app.get("/select-server")
def select_server(authorization: str | None = Header(default=None)) -> dict[str, Any]:
    if not authorization or not authorization.lower().startswith("bearer "):
        raise HTTPException(status_code=401, detail="Missing bearer token")

    token = authorization.split(" ", 1)[1]
    try:
        claims = decode_access_token(token, JWT_CONFIG.secret, JWT_CONFIG.issuer)
    except Exception as exc:  # noqa: BLE001
        raise HTTPException(status_code=401, detail=f"Invalid token: {exc}") from exc

    registry = load_registry()
    snapshots = [fetch_health(server) for server in registry]
    selected = choose_server(snapshots)
    return {
        "username": claims["sub"],
        "ws_url": selected["ws_url"],
        "selected_server": selected["server_name"],
        "active_rooms": selected.get("active_rooms", 0),
        "active_connections": selected.get("active_connections", 0),
    }


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "app:app",
        host=os.getenv("NETRIX_LB_HOST", "0.0.0.0"),
        port=int(os.getenv("NETRIX_LB_PORT", "8002")),
        reload=False,
    )
