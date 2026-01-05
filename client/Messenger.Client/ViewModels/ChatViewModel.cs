using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Messenger.Client.Models;
using Messenger.Client.Services;
using Messenger.Client.Stores;
using Messenger.Client.Utils;

namespace Messenger.Client.ViewModels;

public sealed class ChatViewModel : ViewModelBase
{
    private readonly ApiService _api;
    private readonly WebSocketService _ws;
    private readonly NavigationStore _nav;
    private readonly IServiceProvider _sp;
    private readonly ChatStore _chatStore;
    private readonly UserStore _userStore;

    private Chat? _chat;
    public Chat? Chat { get => _chat; private set => SetField(ref _chat, value); }

    public ObservableCollection<Message> Messages { get; } = new();
    
    public event Action? MessageSent;

    private string _text = "";
    public string Text { get => _text; set => SetField(ref _text, value); }

    private string _error = "";
    public string Error { get => _error; set => SetField(ref _error, value); }

    private Message? _editingMessage;
    public Message? EditingMessage { get => _editingMessage; private set => SetField(ref _editingMessage, value); }

    public bool IsEditing => EditingMessage is not null;

    private Message? _selectedMessage;
    public Message? SelectedMessage { get => _selectedMessage; set => SetField(ref _selectedMessage, value); }

    public RelayCommand BackCommand { get; }
    public RelayCommand SendCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand CancelEditCommand { get; }

    public ChatViewModel(ApiService api, WebSocketService ws, NavigationStore nav, IServiceProvider sp, ChatStore chatStore, UserStore userStore)
    {
        _api = api;
        _ws = ws;
        _nav = nav;
        _sp = sp;
        _chatStore = chatStore;
        _userStore = userStore;

        BackCommand = new RelayCommand(async (_) => _nav.CurrentViewModel = (ViewModelBase)_sp.GetService(typeof(ChatListViewModel))!);

        SendCommand = new RelayCommand(async (_) => await SendMessage());

        EditCommand = new RelayCommand(async (param) =>
        {
            if (param is not Message message || message.SenderId != _userStore.Me?.Id) return;
            StartEditMessage(message);
        });

        DeleteCommand = new RelayCommand(async (param) =>
        {
            if (param is not Message message || message.SenderId != _userStore.Me?.Id) return;
            try
            {
                await _api.Delete<object>($"/messages/{message.Id}", new { for_everyone = true });
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
        });

        CancelEditCommand = new RelayCommand(async (_) =>
        {
            EditingMessage = null;
            Text = "";
        });

        _chatStore.MessagesChanged += id =>
        {
            if (Chat?.Id != id) return;
            LoadFromStore();
        };

        // Обновляем только через store, без запросов к серверу
        _ws.DialogUpdated += chatId =>
        {
            if (Chat?.Id == chatId)
            {
                // Не перезагружаем историю, только обновляем из store
                LoadFromStore();
            }
        };

        // Обновляем статус собеседника при изменении presence
        _ws.PresenceUpdated += (userId, statusInt) =>
        {
            if (Chat is null) return;
            var otherMember = Chat.Members.FirstOrDefault(m => m.UserId == userId && m.UserId != _userStore.Me?.Id);
            if (otherMember is not null)
            {
                var statusStr = statusInt == 1 ? "online" : statusInt == 2 ? "dnd" : "offline";
                ChatStatus = statusStr;
            }
        };

        // Обновляем профиль пользователя
        _ws.UserUpdated += userId =>
        {
            if (Chat is null) return;
            var member = Chat.Members.FirstOrDefault(m => m.UserId == userId);
            if (member is not null)
            {
                // Обновляем данные члена чата (например, аватарку)
                // Поскольку данные приходят через WebSocket, нужно перезагрузить чат или обновить из API
                // Для простоты, перезагрузим чат
                _ = LoadChatDetails();
            }
        };
    }

    private string _chatTitle = "";
    public string ChatTitle { get => _chatTitle; private set => SetField(ref _chatTitle, value); }

    private string _chatStatus = "";
    public string ChatStatus { get => _chatStatus; private set => SetField(ref _chatStatus, value); }

    public bool IsMyMessage(Message message)
    {
        return message.SenderId == _userStore.Me?.Id;
    }

    public void StartEditMessage(Message message)
    {
        if (message.SenderId != _userStore.Me?.Id) return;
        EditingMessage = message;
        Text = message.ContentText ?? "";
    }

    public void SetChat(Chat chat)
    {
        Chat = chat;
        EditingMessage = null;
        Text = "";
        
        // Устанавливаем имя собеседника
        var otherMember = chat.Members.FirstOrDefault(m => m.UserId != _userStore.Me?.Id);
        ChatTitle = otherMember?.Username ?? "Chat";
        ChatStatus = otherMember?.Status ?? "offline";
        
        _ = LoadChatDetails();
        _ = LoadHistory();
        LoadFromStore();
        _ = MarkAsRead();
    }

    private async Task SendMessage()
    {
        if (Chat is null) return;
        var t = Text.Trim();
        if (t.Length == 0) return;

        // Сохраняем текст и очищаем поле сразу для лучшего UX
        var messageText = t;
        Text = "";

        try
        {
            if (EditingMessage is not null)
            {
                // Редактируем сообщение
                await _api.Patch<object>($"/messages/{EditingMessage.Id}", new { text = messageText });
                EditingMessage = null;
                SelectedMessage = null;
            }
            else
            {
                // Отправляем новое сообщение
                await _api.Post<object>("/messages/text", new { chat_id = Chat.Id, text = messageText });
            }
            
            // Уведомляем о том, что сообщение отправлено (для прокрутки вниз)
            MessageSent?.Invoke();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            // Восстанавливаем текст при ошибке
            Text = messageText;
        }
    }

    public async Task HandleKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter && !e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
        {
            e.Handled = true;
            await SendMessage();
            // Убеждаемся, что поле ввода полностью очищено
            Text = "";
        }
    }

    private async Task MarkAsRead()
    {
        if (Chat is null) return;
        try
        {
            await _api.Post<object>($"/messages/chat/{Chat.Id}/mark-read", new { });
        }
        catch { }
    }

    private void LoadFromStore()
    {
        if (Chat is null) return;
        var list = _chatStore.GetMessages(Chat.Id);
        
        // Обновляем только если список изменился (добавились новые сообщения)
        var currentIds = Messages.Select(m => m.Id).ToHashSet();
        var newIds = list.Select(m => m.Id).ToHashSet();
        
        // Если есть новые сообщения, добавляем только их
        var newMessages = list.Where(m => !currentIds.Contains(m.Id)).ToList();
        if (newMessages.Any())
        {
            foreach (var m in newMessages)
            {
                Messages.Add(m);
            }
        }
        
        // Если список полностью изменился (например, при первой загрузке), заменяем весь список
        if (Messages.Count == 0 && list.Any())
        {
            foreach (var m in list)
            {
                Messages.Add(m);
            }
        }
        
        // Обновляем существующие сообщения (если они были изменены)
        foreach (var msg in list)
        {
            var existing = Messages.FirstOrDefault(m => m.Id == msg.Id);
            if (existing is not null && existing != msg)
            {
                var index = Messages.IndexOf(existing);
                Messages[index] = msg;
            }
        }
    }
    

    private async Task LoadHistory()
    {
        if (Chat is null) return;
        try
        {
            Error = "";
            var dto = await _api.Get<List<MessageDto>>($"/messages/chat/{Chat.Id}?limit=100&offset=0");
            var messages = dto.Select(d => d.ToModel()).ToList();
            _chatStore.ReplaceMessages(Chat.Id, messages);
            await MarkAsRead();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private async Task LoadChatDetails()
    {
        if (Chat is null) return;
        try
        {
            Error = "";
            var dto = await _api.Get<List<MessageDto>>($"/messages/chat/{Chat.Id}?limit=100&offset=0");
            var messages = dto.Select(d => d.ToModel()).ToList();
            _chatStore.ReplaceMessages(Chat.Id, messages);
            await MarkAsRead();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
    private sealed class ChatDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
        [JsonPropertyName("members")] public List<ChatMemberDto> Members { get; set; } = new();

        public Chat ToModel() => new(Id, CreatedAt, Members.Select(m => m.ToModel()).ToList());
    }

    private sealed class ChatMemberDto
    {
        [JsonPropertyName("user_id")] public int UserId { get; set; }
        [JsonPropertyName("username")] public string Username { get; set; } = "";
        [JsonPropertyName("avatar_path")] public string? AvatarPath { get; set; }
        [JsonPropertyName("bio")] public string? Bio { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = "offline";
        [JsonPropertyName("last_seen_at")] public DateTime? LastSeenAt { get; set; }

        public ChatMember ToModel() => new(UserId, Username, AvatarPath, Bio, Status, LastSeenAt);
    }

    private sealed class MessageDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("chat_id")] public int ChatId { get; set; }
        [JsonPropertyName("sender_id")] public int SenderId { get; set; }
        [JsonPropertyName("content_text")] public string? ContentText { get; set; }
        [JsonPropertyName("message_type")] public string MessageType { get; set; } = "text";
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
        [JsonPropertyName("edited_at")] public DateTime? EditedAt { get; set; }
        [JsonPropertyName("is_deleted")] public bool IsDeleted { get; set; }
        [JsonPropertyName("files")] public List<FileDto> Files { get; set; } = new();

        public Message ToModel() => new(Id, ChatId, SenderId, ContentText, MessageType, CreatedAt, EditedAt, IsDeleted,
            Files.Select(f => f.ToModel()).ToList());
    }

    private sealed class FileDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("file_path")] public string FilePath { get; set; } = "";
        [JsonPropertyName("file_name")] public string FileName { get; set; } = "";
        [JsonPropertyName("file_size")] public long FileSize { get; set; }
        [JsonPropertyName("mime_type")] public string MimeType { get; set; } = "application/octet-stream";

        public FileAttachment ToModel() => new(Id, FilePath, FileName, FileSize, MimeType);
    }
}


