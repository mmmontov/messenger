from __future__ import annotations

import enum
from datetime import datetime
from typing import Optional

from sqlalchemy import Boolean, DateTime, Enum, ForeignKey, Integer, Text, UniqueConstraint, func
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.db.base import Base


class MessageType(str, enum.Enum):
    text = "text"
    image = "image"
    file = "file"


class DeliveryStatus(str, enum.Enum):
    sent = "sent"
    delivered = "delivered"
    read = "read"


class Message(Base):
    __tablename__ = "messages"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    chat_id: Mapped[int] = mapped_column(ForeignKey("chats.id", ondelete="CASCADE"), index=True, nullable=False)
    sender_id: Mapped[int] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True, nullable=False)

    content_text: Mapped[Optional[str]] = mapped_column(Text, nullable=True)
    message_type: Mapped[MessageType] = mapped_column(Enum(MessageType), nullable=False, default=MessageType.text)

    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
    edited_at: Mapped[Optional[datetime]] = mapped_column(DateTime(timezone=True), nullable=True)
    is_deleted: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)

    chat: Mapped["Chat"] = relationship(back_populates="messages")
    sender: Mapped["User"] = relationship()
    statuses: Mapped[list["MessageStatus"]] = relationship(back_populates="message", cascade="all, delete-orphan")
    files: Mapped[list["File"]] = relationship(back_populates="message", cascade="all, delete-orphan")


class MessageStatus(Base):
    __tablename__ = "message_status"
    __table_args__ = (UniqueConstraint("message_id", "user_id", name="uq_message_status_user"),)

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    message_id: Mapped[int] = mapped_column(
        ForeignKey("messages.id", ondelete="CASCADE"), index=True, nullable=False
    )
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True, nullable=False)
    status: Mapped[DeliveryStatus] = mapped_column(Enum(DeliveryStatus), nullable=False, default=DeliveryStatus.sent)
    updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), onupdate=func.now())

    message: Mapped[Message] = relationship(back_populates="statuses")
    user: Mapped["User"] = relationship()


from app.models.chat import Chat  
from app.models.file import File  
from app.models.user import User  


