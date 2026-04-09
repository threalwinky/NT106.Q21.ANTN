from __future__ import annotations

from dataclasses import dataclass, field

from fastapi import WebSocket


@dataclass
class Participant:
    client_id: str
    websocket: WebSocket
    display_name: str
    role: str
    access_mode: str
    auth_subject: str | None = None


@dataclass
class Room:
    room_id: str
    password_hash: str
    host_client_id: str
    created_by: str
    participants: dict[str, Participant] = field(default_factory=dict)

