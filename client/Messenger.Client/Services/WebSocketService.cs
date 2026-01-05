using System;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Messenger.Client.Models;
using Messenger.Client.Stores;

namespace Messenger.Client.Services;

public sealed class WebSocketService
{
    private readonly IConfiguration _config;
    private readonly AuthStore _auth;
    private readonly ChatStore _chatStore;
    private readonly UserStore _userStore;
    private readonly NotificationService _notifications;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;

    public event Action<string>? DialogUpdated; // chat_id
    public event Action<int, int>? PresenceUpdated; // user_id, status (0=offline, 1=online, 2=dnd)
    public event Action<int>? UserUpdated; // user_id

    public WebSocketService(IConfiguration config, AuthStore auth, ChatStore chatStore, UserStore userStore, NotificationService notifications)
    {
        _config = config;
        _auth = auth;
        _chatStore = chatStore;
        _userStore = userStore;
        _notifications = notifications;
    }

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public async Task Connect()
    {
        if (!_auth.IsAuthenticated || string.IsNullOrEmpty(_auth.SessionId))
            throw new InvalidOperationException("Not authenticated");
        if (IsConnected) return;

        var wsUrl = _config["Backend:WsUrl"] ?? "ws://127.0.0.1:8000/ws/chat";
        var uri = new Uri($"{wsUrl}?session_id={Uri.EscapeDataString(_auth.SessionId!)}");

        _ws = new ClientWebSocket();
        _cts = new CancellationTokenSource();
        await _ws.ConnectAsync(uri, _cts.Token);

        _ = Task.Run(() => ReceiveLoop(_cts.Token));
    }

    public async Task Disconnect()
    {
        if (_ws is null) return;
        try
        {
            _cts?.Cancel();
            if (_ws.State == WebSocketState.Open)
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        }
        catch { }
        finally
        {
            _ws.Dispose();
            _ws = null;
        }
    }

    public async Task SendTyping(int chatId, bool isTyping)
        => await SendJson(new { type = "typing", chat_id = chatId, is_typing = isTyping });

    public async Task SendMessage(string chatId, string text)
        => await SendJson(new { type = "message.send", chat_id = chatId, text });

    public async Task SendStatus(int messageId, string status)
        => await SendJson(new { type = "message.status", message_id = messageId, status });

    public async Task SendJson(object obj)
    {
        if (_ws is null || _ws.State != WebSocketState.Open) return;
        var json = JsonSerializer.Serialize(obj);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 64];
        while (!cancellationToken.IsCancellationRequested && _ws is not null && _ws.State == WebSocketState.Open)
        {
            var sb = new StringBuilder();
            WebSocketReceiveResult? result;
            do
            {
                result = await _ws.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) return;
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            } while (!result.EndOfMessage);

            var text = sb.ToString();
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                var type = root.GetProperty("type").GetString();
                
                if (type == "ws.ready")
                {
                    // WebSocket готов
                }
                else if (type == "message.new")
                {
                    var m = root.GetProperty("message");
                    var msg = ParseMessage(m);
                    _chatStore.AddMessage(msg);
                    DialogUpdated?.Invoke(msg.ChatId.ToString());
                    
                    // Показываем уведомление, если сообщение не от нас и мы не в этом чате
                    if (msg.SenderId != _userStore.Me?.Id && _chatStore.ActiveChatId != msg.ChatId.ToString())
                    {
                        var chat = _chatStore.Dialogs.FirstOrDefault(d => d.Chat.Id == msg.ChatId.ToString());
                        var senderName = chat?.Chat.Members.FirstOrDefault(mem => mem.UserId == msg.SenderId)?.Username ?? "Someone";
                        _notifications.ShowNotification("New message", $"{senderName}: {msg.ContentText ?? "(file)"}", int.Parse(chat?.Chat.Id ?? "0"));
                    }
                }
                else if (type == "message.edited")
                {
                    var m = root.GetProperty("message");
                    var msgId = m.GetProperty("id").GetInt32();
                    var chatId = m.GetProperty("chat_id").GetInt32();
                    var newText = m.TryGetProperty("content_text", out var ct) ? ct.GetString() : null;
                    var editedAt = m.TryGetProperty("edited_at", out var ed) && ed.ValueKind != JsonValueKind.Null 
                        ? DateTime.Parse(ed.GetString()!) : (DateTime?)null;
                    _chatStore.UpdateMessage(chatId.ToString(), msgId, newText, editedAt);
                }
                else if (type == "message.deleted")
                {
                    var chatId = root.GetProperty("chat_id").GetInt32();
                    var msgId = root.GetProperty("message_id").GetInt32();
                    _chatStore.DeleteMessage(chatId.ToString(), msgId);
                }
                else if (type == "message.status")
                {
                    var msgId = root.GetProperty("message_id").GetInt32();
                    var userId = root.GetProperty("user_id").GetInt32();
                    var status = root.GetProperty("status").GetString() ?? "sent";
                    // Обновляем статус в сообщении (если нужно)
                }
                else if (type == "presence.update")
                {
                    var userId = root.GetProperty("user_id").GetInt32();
                    var statusStr = root.GetProperty("status").GetString() ?? "offline";
                    var statusInt = statusStr == "online" ? 1 : statusStr == "dnd" ? 2 : 0;
                    PresenceUpdated?.Invoke(userId, statusInt);
                }
                else if (type == "user.updated")
                {
                    var userId = root.GetProperty("user_id").GetInt32();
                    UserUpdated?.Invoke(userId);
                }
            }
            catch
            {
                // ignore malformed
            }
        }
    }

    private static Message ParseMessage(JsonElement m)
    {
        var files = new List<FileAttachment>();
        if (m.TryGetProperty("files", out var filesEl) && filesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in filesEl.EnumerateArray())
            {
                files.Add(new FileAttachment(
                    f.GetProperty("id").GetInt32(),
                    f.GetProperty("file_path").GetString() ?? "",
                    f.GetProperty("file_name").GetString() ?? "",
                    f.GetProperty("file_size").GetInt64(),
                    f.GetProperty("mime_type").GetString() ?? "application/octet-stream"
                ));
            }
        }

        return new Message(
            m.GetProperty("id").GetInt32(),
            m.GetProperty("chat_id").GetInt32(),
            m.GetProperty("sender_id").GetInt32(),
            m.TryGetProperty("content_text", out var ct) && ct.ValueKind != JsonValueKind.Null ? ct.GetString() : null,
            m.GetProperty("message_type").GetString() ?? "text",
            DateTime.Parse(m.GetProperty("created_at").GetString() ?? DateTime.UtcNow.ToString("O")),
            m.TryGetProperty("edited_at", out var ed) && ed.ValueKind != JsonValueKind.Null ? DateTime.Parse(ed.GetString()!) : null,
            m.GetProperty("is_deleted").GetBoolean(),
            files
        );
    }
}


