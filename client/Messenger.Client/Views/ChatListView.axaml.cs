using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Messenger.Client.Models;
using Messenger.Client.Services;
using Messenger.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace Messenger.Client.Views;

public partial class ChatListView : UserControl
{
    private ApiService? _api;
    private readonly Dictionary<string, Avalonia.Media.Imaging.Bitmap> _avatarCache = new();
    private readonly HashSet<string> _loadingAvatars = new();

    public ChatListView()
    {
        InitializeComponent();
        _api = ((App)Application.Current!).Services.GetService<ApiService>();
        this.AttachedToVisualTree += (_, _) =>
        {
            if (this.FindControl<ListBox>("DialogsList") is { } lb)
            {
                // ЛКМ открывает чат
                lb.SelectionChanged += (_, _) =>
                {
                    if (DataContext is ChatListViewModel vm && lb.SelectedItem is DialogListItem item)
                    {
                        vm.OpenChat(item);
                        lb.SelectedItem = null;
                    }
                };
            }
        };
    }

    private async void OnDialogItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Border border || DataContext is not ChatListViewModel vm) return;
        if (border.DataContext is not DialogListItem item) return;

        var otherMember = vm.GetOtherMember(item);
        if (otherMember is null) return;

        // Используем рекурсивный поиск для нахождения элементов
        var avatarImage = FindControlRecursive<Image>(border, "AvatarImage");
        var avatarText = FindControlRecursive<TextBlock>(border, "AvatarText");
        var usernameText = FindControlRecursive<TextBlock>(border, "UsernameText");

        // Загружаем аватар
        if (avatarImage is not null && avatarText is not null)
        {
            await UpdateAvatar(avatarImage, avatarText, otherMember.AvatarPath);
        }

        if (usernameText is not null)
        {
            usernameText.Text = otherMember.Username;
        }
    }

    private void OnDialogItemPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Проверяем, что была нажата правая кнопка мыши
        var point = e.GetCurrentPoint(null);
        if (!point.Properties.IsRightButtonPressed) return;
        
        // Предотвращаем открытие чата при ПКМ
        e.Handled = true;
        
        if (sender is not Border border || DataContext is not ChatListViewModel vm) return;
        if (border.DataContext is not DialogListItem item) return;

        var otherMember = vm.GetOtherMember(item);
        if (otherMember is null) return;

        var bio = otherMember.Bio ?? "";
        var username = otherMember.Username;
        
        var parentWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        
        var window = new Window
        {
            Title = $"Bio: {username}",
            Width = 400,
            Height = 250,
            WindowStartupLocation = parentWindow is not null 
                ? WindowStartupLocation.CenterOwner 
                : WindowStartupLocation.CenterScreen,
            CanResize = false,
            ShowInTaskbar = false
        };

        var content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 10
        };

        var usernameText = new TextBlock
        {
            Text = username,
            FontSize = 18,
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        };

        var bioText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(bio) ? "(No bio)" : bio,
            FontSize = 14,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var scrollViewer = new ScrollViewer
        {
            Content = bioText,
            MaxHeight = 100,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };

        var closeButton = new Button
        {
            Content = "Close",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Width = 80,
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        };
        closeButton.Click += (_, _) => window.Close();

        content.Children.Add(usernameText);
        content.Children.Add(scrollViewer);
        content.Children.Add(closeButton);

        window.Content = content;
        window.Show();
    }

    private async Task UpdateAvatar(Image avatarImage, TextBlock avatarText, string? avatarPath)
    {
        if (avatarImage is null || avatarText is null || _api is null) return;

        if (string.IsNullOrEmpty(avatarPath))
        {
            avatarImage.IsVisible = false;
            avatarText.IsVisible = true;
            // Устанавливаем первую букву имени, если есть
            var border = avatarImage.Parent as Border;
            if (border?.DataContext is DialogListItem item && 
                DataContext is ChatListViewModel vm)
            {
                var otherMember = vm.GetOtherMember(item);
                if (otherMember?.Username.Length > 0 == true)
                {
                    avatarText.Text = otherMember.Username.Substring(0, 1).ToUpper();
                }
            }
            return;
        }

        // Проверяем кэш
        if (_avatarCache.TryGetValue(avatarPath, out var cachedBitmap))
        {
            avatarImage.Source = cachedBitmap;
            avatarImage.IsVisible = true;
            avatarText.IsVisible = false;
            return;
        }

        // Проверяем, не загружается ли уже
        if (_loadingAvatars.Contains(avatarPath))
        {
            // Уже загружается, показываем текст пока
            avatarImage.IsVisible = false;
            avatarText.IsVisible = true;
            var border = avatarImage.Parent as Border;
            if (border?.DataContext is DialogListItem item && 
                DataContext is ChatListViewModel vm)
            {
                var otherMember = vm.GetOtherMember(item);
                if (otherMember?.Username.Length > 0 == true)
                {
                    avatarText.Text = otherMember.Username.Substring(0, 1).ToUpper();
                }
            }
            return;
        }

        // Начинаем загрузку
        _loadingAvatars.Add(avatarPath);

        try
        {
            var url = _api.GetFileUrl(avatarPath);
            using var client = new System.Net.Http.HttpClient();
            var bytes = await client.GetByteArrayAsync(url);
            using var stream = new System.IO.MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            
            // Сохраняем в кэш
            _avatarCache[avatarPath] = bitmap;
            
            avatarImage.Source = bitmap;
            avatarImage.IsVisible = true;
            avatarText.IsVisible = false;
        }
        catch
        {
            avatarImage.IsVisible = false;
            avatarText.IsVisible = true;
            // Устанавливаем первую букву имени при ошибке
            var border = avatarImage.Parent as Border;
            if (border?.DataContext is DialogListItem item && 
                DataContext is ChatListViewModel vm)
            {
                var otherMember = vm.GetOtherMember(item);
                if (otherMember?.Username.Length > 0 == true)
                {
                    avatarText.Text = otherMember.Username.Substring(0, 1).ToUpper();
                }
            }
        }
        finally
        {
            _loadingAvatars.Remove(avatarPath);
        }
    }

    private static T? FindControlRecursive<T>(Avalonia.Visual? parent, string name) where T : Avalonia.Controls.Control
    {
        if (parent is null) return null;
        
        if (parent is T control && control.Name == name)
            return control;

        foreach (var child in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(parent))
        {
            var found = FindControlRecursive<T>(child as Avalonia.Visual, name);
            if (found is not null) return found;
        }

        return null;
    }
}


