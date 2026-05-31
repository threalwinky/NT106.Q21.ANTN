from __future__ import annotations

from datetime import datetime, timedelta, timezone
from typing import Any
from uuid import uuid4

import bcrypt
import jwt


def utc_now() -> datetime:
    return datetime.now(timezone.utc)


def hash_password(password: str) -> str:
    return bcrypt.hashpw(password.encode("utf-8"), bcrypt.gensalt()).decode("utf-8")


def verify_password(password: str, password_hash: str) -> bool:
    return bcrypt.checkpw(password.encode("utf-8"), password_hash.encode("utf-8"))


def create_access_token(
    username: str,
    secret: str,
    issuer: str,
    lifetime_minutes: int,
    extra_claims: dict[str, Any] | None = None,
) -> tuple[str, dict[str, Any]]:
    now = utc_now()
    claims: dict[str, Any] = {
        "sub": username,
        "iss": issuer,
        "iat": int(now.timestamp()),
        "exp": int((now + timedelta(minutes=lifetime_minutes)).timestamp()),
        "jti": str(uuid4()),
    }
    if extra_claims:
        claims.update(extra_claims)
    token = jwt.encode(claims, secret, algorithm="HS256")
    return token, claims


def decode_access_token(token: str, secret: str, issuer: str) -> dict[str, Any]:
    return jwt.decode(
        token,
        secret,
        algorithms=["HS256"],
        issuer=issuer,
        options={"require": ["sub", "iss", "exp", "iat", "jti"]},
    )
