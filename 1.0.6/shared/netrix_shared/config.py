from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path


def read_bool(name: str, default: bool = False) -> bool:
    raw_value = os.getenv(name)
    if raw_value is None:
        return default
    return raw_value.strip().lower() in {"1", "true", "yes", "on"}


def read_int(name: str, default: int) -> int:
    raw_value = os.getenv(name)
    if raw_value is None:
        return default
    return int(raw_value)


@dataclass(frozen=True)
class JwtConfig:
    secret: str
    issuer: str
    lifetime_minutes: int


def default_data_dir() -> Path:
    return Path(__file__).resolve().parents[2]


def load_jwt_config() -> JwtConfig:
    return JwtConfig(
        secret=os.getenv("NETRIX_JWT_SECRET", "netrix-dev-secret"),
        issuer=os.getenv("NETRIX_JWT_ISSUER", "netrix-auth"),
        lifetime_minutes=read_int("NETRIX_JWT_LIFETIME_MINUTES", 120),
    )
