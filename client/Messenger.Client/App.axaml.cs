using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Messenger.Client.Services;
using Messenger.Client.Stores;
using Messenger.Client.Utils;
using Messenger.Client.ViewModels;
using Messenger.Client.Views;

namespace Messenger.Client;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = default!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppLogger.Info($"App start. BaseDirectory={AppContext.BaseDirectory}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                AppLogger.Error(ex, "UnhandledException");
            else
                AppLogger.Error($"UnhandledException: {e.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLogger.Error(e.Exception, "UnobservedTaskException");
            e.SetObserved();
        };

        IConfiguration config;
        try
        {
            // Важно: appsettings.json должен копироваться в output (см. csproj).
            // Но даже если его нет, приложение должно стартовать с дефолтами.
            config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Failed to build configuration; using defaults");
            config = new ConfigurationBuilder().Build();
        }

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);

        // Stores
        services.AddSingleton<NavigationStore>();
        services.AddSingleton<AuthStore>();
        services.AddSingleton<UserStore>();
        services.AddSingleton<ChatStore>();

        // Services
        services.AddSingleton<ApiService>();
        services.AddSingleton<AuthService>();
        services.AddSingleton<WebSocketService>();
        services.AddSingleton<NotificationService>();

        // ViewModels
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<ChatListViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<SettingsViewModel>();

        Services = services.BuildServiceProvider();

        var nav = Services.GetRequiredService<NavigationStore>();
        var authStore = Services.GetRequiredService<AuthStore>();
        authStore.LoadFromDisk();

        // Стартовый экран: показываем логин
        nav.CurrentViewModel = Services.GetRequiredService<LoginViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<ShellViewModel>()
            };
            desktop.MainWindow.Show();
            AppLogger.Info("MainWindow created and shown.");
        }

        base.OnFrameworkInitializationCompleted();
    }
}


