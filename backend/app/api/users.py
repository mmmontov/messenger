from __future__ import annotations

from fastapi import APIRouter, Depends, File as UploadFileParam, HTTPException, UploadFile, status
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.dependencies import get_current_user
from app.core.websocket_manager import ws_manager
from app.db.session import get_db
from app.models.user import User
from app.schemas.user import (
    ChangeEmailRequest,
    ChangePasswordRequest,
    UserOut,
    UserUpdateProfile,
    UserUpdateSettings,
)
from app.services.user_service import user_service
from app.utils.file_storage import save_upload


router = APIRouter(prefix="/users", tags=["users"])


def _user_to_out(user: User) -> UserOut:
    return UserOut(
        id=user.id,
        email=user.email,
        username=user.username,
        bio=user.bio,
        avatar_path=user.avatar_path,
        status=user.status.value if hasattr(user.status, "value") else str(user.status),
        is_active=user.is_active,
        created_at=user.created_at,
        last_seen_at=user.last_seen_at,
        settings={
            "notifications_enabled": user.settings.notifications_enabled if user.settings else True,
            "notification_sound": user.settings.notification_sound if user.settings else True,
            "show_banner": user.settings.show_banner if user.settings else True,
        },
    )


@router.get("/me", response_model=UserOut)
async def me(
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> UserOut:
    user = await user_service.get_me(db, user_id=current.id)
    return _user_to_out(user)


@router.patch("/me/profile", response_model=UserOut)
async def update_profile(
    payload: UserUpdateProfile,
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> UserOut:
    try:
        # Статус обновляется автоматически через WebSocket, не позволяем менять его вручную
        user = await user_service.update_profile(
            db, user=current, username=payload.username, bio=payload.bio, status=None
        )
        return _user_to_out(user)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))


@router.patch("/me/settings", response_model=UserOut)
async def update_settings(
    payload: UserUpdateSettings,
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> UserOut:
    user = await user_service.update_settings(
        db,
        user=current,
        notifications_enabled=payload.notifications_enabled,
        notification_sound=payload.notification_sound,
        show_banner=payload.show_banner,
    )
    return _user_to_out(user)


@router.post("/me/avatar", response_model=UserOut)
async def upload_avatar(
    file: UploadFile = UploadFileParam(...),
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> UserOut:
    try:
        stored = await save_upload("avatars", file)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))
    current.avatar_path = stored.relative_path
    await db.commit()
    await db.refresh(current)
    
    # Уведомляем всех собеседников об обновлении профиля
    from app.ws.chat_ws import _contact_ids
    contact_user_ids = await _contact_ids(db, current.id)
    await ws_manager.broadcast_to_users(contact_user_ids, {"type": "user.updated", "user_id": current.id})
    
    return _user_to_out(current)


@router.post("/me/change-password")
async def change_password(
    payload: ChangePasswordRequest,
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> dict:
    try:
        await user_service.change_password(db, user=current, current_password=payload.current_password, new_password=payload.new_password)
        return {"ok": True}
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))


@router.post("/me/change-email", response_model=UserOut)
async def change_email(
    payload: ChangeEmailRequest,
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> UserOut:
    try:
        user = await user_service.change_email(db, user=current, new_email=str(payload.new_email), password=payload.password)
        return _user_to_out(user)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))


