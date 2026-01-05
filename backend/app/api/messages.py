from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException, Query, status
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.dependencies import get_current_user
from app.core.websocket_manager import ws_manager
from app.db.session import get_db
from app.models.message import DeliveryStatus
from app.models.message import MessageType
from app.models.user import User
from app.schemas.message import (
    DeleteMessageRequest,
    EditMessageRequest,
    MessageOut,
    SendTextMessageRequest,
    UpdateMessageStatusRequest,
)
from app.services.chat_service import chat_service
from app.services.message_service import message_service


router = APIRouter(prefix="/messages", tags=["messages"])


def _message_to_out(msg) -> MessageOut:
    return MessageOut(
        id=msg.id,
        chat_id=msg.chat_id,
        sender_id=msg.sender_id,
        content_text=msg.content_text,
        message_type=msg.message_type.value if hasattr(msg.message_type, "value") else str(msg.message_type),
        created_at=msg.created_at,
        edited_at=msg.edited_at,
        is_deleted=msg.is_deleted,
        files=[
            {
                "id": f.id,
                "file_path": f.file_path,
                "file_name": f.file_name,
                "file_size": f.file_size,
                "mime_type": f.mime_type,
            }
            for f in getattr(msg, "files", []) or []
        ],
    )


@router.get("/chat/{chat_id}", response_model=list[MessageOut])
async def list_messages(
    chat_id: int,
    limit: int = Query(default=50, ge=1, le=200),
    offset: int = Query(default=0, ge=0),
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> list[MessageOut]:
    try:
        await chat_service.get_chat(db, user_id=current.id, chat_id=chat_id)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=str(e))
    msgs = await message_service.list_messages(db, chat_id=chat_id, limit=limit, offset=offset)
    # Возвращаем в порядке created_at ASC для UI
    return [_message_to_out(m) for m in reversed(msgs)]


@router.post("/text", response_model=MessageOut)
async def send_text(
    payload: SendTextMessageRequest,
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> MessageOut:
    try:
        await chat_service.get_chat(db, user_id=current.id, chat_id=payload.chat_id)
        member_ids = await chat_service.list_member_ids(db, chat_id=payload.chat_id)
        msg = await message_service.create_message(
            db, chat_id=payload.chat_id, sender_id=current.id, message_type=MessageType.text, text=payload.text, member_ids=member_ids
        )
        
        # Отправляем уведомление через WebSocket всем участникам чата
        payload_ws = {
            "type": "message.new",
            "message": {
                "id": msg.id,
                "chat_id": msg.chat_id,
                "sender_id": msg.sender_id,
                "content_text": msg.content_text,
                "message_type": msg.message_type.value,
                "created_at": msg.created_at.isoformat(),
                "edited_at": msg.edited_at.isoformat() if msg.edited_at else None,
                "is_deleted": msg.is_deleted,
                "files": [],
            },
        }
        await ws_manager.broadcast_to_users(member_ids, payload_ws)
        
        return _message_to_out(msg)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))


@router.patch("/{message_id}", response_model=MessageOut)
async def edit_message(
    message_id: int,
    payload: EditMessageRequest,
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> MessageOut:
    try:
        msg = await message_service.edit_message(db, message_id=message_id, user_id=current.id, new_text=payload.text)
        
        # Уведомляем участников чата об изменении
        member_ids = await chat_service.list_member_ids(db, chat_id=msg.chat_id)
        payload_ws = {
            "type": "message.edited",
            "message": {
                "id": msg.id,
                "chat_id": msg.chat_id,
                "content_text": msg.content_text,
                "edited_at": msg.edited_at.isoformat() if msg.edited_at else None,
            },
        }
        await ws_manager.broadcast_to_users(member_ids, payload_ws)
        
        return _message_to_out(msg)
    except ValueError as e:
        code = status.HTTP_403_FORBIDDEN if str(e) == "Forbidden" else status.HTTP_400_BAD_REQUEST
        raise HTTPException(status_code=code, detail=str(e))


@router.delete("/{message_id}", response_model=dict)
async def delete_message(
    message_id: int,
    payload: DeleteMessageRequest,
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> dict:
    try:
        result = await message_service.delete_message(
            db, message_id=message_id, user_id=current.id, for_everyone=payload.for_everyone
        )
        
        # Уведомляем участников чата об удалении
        member_ids = await chat_service.list_member_ids(db, chat_id=result["chat_id"])
        payload_ws = {
            "type": "message.deleted",
            "message_id": message_id,
            "chat_id": result["chat_id"],
            "for_everyone": payload.for_everyone,
        }
        await ws_manager.broadcast_to_users(member_ids, payload_ws)
        
        return result
    except ValueError as e:
        code = status.HTTP_403_FORBIDDEN if str(e) == "Forbidden" else status.HTTP_400_BAD_REQUEST
        raise HTTPException(status_code=code, detail=str(e))


@router.post("/{message_id}/status")
async def update_status(
    message_id: int,
    payload: UpdateMessageStatusRequest,
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> dict:
    try:
        st = DeliveryStatus(payload.status)
        ms = await message_service.mark_status(db, message_id=message_id, user_id=current.id, status=st)
        
        # Уведомляем отправителя об изменении статуса
        from app.models.message import Message
        from sqlalchemy import select
        res_m = await db.execute(select(Message).where(Message.id == message_id))
        m = res_m.scalar_one_or_none()
        if m:
            await ws_manager.send_to_user(
                m.sender_id,
                {
                    "type": "message.status",
                    "message_id": ms.message_id,
                    "user_id": ms.user_id,
                    "status": ms.status.value,
                    "updated_at": (ms.updated_at.isoformat() if ms.updated_at else None),
                },
            )
        
        return {"ok": True, "message_id": ms.message_id, "user_id": ms.user_id, "status": ms.status.value}
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))


@router.post("/chat/{chat_id}/mark-read")
async def mark_chat_read(
    chat_id: int,
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> dict:
    """Отметить все сообщения в чате как прочитанные"""
    try:
        await chat_service.get_chat(db, user_id=current.id, chat_id=chat_id)
        from app.models.message import Message, MessageStatus
        from sqlalchemy import select, and_
        
        # Получаем все непрочитанные сообщения в чате
        res = await db.execute(
            select(Message.id)
            .where(
                and_(
                    Message.chat_id == chat_id,
                    Message.sender_id != current.id,
                    Message.is_deleted.is_(False),
                )
            )
        )
        message_ids = [int(x) for x in res.scalars().all()]
        
        # Обновляем статусы
        updated_count = 0
        for msg_id in message_ids:
            try:
                await message_service.mark_status(db, message_id=msg_id, user_id=current.id, status=DeliveryStatus.read)
                updated_count += 1
            except ValueError:
                pass  # Статус уже не существует или уже прочитан
        
        return {"ok": True, "updated_count": updated_count}
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))


