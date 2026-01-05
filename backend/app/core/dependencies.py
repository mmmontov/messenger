from __future__ import annotations

from fastapi import Cookie, Depends, HTTPException, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from app.core.session import is_session_expired
from app.db.session import get_db
from app.models.user import Session, User


async def get_current_user(
    session_id: str | None = Cookie(None, alias="session_id"),
    db: AsyncSession = Depends(get_db),
) -> User:
    if not session_id:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Not authenticated")
    
    # Находим сессию
    res = await db.execute(select(Session).where(Session.session_id == session_id))
    session = res.scalar_one_or_none()
    if not session:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid session")
    
    # Проверяем срок действия
    if is_session_expired(session.expires_at):
        # Удаляем истекшую сессию
        await db.delete(session)
        await db.commit()
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Session expired")
    
    # Получаем пользователя с настройками
    res = await db.execute(select(User).options(selectinload(User.settings)).where(User.id == session.user_id))
    user = res.scalar_one_or_none()
    if not user or not user.is_active:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="User not found or inactive")
    return user


