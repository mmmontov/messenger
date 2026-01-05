using System;
using Messenger.Client.Models;

namespace Messenger.Client.Stores;

public sealed class UserStore
{
    public UserProfile? Me { get; private set; }
    public event Action? MeChanged;

    public void SetMe(UserProfile? me)
    {
        Me = me;
        MeChanged?.Invoke();
    }
}


