from __future__ import annotations

import asyncio
import json
import os
from typing import Any

from fastapi import APIRouter, FastAPI, WebSocket, WebSocketDisconnect

from .service import RoomService
from .store import ServerState

DEFAULT_PUBLIC_WS_URL = "wss://main.threalwinky.id.vn/ws"


def register_routes(app: FastAPI, state: ServerState, room_service: RoomService) -> None:
    router = APIRouter()

    @router.get("/health")
    async def health() -> dict[str, Any]:
        public_ws_url = os.getenv("NETRIX_PUBLIC_WS_URL", DEFAULT_PUBLIC_WS_URL)
        server_name = os.getenv("NETRIX_SERVER_NAME", "main-server-1")
        return {
            "status": "ok",
            "version": "1.0.2",
            "server_name": server_name,
            "active_rooms": len(state.rooms),
            "active_connections": state.active_connections,
            "room_ids": sorted(state.rooms.keys()),
            "ws_url": public_ws_url,
            "supports_secure_payload": True,
            "supports_file_transfer": True,
            "supports_room_affinity": True,
            "requires_jwt": True,
            "room_id_format": "base32-12",
            "transport_security": "Use WSS via Cloudflare Tunnel or ngrok for Internet mode.",
        }

    @router.websocket("/ws")
    async def websocket_endpoint(websocket: WebSocket) -> None:
        await websocket.accept()
        state.active_connections += 1

        current_client_id: str | None = None
        current_room_id: str | None = None

        await room_service.send_json(
            websocket,
            {
                "type": "hello",
                "message": "Netrix main server ready",
                "supported_modes": ["lan", "internet"],
            },
        )

        try:
            while True:
                raw_message = await websocket.receive_text()
                message = json.loads(raw_message)
                message_type = str(message.get("type", "")).strip().lower()

                if message_type == "ping":
                    await room_service.send_json(websocket, {"type": "pong"})
                    continue

                if message_type in {"create_room", "join_room"} and current_client_id and current_room_id:
                    raise ValueError("Disconnect from the current room before creating or joining another room.")

                if message_type == "create_room":
                    room, participant, access_mode = await room_service.create_room(websocket, message)
                    current_client_id = participant.client_id
                    current_room_id = room.room_id

                    await room_service.send_json(
                        websocket,
                        {
                            "type": "room_created",
                            "room_id": room.room_id,
                            "client_id": participant.client_id,
                            "role": "host",
                            "access_mode": access_mode,
                        },
                    )
                    await room_service.broadcast_room_state(room)
                    continue

                if message_type == "join_room":
                    room, participant, access_mode = await room_service.join_room(websocket, message)
                    current_client_id = participant.client_id
                    current_room_id = room.room_id

                    await room_service.send_json(
                        websocket,
                        {
                            "type": "joined_room",
                            "room_id": room.room_id,
                            "client_id": participant.client_id,
                            "role": participant.role,
                            "access_mode": access_mode,
                            "host_client_id": room.host_client_id,
                            "control_approved": participant.control_approved,
                        },
                    )
                    await room_service.broadcast_room_state(room)
                    if participant.role == "controller" and not participant.control_approved:
                        asyncio.create_task(
                            room_service.notify_host_about_controller_request(room, participant)
                        )
                    continue

                if not current_client_id or not current_room_id:
                    raise ValueError("Join or create a room before sending data")

                room = state.rooms.get(current_room_id)
                if room is None:
                    raise ValueError("Room is no longer available")

                participant = room.participants.get(current_client_id)
                if participant is None:
                    raise ValueError("Participant state is not available")

                if message_type == "frame":
                    if participant.role != "host":
                        raise ValueError("Only host can send screen frames")
                    await room_service.broadcast(
                        room,
                        {
                            "type": "frame",
                            "jpeg_base64": message.get("jpeg_base64", ""),
                            "width": int(message.get("width", 0)),
                            "height": int(message.get("height", 0)),
                            "sent_at": message.get("sent_at"),
                        },
                        exclude_client_id=current_client_id,
                    )
                    continue

                if message_type == "input":
                    if participant.role != "controller":
                        raise ValueError("Only a controller can send input events")
                    if not participant.control_approved:
                        raise ValueError("Controller approval is still pending")
                    host = room.participants.get(room.host_client_id)
                    if host is None:
                        raise ValueError("Host is not connected")
                    await room_service.send_json(
                        host.websocket,
                        {
                            "type": "input",
                            "event": message.get("event"),
                            "x_ratio": message.get("x_ratio"),
                            "y_ratio": message.get("y_ratio"),
                            "button": message.get("button"),
                            "delta": message.get("delta"),
                            "key_code": message.get("key_code"),
                            "sender": participant.display_name,
                        },
                    )
                    continue

                if message_type == "control_decision":
                    if participant.role != "host":
                        raise ValueError("Only the host can approve or deny controller access")

                    target_client_id = str(message.get("target_client_id", "")).strip()
                    if not target_client_id:
                        raise ValueError("target_client_id is required")

                    await room_service.set_controller_permission(
                        room=room,
                        host_client_id=current_client_id,
                        target_client_id=target_client_id,
                        approved=bool(message.get("approved", False)),
                    )
                    await room_service.broadcast_room_state(room)
                    continue

                if message_type == "chat":
                    text = str(message.get("text", "")).strip()
                    if not text:
                        raise ValueError("Chat message cannot be empty")
                    await room_service.broadcast(
                        room,
                        {
                            "type": "chat",
                            "text": text,
                            "sender": participant.display_name,
                            "role": participant.role,
                        },
                    )
                    continue

                if message_type in {"file_offer", "file_chunk", "file_complete"}:
                    transfer_id = str(message.get("transfer_id", "")).strip()
                    if not transfer_id:
                        raise ValueError("transfer_id is required")

                    if message_type == "file_offer":
                        file_name = str(message.get("file_name", "")).strip()
                        if not file_name:
                            raise ValueError("file_name is required")

                    await room_service.broadcast(
                        room,
                        message,
                        exclude_client_id=current_client_id,
                    )
                    continue

                if message_type == "secure_payload":
                    channel = str(message.get("channel", "")).strip().lower()
                    nonce_base64 = str(message.get("nonce_base64", "")).strip()
                    ciphertext_base64 = str(message.get("ciphertext_base64", "")).strip()
                    if channel not in {"frame", "input", "chat", "file_offer", "file_chunk", "file_complete"}:
                        raise ValueError("Unsupported secure payload channel")
                    if not nonce_base64 or not ciphertext_base64:
                        raise ValueError("Encrypted payload is incomplete")

                    secure_message = {
                        "type": "secure_payload",
                        "channel": channel,
                        "nonce_base64": nonce_base64,
                        "ciphertext_base64": ciphertext_base64,
                    }

                    if channel == "frame":
                        if participant.role != "host":
                            raise ValueError("Only host can send screen frames")
                        await room_service.broadcast(room, secure_message, exclude_client_id=current_client_id)
                        continue

                    if channel == "input":
                        if participant.role != "controller":
                            raise ValueError("Only a controller can send input events")
                        if not participant.control_approved:
                            raise ValueError("Controller approval is still pending")
                        host = room.participants.get(room.host_client_id)
                        if host is None:
                            raise ValueError("Host is not connected")
                        await room_service.send_json(host.websocket, secure_message)
                        continue

                    if channel == "chat":
                        await room_service.broadcast(room, secure_message)
                        continue

                    await room_service.broadcast(room, secure_message, exclude_client_id=current_client_id)
                    continue

                raise ValueError(f"Unsupported message type: {message_type}")

        except WebSocketDisconnect:
            pass
        except Exception as exc:  # noqa: BLE001
            await room_service.send_error(websocket, str(exc))
        finally:
            state.active_connections = max(0, state.active_connections - 1)
            await room_service.cleanup_connection(current_client_id, current_room_id)

    app.include_router(router)
