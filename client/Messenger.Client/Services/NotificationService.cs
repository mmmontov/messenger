using System.Diagnostics;
using System.Runtime.InteropServices;
using Messenger.Client.Stores;

namespace Messenger.Client.Services;

public sealed class NotificationService
{
    private readonly UserStore _userStore;

    public NotificationService(UserStore userStore)
    {
        _userStore = userStore;
    }    public void Show(string title, string body)
    {
        Debug.WriteLine($"[Notification] {title}: {body}");
        // Для Windows можно использовать Windows Forms или WinRT Toast
        // Пока используем простой вывод в Debug
    }

    public void ShowNotification(string title, string body, int chatId)
    {
        Show(title, body);
        if (_userStore.NotificationSound)
        {
            PlayNotificationSound();
        }
        // Здесь можно добавить логику показа системных уведомлений
    }

    public void PlayNotificationSound()
    {
        try
        {
            // Для Windows используем System.Media.SystemSounds
            System.Media.SystemSounds.Beep.Play();
        }
        catch
        {
            // Fallback - ничего не делаем
        }
    }
}


