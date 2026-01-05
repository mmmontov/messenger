### Messenger (FastAPI + Avalonia)

Учебный, но архитектурно цельный проект десктопного мессенджера:
- **Backend**: FastAPI + async SQLAlchemy + SQLite, JWT (access+refresh), WebSocket realtime, локальное хранение файлов
- **Client**: C# + Avalonia UI + MVVM (в разработке в этом репозитории)

### Запуск через Docker (рекомендуется)

- **Backend**:

```bash
docker compose up --build
```

Backend будет доступен на `http://127.0.0.1:8000`, WS на `ws://127.0.0.1:8000/ws/chat`.

- **Сборка клиента в Docker (артефакт)**:

```bash
docker compose --profile build build client-build
```

Важно: **GUI Avalonia** не предназначен для запуска “в контейнере” без отдельной GUI-среды, поэтому Docker для клиента здесь используется как **build-container**.

### Запуск локально (Windows)

#### Backend

1) Перейди в папку `backend/`
2) Создай venv и установи зависимости:

```powershell
py -3 -m venv .venv
.\.venv\Scripts\python -m pip install -r requirements.txt
```

3) Запусти:

```powershell
$env:PYTHONPATH = (Resolve-Path .).Path
.\.venv\Scripts\python -m uvicorn app.main:app --reload --host 127.0.0.1 --port 8000
```

#### Client

```powershell
dotnet run --project client/Messenger.Client/Messenger.Client.csproj
```


