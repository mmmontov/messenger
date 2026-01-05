from __future__ import annotations

import secrets
from datetime import datetime, timedelta, timezone

from sqlalchemy import select, update
from sqlalchemy.ext.asyncio import AsyncSession

from app.config import settings
from app.core.security import hash_password, verify_password
from app.core.session import create_session_id, get_session_expires_at
from app.models.user import PasswordResetToken, Session, User, UserSettings
from app.utils.email import EmailPayload, send_email


class AuthService:
    async def register(self, db: AsyncSession, *, email: str, password: str, username: str) -> User:
        res = await db.execute(select(User).where(User.email == email))
        if res.scalar_one_or_none():
            raise ValueError("Email already registered")
        
        res_username = await db.execute(select(User).where(User.username == username))
        if res_username.scalar_one_or_none():
            raise ValueError("Username already taken")

        user = User(email=email, password_hash=hash_password(password), username=username)
        user.settings = UserSettings()
        db.add(user)
        await db.commit()
        await db.refresh(user)
        return user

    async def login(self, db: AsyncSession, *, email: str, password: str) -> tuple[User, str]:
        res = await db.execute(select(User).where(User.email == email))
        user = res.scalar_one_or_none()
        if not user or not user.is_active:
            raise ValueError("Invalid credentials")
        try:
            if not verify_password(password, user.password_hash):
                raise ValueError("Invalid credentials")
        except ValueError:
            # например, пароль длиннее лимита bcrypt (72 bytes) — не даём 500
            raise ValueError("Invalid credentials")

        # Создаём сессию
        session_id = create_session_id()
        expires_at = get_session_expires_at()
        session = Session(user_id=user.id, session_id=session_id, expires_at=expires_at)
        db.add(session)
        await db.commit()
        return user, session_id

    async def logout(self, db: AsyncSession, *, session_id: str) -> None:
        res = await db.execute(select(Session).where(Session.session_id == session_id))
        session = res.scalar_one_or_none()
        if session:
            await db.delete(session)
            await db.commit()

    async def request_password_reset(self, db: AsyncSession, *, email: str) -> None:
        res = await db.execute(select(User).where(User.email == email))
        user = res.scalar_one_or_none()
        # Не раскрываем, существует ли email.
        if not user:
            return

        token = secrets.token_urlsafe(32)
        expires_at = datetime.now(timezone.utc) + timedelta(minutes=settings.password_reset_token_exp_minutes)
        db.add(PasswordResetToken(user_id=user.id, token=token, expires_at=expires_at, used=False))
        await db.commit()

        link = f"{settings.password_reset_frontend_url}?token={token}"
        await send_email(
            EmailPayload(
                to=user.email,
                subject="Password reset",
                body_text=f"Use this link to reset your password (valid {settings.password_reset_token_exp_minutes} min):\n{link}",
            )
        )

    async def confirm_password_reset(self, db: AsyncSession, *, token: str, new_password: str) -> None:
        res = await db.execute(select(PasswordResetToken).where(PasswordResetToken.token == token))
        prt = res.scalar_one_or_none()
        if not prt or prt.used:
            raise ValueError("Invalid token")
        if prt.expires_at <= datetime.now(timezone.utc):
            raise ValueError("Token expired")

        res_u = await db.execute(select(User).where(User.id == prt.user_id))
        user = res_u.scalar_one()
        user.password_hash = hash_password(new_password)
        prt.used = True

        # Удаляем все сессии пользователя
        await db.execute(
            update(Session)
            .where(Session.user_id == user.id)
            .values(expires_at=datetime.now(timezone.utc) - timedelta(seconds=1))
        )
        await db.commit()


auth_service = AuthService()


