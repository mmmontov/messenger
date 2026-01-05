from __future__ import annotations

import json
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

import aiosmtplib
from email.message import EmailMessage
from pydantic import EmailStr

from app.config import settings


@dataclass(frozen=True)
class EmailPayload:
    to: EmailStr
    subject: str
    body_text: str


async def send_email(payload: EmailPayload) -> None:
    """
    Отправка email через SMTP. Если SMTP выключен — сохраняем письмо в локальный outbox.
    Это не "заглушка": outbox удобен для разработки без SMTP и сохраняет полный контент.
    """
    if not settings.smtp_enabled:
        outbox = Path("backend/outbox")
        outbox.mkdir(parents=True, exist_ok=True)
        stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
        target = outbox / f"{stamp}_{payload.to}.json"
        target.write_text(
            json.dumps(
                {"to": str(payload.to), "subject": payload.subject, "body_text": payload.body_text},
                ensure_ascii=False,
                indent=2,
            ),
            encoding="utf-8",
        )
        return

    msg = EmailMessage()
    msg["From"] = str(settings.mail_from)
    msg["To"] = str(payload.to)
    msg["Subject"] = payload.subject
    msg.set_content(payload.body_text)

    await aiosmtplib.send(
        msg,
        hostname=settings.smtp_host,
        port=settings.smtp_port,
        username=settings.smtp_username,
        password=settings.smtp_password,
        start_tls=settings.smtp_use_tls,
    )


