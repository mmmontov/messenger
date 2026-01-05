from __future__ import annotations

from datetime import datetime, timezone
from typing import Any

from fastapi import APIRouter, WebSocket, WebSocketDisconnect
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.session import is_session_expired
from app.core.websocket_manager import ws_manager
from app.db.session import AsyncSessionLocal
from app.models.chat import ChatMember
from app.models.message import DeliveryStatus, MessageType
from app.models.user import Session, User, UserStatus
from app.services.chat_service import chat_service
from app.services.message_service import message_service
from app.services.user_service import user_service


router = APIRouter(tags=["ws"])


async def _get_user_by_session(db: AsyncSession, session_id: str) -> User:
    if not session_id:
        raise ValueError("No session")
    
    res = await db.execute(select(Session).where(Session.session_id == session_id))
    session = res.scalar_one_or_none()
    if not session:
        raise ValueError("Invalid session")
    
    if is_session_expired(session.expires_at):
        await db.delete(session)
        await db.commit()
        raise ValueError("Session expired")
    
    res = await db.execute(select(User).where(User.id == session.user_id))
    user = res.scalar_one_or_none()
    if not user or not user.is_active:
        raise ValueError("User not found or inactive")
    return user


async def _contact_ids(db: AsyncSession, user_id: int) -> list[int]:
    """
    Все собеседники пользователя (в личных диалогах).
    """
    res = await db.execute(select(ChatMember.chat_id).where(ChatMember.user_id == user_id))
    chat_ids = [int(x) for x in res.scalars().all()]
    if not chat_ids:
        return []
    res2 = await db.execute(select(ChatMember.user_id).where(ChatMember.chat_id.in_(chat_ids), ChatMember.user_id != user_id))
    return sorted(set(int(x) for x in res2.scalars().all()))


@router.websocket("/ws/chat")
async def chat_socket(ws: WebSocket) -> None:
    session_id = ws.query_params.get("session_id") or ""
    async with AsyncSessionLocal() as db:
        try:
            user = await _get_user_by_session(db, session_id)
        except ValueError:
            await ws.close(code=4401)
            return

        await ws_manager.connect(user.id, ws)
        await user_service.set_online(db, user=user)

        # уведомляем контакты о статусе
        contacts = await _contact_ids(db, user.id)
        await ws_manager.broadcast_to_users(
            contacts,
            {"type": "presence.update", "user_id": user.id, "status": "online", "last_seen_at": None},
        )

        try:
            await ws.send_json({"type": "ws.ready", "user_id": user.id})
            while True:
                data: dict[str, Any] = await ws.receive_json()
                msg_type = data.get("type")

                if msg_type == "typing":
                    chat_id = int(data.get("chat_id") or 0)
                    is_typing = bool(data.get("is_typing", True))
                    if chat_id <= 0:
                        continue
                    # проверка членства + получение участников
                    await chat_service.get_chat(db, user_id=user.id, chat_id=chat_id)
                    member_ids = await chat_service.list_member_ids(db, chat_id=chat_id)
                    await ws_manager.broadcast_to_users(
                        [uid for uid in member_ids if uid != user.id],
                        {"type": "typing", "chat_id": chat_id, "user_id": user.id, "is_typing": is_typing},
                    )

                elif msg_type == "message.send":
                    chat_id = int(data.get("chat_id") or 0)
                    text = data.get("text")
                    if chat_id <= 0 or not isinstance(text, str) or not text.strip():
                        await ws.send_json({"type": "error", "code": "bad_request", "message": "Invalid message"})
                        continue
                    await chat_service.get_chat(db, user_id=user.id, chat_id=chat_id)
                    member_ids = await chat_service.list_member_ids(db, chat_id=chat_id)
                    msg = await message_service.create_message(
                        db,
                        chat_id=chat_id,
                        sender_id=user.id,
                        message_type=MessageType.text,
                        text=text,
                        member_ids=member_ids,
                    )
                    payload = {
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
                    await ws_manager.broadcast_to_users(member_ids, payload)

                elif msg_type == "message.status":
                    message_id = int(data.get("message_id") or 0)
                    status_str = data.get("status")
                    if message_id <= 0 or status_str not in ("delivered", "read"):
                        continue
                    st = DeliveryStatus(status_str)
                    ms = await message_service.mark_status(db, message_id=message_id, user_id=user.id, status=st)

                    # находим sender_id для уведомления
                    from app.models.message import Message

                    res_m = await db.execute(select(Message).where(Message.id == message_id))
                    m = res_m.scalar_one_or_none()
                    if not m:
                        continue
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

                else:
                    await ws.send_json({"type": "error", "code": "unknown_type", "message": "Unknown event type"})

        except WebSocketDisconnect:
            pass
        finally:
            await ws_manager.disconnect(user.id, ws)

            # offline только если нет других сокетов этого пользователя
            if not ws_manager.is_user_online(user.id):
                # refresh user instance for commit safety
                res = await db.execute(select(User).where(User.id == user.id))
                u = res.scalar_one_or_none()
                if u:
                    u.status = UserStatus.offline
                    u.last_seen_at = datetime.now(timezone.utc)
                    await db.commit()

                contacts = await _contact_ids(db, user.id)
                await ws_manager.broadcast_to_users(
                    contacts,
                    {
                        "type": "presence.update",
                        "user_id": user.id,
                        "status": "offline",
                        "last_seen_at": datetime.now(timezone.utc).isoformat(),
                    },
                )


