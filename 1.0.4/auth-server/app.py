from __future__ import annotations

import json
import os
import sys
from contextlib import closing
from dataclasses import dataclass
from pathlib import Path
from urllib.parse import parse_qs

from fastapi import FastAPI, Header, HTTPException, Request, status
from pydantic import BaseModel, Field
from psycopg2 import Error as PsycopgError
from psycopg2 import connect
from psycopg2.extras import RealDictCursor

ROOT_DIR = Path(__file__).resolve().parents[1]
sys.path.append(str(ROOT_DIR / "shared"))

from netrix_shared.config import load_jwt_config  # noqa: E402
from netrix_shared.security import (  # noqa: E402
    create_access_token,
    hash_password,
    verify_password,
)


app = FastAPI(title="Netrix Auth Server", version="1.0.4")

JWT_CONFIG = load_jwt_config()


def read_bool(name: str, default: bool = False) -> bool:
    raw_value = os.getenv(name)
    if raw_value is None:
        return default
    return raw_value.strip().lower() in {"1", "true", "yes", "on"}


@dataclass(frozen=True)
class DatabaseConfig:
    host: str
    port: int
    dbname: str
    user: str
    password: str
    connect_timeout: int = 5


DB_CONFIG = DatabaseConfig(
    host=os.getenv("NETRIX_AUTH_DB_HOST", "localhost"),
    port=int(os.getenv("NETRIX_AUTH_DB_PORT", "5433")),
    dbname=os.getenv("NETRIX_AUTH_DB_NAME", "netrix_auth"),
    user=os.getenv("NETRIX_AUTH_DB_USER", "netrix"),
    password=os.getenv("NETRIX_AUTH_DB_PASSWORD", "netrix"),
    connect_timeout=int(os.getenv("NETRIX_AUTH_DB_TIMEOUT", "5")),
)


class AuthPayload(BaseModel):
    username: str = Field(min_length=3, max_length=50)
    password: str = Field(min_length=4, max_length=128)


class TokenResponse(BaseModel):
    access_token: str
    token_type: str = "bearer"
    expires_in_minutes: int
    username: str


def get_connection():
    return connect(
        host=DB_CONFIG.host,
        port=DB_CONFIG.port,
        dbname=DB_CONFIG.dbname,
        user=DB_CONFIG.user,
        password=DB_CONFIG.password,
        connect_timeout=DB_CONFIG.connect_timeout,
        cursor_factory=RealDictCursor,
        application_name="netrix-auth-server",
    )


def init_db() -> None:
    with closing(get_connection()) as connection, connection.cursor() as cursor:
        cursor.execute(
            """
            CREATE TABLE IF NOT EXISTS users (
                username VARCHAR(50) PRIMARY KEY,
                password_hash TEXT NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """
        )
        cursor.execute(
            """

            CREATE TABLE IF NOT EXISTS sessions (
                jti TEXT PRIMARY KEY,
                username VARCHAR(50) NOT NULL,
                issued_at INTEGER NOT NULL,
                expires_at INTEGER NOT NULL,
                revoked BOOLEAN NOT NULL DEFAULT FALSE,
                CONSTRAINT fk_sessions_username
                    FOREIGN KEY(username)
                    REFERENCES users(username)
                    ON DELETE CASCADE
            );
            """
        )
        cursor.execute("CREATE INDEX IF NOT EXISTS idx_sessions_username ON sessions(username);")
        connection.commit()


@app.on_event("startup")
def startup() -> None:
    init_db()


def find_user(connection, username: str):
    with connection.cursor() as cursor:
        cursor.execute(
            "SELECT username, password_hash FROM users WHERE username = %s",
            (username,),
        )
        return cursor.fetchone()


def issue_token(connection, username: str) -> TokenResponse:
    token, claims = create_access_token(
        username=username,
        secret=JWT_CONFIG.secret,
        issuer=JWT_CONFIG.issuer,
        lifetime_minutes=JWT_CONFIG.lifetime_minutes,
    )
    with connection.cursor() as cursor:
        cursor.execute(
            """
            INSERT INTO sessions (jti, username, issued_at, expires_at, revoked)
            VALUES (%s, %s, %s, %s, FALSE)
            """,
            (claims["jti"], username, claims["iat"], claims["exp"]),
        )
    connection.commit()
    return TokenResponse(
        access_token=token,
        expires_in_minutes=JWT_CONFIG.lifetime_minutes,
        username=username,
    )


async def read_auth_payload(request: Request) -> AuthPayload:
    raw_body = await request.body()
    if not raw_body:
        raise HTTPException(status_code=422, detail="Username and password are required.")

    content_type = request.headers.get("content-type", "").lower()
    payload: dict[str, object]

    if "application/json" in content_type:
        try:
            parsed = json.loads(raw_body.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise HTTPException(status_code=422, detail="Request body must be valid JSON.") from exc
        if not isinstance(parsed, dict):
            raise HTTPException(status_code=422, detail="Request body must be a JSON object.")
        payload = parsed
    elif "application/x-www-form-urlencoded" in content_type:
        parsed = parse_qs(raw_body.decode("utf-8"), keep_blank_values=True)
        payload = {key: values[0] if values else "" for key, values in parsed.items()}
    else:
        try:
            parsed = json.loads(raw_body.decode("utf-8"))
            if not isinstance(parsed, dict):
                raise HTTPException(status_code=422, detail="Request body must be a JSON object.")
            payload = parsed
        except (UnicodeDecodeError, json.JSONDecodeError):
            parsed = parse_qs(raw_body.decode("utf-8"), keep_blank_values=True)
            payload = {key: values[0] if values else "" for key, values in parsed.items()}

    username = str(payload.get("username", "")).strip()
    password = str(payload.get("password", ""))

    if len(username) < 3:
        raise HTTPException(status_code=422, detail="Username must be at least 3 characters.")

    if len(password) < 4:
        raise HTTPException(status_code=422, detail="Password must be at least 4 characters.")

    if len(username) > 50:
        raise HTTPException(status_code=422, detail="Username must not exceed 50 characters.")

    if len(password) > 128:
        raise HTTPException(status_code=422, detail="Password must not exceed 128 characters.")

    return AuthPayload(username=username, password=password)


@app.get("/health")
def health() -> dict[str, str | int]:
    try:
        with closing(get_connection()) as connection, connection.cursor() as cursor:
            cursor.execute("SELECT 1")
            cursor.fetchone()
    except PsycopgError as exc:
        raise HTTPException(status_code=503, detail=f"Database unavailable: {exc}") from exc

    return {
        "status": "ok",
        "version": "1.0.4",
        "database": "postgresql",
        "db_host": DB_CONFIG.host,
        "db_port": DB_CONFIG.port,
        "db_name": DB_CONFIG.dbname,
    }


@app.post("/register", response_model=TokenResponse, status_code=status.HTTP_201_CREATED)
async def register(request: Request) -> TokenResponse:
    payload = await read_auth_payload(request)
    with closing(get_connection()) as connection:
        if find_user(connection, payload.username) is not None:
            raise HTTPException(status_code=409, detail="Username already exists")

        with connection.cursor() as cursor:
            cursor.execute(
                """
                INSERT INTO users (username, password_hash)
                VALUES (%s, %s)
                """,
                (payload.username, hash_password(payload.password)),
            )
        connection.commit()
        return issue_token(connection, payload.username)


@app.post("/login", response_model=TokenResponse)
async def login(request: Request) -> TokenResponse:
    payload = await read_auth_payload(request)
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
        with connection.cursor() as cursor:
            cursor.execute(
                """
                SELECT revoked, expires_at
                FROM sessions
                WHERE jti = %s AND username = %s
                """,
                (claims["jti"], claims["sub"]),
            )
            session_row = cursor.fetchone()
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
        access_log=read_bool("NETRIX_ACCESS_LOG"),
    )
