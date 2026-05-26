from __future__ import annotations

import asyncio
import ipaddress
import json
import os
import sys
from pathlib import Path
from typing import Any
from urllib.parse import urlparse

import httpx
from fastapi import FastAPI, Header, HTTPException, Query
from fastapi.responses import HTMLResponse

ROOT_DIR = Path(__file__).resolve().parents[1]
sys.path.append(str(ROOT_DIR / "shared"))

from netrix_shared.config import load_jwt_config, read_bool  # noqa: E402
from netrix_shared.security import decode_access_token  # noqa: E402


app = FastAPI(title="Netrix Load Balancer", version="1.0.5")
JWT_CONFIG = load_jwt_config()


def resolve_registry_path() -> Path:
    configured_path = os.getenv("NETRIX_LB_REGISTRY_PATH", "").strip()
    if configured_path:
        return Path(configured_path)
    return Path(__file__).resolve().with_name("servers.json")


REGISTRY_PATH = resolve_registry_path()


def default_registry() -> list[dict[str, str]]:
    return [
        {
            "name": "main-server-1",
            "health_url": os.getenv("NETRIX_MAIN_HEALTH_URL", "http://127.0.0.1:8000/health"),
        },
        {
            "name": "main-server-2",
            "health_url": os.getenv("NETRIX_MAIN2_HEALTH_URL", "http://127.0.0.1:8003/health"),
        },
        {
            "name": "main-server-3",
            "health_url": os.getenv("NETRIX_MAIN3_HEALTH_URL", "http://127.0.0.1:8004/health"),
        },
    ]


def load_registry() -> list[dict[str, str]]:
    if not REGISTRY_PATH.exists():
        REGISTRY_PATH.parent.mkdir(parents=True, exist_ok=True)
        REGISTRY_PATH.write_text(json.dumps(default_registry(), indent=2), encoding="utf-8")
    return json.loads(REGISTRY_PATH.read_text(encoding="utf-8"))


def is_public_ws_url(ws_url: str) -> bool:
    if not ws_url:
        return False

    try:
        parsed = urlparse(ws_url)
    except ValueError:
        return False

    if parsed.scheme not in {"ws", "wss"} or not parsed.hostname:
        return False

    if read_bool("NETRIX_ALLOW_PRIVATE_WS_URLS"):
        return True

    if parsed.hostname in {"localhost"}:
        return False

    try:
        address = ipaddress.ip_address(parsed.hostname)
    except ValueError:
        return True

    return not (address.is_loopback or address.is_private or address.is_link_local)


async def fetch_health(client: httpx.AsyncClient, server: dict[str, str]) -> dict[str, Any]:
    try:
        response = await client.get(server["health_url"], timeout=2.0)
        payload: dict[str, Any] = response.json()
        payload["server_name"] = server["name"]
        payload["health_url"] = server["health_url"]
        payload["ws_url_valid"] = is_public_ws_url(payload.get("ws_url", ""))
        if not payload["ws_url_valid"]:
            payload["status"] = "down"
            payload["error"] = "Main server returned a private or invalid ws_url"
        return payload
    except Exception as exc:  # noqa: BLE001
        return {
            "status": "down",
            "active_rooms": 10**9,
            "active_connections": 10**9,
            "ws_url": "",
            "ws_url_valid": False,
            "server_name": server["name"],
            "health_url": server["health_url"],
            "error": str(exc),
        }


async def fetch_all_health(registry: list[dict[str, str]]) -> list[dict[str, Any]]:
    async with httpx.AsyncClient() as client:
        results = await asyncio.gather(*[fetch_health(client, server) for server in registry])
    return list(results)


def choose_server(servers: list[dict[str, Any]], room_id: str | None = None) -> dict[str, Any]:
    healthy_servers = [
        server
        for server in servers
        if server.get("status") == "ok"
        and server.get("ws_url")
        and server.get("ws_url_valid")
    ]
    if not healthy_servers:
        raise HTTPException(status_code=503, detail="No healthy main server with a public WebSocket URL is available")

    pool = healthy_servers
    if room_id:
        room_affinity_servers = [
            server
            for server in healthy_servers
            if room_id in set(server.get("room_ids", []))
        ]
        if room_affinity_servers:
            pool = room_affinity_servers

    return min(pool, key=lambda server: (int(server.get("active_connections", 0)), int(server.get("active_rooms", 0))))


def _require_bearer(authorization: str | None) -> dict[str, Any]:
    if not authorization or not authorization.lower().startswith("bearer "):
        raise HTTPException(status_code=401, detail="Missing bearer token")
    token = authorization.split(" ", 1)[1]
    try:
        return decode_access_token(token, JWT_CONFIG.secret, JWT_CONFIG.issuer)
    except Exception as exc:  # noqa: BLE001
        raise HTTPException(status_code=401, detail=f"Invalid token: {exc}") from exc


@app.get("/health")
def health() -> dict[str, Any]:
    return {
        "status": "ok",
        "version": "1.0.5",
        "registry_size": len(load_registry()),
    }


@app.get("/servers")
async def servers() -> dict[str, Any]:
    registry = load_registry()
    snapshots = await fetch_all_health(registry)
    return {"servers": snapshots}


@app.get("/dashboard", response_class=HTMLResponse)
async def dashboard(authorization: str | None = Header(default=None)) -> str:
    _require_bearer(authorization)

    registry = load_registry()
    snapshots = await fetch_all_health(registry)

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
            <h1>Netrix 1.0.5 Load Balancer</h1>
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
async def select_server(
    room_id: str | None = Query(default=None),
    authorization: str | None = Header(default=None),
) -> dict[str, Any]:
    claims = _require_bearer(authorization)

    registry = load_registry()
    snapshots = await fetch_all_health(registry)
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
        access_log=read_bool("NETRIX_ACCESS_LOG"),
    )
