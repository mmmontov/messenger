from __future__ import annotations

from datetime import datetime
from typing import Optional

from pydantic import BaseModel, EmailStr, Field


class UserSettingsOut(BaseModel):
    notifications_enabled: bool
    notification_sound: bool
    show_banner: bool


class UserOut(BaseModel):
    id: int
    email: EmailStr
    username: str
    bio: Optional[str] = None
    avatar_path: Optional[str] = None
    status: str
    is_active: bool
    created_at: datetime
    last_seen_at: Optional[datetime] = None
    settings: UserSettingsOut


class UserUpdateProfile(BaseModel):
    username: Optional[str] = Field(default=None, min_length=2, max_length=64)
    bio: Optional[str] = Field(default=None, max_length=400)
    status: Optional[str] = Field(default=None)


class UserUpdateSettings(BaseModel):
    notifications_enabled: Optional[bool] = None
    notification_sound: Optional[bool] = None
    show_banner: Optional[bool] = None


class ChangePasswordRequest(BaseModel):
    current_password: str = Field(min_length=8, max_length=128)
    new_password: str = Field(min_length=8, max_length=128)


class ChangeEmailRequest(BaseModel):
    new_email: EmailStr
    password: str = Field(min_length=8, max_length=128)


