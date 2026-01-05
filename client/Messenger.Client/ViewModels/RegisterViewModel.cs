using System.Text.Json.Serialization;
using Messenger.Client.Models;
using Messenger.Client.Services;
using Messenger.Client.Stores;
using Messenger.Client.Utils;

namespace Messenger.Client.ViewModels;

public sealed class RegisterViewModel : ViewModelBase
{
    private readonly AuthService _auth;
    private readonly NavigationStore _nav;
    private readonly IServiceProvider _sp;
    private readonly WebSocketService _ws;
    private readonly ApiService _api;
    private readonly UserStore _userStore;

    private string _email = "";
    public string Email { get => _email; set => SetField(ref _email, value); }

    private string _username = "";
    public string Username { get => _username; set => SetField(ref _username, value); }

    private string _password = "";
    public string Password { get => _password; set => SetField(ref _password, value); }

    private string _error = "";
    public string Error { get => _error; set => SetField(ref _error, value); }

    public RelayCommand RegisterCommand { get; }
    public RelayCommand GoLoginCommand { get; }

    public RegisterViewModel(AuthService auth, NavigationStore nav, IServiceProvider sp, WebSocketService ws, ApiService api, UserStore userStore)
    {
        _auth = auth;
        _nav = nav;
        _sp = sp;
        _ws = ws;
        _api = api;
        _userStore = userStore;

        RegisterCommand = new RelayCommand(async (_) =>
        {
            Error = "";
            try
            {
                await _auth.Register(Email.Trim(), Password, Username.Trim());
                await LoadUserProfile();
                await _ws.Connect();
                _nav.CurrentViewModel = (ViewModelBase)_sp.GetService(typeof(ChatListViewModel))!;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
        });

        GoLoginCommand = new RelayCommand(async (_) =>
        {
            _nav.CurrentViewModel = (ViewModelBase)_sp.GetService(typeof(LoginViewModel))!;
        });
    }

    private sealed class MeDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("email")] public string Email { get; set; } = "";
        [JsonPropertyName("username")] public string Username { get; set; } = "";
        [JsonPropertyName("bio")] public string? Bio { get; set; }
        [JsonPropertyName("avatar_path")] public string? AvatarPath { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = "offline";
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
        [JsonPropertyName("last_seen_at")] public DateTime? LastSeenAt { get; set; }

        public UserProfile ToModel() => new(Id, Email, Username, Bio, AvatarPath, Status, CreatedAt, LastSeenAt);
    }

    private async Task LoadUserProfile()
    {
        try
        {
            var me = await _api.Get<MeDto>("/users/me");
            _userStore.SetMe(me.ToModel());
        }
        catch
        {
            // Игнорируем ошибки загрузки профиля
        }
    }
}


