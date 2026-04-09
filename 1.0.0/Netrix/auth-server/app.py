from __future__ import annotations

import sqlite3
import sys
from contextlib import closing
from pathlib import Path

from fastapi import FastAPI, Header, HTTPException, status
from pydantic import BaseModel, Field

ROOT_DIR = Path(__file__).resolve().parents[1]
sys.path.append(str(ROOT_DIR / "shared" / "py"))

from netrix_shared.config import load_jwt_config  # noqa: E402
from netrix_shared.security import (  # noqa: E402
    create_access_token,
    hash_password,
    verify_password,
)


app = FastAPI(title="Netrix Auth Server", version="1.0.0")

JWT_CONFIG = load_jwt_config()
DB_PATH = Path(__file__).resolve().with_name("netrix_auth.db")


class AuthPayload(BaseModel):
    username: str = Field(min_length=3, max_length=50)
    password: str = Field(min_length=8, max_length=128)


class TokenResponse(BaseModel):
    access_token: str
    token_type: str = "bearer"
    expires_in_minutes: int
    username: str


def get_connection() -> sqlite3.Connection:
    connection = sqlite3.connect(DB_PATH, check_same_thread=False)
    connection.row_factory = sqlite3.Row
    return connection


def init_db() -> None:
    with closing(get_connection()) as connection:
        connection.executescript(
            """
            CREATE TABLE IF NOT EXISTS users (
                username TEXT PRIMARY KEY,
                password_hash TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS sessions (
                jti TEXT PRIMARY KEY,
                username TEXT NOT NULL,
                issued_at INTEGER NOT NULL,
                expires_at INTEGER NOT NULL,
                revoked INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY(username) REFERENCES users(username)
            );
            """
        )
        connection.commit()


@app.on_event("startup")
def startup() -> None:
    init_db()


def find_user(connection: sqlite3.Connection, username: str) -> sqlite3.Row | None:
    return connection.execute(
        "SELECT username, password_hash FROM users WHERE username = ?",
        (username,),
    ).fetchone()


def issue_token(connection: sqlite3.Connection, username: str) -> TokenResponse:
    token, claims = create_access_token(
        username=username,
        secret=JWT_CONFIG.secret,
        issuer=JWT_CONFIG.issuer,
        lifetime_minutes=JWT_CONFIG.lifetime_minutes,
    )
    connection.execute(
        """
        INSERT INTO sessions (jti, username, issued_at, expires_at, revoked)
        VALUES (?, ?, ?, ?, 0)
        """,
        (claims["jti"], username, claims["iat"], claims["exp"]),
    )
    connection.commit()
    return TokenResponse(
        access_token=token,
        expires_in_minutes=JWT_CONFIG.lifetime_minutes,
        username=username,
    )


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/register", response_model=TokenResponse, status_code=status.HTTP_201_CREATED)
def register(payload: AuthPayload) -> TokenResponse:
    with closing(get_connection()) as connection:
        if find_user(connection, payload.username) is not None:
            raise HTTPException(status_code=409, detail="Username already exists")

        connection.execute(
            """
            INSERT INTO users (username, password_hash, created_at)
            VALUES (?, ?, datetime('now'))
            """,
            (payload.username, hash_password(payload.password)),
        )
        connection.commit()
        return issue_token(connection, payload.username)


@app.post("/login", response_model=TokenResponse)
def login(payload: AuthPayload) -> TokenResponse:
    with closing(get_connection()) as connection:
        user = find_user(connection, payload.username)
        if user is None or not verify_password(payload.password, user["password_hash"]):
            raise HTTPException(status_code=401, detail="Invalid username or password")
        return issue_token(connection, payload.username)


@app.get("/validate")
def validate(authorization: str | None = Header(default=None)) -> dict[str, str | int]:
    if not authorization or not authorization.lower().startswith("bearer "):
        raise HTTPException(status_code=401, detail="Missing bearer token")

    token = authorization.split(" ", 1)[1]
    try:
        from netrix_shared.security import decode_access_token  # noqa: WPS433,E402

        claims = decode_access_token(token, JWT_CONFIG.secret, JWT_CONFIG.issuer)
    except Exception as exc:  # noqa: BLE001
        raise HTTPException(status_code=401, detail=f"Invalid token: {exc}") from exc

    with closing(get_connection()) as connection:
        session_row = connection.execute(
            """
            SELECT revoked, expires_at
            FROM sessions
            WHERE jti = ? AND username = ?
            """,
            (claims["jti"], claims["sub"]),
        ).fetchone()
        if session_row is None or session_row["revoked"]:
            raise HTTPException(status_code=401, detail="Session not active")
        return {
            "username": claims["sub"],
            "jti": claims["jti"],
            "expires_at": claims["exp"],
        }


if __name__ == "__main__":
    import os

    import uvicorn

    uvicorn.run(
        "app:app",
        host=os.getenv("NETRIX_AUTH_HOST", "0.0.0.0"),
        port=int(os.getenv("NETRIX_AUTH_PORT", "8001")),
        reload=False,
    )
