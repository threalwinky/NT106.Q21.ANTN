from __future__ import annotations

import json
import os
import sys
from pathlib import Path
from typing import Any
from urllib.error import URLError
from urllib.request import urlopen

from fastapi import FastAPI, Header, HTTPException, Query
from fastapi.responses import HTMLResponse

ROOT_DIR = Path(__file__).resolve().parents[1]
sys.path.append(str(ROOT_DIR / "shared"))

from netrix_shared.config import load_jwt_config  # noqa: E402
from netrix_shared.security import decode_access_token  # noqa: E402


app = FastAPI(title="Netrix Load Balancer", version="1.0.1")
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


def choose_server(servers: list[dict[str, Any]], room_id: str | None = None) -> dict[str, Any]:
    healthy_servers = [server for server in servers if server.get("status") == "ok" and server.get("ws_url")]
    if not healthy_servers:
        raise HTTPException(status_code=503, detail="No healthy main server is available")

    room_affinity_servers = healthy_servers
    if room_id:
        room_affinity_servers = [
            server
            for server in healthy_servers
            if room_id in set(server.get("room_ids", []))
        ]

    pool = room_affinity_servers or healthy_servers
    return min(pool, key=lambda server: (int(server.get("active_connections", 0)), int(server.get("active_rooms", 0))))


@app.get("/health")
def health() -> dict[str, Any]:
    return {
        "status": "ok",
        "version": "1.0.1",
        "registry_size": len(load_registry()),
    }


@app.get("/servers")
def servers() -> dict[str, Any]:
    registry = load_registry()
    snapshots = [fetch_health(server) for server in registry]
    return {"servers": snapshots}


@app.get("/dashboard", response_class=HTMLResponse)
def dashboard() -> str:
    registry = load_registry()
    snapshots = [fetch_health(server) for server in registry]

    rows = []
    for server in snapshots:
        room_ids = ", ".join(server.get("room_ids", [])) or "-"
        rows.append(
            "<tr>"
            f"<td>{server.get('server_name', '-')}</td>"
            f"<td>{server.get('status', '-')}</td>"
            f"<td>{server.get('active_connections', 0)}</td>"
            f"<td>{server.get('active_rooms', 0)}</td>"
            f"<td>{room_ids}</td>"
            f"<td>{server.get('ws_url', '-')}</td>"
            "</tr>"
        )

    body = "".join(rows) or "<tr><td colspan='6'>No servers registered.</td></tr>"
    return f"""
    <html>
        <head>
            <title>Netrix Load Balancer Dashboard</title>
            <style>
                body {{ font-family: Segoe UI, Arial, sans-serif; background: #111; color: #f5f5f5; margin: 24px; }}
                h1 {{ margin-bottom: 8px; }}
                p {{ color: #bbb; }}
                table {{ width: 100%; border-collapse: collapse; margin-top: 16px; }}
                th, td {{ border: 1px solid #333; padding: 10px; text-align: left; vertical-align: top; }}
                th {{ background: #1c1c1c; }}
                tr:nth-child(even) td {{ background: #171717; }}
                code {{ color: #f8d66d; }}
            </style>
        </head>
        <body>
            <h1>Netrix 1.0.1 Load Balancer</h1>
            <p>Room-aware selection is enabled. Pass <code>room_id</code> to keep reconnects on the same main server when possible.</p>
            <table>
                <thead>
                    <tr>
                        <th>Server</th>
                        <th>Status</th>
                        <th>Connections</th>
                        <th>Rooms</th>
                        <th>Room IDs</th>
                        <th>WebSocket URL</th>
                    </tr>
                </thead>
                <tbody>{body}</tbody>
            </table>
        </body>
    </html>
    """


@app.get("/select-server")
def select_server(
    room_id: str | None = Query(default=None),
    authorization: str | None = Header(default=None),
) -> dict[str, Any]:
    if not authorization or not authorization.lower().startswith("bearer "):
        raise HTTPException(status_code=401, detail="Missing bearer token")

    token = authorization.split(" ", 1)[1]
    try:
        claims = decode_access_token(token, JWT_CONFIG.secret, JWT_CONFIG.issuer)
    except Exception as exc:  # noqa: BLE001
        raise HTTPException(status_code=401, detail=f"Invalid token: {exc}") from exc

    registry = load_registry()
    snapshots = [fetch_health(server) for server in registry]
    selected = choose_server(snapshots, room_id)
    matched_room_affinity = bool(room_id and room_id in set(selected.get("room_ids", [])))
    return {
        "username": claims["sub"],
        "ws_url": selected["ws_url"],
        "selected_server": selected["server_name"],
        "active_rooms": selected.get("active_rooms", 0),
        "active_connections": selected.get("active_connections", 0),
        "room_id": room_id,
        "room_affinity": matched_room_affinity,
    }


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "app:app",
        host=os.getenv("NETRIX_LB_HOST", "0.0.0.0"),
        port=int(os.getenv("NETRIX_LB_PORT", "8002")),
        reload=False,
    )
