using System;
using System.IO;
using AsyncImageLoader;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using IptvClient.Core.Configuration;
using IptvClient.Imaging;
using IptvClient.ViewModels;
using IptvClient.Views;

namespace IptvClient;

public partial class App : Application
{
    public override void Initialize()
    {
        ImageLoader.AsyncImageLoader = new LogoImageLoader();

        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // AppContext.BaseDirectory only makes sense for the desktop heads (config file sits
        // next to the exe); Android will need its own settings source once that head is built.
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json");
        var settings = AppSettingsLoader.Load(settingsPath);

        var mainViewModel = new MainViewModel();
        if (!string.IsNullOrWhiteSpace(settings.PlaylistUrl))
        {
            _ = mainViewModel.LoadPlaylistAsync(settings.PlaylistUrl);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };
            desktop.Exit += (_, _) => mainViewModel.Dispose();
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = mainViewModel };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = mainViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}