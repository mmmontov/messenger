using System;
using System.Collections.Generic;
using System.Linq;
using Messenger.Client.Models;

namespace Messenger.Client.Stores;

public sealed class ChatStore
{
    public IReadOnlyList<DialogListItem> Dialogs { get; private set; } = Array.Empty<DialogListItem>();
    public event Action? DialogsChanged;

    public string? ActiveChatId { get; private set; }
    public event Action? ActiveChatChanged;

    private readonly Dictionary<string, List<Message>> _messagesByChat = new();
    public event Action<string>? MessagesChanged;

    public void SetDialogs(IEnumerable<DialogListItem> dialogs)
    {
        Dialogs = dialogs.ToList();
        DialogsChanged?.Invoke();
    }

    public void SetActiveChat(string? chatId)
    {
        ActiveChatId = chatId;
        ActiveChatChanged?.Invoke();
    }

    public IReadOnlyList<Message> GetMessages(string chatId)
        => _messagesByChat.TryGetValue(chatId, out var list) ? list : Array.Empty<Message>();

    public void ReplaceMessages(string chatId, IEnumerable<Message> messages)
    {
        _messagesByChat[chatId] = messages.OrderBy(m => m.CreatedAt).ToList();
        MessagesChanged?.Invoke(chatId);
    }

    public void AddMessage(Message message)
    {
        var chatIdStr = message.ChatId.ToString();
        if (!_messagesByChat.TryGetValue(chatIdStr, out var list))
        {
            list = new List<Message>();
            _messagesByChat[chatIdStr] = list;
        }
        // Проверяем, нет ли уже такого сообщения
        if (list.Any(m => m.Id == message.Id)) return;
        list.Add(message);
        _messagesByChat[chatIdStr] = list.OrderBy(m => m.CreatedAt).ToList();
        MessagesChanged?.Invoke(chatIdStr);
    }

    public void UpdateMessage(string chatId, int messageId, string? newText, DateTime? editedAt)
    {
        if (!_messagesByChat.TryGetValue(chatId, out var list)) return;
        var msg = list.FirstOrDefault(m => m.Id == messageId);
        if (msg is null) return;
        var updated = msg with { ContentText = newText ?? msg.ContentText, EditedAt = editedAt ?? msg.EditedAt };
        var idx = list.IndexOf(msg);
        list[idx] = updated;
        MessagesChanged?.Invoke(chatId);
    }

    public void DeleteMessage(string chatId, int messageId)
    {
        if (!_messagesByChat.TryGetValue(chatId, out var list)) return;
        var msg = list.FirstOrDefault(m => m.Id == messageId);
        if (msg is null) return;
        var deleted = msg with { IsDeleted = true };
        var idx = list.IndexOf(msg);
        list[idx] = deleted;
        MessagesChanged?.Invoke(chatId);
    }
}


