using System;

namespace Messenger.Client.Stores;

public sealed class AuthStore
{
    public int? UserId { get; private set; }
    public string? Username { get; private set; }
    public string? SessionId { get; private set; }
    public bool IsAuthenticated => UserId.HasValue && !string.IsNullOrEmpty(SessionId);

    public event Action? AuthChanged;

    public void SetAuthenticated(int? userId, string? username, string? sessionId = null)
    {
        UserId = userId;
        Username = username;
        SessionId = sessionId;
        AuthChanged?.Invoke();
    }

    public void LoadFromDisk()
    {
        // Cookies хранятся в HttpClient, здесь ничего не нужно
    }
}


