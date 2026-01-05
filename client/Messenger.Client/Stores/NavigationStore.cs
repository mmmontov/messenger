using System;
using Messenger.Client.ViewModels;

namespace Messenger.Client.Stores;

public sealed class NavigationStore
{
    private ViewModelBase? _current;
    public ViewModelBase? CurrentViewModel
    {
        get => _current;
        set
        {
            if (_current == value) return;
            _current = value;
            CurrentViewModelChanged?.Invoke();
        }
    }

    public event Action? CurrentViewModelChanged;
}


