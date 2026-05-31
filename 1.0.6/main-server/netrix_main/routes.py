from __future__ import annotations

import os
from typing import Any

from fastapi import APIRouter, FastAPI

from .service import RoomService
from .store import ServerState

DEFAULT_PUBLIC_TCP_ENDPOINT = "45.122.249.68:10311"


def register_routes(app: FastAPI, state: ServerState, room_service: RoomService) -> None:
    router = APIRouter()

    @router.get("/health")
    async def health() -> dict[str, Any]:
        public_tcp_endpoint = os.getenv("NETRIX_PUBLIC_TCP_ENDPOINT", DEFAULT_PUBLIC_TCP_ENDPOINT)
        server_name = os.getenv("NETRIX_SERVER_NAME", "main-server-1")
        return {
            "status": "ok",
            "version": "1.0.6",
            "server_name": server_name,
            "active_rooms": len(state.rooms),
            "active_connections": state.active_connections,
            "room_ids": sorted(state.rooms.keys()),
            "tcp_endpoint": public_tcp_endpoint,
            "supports_tcp_realtime_transport": True,
            "supports_secure_payload": True,
            "supports_file_transfer": True,
            "supports_binary_frame_transport": True,
            "supports_h264_frame_transport": True,
            "video_codec": "h264-openh264-with-jpeg-fallback",
            "target_stream_fps": 30,
            "supports_room_affinity": True,
            "requires_jwt": True,
            "room_id_format": "base32-12",
            "transport_security": "Raw TCP socket with length-prefixed encrypted payloads; use firewall rules or a TCP tunnel for Internet exposure.",
        }

    app.include_router(router)
