from __future__ import annotations

from pathlib import Path
from typing import Literal

from pydantic import EmailStr, Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """
    Центральная конфигурация приложения.
    По умолчанию настроена на локальную SQLite и локальное файловое хранилище.
    """

    model_config = SettingsConfigDict(env_prefix="MESSENGER_", env_file=".env", extra="ignore")

    # App
    app_name: str = "Messenger Backend"
    environment: Literal["dev", "prod"] = "dev"
    api_prefix: str = "/api"
    allowed_origins: list[str] = ["*"]

    # Database
    sqlite_path: str = Field(default="backend/app.db")
    db_echo: bool = False

    # Security / JWT
    jwt_secret_key: str = Field(default="change-me-in-env", min_length=16)
    jwt_algorithm: str = "HS256"
    access_token_exp_minutes: int = 15
    refresh_token_exp_days: int = 14

    # Password reset
    password_reset_token_exp_minutes: int = 30

    # Media storage
    project_root: Path = Field(default_factory=lambda: Path(__file__).resolve().parents[1].parents[0])
    media_root: Path = Field(default_factory=lambda: Path("backend/media"))
    avatars_dir: Path = Field(default_factory=lambda: Path("backend/media/avatars"))
    images_dir: Path = Field(default_factory=lambda: Path("backend/media/images"))
    documents_dir: Path = Field(default_factory=lambda: Path("backend/media/documents"))
    max_upload_mb: int = 25

    # Email (SMTP)
    smtp_enabled: bool = False
    smtp_host: str = "smtp.example.com"
    smtp_port: int = 587
    smtp_username: str = "user@example.com"
    smtp_password: str = "password"
    smtp_use_tls: bool = True
    mail_from: EmailStr = "noreply@example.com"

    # Frontend URLs (for reset links)
    password_reset_frontend_url: str = "http://localhost/reset-password"

    @property
    def sqlite_dsn(self) -> str:
        # aiosqlite + SQLAlchemy async engine
        # NB: абсолютный путь в Windows безопаснее, но оставляем относительный для учебного проекта.
        return f"sqlite+aiosqlite:///{self.sqlite_path}"


settings = Settings()


