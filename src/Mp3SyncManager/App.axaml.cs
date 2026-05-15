using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Mp3SyncManager.Services;
using Mp3SyncManager.Services.Interfaces;
using Mp3SyncManager.ViewModels;
using Mp3SyncManager.Views;

namespace Mp3SyncManager;

public partial class App : Application
{
    private ServiceProvider? _services;

    public static IServiceProvider Services => ((App)Current!)._services!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var collection = new ServiceCollection();
        ConfigureServices(collection);
        _services = collection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = _services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = mainVm };
            _ = mainVm.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDeviceDetectionService, DeviceDetectionService>();
        services.AddSingleton<IFileTransferService, FileTransferService>();
        services.AddSingleton<IConfirmationService, ConfirmationService>();

        services.AddSingleton<SetupViewModel>();
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<DeviceViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}
