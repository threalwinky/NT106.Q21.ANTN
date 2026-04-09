from __future__ import annotations

import json
import os
from typing import Any
from uuid import uuid4

from fastapi import WebSocket

from .bootstrap import ROOT_DIR
from .models import Participant, Room
from .store import ServerState

import sys

if str(ROOT_DIR / "shared" / "py") not in sys.path:
    sys.path.append(str(ROOT_DIR / "shared" / "py"))

from netrix_shared.config import load_jwt_config  # noqa: E402
from netrix_shared.security import hash_password, verify_password  # noqa: E402


JWT_CONFIG = load_jwt_config()


class RoomService:
    def __init__(self, state: ServerState) -> None:
        self.state = state

    def validate_mode(self, message: dict[str, Any]) -> str:
        access_mode = str(message.get("access_mode", "lan")).lower()
        if access_mode not in {"lan", "internet"}:
            raise ValueError("access_mode must be lan or internet")
        return access_mode

    def validate_role(self, role: str) -> str:
        normalized = role.lower()
        if normalized not in {"host", "controller", "viewer"}:
            raise ValueError("role must be host, controller, or viewer")
        return normalized

    def validate_token_if_needed(self, message: dict[str, Any], access_mode: str) -> str | None:
        if access_mode != "internet":
            return None

        token = str(message.get("token", "")).strip()
        if not token:
            raise ValueError("JWT token is required for internet mode")

        from netrix_shared.security import decode_access_token  # noqa: WPS433,E402

        claims = decode_access_token(token, JWT_CONFIG.secret, JWT_CONFIG.issuer)
        return str(claims["sub"])

    def next_room_id(self) -> str:
        candidate = f"{int.from_bytes(os.urandom(3), 'big') % 900000 + 100000}"
        while candidate in self.state.rooms:
            candidate = f"{int.from_bytes(os.urandom(3), 'big') % 900000 + 100000}"
        return candidate

    def build_room_state(self, room: Room) -> dict[str, Any]:
        return {
            "type": "room_state",
            "room_id": room.room_id,
            "host_client_id": room.host_client_id,
            "participants": [
                {
                    "client_id": participant.client_id,
                    "display_name": participant.display_name,
                    "role": participant.role,
                    "access_mode": participant.access_mode,
                    "is_host": participant.client_id == room.host_client_id,
                }
                for participant in room.participants.values()
            ],
        }

    async def send_json(self, websocket: WebSocket, message: dict[str, Any]) -> None:
        await websocket.send_text(json.dumps(message))

    async def send_error(self, websocket: WebSocket, detail: str) -> None:
        await self.send_json(websocket, {"type": "error", "detail": detail})

    async def broadcast(self, room: Room, message: dict[str, Any], exclude_client_id: str | None = None) -> None:
        recipients = [
            participant
            for participant in room.participants.values()
            if participant.client_id != exclude_client_id
        ]
        dead_clients: list[str] = []
        for participant in recipients:
            try:
                await self.send_json(participant.websocket, message)
            except Exception:  # noqa: BLE001
                dead_clients.append(participant.client_id)

        if dead_clients:
            async with self.state.state_lock:
                for client_id in dead_clients:
                    room.participants.pop(client_id, None)
                    self.state.participants_by_id.pop(client_id, None)

    async def broadcast_room_state(self, room: Room) -> None:
        await self.broadcast(room, self.build_room_state(room))

    async def add_participant_to_room(
        self,
        websocket: WebSocket,
        room: Room,
        display_name: str,
        role: str,
        access_mode: str,
        auth_subject: str | None,
    ) -> Participant:
        participant = Participant(
            client_id=str(uuid4()),
            websocket=websocket,
            display_name=display_name,
            role=role,
            access_mode=access_mode,
            auth_subject=auth_subject,
        )
        room.participants[participant.client_id] = participant
        self.state.participants_by_id[participant.client_id] = (room.room_id, role)
        return participant

    async def close_room(self, room: Room, reason: str) -> None:
        await self.broadcast(room, {"type": "room_closed", "detail": reason})
        for participant in list(room.participants.values()):
            try:
                await participant.websocket.close()
            except Exception:  # noqa: BLE001
                pass

    async def cleanup_connection(self, current_client_id: str | None, current_room_id: str | None) -> None:
        room_to_close: Room | None = None
        room_to_update: Room | None = None

        if current_client_id and current_room_id:
            async with self.state.state_lock:
                room = self.state.rooms.get(current_room_id)
                if room is not None:
                    participant = room.participants.pop(current_client_id, None)
                    self.state.participants_by_id.pop(current_client_id, None)
                    if participant is not None and participant.role == "host":
                        self.state.rooms.pop(current_room_id, None)
                        room_to_close = room
                    else:
                        if not room.participants:
                            self.state.rooms.pop(current_room_id, None)
                        else:
                            room_to_update = room

        if room_to_close is not None:
            await self.close_room(room_to_close, "Host disconnected")
        elif room_to_update is not None:
            await self.broadcast_room_state(room_to_update)

    async def create_room(
        self,
        websocket: WebSocket,
        message: dict[str, Any],
    ) -> tuple[Room, Participant, str]:
        display_name = str(message.get("display_name", "")).strip() or "Host"
        password = str(message.get("room_password", "")).strip()
        if len(password) < 4:
            raise ValueError("Room password must have at least 4 characters")

        access_mode = self.validate_mode(message)
        auth_subject = self.validate_token_if_needed(message, access_mode)

        async with self.state.state_lock:
            room_id = self.next_room_id()
            room = Room(
                room_id=room_id,
                password_hash=hash_password(password),
                host_client_id="",
                created_by=auth_subject or display_name,
            )
            participant = await self.add_participant_to_room(
                websocket=websocket,
                room=room,
                display_name=display_name,
                role="host",
                access_mode=access_mode,
                auth_subject=auth_subject,
            )
            room.host_client_id = participant.client_id
            self.state.rooms[room_id] = room

        return room, participant, access_mode

    async def join_room(
        self,
        websocket: WebSocket,
        message: dict[str, Any],
    ) -> tuple[Room, Participant, str]:
        room_id = str(message.get("room_id", "")).strip()
        password = str(message.get("room_password", "")).strip()
        display_name = str(message.get("display_name", "")).strip() or "Guest"
        role = self.validate_role(str(message.get("role", "viewer")))
        if role == "host":
            raise ValueError("Use create_room to become host")

        access_mode = self.validate_mode(message)
        auth_subject = self.validate_token_if_needed(message, access_mode)

        async with self.state.state_lock:
            room = self.state.rooms.get(room_id)
            if room is None:
                raise ValueError("Room not found")
            if not verify_password(password, room.password_hash):
                raise ValueError("Invalid room password")
            participant = await self.add_participant_to_room(
                websocket=websocket,
                room=room,
                display_name=display_name,
                role=role,
                access_mode=access_mode,
                auth_subject=auth_subject,
            )

        return room, participant, access_mode

