from __future__ import annotations

from datetime import datetime, timezone

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from app.core.security import hash_password, verify_password
from app.models.user import User, UserSettings, UserStatus


class UserService:
    async def get_me(self, db: AsyncSession, *, user_id: int) -> User:
        res = await db.execute(select(User).options(selectinload(User.settings)).where(User.id == user_id))
        user = res.scalar_one()
        if not user.settings:
            user.settings = UserSettings()
            await db.commit()
            await db.refresh(user)
        return user

    async def update_profile(
        self,
        db: AsyncSession,
        *,
        user: User,
        username: str | None,
        bio: str | None,
        status: str | None,
    ) -> User:
        if username is not None:
            user.username = username
        if bio is not None:
            user.bio = bio
        if status is not None:
            try:
                user.status = UserStatus(status)
            except Exception:
                raise ValueError("Invalid status")
        await db.commit()
        await db.refresh(user)
        return user

    async def update_settings(
        self,
        db: AsyncSession,
        *,
        user: User,
        notifications_enabled: bool | None,
        notification_sound: bool | None,
        show_banner: bool | None,
    ) -> User:
        if not user.settings:
            user.settings = UserSettings()
        if notifications_enabled is not None:
            user.settings.notifications_enabled = notifications_enabled
        if notification_sound is not None:
            user.settings.notification_sound = notification_sound
        if show_banner is not None:
            user.settings.show_banner = show_banner
        await db.commit()
        await db.refresh(user)
        return user

    async def change_password(self, db: AsyncSession, *, user: User, current_password: str, new_password: str) -> None:
        if not verify_password(current_password, user.password_hash):
            raise ValueError("Invalid current password")
        user.password_hash = hash_password(new_password)
        await db.commit()

    async def change_email(self, db: AsyncSession, *, user: User, new_email: str, password: str) -> User:
        if not verify_password(password, user.password_hash):
            raise ValueError("Invalid password")
        res = await db.execute(select(User).where(User.email == new_email))
        if res.scalar_one_or_none():
            raise ValueError("Email already used")
        user.email = new_email
        await db.commit()
        await db.refresh(user)
        return user

    async def set_online(self, db: AsyncSession, *, user: User) -> None:
        user.status = UserStatus.online
        await db.commit()

    async def set_offline(self, db: AsyncSession, *, user: User) -> None:
        user.status = UserStatus.offline
        user.last_seen_at = datetime.now(timezone.utc)
        await db.commit()


user_service = UserService()


