from __future__ import annotations

import hashlib
import secrets
from datetime import datetime, timedelta, timezone
from typing import Any

from jose import JWTError, jwt

from app.config import settings


class TokenError(Exception):
    pass


def _now() -> datetime:
    return datetime.now(timezone.utc)


def _hash_token(raw: str) -> str:
    return hashlib.sha256(raw.encode("utf-8")).hexdigest()


def create_access_token(*, user_id: int) -> str:
    now = _now()
    exp = now + timedelta(minutes=settings.access_token_exp_minutes)
    payload: dict[str, Any] = {
        "sub": str(user_id),
        "type": "access",
        "iat": int(now.timestamp()),
        "exp": int(exp.timestamp()),
    }
    return jwt.encode(payload, settings.jwt_secret_key, algorithm=settings.jwt_algorithm)


def create_refresh_token(*, user_id: int, jti: str | None = None) -> tuple[str, str, datetime]:
    """
    Возвращает (refresh_jwt, jti, expires_at).
    Refresh-token хранится в БД в виде хэша (sha256).
    """
    now = _now()
    exp = now + timedelta(days=settings.refresh_token_exp_days)
    jti = jti or secrets.token_hex(16)
    payload: dict[str, Any] = {
        "sub": str(user_id),
        "type": "refresh",
        "jti": jti,
        "iat": int(now.timestamp()),
        "exp": int(exp.timestamp()),
    }
    raw = jwt.encode(payload, settings.jwt_secret_key, algorithm=settings.jwt_algorithm)
    return raw, jti, exp


def decode_token(token: str) -> dict[str, Any]:
    try:
        return jwt.decode(token, settings.jwt_secret_key, algorithms=[settings.jwt_algorithm])
    except JWTError as e:
        raise TokenError("Invalid token") from e


def token_hash(token: str) -> str:
    return _hash_token(token)


