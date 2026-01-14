from __future__ import annotations

from datetime import datetime
from typing import Optional

from pydantic import BaseModel, Field
from pydantic import EmailStr


class ChatMemberOut(BaseModel):
    user_id: int
    username: str
    avatar_path: Optional[str] = None
    bio: Optional[str] = None
    status: str
    last_seen_at: Optional[datetime] = None


class ChatOut(BaseModel):
    id: str
    created_at: datetime
    members: list[ChatMemberOut]


class CreateDialogRequest(BaseModel):
    peer_user_id: int = Field(gt=0)


class CreateDialogByEmailRequest(BaseModel):
    peer_email: EmailStr


class CreateDialogByUsernameRequest(BaseModel):
    username: str = Field(min_length=2, max_length=64)


class DialogListItem(BaseModel):
    chat: ChatOut
    last_message_preview: Optional[str] = None
    last_message_at: Optional[datetime] = None


