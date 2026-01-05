from __future__ import annotations

from app.db.base import Base
from app.db.session import engine

# Важно: импортируем модели, чтобы они зарегистрировались в metadata.
from app.models.chat import Chat, ChatMember  # noqa: F401
from app.models.file import File  # noqa: F401
from app.models.message import Message, MessageStatus  # noqa: F401
from app.models.notification import Notification  # noqa: F401
from app.models.user import PasswordResetToken, Session, User, UserSettings  # noqa: F401


async def init_db() -> None:
    """
    Для учебного проекта используем create_all. В реальном проде — Alembic миграции.
    """
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)


