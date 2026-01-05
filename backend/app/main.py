from __future__ import annotations

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles

from app.api.auth import router as auth_router
from app.api.chats import router as chats_router
from app.api.files import router as files_router
from app.api.messages import router as messages_router
from app.api.users import router as users_router
from app.config import settings
from app.db.init_db import init_db
from app.ws.chat_ws import router as ws_router


def create_app() -> FastAPI:
    app = FastAPI(title=settings.app_name)

    app.add_middleware(
        CORSMiddleware,
        allow_origins=settings.allowed_origins,
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    app.include_router(auth_router, prefix=settings.api_prefix)
    app.include_router(users_router, prefix=settings.api_prefix)
    app.include_router(chats_router, prefix=settings.api_prefix)
    app.include_router(messages_router, prefix=settings.api_prefix)
    app.include_router(files_router, prefix=settings.api_prefix)
    app.include_router(ws_router)

    # Статические файлы для медиа (аватары, изображения, документы)
    media_path = settings.project_root / settings.media_root
    if media_path.exists():
        app.mount("/media", StaticFiles(directory=str(media_path)), name="media")

    @app.on_event("startup")
    async def _startup() -> None:
        await init_db()

    return app


app = create_app()


