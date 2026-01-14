from __future__ import annotations

import mimetypes
import os
import secrets
from dataclasses import dataclass
from pathlib import Path

import aiofiles
from fastapi import UploadFile

from app.config import settings


@dataclass(frozen=True)
class StoredFile:
    relative_path: str
    file_name: str
    file_size: int
    mime_type: str


def _ensure_dirs() -> None:
    settings.avatars_dir.mkdir(parents=True, exist_ok=True)
    settings.images_dir.mkdir(parents=True, exist_ok=True)
    settings.documents_dir.mkdir(parents=True, exist_ok=True)


def _safe_filename(original: str) -> str:
    name = os.path.basename(original).replace("\x00", "")
    return name[:255] if name else "file"


def _pick_dir(category: str) -> Path:
    if category == "avatars":
        return settings.avatars_dir
    if category == "images":
        return settings.images_dir
    if category == "documents":
        return settings.documents_dir
    raise ValueError("Unknown category")


async def save_upload(category: str, upload: UploadFile) -> StoredFile:
    _ensure_dirs()
    dest_dir = _pick_dir(category)
    safe_name = _safe_filename(upload.filename or "file")
    ext = Path(safe_name).suffix
    token = secrets.token_hex(12)
    stored_name = f"{token}{ext}"
    dest_path = dest_dir / stored_name

    size = 0
    async with aiofiles.open(dest_path, "wb") as f:
        while True:
            chunk = await upload.read(1024 * 1024)
            if not chunk:
                break
            size += len(chunk)
            if size > settings.max_upload_mb * 1024 * 1024:
                raise ValueError("File too large")
            await f.write(chunk)

    mime = upload.content_type or mimetypes.guess_type(safe_name)[0] or "application/octet-stream"
    # relative_path относительно media_root
    rel = str(dest_path.relative_to(settings.media_root))
    return StoredFile(relative_path=rel, file_name=safe_name, file_size=size, mime_type=mime)


