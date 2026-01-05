from __future__ import annotations

from pathlib import Path

from fastapi import APIRouter, Depends, HTTPException, UploadFile, status
from fastapi.responses import FileResponse
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.dependencies import get_current_user
from app.db.session import get_db
from app.models.file import File
from app.models.message import MessageType
from app.models.user import User
from app.services.chat_service import chat_service
from app.services.message_service import message_service
from app.utils.file_storage import save_upload
from app.core.websocket_manager import ws_manager


router = APIRouter(prefix="/files", tags=["files"])


@router.post("/chat/{chat_id}/upload")
async def upload_to_chat(
    chat_id: int,
    file: UploadFile,
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> dict:
    """
    Загружает файл и создаёт сообщение типа image/file.
    Для изображений можно использовать content-type image/*.
    """
    try:
        await chat_service.get_chat(db, user_id=current.id, chat_id=chat_id)
        member_ids = await chat_service.list_member_ids(db, chat_id=chat_id)

        is_image = (file.content_type or "").startswith("image/")
        category = "images" if is_image else "documents"
        stored = await save_upload(category, file)

        # Создаём сообщение типа image/file.
        msg_type = MessageType.image if is_image else MessageType.file
        msg = await message_service.create_message(
            db, chat_id=chat_id, sender_id=current.id, message_type=msg_type, text=None, member_ids=member_ids
        )

        f = await message_service.attach_file(
            db,
            message=msg,
            stored_path=stored.relative_path,
            file_name=stored.file_name,
            file_size=stored.file_size,
            mime_type=stored.mime_type,
        )
        # Реалтайм: уведомляем участников.
        await ws_manager.broadcast_to_users(
            member_ids,
            {
                "type": "message.new",
                "message": {
                    "id": msg.id,
                    "chat_id": msg.chat_id,
                    "sender_id": msg.sender_id,
                    "content_text": msg.content_text,
                    "message_type": msg.message_type.value,
                    "created_at": msg.created_at.isoformat(),
                    "edited_at": None,
                    "is_deleted": msg.is_deleted,
                    "files": [
                        {
                            "id": f.id,
                            "file_path": f.file_path,
                            "file_name": f.file_name,
                            "file_size": f.file_size,
                            "mime_type": f.mime_type,
                        }
                    ],
                },
            },
        )
        return {"ok": True, "message_id": msg.id, "file_id": f.id}
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))


@router.get("/{file_id}")
async def download_file(
    file_id: int,
    current: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
) -> FileResponse:
    res = await db.execute(select(File).where(File.id == file_id))
    f = res.scalar_one_or_none()
    if not f:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="File not found")

    # Проверяем, что пользователь участник чата сообщения.
    # (file -> message_id -> message.chat_id)
    from app.models.message import Message

    res_m = await db.execute(select(Message).where(Message.id == f.message_id))
    msg = res_m.scalar_one_or_none()
    if not msg:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Message not found")
    try:
        await chat_service.get_chat(db, user_id=current.id, chat_id=msg.chat_id)
    except ValueError:
        raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="Forbidden")

    path = Path(f.file_path)
    if not path.exists():
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="File missing on disk")
    return FileResponse(path=path, media_type=f.mime_type, filename=f.file_name)


