using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Messenger.Client.Models;
using Messenger.Client.Services;
using Messenger.Client.Stores;
using Messenger.Client.Utils;

namespace Messenger.Client.ViewModels;

public sealed class ProfileViewModel : ViewModelBase
{
    private readonly ApiService _api;
    private readonly UserStore _userStore;
    private readonly NavigationStore _nav;
    private readonly IServiceProvider _sp;

    private string _username = "";
    public string Username { get => _username; set => SetField(ref _username, value); }

    private string _bio = "";
    public string Bio { get => _bio; set => SetField(ref _bio, value); }

    private string _status = "online";
    public string Status { get => _status; private set => SetField(ref _status, value); }

    private string? _avatarPath;
    public string? AvatarPath { get => _avatarPath; set => SetField(ref _avatarPath, value); }

    private string _error = "";
    public string Error { get => _error; set => SetField(ref _error, value); }

    public RelayCommand BackCommand { get; }
    public RelayCommand LoadCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand UploadAvatarCommand { get; }

    public ProfileViewModel(ApiService api, UserStore userStore, NavigationStore nav, IServiceProvider sp)
    {
        _api = api;
        _userStore = userStore;
        _nav = nav;
        _sp = sp;

        BackCommand = new RelayCommand(async (_) => _nav.CurrentViewModel = (ViewModelBase)_sp.GetService(typeof(ChatListViewModel))!);
        LoadCommand = new RelayCommand(async (_) => await Load());
        SaveCommand = new RelayCommand(async (_) => await Save());
        UploadAvatarCommand = new RelayCommand(async (_) => await UploadAvatar());

        _ = Load();
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

    private async Task Load()
    {
        try
        {
            Error = "";
            var me = await _api.Get<MeDto>("/users/me");
            var model = me.ToModel();
            _userStore.SetMe(model);
            Username = model.Username;
            Bio = model.Bio ?? "";
            Status = model.Status;
            AvatarPath = model.AvatarPath;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private async Task UploadAvatar()
    {
        try
        {
            Error = "";
            var parentWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            var topLevel = TopLevel.GetTopLevel(parentWindow ?? throw new InvalidOperationException("No main window"));
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Avatar",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Images")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp" }
                    }
                }
            });

            if (files.Count == 0) return;

            var path = files[0].Path;
            if (path is null) return;
            var filePath = path.LocalPath;
            if (string.IsNullOrEmpty(filePath)) return;
            var me = await _api.PostFile<MeDto>("/users/me/avatar", filePath);
            _userStore.SetMe(me.ToModel());
            AvatarPath = me.AvatarPath;
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
            // Не отправляем status - он обновляется автоматически через WebSocket
            var me = await _api.Patch<MeDto>("/users/me/profile", new { username = Username, bio = Bio });
            _userStore.SetMe(me.ToModel());
            Status = me.Status; // Обновляем отображение статуса
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}


