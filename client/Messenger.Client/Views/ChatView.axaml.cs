using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Markup.Xaml;
using Avalonia.Data;
using Avalonia.Interactivity;
using Messenger.Client.Models;
using Messenger.Client.ViewModels;

namespace Messenger.Client.Views;

public partial class ChatView : UserControl
{
    private ScrollViewer? _messagesScroll;
    private ItemsControl? _messagesList;

    public ChatView()
    {
        InitializeComponent();
        this.AttachedToVisualTree += (_, _) =>
        {
            _messagesScroll = this.FindControl<ScrollViewer>("MessagesScroll");
            _messagesList = this.FindControl<ItemsControl>("MessagesList");
            
            // Подписываемся на изменения коллекции сообщений для автоматической прокрутки
            if (DataContext is ChatViewModel vm)
            {
                vm.Messages.CollectionChanged += (_, _) => ScrollToBottom();
            }
        };
        
        this.DataContextChanged += (_, _) =>
        {
            if (DataContext is ChatViewModel vm)
            {
                vm.Messages.CollectionChanged += (_, _) => ScrollToBottom();
                vm.MessageSent += () => ScrollToBottom();
            }
        };
    }

    private void ScrollToBottom()
    {
        if (_messagesScroll is null) return;
        
        // Используем Dispatcher для отложенной прокрутки после рендеринга
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_messagesScroll is not null)
            {
                _messagesScroll.Offset = new Avalonia.Vector(0, _messagesScroll.Extent.Height);
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void OnMessageGridLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Grid grid || DataContext is not ChatViewModel vm) return;
        
        var otherMessage = FindControlRecursive<Border>(grid, "OtherMessage");
        var myMessage = FindControlRecursive<Border>(grid, "MyMessage");
        
        if (otherMessage is null || myMessage is null) return;
        
        // Получаем сообщение из DataContext
        if (grid.DataContext is not Message message) return;
        
        // Определяем, является ли сообщение нашим
        var isMyMessage = vm.IsMyMessage(message);
        
        // Показываем/скрываем соответствующие блоки
        otherMessage.IsVisible = !isMyMessage;
        myMessage.IsVisible = isMyMessage;
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

    private void OnMyMessagePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || DataContext is not ChatViewModel vm) return;
        if (border.DataContext is not Message message) return;
        
        // Проверяем, что это наше сообщение и был одинарный клик
        if (!vm.IsMyMessage(message)) return;
        if (e.ClickCount != 1) return;
        
        // Устанавливаем выбранное сообщение для редактирования
        vm.SelectedMessage = message;
        vm.StartEditMessage(message);
    }

    private void OnEditClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ChatViewModel vm && sender is MenuItem menuItem && menuItem.DataContext is Message message)
        {
            vm.EditCommand.Execute(message);
        }
    }

    private void OnDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ChatViewModel vm && sender is MenuItem menuItem && menuItem.DataContext is Message message)
        {
            vm.DeleteCommand.Execute(message);
        }
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is ChatViewModel vm)
        {
            await vm.HandleKeyDown(sender, e);
            // Прокручиваем вниз после отправки
            ScrollToBottom();
        }
    }
}
