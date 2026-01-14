- **Backend**:

```bash
docker compose up --build
```

Backend будет доступен на `http://127.0.0.1:8000`, WS на `ws://127.0.0.1:8000/ws/chat`.

#### Client

```powershell
dotnet run --project client/Messenger.Client/Messenger.Client.csproj
```


