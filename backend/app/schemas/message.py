from __future__ import annotations

from datetime import datetime
from typing import Optional

from pydantic import BaseModel, Field


class FileOut(BaseModel):
    id: int
    file_path: str
    file_name: str
    file_size: int
    mime_type: str


class MessageOut(BaseModel):
    id: int
    chat_id: int
    sender_id: int
    content_text: Optional[str] = None
    message_type: str
    created_at: datetime
    edited_at: Optional[datetime] = None
    is_deleted: bool
    files: list[FileOut] = []


class SendTextMessageRequest(BaseModel):
    chat_id: int = Field(gt=0)
    text: str = Field(min_length=1, max_length=4000)


class EditMessageRequest(BaseModel):
    text: str = Field(min_length=1, max_length=4000)


class DeleteMessageRequest(BaseModel):
    for_everyone: bool = True


class UpdateMessageStatusRequest(BaseModel):
    status: str = Field(pattern="^(delivered|read)$")


