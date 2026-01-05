from __future__ import annotations

from datetime import datetime, timezone

from sqlalchemy import and_, desc, select
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from app.models.file import File
from app.models.message import DeliveryStatus, Message, MessageStatus, MessageType


class MessageService:
    async def list_messages(self, db: AsyncSession, *, chat_id: int, limit: int = 50, offset: int = 0) -> list[Message]:
        res = await db.execute(
            select(Message)
            .options(selectinload(Message.files))
            .where(Message.chat_id == chat_id)
            .order_by(desc(Message.created_at))
            .offset(offset)
            .limit(limit)
        )
        return list(res.scalars().unique().all())

    async def create_message(
        self,
        db: AsyncSession,
        *,
        chat_id: int,
        sender_id: int,
        message_type: MessageType,
        text: str | None,
        member_ids: list[int],
    ) -> Message:
        msg = Message(chat_id=chat_id, sender_id=sender_id, content_text=text, message_type=message_type)
        db.add(msg)
        await db.flush()  # получить msg.id

        statuses: list[MessageStatus] = []
        for uid in member_ids:
            statuses.append(MessageStatus(message_id=msg.id, user_id=uid, status=DeliveryStatus.sent))
        db.add_all(statuses)

        await db.commit()
        await db.refresh(msg)
        return msg

    async def attach_file(
        self,
        db: AsyncSession,
        *,
        message: Message,
        stored_path: str,
        file_name: str,
        file_size: int,
        mime_type: str,
    ) -> File:
        f = File(message_id=message.id, file_path=stored_path, file_name=file_name, file_size=file_size, mime_type=mime_type)
        db.add(f)
        await db.commit()
        await db.refresh(f)
        return f

    async def mark_status(
        self, db: AsyncSession, *, message_id: int, user_id: int, status: DeliveryStatus
    ) -> MessageStatus:
        res = await db.execute(
            select(MessageStatus).where(and_(MessageStatus.message_id == message_id, MessageStatus.user_id == user_id))
        )
        ms = res.scalar_one_or_none()
        if not ms:
            raise ValueError("Status row not found")

        # Переходы только вперёд.
        order = {DeliveryStatus.sent: 1, DeliveryStatus.delivered: 2, DeliveryStatus.read: 3}
        if order[status] >= order[ms.status]:
            ms.status = status
            ms.updated_at = datetime.now(timezone.utc)
            await db.commit()
            await db.refresh(ms)
        return ms

    async def edit_message(self, db: AsyncSession, *, message_id: int, user_id: int, new_text: str) -> Message:
        res = await db.execute(select(Message).options(selectinload(Message.files)).where(Message.id == message_id))
        msg = res.scalar_one_or_none()
        if not msg or msg.is_deleted:
            raise ValueError("Message not found")
        if msg.sender_id != user_id:
            raise ValueError("Forbidden")
        if msg.message_type != MessageType.text:
            raise ValueError("Only text messages can be edited")
        msg.content_text = new_text
        msg.edited_at = datetime.now(timezone.utc)
        await db.commit()
        await db.refresh(msg)
        return msg

    async def delete_message(self, db: AsyncSession, *, message_id: int, user_id: int, for_everyone: bool) -> Message:
        res = await db.execute(select(Message).options(selectinload(Message.files)).where(Message.id == message_id))
        msg = res.scalar_one_or_none()
        if not msg or msg.is_deleted:
            raise ValueError("Message not found")
        if msg.sender_id != user_id and for_everyone:
            raise ValueError("Forbidden")

        # Для учебного проекта: "удаление" — soft delete.
        msg.is_deleted = True
        await db.commit()
        await db.refresh(msg)
        return msg


message_service = MessageService()


