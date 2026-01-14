from __future__ import annotations

from fastapi import APIRouter, Cookie, Depends, HTTPException, Response, status
from sqlalchemy.ext.asyncio import AsyncSession

from app.db.session import get_db
from app.schemas.auth import (
    LoginRequest,
    PasswordResetConfirmRequest,
    PasswordResetRequest,
    RegisterRequest,
)
from app.services.auth_service import auth_service


router = APIRouter(prefix="/auth", tags=["auth"])


@router.post("/register")
async def register(
    payload: RegisterRequest,
    response: Response,
    db: AsyncSession = Depends(get_db),
) -> dict:
    try:
        user = await auth_service.register(
            db, email=str(payload.email), password=payload.password, username=payload.username
        )
        _, session_id = await auth_service.login(db, email=user.email, password=payload.password)
        # Устанавливаем cookie с сессией
        response.set_cookie(
            key="session_id",
            value=session_id,
            max_age=30 * 24 * 60 * 60,  # 30 дней
            httponly=True,
            secure=False,  
            samesite="lax",
        )
        return {"ok": True, "user_id": user.id, "username": user.username}
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))


@router.post("/login")
async def login(
    payload: LoginRequest,
    response: Response,
    db: AsyncSession = Depends(get_db),
) -> dict:
    try:
        user, session_id = await auth_service.login(db, email=str(payload.email), password=payload.password)
        # Устанавливаем cookie с сессией
        response.set_cookie(
            key="session_id",
            value=session_id,
            max_age=30 * 24 * 60 * 60,  # 30 дней
            httponly=True,
            secure=False, 
            samesite="lax",
        )
        return {"ok": True, "user_id": user.id, "username": user.username}
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))


@router.post("/logout")
async def logout(
    response: Response,
    session_id: str | None = Cookie(None, alias="session_id"),
    db: AsyncSession = Depends(get_db),
) -> dict:
    if session_id:
        await auth_service.logout(db, session_id=session_id)
    # Удаляем cookie
    response.delete_cookie(key="session_id")
    return {"ok": True}


@router.post("/password-reset/request")
async def request_password_reset(payload: PasswordResetRequest, db: AsyncSession = Depends(get_db)) -> dict:
    await auth_service.request_password_reset(db, email=str(payload.email))
    return {"ok": True}


@router.post("/password-reset/confirm")
async def confirm_password_reset(payload: PasswordResetConfirmRequest, db: AsyncSession = Depends(get_db)) -> dict:
    try:
        await auth_service.confirm_password_reset(db, token=payload.token, new_password=payload.new_password)
        return {"ok": True}
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))


