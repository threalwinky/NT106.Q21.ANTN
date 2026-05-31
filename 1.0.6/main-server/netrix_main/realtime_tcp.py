from __future__ import annotations

import asyncio
import json
from typing import Any

from .service import RoomService
from .store import ServerState

MAX_TCP_PACKET_BYTES = 12 * 1024 * 1024
SECURE_FRAME_MAGIC = b"NXF1"


async def read_packet(reader: asyncio.StreamReader) -> bytes:
    header = await reader.readexactly(4)
    length = int.from_bytes(header, byteorder="big", signed=True)
    if length <= 0 or length > MAX_TCP_PACKET_BYTES:
        raise ValueError("Invalid TCP packet size")
    return await reader.readexactly(length)


async def handle_binary_packet(
    packet: bytes,
    current_client_id: str | None,
    current_room_id: str | None,
    state: ServerState,
    room_service: RoomService,
) -> None:
    if not current_client_id or not current_room_id:
        raise ValueError("Join or create a room before sending data")
    if not packet.startswith(SECURE_FRAME_MAGIC):
        raise ValueError("Unsupported binary message")

    room = state.rooms.get(current_room_id)
    if room is None:
        raise ValueError("Room is no longer available")

    participant = room.participants.get(current_client_id)
    if participant is None:
        raise ValueError("Participant state is not available")
    if participant.role != "host":
        raise ValueError("Only host can send screen frames")

    await room_service.broadcast_bytes(room, packet, exclude_client_id=current_client_id)


async def handle_json_message(
    writer: asyncio.StreamWriter,
    message: dict[str, Any],
    current_client_id: str | None,
    current_room_id: str | None,
    state: ServerState,
    room_service: RoomService,
) -> tuple[str | None, str | None]:
    message_type = str(message.get("type", "")).strip().lower()

    if message_type == "ping":
        await room_service.send_json(writer, {"type": "pong"})
        return current_client_id, current_room_id

    if message_type in {"create_room", "join_room"} and current_client_id and current_room_id:
        raise ValueError("Disconnect from the current room before creating or joining another room.")

    if message_type == "create_room":
        room, participant, access_mode = await room_service.create_room(writer, message)
        await room_service.send_json(
            writer,
            {
                "type": "room_created",
                "room_id": room.room_id,
                "client_id": participant.client_id,
                "role": "host",
                "access_mode": access_mode,
                "control_approved": True,
            },
        )
        participant.realtime_ready = True
        await room_service.broadcast_room_state(room)
        return participant.client_id, room.room_id

    if message_type == "join_room":
        room, participant, access_mode = await room_service.join_room(writer, message)
        await room_service.send_json(
            writer,
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
        participant.realtime_ready = True
        await room_service.broadcast_room_state(room)
        if participant.role == "controller" and not participant.control_approved:
            asyncio.create_task(room_service.notify_host_about_controller_request(room, participant))
        return participant.client_id, room.room_id

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
                "codec": message.get("codec", "jpeg"),
                "payload_base64": message.get("payload_base64") or message.get("jpeg_base64", ""),
                "jpeg_base64": message.get("jpeg_base64", ""),
                "width": int(message.get("width", 0)),
                "height": int(message.get("height", 0)),
                "sent_at": message.get("sent_at"),
            },
            exclude_client_id=current_client_id,
        )
        return current_client_id, current_room_id

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
        return current_client_id, current_room_id

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
        return current_client_id, current_room_id

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
        return current_client_id, current_room_id

    if message_type in {"file_offer", "file_chunk", "file_complete"}:
        transfer_id = str(message.get("transfer_id", "")).strip()
        if not transfer_id:
            raise ValueError("transfer_id is required")

        if message_type == "file_offer":
            file_name = str(message.get("file_name", "")).strip()
            if not file_name:
                raise ValueError("file_name is required")

        await room_service.broadcast(room, message, exclude_client_id=current_client_id)
        return current_client_id, current_room_id

    if message_type == "secure_payload":
        channel = str(message.get("channel", "")).strip().lower()
        nonce_base64 = str(message.get("nonce_base64", "")).strip()
        ciphertext_base64 = str(message.get("ciphertext_base64", "")).strip()
        if channel not in {"frame", "input", "chat", "file_offer", "file_chunk", "file_complete", "clipboard", "process_request", "process_snapshot"}:
            raise ValueError("Unsupported secure payload channel")
        if not nonce_base64 or not ciphertext_base64:
            raise ValueError("Encrypted payload is incomplete")

        secure_message = {
            "type": "secure_payload",
            "channel": channel,
            "nonce_base64": nonce_base64,
            "ciphertext_base64": ciphertext_base64,
            "sender_client_id": current_client_id,
            "sender_display_name": participant.display_name,
        }
        if "request_id" in message:
            secure_message["request_id"] = message.get("request_id")
        if "target_client_id" in message:
            secure_message["target_client_id"] = message.get("target_client_id")

        if channel == "frame":
            if participant.role != "host":
                raise ValueError("Only host can send screen frames")
            await room_service.broadcast(room, secure_message, exclude_client_id=current_client_id)
            return current_client_id, current_room_id

        if channel == "input":
            if participant.role != "controller":
                raise ValueError("Only a controller can send input events")
            if not participant.control_approved:
                raise ValueError("Controller approval is still pending")
            host = room.participants.get(room.host_client_id)
            if host is None:
                raise ValueError("Host is not connected")
            await room_service.send_json(host.websocket, secure_message)
            return current_client_id, current_room_id

        if channel == "chat":
            await room_service.broadcast(room, secure_message)
            return current_client_id, current_room_id

        if channel == "clipboard":
            await room_service.broadcast(room, secure_message, exclude_client_id=current_client_id)
            return current_client_id, current_room_id

        if channel == "process_request":
            if participant.role != "controller":
                raise ValueError("Only a controller can request the host process list")
            if not participant.control_approved:
                raise ValueError("Controller approval is still pending")
            host = room.participants.get(room.host_client_id)
            if host is None:
                raise ValueError("Host is not connected")
            await room_service.send_json(host.websocket, secure_message)
            return current_client_id, current_room_id

        if channel == "process_snapshot":
            if participant.role != "host":
                raise ValueError("Only host can send process snapshots")
            target_client_id = str(message.get("target_client_id", "")).strip()
            if not target_client_id:
                raise ValueError("target_client_id is required")
            target = room.participants.get(target_client_id)
            if target is None:
                raise ValueError("Process snapshot target is no longer connected")
            await room_service.send_json(target.websocket, secure_message)
            return current_client_id, current_room_id

        await room_service.broadcast(room, secure_message, exclude_client_id=current_client_id)
        return current_client_id, current_room_id

    raise ValueError(f"Unsupported message type: {message_type}")


async def handle_tcp_client(
    reader: asyncio.StreamReader,
    writer: asyncio.StreamWriter,
    state: ServerState,
    room_service: RoomService,
) -> None:
    state.active_connections += 1
    current_client_id: str | None = None
    current_room_id: str | None = None

    await room_service.send_json(
        writer,
        {
            "type": "hello",
            "message": "Netrix main TCP server ready",
            "supported_modes": ["lan", "internet"],
            "transport": "tcp",
        },
    )

    try:
        while True:
            packet = await read_packet(reader)
            if packet.startswith(SECURE_FRAME_MAGIC):
                await handle_binary_packet(packet, current_client_id, current_room_id, state, room_service)
                continue

            message = json.loads(packet.decode("utf-8"))
            current_client_id, current_room_id = await handle_json_message(
                writer,
                message,
                current_client_id,
                current_room_id,
                state,
                room_service,
            )
    except (asyncio.IncompleteReadError, ConnectionResetError, BrokenPipeError):
        pass
    except Exception as exc:  # noqa: BLE001
        try:
            await room_service.send_error(writer, str(exc))
        except Exception:  # noqa: BLE001
            pass
    finally:
        state.active_connections = max(0, state.active_connections - 1)
        await room_service.cleanup_connection(current_client_id, current_room_id)
        await room_service.close_transport(writer)


async def start_realtime_tcp_server(
    state: ServerState,
    room_service: RoomService,
    host: str,
    port: int,
) -> asyncio.AbstractServer:
    return await asyncio.start_server(
        lambda reader, writer: handle_tcp_client(reader, writer, state, room_service),
        host=host,
        port=port,
    )
