using System;
using System.IO;
using AsyncImageLoader;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using IptvClient.Core.Configuration;
using IptvClient.Imaging;
using IptvClient.ViewModels;
using IptvClient.Views;
using LibVLCSharp.Shared;

namespace IptvClient;

public partial class App : Application
{
    /// <summary>Lets a head supply settings some other way than the desktop head's "file next
    /// to the exe" convention (<see cref="AppContext.BaseDirectory"/> doesn't mean anything
    /// useful on Android) — set this before Avalonia finishes starting (e.g. from
    /// IptvClient.Android's Application.CustomizeAppBuilder, reading a bundled asset) and it
    /// takes priority over the file-path lookup below.</summary>
    public static AppSettings? SettingsOverride { get; set; }

    /// <summary>Android supplies its own native video surface through this factory — set from
    /// IptvClient.Android's Application.CustomizeAppBuilder before Avalonia finishes starting.
    /// The shared project can't reference Android-only types directly (Desktop wouldn't
    /// compile), so it only knows "give me a Control for this MediaPlayer" and leaves the
    /// concrete implementation (LibVLCSharp.Platforms.Android.VideoView wrapped in a
    /// NativeControlHost — see IptvClient.Android/VlcVideoHost.cs) to the head that can
    /// actually reference it. Desktop doesn't use this at all: MainView constructs a
    /// LibVLCSharp.Avalonia.VideoView directly instead, since that control works fine there
    /// and doesn't work on Android at all (see CLAUDE.md).
    ///
    /// The <see cref="Action"/> parameter is "the user tapped the video surface" — routed to
    /// MainViewModel.ToggleControlsVisibility. It exists because native Android touch on a
    /// SurfaceView doesn't bubble into Avalonia's own pointer-event pipeline any more than
    /// mouse-move does on Windows (same "airspace" class of issue, touch flavor); the
    /// NativeControlHost implementation wires a real Android touch/click listener directly
    /// onto the native View and calls this instead.</summary>
    public static Func<MediaPlayer, Action, Control>? AndroidVideoHostFactory { get; set; }

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