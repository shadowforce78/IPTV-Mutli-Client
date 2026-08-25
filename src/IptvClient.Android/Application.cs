using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using IptvClient.Core.Configuration;

namespace IptvClient.Android
{
    [Application]
    public class Application : AvaloniaAndroidApplication<App>
    {
        protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            // Same requirement as the desktop head's Program.Main: must run once before any
            // LibVLC instance is constructed (MainViewModel's constructor does that). The
            // native .so libraries themselves come from the VideoLAN.LibVLC.Android package,
            // bundled into the APK — no path argument needed, same single-overload API as
            // the desktop head.
            LibVLCSharp.Shared.Core.Initialize();

            // No "next to the exe" file location on Android — appsettings.Local.json is
            // bundled as a raw asset instead (see the csproj's AndroidAsset item) and read
            // through AssetManager rather than System.IO. A missing asset (e.g. a fresh clone
            // with no local playlist configured) just means no override, same spirit as the
            // desktop head's AppSettingsLoader.Load returning defaults for a missing file —
            // caught broadly since what matters here is "did it load", not which exact
            // exception type AssetManager.Open throws for a missing entry.
            try
            {
                using var stream = Assets!.Open("appsettings.Local.json");
                App.SettingsOverride = AppSettingsLoader.Load(stream);
            }
            catch
            {
                // No bundled asset — App falls back to its own file-path lookup, which will
                // also find nothing and use defaults. Not fatal either way.
            }

            return base.CustomizeAppBuilder(builder)
            .WithInterFont();
        }
    }
}
