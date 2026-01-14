using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Messenger.Client.Models;
using Messenger.Client.Services;
using Messenger.Client.Stores;
using Messenger.Client.Utils;

namespace Messenger.Client.ViewModels;

public sealed class ChatListViewModel : ViewModelBase
{
    private readonly ApiService _api;
    private readonly NavigationStore _nav;
    private readonly IServiceProvider _sp;
    private readonly ChatStore _chatStore;
    private readonly WebSocketService _ws;
    private readonly UserStore _userStore;
    private System.Threading.Timer? _debounceTimer;

    public ObservableCollection<DialogListItem> Dialogs { get; } = new();

    private string _search = "";
    public string Search { get => _search; set => SetField(ref _search, value); }

    private string _error = "";
    public string Error { get => _error; set => SetField(ref _error, value); }

    private bool _notificationsEnabled = true;
    public bool NotificationsEnabled { get => _userStore.NotificationsEnabled; set { _userStore.NotificationsEnabled = value; RaisePropertyChanged(); } }

    public bool NotificationSound { get => _userStore.NotificationSound; set { _userStore.NotificationSound = value; RaisePropertyChanged(); } }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand OpenProfileCommand { get; }
    public RelayCommand StartChatCommand { get; }
    public RelayCommand ToggleNotificationsCommand { get; }

    public ChatListViewModel(ApiService api, NavigationStore nav, IServiceProvider sp, ChatStore chatStore, WebSocketService ws, UserStore userStore)
    {
        _api = api;
        _nav = nav;
        _sp = sp;
        _chatStore = chatStore;
        _ws = ws;
        _userStore = userStore;

        RefreshCommand = new RelayCommand(async (_) => await LoadDialogs());
        OpenProfileCommand = new RelayCommand(async (_) => _nav.CurrentViewModel = (ViewModelBase)_sp.GetService(typeof(ProfileViewModel))!);
        StartChatCommand = new RelayCommand(async (_) => await StartChat());
        ToggleNotificationsCommand = new RelayCommand(async (_) => NotificationSound = !NotificationSound);

        // Подписываемся на события с дебаунсингом
        _chatStore.DialogsChanged += DebouncedLoadDialogs;
        _ws.DialogUpdated += _ => DebouncedLoadDialogs();
        // PresenceUpdated не требует полной перезагрузки - обновляем только UI
        _ws.PresenceUpdated += (userId, status) => UpdatePresenceInUI(userId, status);

        _ = LoadDialogs();
    }

    private void DebouncedLoadDialogs()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(_ => _ = LoadDialogs(), null, 500, Timeout.Infinite);
    }

    private void UpdatePresenceInUI(int userId, int status)
    {
        // Обновляем статус в UI без запроса к серверу
        foreach (var dialog in Dialogs)
        {
            var member = dialog.Chat.Members.FirstOrDefault(m => m.UserId == userId);
            if (member is not null)
            {
                var statusStr = status == 1 ? "online" : status == 2 ? "dnd" : "offline";
                // Обновляем через store или напрямую в UI
            }
        }
    }

    public async Task LoadDialogs()
    {
        try
        {
            Error = "";
            var dialogs = await _api.Get<List<DialogListItemDto>>("/chats/dialogs");
            var mapped = dialogs.Select(d => d.ToModel()).ToList();
            _chatStore.SetDialogs(mapped);

            Dialogs.Clear();
            foreach (var d in mapped) Dialogs.Add(d);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    public void OpenChat(DialogListItem item)
    {
        _chatStore.SetActiveChat(item.Chat.Id);
        var vm = (ChatViewModel)_sp.GetService(typeof(ChatViewModel))!;
        vm.SetChat(item.Chat);
        _nav.CurrentViewModel = vm;
    }

    public async Task StartChat()
    {
        try
        {
            Error = "";
            var username = (Search ?? "").Trim();
            if (username.Length == 0)
            {
                Error = "Введите имя пользователя";
                return;
            }

            var chat = await _api.Post<ChatDto>("/chats/dialogs/by-username", new { username });
            // Обновляем список и открываем чат
            await LoadDialogs();
            var vm = (ChatViewModel)_sp.GetService(typeof(ChatViewModel))!;
            vm.SetChat(chat.ToModel());
            _nav.CurrentViewModel = vm;
            Search = ""; // Очищаем поле поиска
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private sealed class DialogListItemDto
    {
        [JsonPropertyName("chat")] public ChatDto Chat { get; set; } = new();
        [JsonPropertyName("last_message_preview")] public string? LastMessagePreview { get; set; }
        [JsonPropertyName("last_message_at")] public DateTime? LastMessageAt { get; set; }

        public DialogListItem ToModel() => new(Chat.ToModel(), LastMessagePreview, LastMessageAt);
    }

    public ChatMember? GetOtherMember(DialogListItem item)
    {
        if (_userStore.Me is null) return item.Chat.Members.FirstOrDefault();
        return item.GetOtherMember(_userStore.Me.Id);
    }

    private sealed class ChatDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
        [JsonPropertyName("members")] public List<MemberDto> Members { get; set; } = new();

        public Chat ToModel() => new(Id, CreatedAt, Members.Select(m => m.ToModel()).ToList());
    }

    private sealed class MemberDto
    {
        [JsonPropertyName("user_id")] public int UserId { get; set; }
        [JsonPropertyName("username")] public string Username { get; set; } = "";
        [JsonPropertyName("avatar_path")] public string? AvatarPath { get; set; }
        [JsonPropertyName("bio")] public string? Bio { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = "offline";
        [JsonPropertyName("last_seen_at")] public DateTime? LastSeenAt { get; set; }

        public ChatMember ToModel() => new(UserId, Username, AvatarPath, Bio, Status, LastSeenAt);
    }
}


