using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Messenger.Client.Models;
using Messenger.Client.ViewModels;

namespace Messenger.Client.Views;

public partial class ChatListView : UserControl
{
    public ChatListView()
    {
        InitializeComponent();
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

    private void OnDialogItemLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Border border || DataContext is not ChatListViewModel vm) return;
        if (border.DataContext is not DialogListItem item) return;

        var otherMember = vm.GetOtherMember(item);
        if (otherMember is null) return;

        // Используем рекурсивный поиск для нахождения элементов
        var avatarText = FindControlRecursive<TextBlock>(border, "AvatarText");
        var usernameText = FindControlRecursive<TextBlock>(border, "UsernameText");

        if (avatarText is not null && otherMember.Username.Length > 0)
        {
            avatarText.Text = otherMember.Username.Substring(0, 1).ToUpper();
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


