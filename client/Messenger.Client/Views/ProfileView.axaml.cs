using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Messenger.Client.Services;
using Messenger.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Messenger.Client.Views;

public partial class ProfileView : UserControl
{
    private ApiService? _api;

    public ProfileView()
    {
        InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ProfileViewModel vm)
        {
            _api = ((App)Application.Current!).Services.GetService<ApiService>();
            UpdateAvatar(vm.AvatarPath);
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ProfileViewModel.AvatarPath))
                {
                    UpdateAvatar(vm.AvatarPath);
                }
            };
        }
    }

    private async void UpdateAvatar(string? avatarPath)
    {
        var avatarImage = this.FindControl<Image>("AvatarImage");
        var avatarText = this.FindControl<TextBlock>("AvatarText");
        
        if (avatarImage is null || avatarText is null || _api is null) return;

        if (string.IsNullOrEmpty(avatarPath))
        {
            avatarImage.IsVisible = false;
            avatarText.IsVisible = true;
            return;
        }

        try
        {
            var url = _api.GetFileUrl(avatarPath);
            using var client = new System.Net.Http.HttpClient();
            var bytes = await client.GetByteArrayAsync(url);
            using var stream = new System.IO.MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            avatarImage.Source = bitmap;
            avatarImage.IsVisible = true;
            avatarText.IsVisible = false;
        }
        catch
        {
            avatarImage.IsVisible = false;
            avatarText.IsVisible = true;
        }
    }
}


