from __future__ import annotations

import asyncio
import ipaddress
import json
import os
import sys
import time
import warnings
from contextlib import asynccontextmanager
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import httpx
from fastapi import FastAPI, Header, HTTPException, Query, Request, Response

ROOT_DIR = Path(__file__).resolve().parents[1]
sys.path.append(str(ROOT_DIR / "shared"))

from netrix_shared.config import load_jwt_config, read_bool, read_int  # noqa: E402
from netrix_shared.security import decode_access_token  # noqa: E402


JWT_CONFIG = load_jwt_config()
UNREACHABLE_LOAD = 10**9


def read_float(name: str, default: float) -> float:
    raw_value = os.getenv(name)
    if raw_value is None or not raw_value.strip():
        return default
    try:
        return float(raw_value)
    except ValueError:
        return default


VALID_STRATEGIES = {"least-connections", "weighted", "round-robin"}
SELECTION_STRATEGY = os.getenv("NETRIX_LB_STRATEGY", "least-connections").strip().lower()
if SELECTION_STRATEGY not in VALID_STRATEGIES:
    warnings.warn(
        f"NETRIX_LB_STRATEGY='{SELECTION_STRATEGY}' is not recognised; "
        f"falling back to 'least-connections'. Valid values: {sorted(VALID_STRATEGIES)}.",
        stacklevel=1,
    )
    SELECTION_STRATEGY = "least-connections"

HEALTH_INTERVAL_SECONDS = max(1, read_int("NETRIX_LB_HEALTH_INTERVAL", 3))
HEALTH_TIMEOUT_SECONDS = read_float("NETRIX_LB_HEALTH_TIMEOUT", 2.0)
HEALTH_RETRIES = max(0, read_int("NETRIX_LB_HEALTH_RETRIES", 1))
HEALTH_MAX_AGE_SECONDS = read_float("NETRIX_LB_HEALTH_MAX_AGE", 15.0)
AUTH_UPSTREAM = os.getenv("NETRIX_AUTH_UPSTREAM", "http://127.0.0.1:8001").rstrip("/")


def resolve_registry_path() -> Path:
    configured_path = os.getenv("NETRIX_LB_REGISTRY_PATH", "").strip()
    if configured_path:
        return Path(configured_path)
    return Path(__file__).resolve().with_name("servers.json")


REGISTRY_PATH = resolve_registry_path()


def default_registry() -> list[dict[str, Any]]:
    return [
        {
            "name": "main-server-1",
            "health_url": os.getenv("NETRIX_MAIN_HEALTH_URL", "http://127.0.0.1:8000/health"),
            "weight": 1,
        },
        {
            "name": "main-server-2",
            "health_url": os.getenv("NETRIX_MAIN2_HEALTH_URL", "http://127.0.0.1:8003/health"),
            "weight": 1,
        },
        {
            "name": "main-server-3",
            "health_url": os.getenv("NETRIX_MAIN3_HEALTH_URL", "http://127.0.0.1:8004/health"),
            "weight": 1,
        },
    ]


def load_registry() -> list[dict[str, Any]]:
    if not REGISTRY_PATH.exists():
        REGISTRY_PATH.parent.mkdir(parents=True, exist_ok=True)
        REGISTRY_PATH.write_text(json.dumps(default_registry(), indent=2), encoding="utf-8")
    return json.loads(REGISTRY_PATH.read_text(encoding="utf-8"))


def is_public_tcp_endpoint(endpoint: str) -> bool:
    if not endpoint:
        return False

    separator_index = endpoint.rfind(":")
    if separator_index <= 0 or separator_index == len(endpoint) - 1:
        return False

    host = endpoint[:separator_index].strip()
    port_text = endpoint[separator_index + 1 :].strip()
    if not host or not port_text.isdigit():
        return False
    port = int(port_text)
    if port <= 0 or port > 65535:
        return False

    if read_bool("NETRIX_ALLOW_PRIVATE_TCP_ENDPOINTS"):
        return True

    if host.lower() == "localhost":
        return False

    try:
        address = ipaddress.ip_address(host)
    except ValueError:
        return True

    return not (address.is_loopback or address.is_private or address.is_link_local)


@dataclass
class HealthCache:
    snapshots: list[dict[str, Any]] = field(default_factory=list)
    updated_monotonic: float = 0.0
    updated_wall: float = 0.0


class LoadBalancerState:
    def __init__(self) -> None:
        self.cache = HealthCache()
        self.client: httpx.AsyncClient | None = None
        self.poll_task: asyncio.Task[None] | None = None
        self.round_robin_index = 0


STATE = LoadBalancerState()


def cache_age_seconds() -> float:
    if STATE.cache.updated_monotonic == 0.0:
        return float("inf")
    return round(time.monotonic() - STATE.cache.updated_monotonic, 3)


async def fetch_health(server: dict[str, Any]) -> dict[str, Any]:
    assert STATE.client is not None, "HTTP client is not initialized"
    weight = max(1, int(server.get("weight", 1)))
    last_error = ""
    for attempt in range(HEALTH_RETRIES + 1):
        try:
            response = await STATE.client.get(server["health_url"], timeout=HEALTH_TIMEOUT_SECONDS)
            payload: dict[str, Any] = response.json()
            payload["server_name"] = server["name"]
            payload["health_url"] = server["health_url"]
            payload["weight"] = weight
            payload["tcp_endpoint_valid"] = is_public_tcp_endpoint(payload.get("tcp_endpoint", ""))
            if not payload["tcp_endpoint_valid"]:
                payload["status"] = "down"
                payload["error"] = "Main server returned a private or invalid tcp_endpoint"
            return payload
        except Exception as exc:  # noqa: BLE001
            last_error = str(exc)
            if attempt < HEALTH_RETRIES:
                await asyncio.sleep(0.2)

    return {
        "status": "down",
        "active_rooms": UNREACHABLE_LOAD,
        "active_connections": UNREACHABLE_LOAD,
        "tcp_endpoint": "",
        "tcp_endpoint_valid": False,
        "server_name": server["name"],
        "health_url": server["health_url"],
        "weight": weight,
        "error": last_error or "unreachable",
    }


async def refresh_health() -> list[dict[str, Any]]:
    registry = load_registry()
    snapshots = list(await asyncio.gather(*[fetch_health(server) for server in registry]))
    STATE.cache.snapshots = snapshots
    STATE.cache.updated_monotonic = time.monotonic()
    STATE.cache.updated_wall = time.time()
    return snapshots


async def get_snapshots() -> list[dict[str, Any]]:
    if not STATE.cache.snapshots or cache_age_seconds() > HEALTH_MAX_AGE_SECONDS:
        return await refresh_health()
    return STATE.cache.snapshots


async def health_poll_loop() -> None:
    while True:
        await asyncio.sleep(HEALTH_INTERVAL_SECONDS)
        try:
            await refresh_health()
        except Exception:  # noqa: BLE001
            # Keep polling across transient failures; a node's own snapshot marks it down.
            pass


def healthy_servers(snapshots: list[dict[str, Any]]) -> list[dict[str, Any]]:
    return [
        server
        for server in snapshots
        if server.get("status") == "ok" and server.get("tcp_endpoint") and server.get("tcp_endpoint_valid")
    ]


def _least_connections_key(server: dict[str, Any]) -> tuple[int, int]:
    return (int(server.get("active_connections", 0)), int(server.get("active_rooms", 0)))


def _weighted_key(server: dict[str, Any]) -> tuple[float, float]:
    weight = max(1, int(server.get("weight", 1)))
    return (
        int(server.get("active_connections", 0)) / weight,
        int(server.get("active_rooms", 0)) / weight,
    )


def _round_robin_pick(servers: list[dict[str, Any]]) -> dict[str, Any]:
    ordered = sorted(servers, key=lambda server: server["server_name"])
    chosen = ordered[STATE.round_robin_index % len(ordered)]
    STATE.round_robin_index += 1
    return chosen


def choose_server(snapshots: list[dict[str, Any]], room_id: str | None = None) -> dict[str, Any]:
    pool = healthy_servers(snapshots)
    if not pool:
        raise HTTPException(
            status_code=503,
            detail="No healthy main server with a public TCP endpoint is available",
        )

    if room_id:
        affinity_pool = [server for server in pool if room_id in set(server.get("room_ids", []))]
        if affinity_pool:
            # Reconnect to the node already holding the room, regardless of strategy.
            return min(affinity_pool, key=_least_connections_key)

    if SELECTION_STRATEGY == "round-robin":
        return _round_robin_pick(pool)

    if SELECTION_STRATEGY == "weighted":
        return min(pool, key=_weighted_key)

    # least-connections (default): pick the fewest-loaded node, but round-robin
    # among nodes tied at that minimum. The health cache can be a few seconds
    # stale, so without the tie-break a burst of requests in the same window
    # would all see equal loads and pile onto the same node.
    fewest = min(_least_connections_key(server) for server in pool)
    tied = [server for server in pool if _least_connections_key(server) == fewest]
    return tied[0] if len(tied) == 1 else _round_robin_pick(tied)


def require_bearer(authorization: str | None) -> dict[str, Any]:
    if not authorization or not authorization.lower().startswith("bearer "):
        raise HTTPException(status_code=401, detail="Missing bearer token")
    token = authorization.split(" ", 1)[1]
    try:
        return decode_access_token(token, JWT_CONFIG.secret, JWT_CONFIG.issuer)
    except Exception as exc:  # noqa: BLE001
        raise HTTPException(status_code=401, detail=f"Invalid token: {exc}") from exc


@asynccontextmanager
async def lifespan(_: FastAPI):
    STATE.client = httpx.AsyncClient(
        timeout=HEALTH_TIMEOUT_SECONDS,
        limits=httpx.Limits(max_keepalive_connections=20, keepalive_expiry=30.0),
    )
    try:
        await refresh_health()
    except Exception:  # noqa: BLE001
        pass
    STATE.poll_task = asyncio.create_task(health_poll_loop())
    try:
        yield
    finally:
        if STATE.poll_task is not None:
            STATE.poll_task.cancel()
            try:
                await STATE.poll_task
            except asyncio.CancelledError:
                pass
        if STATE.client is not None:
            await STATE.client.aclose()


app = FastAPI(title="Netrix Load Balancer", version="1.0.6", lifespan=lifespan)


@app.get("/health")
def health() -> dict[str, Any]:
    return {
        "status": "ok",
        "version": "1.0.6",
        "registry_size": len(load_registry()),
        "strategy": SELECTION_STRATEGY,
        "auth_upstream": AUTH_UPSTREAM,
        "health_interval_seconds": HEALTH_INTERVAL_SECONDS,
        "cache_age_seconds": cache_age_seconds(),
        "healthy_servers": len(healthy_servers(STATE.cache.snapshots)),
    }


@app.get("/servers")
async def servers() -> dict[str, Any]:
    snapshots = await get_snapshots()
    return {
        "strategy": SELECTION_STRATEGY,
        "cache_age_seconds": cache_age_seconds(),
        "updated_at": STATE.cache.updated_wall,
        "servers": snapshots,
    }


@app.get("/select-server")
async def select_server(
    room_id: str | None = Query(default=None),
    authorization: str | None = Header(default=None),
) -> dict[str, Any]:
    claims = require_bearer(authorization)

    snapshots = await get_snapshots()
    selected = choose_server(snapshots, room_id)
    matched_room_affinity = bool(room_id and room_id in set(selected.get("room_ids", [])))
    return {
        "username": claims["sub"],
        "tcp_endpoint": selected["tcp_endpoint"],
        "selected_server": selected["server_name"],
        "strategy": SELECTION_STRATEGY,
        "active_rooms": selected.get("active_rooms", 0),
        "active_connections": selected.get("active_connections", 0),
        "room_id": room_id,
        "room_affinity": matched_room_affinity,
        "cache_age_seconds": cache_age_seconds(),
    }


@app.api_route("/auth/{path:path}", methods=["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"])
async def auth_proxy(path: str, request: Request) -> Response:
    assert STATE.client is not None, "HTTP client is not initialized"
    upstream_url = f"{AUTH_UPSTREAM}/{path}"
    headers = {
        key: value
        for key, value in request.headers.items()
        if key.lower() not in {"host", "content-length"}
    }
    upstream_response = await STATE.client.request(
        method=request.method,
        url=upstream_url,
        params=request.query_params,
        headers=headers,
        content=await request.body(),
        timeout=HEALTH_TIMEOUT_SECONDS,
    )
    return Response(
        content=upstream_response.content,
        status_code=upstream_response.status_code,
        media_type=upstream_response.headers.get("content-type"),
    )


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "app:app",
        host=os.getenv("NETRIX_LB_HOST", "0.0.0.0"),
        port=int(os.getenv("NETRIX_LB_PORT", "8002")),
        reload=False,
        access_log=read_bool("NETRIX_ACCESS_LOG"),
    )
