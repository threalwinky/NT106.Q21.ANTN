from __future__ import annotations

from fastapi import FastAPI

from .routes import register_routes
from .service import RoomService
from .store import ServerState


def create_app() -> FastAPI:
    app = FastAPI(title="Netrix Main Server", version="1.0.3")
    state = ServerState()
    room_service = RoomService(state)
    register_routes(app, state, room_service)
    return app
