from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


@dataclass
class Participant:
    client_id: str
    websocket: Any
    display_name: str
    role: str
    access_mode: str
    auth_subject: str | None = None
    source_ip: str = ""
    network_scope: str = ""
    control_approved: bool = True
    realtime_ready: bool = False


@dataclass
class Room:
    room_id: str
    password_hash: str
    host_client_id: str
    created_by: str
    access_mode: str
    network_scope: str
    participants: dict[str, Participant] = field(default_factory=dict)
