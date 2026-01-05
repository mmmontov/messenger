from __future__ import annotations

from sqlalchemy import and_, func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.chat import Chat, ChatMember
from app.models.message import DeliveryStatus, Message, MessageStatus
from app.models.user import User


class ChatService:
    async def get_or_create_dialog(self, db: AsyncSession, *, user_id: int, peer_user_id: int) -> Chat:
        if user_id == peer_user_id:
            raise ValueError("Cannot create dialog with yourself")

        # Проверяем, что peer существует.
        res_peer = await db.execute(select(User).where(User.id == peer_user_id))
        if res_peer.scalar_one_or_none() is None:
            raise ValueError("Peer user not found")

        # Ищем чат, где ровно два участника: user_id и peer_user_id.
        cm = ChatMember
        subq = (
            select(cm.chat_id)
            .where(cm.user_id.in_([user_id, peer_user_id]))
            .group_by(cm.chat_id)
            .having(func.count(cm.user_id) == 2)
            .subquery()
        )
        res = await db.execute(select(Chat).where(Chat.id.in_(select(subq.c.chat_id))))
        chat = res.scalar_one_or_none()
        if chat:
            return chat

        chat = Chat()
        chat.members = [ChatMember(user_id=user_id), ChatMember(user_id=peer_user_id)]
        db.add(chat)
        await db.commit()
        await db.refresh(chat)
        return chat

    async def get_or_create_dialog_by_email(self, db: AsyncSession, *, user_id: int, peer_email: str) -> Chat:
        res_peer = await db.execute(select(User).where(User.email == peer_email))
        peer = res_peer.scalar_one_or_none()
        if not peer:
            raise ValueError("User not found")
        return await self.get_or_create_dialog(db, user_id=user_id, peer_user_id=peer.id)

    async def get_or_create_dialog_by_username(self, db: AsyncSession, *, user_id: int, username: str) -> Chat:
        res_peer = await db.execute(select(User).where(User.username == username))
        peer = res_peer.scalar_one_or_none()
        if not peer:
            raise ValueError("User not found")
        return await self.get_or_create_dialog(db, user_id=user_id, peer_user_id=peer.id)

    async def list_dialogs(self, db: AsyncSession, *, user_id: int) -> list[Chat]:
        res = await db.execute(
            select(Chat)
            .join(ChatMember, ChatMember.chat_id == Chat.id)
            .where(ChatMember.user_id == user_id)
            .order_by(Chat.created_at.desc())
        )
        return list(res.scalars().unique().all())

    async def get_chat(self, db: AsyncSession, *, user_id: int, chat_id: int) -> Chat:
        res = await db.execute(
            select(Chat)
            .join(ChatMember, ChatMember.chat_id == Chat.id)
            .where(and_(Chat.id == chat_id, ChatMember.user_id == user_id))
        )
        chat = res.scalar_one_or_none()
        if not chat:
            raise ValueError("Chat not found")
        return chat

    async def list_member_ids(self, db: AsyncSession, *, chat_id: int) -> list[int]:
        res = await db.execute(select(ChatMember.user_id).where(ChatMember.chat_id == chat_id))
        return [int(x) for x in res.scalars().all()]

    async def unread_count(self, db: AsyncSession, *, user_id: int, chat_id: int) -> int:
        # Непрочитанные = статусы != read для user_id
        res = await db.execute(
            select(func.count(MessageStatus.id))
            .join(Message, Message.id == MessageStatus.message_id)
            .where(
                and_(
                    Message.chat_id == chat_id,
                    MessageStatus.user_id == user_id,
                    MessageStatus.status != DeliveryStatus.read,
                    Message.is_deleted.is_(False),
                )
            )
        )
        return int(res.scalar_one() or 0)


chat_service = ChatService()


