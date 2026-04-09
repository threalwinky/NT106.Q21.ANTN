from __future__ import annotations

import json
import os
from typing import Any

from fastapi import APIRouter, FastAPI, WebSocket, WebSocketDisconnect

from .service import RoomService
from .store import ServerState


def register_routes(app: FastAPI, state: ServerState, room_service: RoomService) -> None:
    router = APIRouter()

    @router.get("/health")
    async def health() -> dict[str, Any]:
        public_ws_url = os.getenv("NETRIX_PUBLIC_WS_URL", "ws://127.0.0.1:8000/ws")
        return {
            "status": "ok",
            "active_rooms": len(state.rooms),
            "active_connections": state.active_connections,
            "ws_url": public_ws_url,
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
                        },
                    )
                    await room_service.broadcast_room_state(room)
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

                raise ValueError(f"Unsupported message type: {message_type}")

        except WebSocketDisconnect:
            pass
        except Exception as exc:  # noqa: BLE001
            await room_service.send_error(websocket, str(exc))
        finally:
            state.active_connections = max(0, state.active_connections - 1)
            await room_service.cleanup_connection(current_client_id, current_room_id)

    app.include_router(router)

