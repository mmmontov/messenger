from __future__ import annotations

import secrets
from datetime import datetime, timedelta, timezone

from app.config import settings


def create_session_id() -> str:
    """Создаёт уникальный ID сессии."""
    return secrets.token_urlsafe(32)


def get_session_expires_at() -> datetime:
    """Возвращает время истечения сессии (30 дней)."""
    return datetime.now(timezone.utc) + timedelta(days=30)


def is_session_expired(expires_at: datetime) -> bool:
    """Проверяет, истекла ли сессия."""
    now = datetime.now(timezone.utc)
    # Если expires_at не имеет timezone, считаем его UTC
    if expires_at.tzinfo is None:
        expires_at = expires_at.replace(tzinfo=timezone.utc)
    return expires_at <= now

