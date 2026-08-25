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
    /// <summary>Lets a head supply settings some other way than the desktop head's "file next
    /// to the exe" convention (<see cref="AppContext.BaseDirectory"/> doesn't mean anything
    /// useful on Android) — set this before Avalonia finishes starting (e.g. from
    /// IptvClient.Android's Application.CustomizeAppBuilder, reading a bundled asset) and it
    /// takes priority over the file-path lookup below.</summary>
    public static AppSettings? SettingsOverride { get; set; }

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
        // AppContext.BaseDirectory only makes sense for the desktop head (config file sits
        // next to the exe) — Android supplies SettingsOverride instead (see its own
        // Application.cs), since there's no equivalent "next to the exe" location there.
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json");
        var settings = SettingsOverride ?? AppSettingsLoader.Load(settingsPath);

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