using Messenger.Client.Stores;

namespace Messenger.Client.ViewModels;

public sealed class ShellViewModel : ViewModelBase
{
    private readonly NavigationStore _nav;

    public ShellViewModel(NavigationStore nav)
    {
        _nav = nav;
        _nav.CurrentViewModelChanged += () => RaisePropertyChanged(nameof(CurrentViewModel));
    }

    public ViewModelBase? CurrentViewModel => _nav.CurrentViewModel;
}


