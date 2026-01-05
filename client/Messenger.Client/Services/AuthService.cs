using System.Text.Json.Serialization;
using Messenger.Client.Stores;

namespace Messenger.Client.Services;

public sealed class AuthService
{
    private readonly ApiService _api;
    private readonly AuthStore _auth;

    public AuthService(ApiService api, AuthStore auth)
    {
        _api = api;
        _auth = auth;
    }

    private sealed class LoginResponseDto
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("user_id")] public int UserId { get; set; }
        [JsonPropertyName("username")] public string Username { get; set; } = "";
    }

    public async Task Login(string email, string password)
    {
        // Cookie автоматически сохраняется в HttpClient
        var dto = await _api.Post<LoginResponseDto>("/auth/login", new { email, password });
        // Получаем session_id из cookie
        var sessionId = _api.GetSessionId();
        _auth.SetAuthenticated(dto.UserId, dto.Username, sessionId);
    }

    public async Task Register(string email, string password, string username)
    {
        // Cookie автоматически сохраняется в HttpClient
        var dto = await _api.Post<LoginResponseDto>("/auth/register", new { email, password, username });
        // Получаем session_id из cookie
        var sessionId = _api.GetSessionId();
        _auth.SetAuthenticated(dto.UserId, dto.Username, sessionId);
    }

    public async Task Logout()
    {
        await _api.Post<Dictionary<string, object>>("/auth/logout");
        _auth.SetAuthenticated(null, null);
    }
}


