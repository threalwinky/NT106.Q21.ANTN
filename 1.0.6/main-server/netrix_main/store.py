from __future__ import annotations

import asyncio

from .models import Room


class ServerState:
    def __init__(self) -> None:
        self.rooms: dict[str, Room] = {}
        self.participants_by_id: dict[str, tuple[str, str]] = {}
        self.state_lock = asyncio.Lock()
        self.active_connections = 0

