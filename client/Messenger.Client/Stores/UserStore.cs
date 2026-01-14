using System;
using System.ComponentModel;
using Messenger.Client.Models;

namespace Messenger.Client.Stores;

public sealed class UserStore : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaisePropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public UserProfile? Me { get; private set; }
    public event Action? MeChanged;

    private bool _notificationsEnabled = true;
    public bool NotificationsEnabled { get => _notificationsEnabled; set { _notificationsEnabled = value; RaisePropertyChanged(nameof(NotificationsEnabled)); } }

    private bool _notificationSound = true;
    public bool NotificationSound { get => _notificationSound; set { _notificationSound = value; RaisePropertyChanged(nameof(NotificationSound)); } }

    public void SetMe(UserProfile? me)
    {
        Me = me;
        MeChanged?.Invoke();
    }
}


