from __future__ import annotations

import asyncio
from collections import defaultdict
from typing import Any

from fastapi import WebSocket


class WebSocketManager:
    """
    Менеджер соединений:
    - user_id -> set[WebSocket] (несколько устройств/окон)
    - send_to_user / broadcast_to_users
    """

    def __init__(self) -> None:
        self._user_sockets: dict[int, set[WebSocket]] = defaultdict(set)
        self._lock = asyncio.Lock()

    async def connect(self, user_id: int, websocket: WebSocket) -> None:
        await websocket.accept()
        async with self._lock:
            self._user_sockets[user_id].add(websocket)

    async def disconnect(self, user_id: int, websocket: WebSocket) -> None:
        async with self._lock:
            sockets = self._user_sockets.get(user_id)
            if not sockets:
                return
            sockets.discard(websocket)
            if not sockets:
                self._user_sockets.pop(user_id, None)

    async def send_to_user(self, user_id: int, message: dict[str, Any]) -> None:
        async with self._lock:
            sockets = list(self._user_sockets.get(user_id, set()))
        for ws in sockets:
            try:
                await ws.send_json(message)
            except Exception:
                # Сокет мог умереть — игнорируем, cleanup будет на disconnect.
                pass

    async def broadcast_to_users(self, user_ids: list[int], message: dict[str, Any]) -> None:
        await asyncio.gather(*(self.send_to_user(uid, message) for uid in user_ids))

    def is_user_online(self, user_id: int) -> bool:
        return bool(self._user_sockets.get(user_id))


ws_manager = WebSocketManager()


