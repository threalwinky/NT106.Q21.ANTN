from __future__ import annotations

import asyncio
import base64
import ipaddress
import json
import os
import sys
from typing import Any
from uuid import uuid4

from fastapi import WebSocket

from .bootstrap import ROOT_DIR, SHARED_DIR
from .models import Participant, Room
from .store import ServerState

if str(SHARED_DIR) not in sys.path:
    sys.path.append(str(SHARED_DIR))

from netrix_shared.config import load_jwt_config  # noqa: E402
from netrix_shared.security import hash_password, verify_password  # noqa: E402


JWT_CONFIG = load_jwt_config()


class RoomService:
    def __init__(self, state: ServerState) -> None:
        self.state = state

    @staticmethod
    def count_remote_participants(room: Room) -> int:
        return sum(1 for participant in room.participants.values() if participant.client_id != room.host_client_id)

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

    def validate_token(self, message: dict[str, Any]) -> str:
        token = str(message.get("token", "")).strip()
        if not token:
            raise ValueError("JWT token is required. Sign in before using Netrix.")

        from netrix_shared.security import decode_access_token  # noqa: WPS433,E402

        claims = decode_access_token(token, JWT_CONFIG.secret, JWT_CONFIG.issuer)
        return str(claims["sub"])

    def next_room_id(self) -> str:
        candidate = base64.b32encode(os.urandom(8)).decode("ascii").rstrip("=")[:12]
        while candidate in self.state.rooms:
            candidate = base64.b32encode(os.urandom(8)).decode("ascii").rstrip("=")[:12]
        return candidate

    def resolve_source_ip(self, websocket: WebSocket) -> str:
        for header_name in ("cf-connecting-ip", "x-forwarded-for", "x-real-ip"):
            header_value = websocket.headers.get(header_name, "").strip()
            if not header_value:
                continue
            if header_name == "x-forwarded-for":
                header_value = header_value.split(",", 1)[0].strip()
            if header_value:
                return header_value

        if websocket.client is not None and websocket.client.host:
            return str(websocket.client.host).strip()

        return ""

    def build_network_scope(self, source_ip: str, access_mode: str) -> str:
        if not source_ip:
            return f"{access_mode}:unknown"

        try:
            address = ipaddress.ip_address(source_ip)
        except ValueError:
            return f"{access_mode}:{source_ip}"

        if access_mode == "internet":
            return f"internet:{address.compressed}"

        if address.version == 4:
            if address.is_private or address.is_loopback or address.is_link_local:
                return f"lan:{ipaddress.ip_network(f'{address}/24', strict=False)}"
            return f"lan:{address.compressed}/32"

        if address.is_private or address.is_loopback or address.is_link_local:
            return f"lan:{ipaddress.ip_network(f'{address}/64', strict=False)}"
        return f"lan:{address.compressed}/128"

    def build_room_state(self, room: Room) -> dict[str, Any]:
        return {
            "type": "room_state",
            "room_id": room.room_id,
            "host_client_id": room.host_client_id,
            "access_mode": room.access_mode,
            "participants": [
                {
                    "client_id": participant.client_id,
                    "display_name": participant.display_name,
                    "role": participant.role,
                    "access_mode": participant.access_mode,
                    "is_host": participant.client_id == room.host_client_id,
                    "control_approved": participant.control_approved,
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
        auth_subject: str,
        source_ip: str,
        network_scope: str,
        control_approved: bool,
    ) -> Participant:
        participant = Participant(
            client_id=str(uuid4()),
            websocket=websocket,
            display_name=display_name,
            role=role,
            access_mode=access_mode,
            auth_subject=auth_subject,
            source_ip=source_ip,
            network_scope=network_scope,
            control_approved=control_approved,
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

    async def notify_host_about_controller_request(self, room: Room, participant: Participant) -> None:
        host = room.participants.get(room.host_client_id)
        if host is None:
            return

        await asyncio.wait_for(
            self.send_json(
                host.websocket,
                {
                    "type": "control_request",
                    "room_id": room.room_id,
                    "target_client_id": participant.client_id,
                    "display_name": participant.display_name,
                },
            ),
            timeout=5,
        )

    async def set_controller_permission(
        self,
        room: Room,
        host_client_id: str,
        target_client_id: str,
        approved: bool,
    ) -> None:
        if host_client_id != room.host_client_id:
            raise ValueError("Only the host can manage controller approvals")

        participant = room.participants.get(target_client_id)
        if participant is None:
            raise ValueError("Requested participant is no longer connected")

        if participant.role != "controller":
            raise ValueError("Selected participant is not waiting as a controller")

        participant.control_approved = approved
        if not approved:
            participant.role = "viewer"
            self.state.participants_by_id[target_client_id] = (room.room_id, "viewer")

        await self.send_json(
            participant.websocket,
            {
                "type": "control_granted" if approved else "control_denied",
                "detail": "Host approved remote control." if approved else "Host denied controller access. You were switched to viewer mode.",
                "room_id": room.room_id,
                "target_client_id": participant.client_id,
            },
        )

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
        auth_subject = self.validate_token(message)
        source_ip = self.resolve_source_ip(websocket)
        network_scope = self.build_network_scope(source_ip, access_mode)

        async with self.state.state_lock:
            room_id = self.next_room_id()
            room = Room(
                room_id=room_id,
                password_hash=hash_password(password),
                host_client_id="",
                created_by=auth_subject,
                access_mode=access_mode,
                network_scope=network_scope,
            )
            participant = await self.add_participant_to_room(
                websocket=websocket,
                room=room,
                display_name=display_name,
                role="host",
                access_mode=access_mode,
                auth_subject=auth_subject,
                source_ip=source_ip,
                network_scope=network_scope,
                control_approved=True,
            )
            room.host_client_id = participant.client_id
            self.state.rooms[room_id] = room

        return room, participant, access_mode

    async def join_room(
        self,
        websocket: WebSocket,
        message: dict[str, Any],
    ) -> tuple[Room, Participant, str]:
        room_id = str(message.get("room_id", "")).strip().upper()
        password = str(message.get("room_password", "")).strip()
        display_name = str(message.get("display_name", "")).strip() or "Guest"
        role = self.validate_role(str(message.get("role", "viewer")))
        if role == "host":
            raise ValueError("Use create_room to become host")

        access_mode = self.validate_mode(message)
        auth_subject = self.validate_token(message)
        source_ip = self.resolve_source_ip(websocket)
        network_scope = self.build_network_scope(source_ip, access_mode)

        async with self.state.state_lock:
            room = self.state.rooms.get(room_id)
            if room is None:
                raise ValueError("Room not found")
            if access_mode != room.access_mode:
                raise ValueError(f"Room was created in {room.access_mode.upper()} mode. Switch mode before joining.")
            if room.access_mode == "lan" and room.network_scope != network_scope:
                raise ValueError("LAN rooms can only be joined from the same network as the host.")
            if self.count_remote_participants(room) >= 1:
                raise ValueError("Room already has an active remote connection. Netrix 1.0.2 only allows one host and one remote peer.")
            if not verify_password(password, room.password_hash):
                raise ValueError("Invalid room password")
            participant = await self.add_participant_to_room(
                websocket=websocket,
                room=room,
                display_name=display_name,
                role=role,
                access_mode=access_mode,
                auth_subject=auth_subject,
                source_ip=source_ip,
                network_scope=network_scope,
                control_approved=role != "controller",
            )

        return room, participant, access_mode
