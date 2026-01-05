from __future__ import annotations

from passlib.context import CryptContext


pwd_context = CryptContext(schemes=["bcrypt"], deprecated="auto")


def _ensure_bcrypt_length(password: str) -> None:
    # bcrypt ограничен 72 байтами (не символами).
    if len(password.encode("utf-8")) > 72:
        raise ValueError("Password must be at most 72 bytes (bcrypt limit).")


def hash_password(password: str) -> str:
    _ensure_bcrypt_length(password)
    return pwd_context.hash(password)


def verify_password(password: str, password_hash: str) -> bool:
    # verify тоже потенциально может упасть на некоторых реализациях — нормализуем сообщение.
    _ensure_bcrypt_length(password)
    return pwd_context.verify(password, password_hash)


