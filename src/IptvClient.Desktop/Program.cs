using System;
using Avalonia;

namespace IptvClient.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Loads the native libvlc binaries (VideoLAN.LibVLC.Windows on this head) — must run
        // once before any LibVLC instance is constructed anywhere in the app. Fully qualified
        // because the bare name `Core` resolves to our own IptvClient.Core namespace first
        // (enclosing-namespace lookup beats a `using` import).
        LibVLCSharp.Shared.Core.Initialize();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
