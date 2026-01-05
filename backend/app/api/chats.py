from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException, Query, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.dependencies import get_current_user
from app.db.session import get_db
from app.models.chat import ChatMember
from app.models.message import Message
from app.models.user import User
from app.schemas.chat import (
    ChatOut,
    ChatMemberOut,
    CreateDialogByEmailRequest,
    CreateDialogByUsernameRequest,
    CreateDialogRequest,
    DialogListItem,
)
from app.services.chat_service import chat_service


router = APIRouter(prefix="/chats", tags=["chats"])


async def _chat_to_out(db: AsyncSession, *, current_user_id: int, chat_id: int) -> ChatOut:
    # Проверяем, что текущий пользователь участник (и заодно получаем Chat с created_at)
    chat = await chat_service.get_chat(db, user_id=current_user_id, chat_id=chat_id)
    res = await db.execute(
        select(ChatMember, User)
        .join(User, User.id == ChatMember.user_id)
        .where(ChatMember.chat_id == chat_id)
    )
    members: list[ChatMemberOut] = []
    for cm, u in res.all():
        members.append(
            ChatMemberOut(
                user_id=u.id,
                username=u.username,
                avatar_path=u.avatar_path,
                bio=u.bio,
                status=u.status.value if hasattr(u.status, "value") else str(u.status),
                last_seen_at=u.last_seen_at,
            )
        )
    return ChatOut(id=str(chat_id), created_at=chat.created_at, members=members)


@router.get("/{chat_id}", response_model=ChatOut)
async def get_chat(
    chat_id: int,
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> ChatOut:
    return await _chat_to_out(db, current_user_id=current.id, chat_id=chat_id)
async def create_dialog(
    payload: CreateDialogRequest,
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> ChatOut:
    try:
        chat = await chat_service.get_or_create_dialog(db, user_id=current.id, peer_user_id=payload.peer_user_id)
        return await _chat_to_out(db, current_user_id=current.id, chat_id=chat.id)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))


@router.post("/dialogs/by-email", response_model=ChatOut)
async def create_dialog_by_email(
    payload: CreateDialogByEmailRequest,
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> ChatOut:
    """
    Создать (или получить) личный диалог по email собеседника.
    Удобно для UI: пользователь вводит email и сразу открывает чат.
    """
    try:
        chat = await chat_service.get_or_create_dialog_by_email(db, user_id=current.id, peer_email=str(payload.peer_email))
        return await _chat_to_out(db, current_user_id=current.id, chat_id=chat.id)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))


@router.get("/dialogs", response_model=list[DialogListItem])
async def list_dialogs(
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> list[DialogListItem]:
    chats = await chat_service.list_dialogs(db, user_id=current.id)
    out: list[DialogListItem] = []
    for chat in chats:
        # last message
        res = await db.execute(
            select(Message).where(Message.chat_id == chat.id, Message.is_deleted.is_(False)).order_by(Message.created_at.desc()).limit(1)
        )
        last = res.scalar_one_or_none()
        unread = await chat_service.unread_count(db, user_id=current.id, chat_id=chat.id)
        chat_out = await _chat_to_out(db, current_user_id=current.id, chat_id=chat.id)
        out.append(
            DialogListItem(
                chat=chat_out,
                last_message_preview=(last.content_text[:80] if last and last.content_text else None),
                last_message_at=(last.created_at if last else None),
                unread_count=unread,
            )
        )
    return out


@router.post("/dialogs/by-username", response_model=ChatOut)
async def create_dialog_by_username(
    payload: CreateDialogByUsernameRequest,
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> ChatOut:
    """
    Создать (или получить) личный диалог по username собеседника.
    Удобно для UI: пользователь вводит username и сразу открывает чат.
    """
    try:
        chat = await chat_service.get_or_create_dialog_by_username(
            db, user_id=current.id, username=payload.username
        )
        return await _chat_to_out(db, current_user_id=current.id, chat_id=chat.id)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))


@router.get("/contacts/search")
async def search_contacts(
    q: str = Query(min_length=1, max_length=64),
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> list[dict]:
    # Поиск по username (приоритет) и email. Возвращаем минимальный DTO.
    res = await db.execute(
        select(User)
        .where(User.id != current.id, (User.username.ilike(f"%{q}%")) | (User.email.ilike(f"%{q}%")))
        .limit(20)
    )
    users = res.scalars().all()
    return [
        {
            "id": u.id,
            "email": u.email,
            "username": u.username,
            "avatar_path": u.avatar_path,
            "status": u.status.value if hasattr(u.status, "value") else str(u.status),
            "last_seen_at": u.last_seen_at,
        }
        for u in users
    ]


