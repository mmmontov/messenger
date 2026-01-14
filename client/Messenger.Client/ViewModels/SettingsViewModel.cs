using System.Text.Json.Serialization;
using Messenger.Client.Services;
using Messenger.Client.Stores;
using Messenger.Client.Utils;

namespace Messenger.Client.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ApiService _api;
    private readonly NavigationStore _nav;
    private readonly IServiceProvider _sp;
    private readonly AuthService _auth;
    private readonly WebSocketService _ws;
    private readonly UserStore _userStore;

    private bool _notificationsEnabled = true;
    public bool NotificationsEnabled { get => _notificationsEnabled; set { if (SetField(ref _notificationsEnabled, value)) { if (!value) { NotificationSound = false; ShowBanner = false; } } } }

    private bool _notificationSound = true;
    public bool NotificationSound { get => _notificationSound; set => SetField(ref _notificationSound, value); }

    private bool _showBanner = true;
    public bool ShowBanner { get => _showBanner; set => SetField(ref _showBanner, value); }

    private string _error = "";
    public string Error { get => _error; set => SetField(ref _error, value); }

    public RelayCommand BackCommand { get; }
    public RelayCommand LoadCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand LogoutCommand { get; }

    public SettingsViewModel(ApiService api, NavigationStore nav, IServiceProvider sp, AuthService auth, WebSocketService ws, UserStore userStore)
    {
        _api = api;
        _nav = nav;
        _sp = sp;
        _auth = auth;
        _ws = ws;
        _userStore = userStore;

        BackCommand = new RelayCommand(async (_) => _nav.CurrentViewModel = (ViewModelBase)_sp.GetService(typeof(ChatListViewModel))!);
        LoadCommand = new RelayCommand(async (_) => await Load());
        SaveCommand = new RelayCommand(async (_) => await Save());
        LogoutCommand = new RelayCommand(async (_) =>
        {
            await _ws.Disconnect();
            await _auth.Logout();
            _nav.CurrentViewModel = (ViewModelBase)_sp.GetService(typeof(LoginViewModel))!;
        });

        _ = Load();
    }

    private sealed class MeDto
    {
        [JsonPropertyName("settings")] public SettingsDto Settings { get; set; } = new();
    }

    private sealed class SettingsDto
    {
        [JsonPropertyName("notifications_enabled")] public bool NotificationsEnabled { get; set; }
        [JsonPropertyName("notification_sound")] public bool NotificationSound { get; set; }
        [JsonPropertyName("show_banner")] public bool ShowBanner { get; set; }
    }

    private async Task Load()
    {
        try
        {
            Error = "";
            var me = await _api.Get<MeDto>("/users/me");
            NotificationsEnabled = me.Settings.NotificationsEnabled;
            NotificationSound = me.Settings.NotificationSound;
            ShowBanner = me.Settings.ShowBanner;
            _userStore.NotificationsEnabled = NotificationsEnabled;
            _userStore.NotificationSound = NotificationSound;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private async Task Save()
    {
        try
        {
            Error = "";
            await _api.Patch<MeDto>("/users/me/settings", new
            {
                notifications_enabled = NotificationsEnabled,
                notification_sound = NotificationSound,
                show_banner = ShowBanner
            });
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}


