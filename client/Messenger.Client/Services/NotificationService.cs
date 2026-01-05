using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Messenger.Client.Services;

public sealed class NotificationService
{
    public void Show(string title, string body)
    {
        Debug.WriteLine($"[Notification] {title}: {body}");
        // Для Windows можно использовать Windows Forms или WinRT Toast
        // Пока используем простой вывод в Debug
    }

    public void ShowNotification(string title, string body, int chatId)
    {
        Show(title, body);
        // Здесь можно добавить логику показа системных уведомлений
    }
}


