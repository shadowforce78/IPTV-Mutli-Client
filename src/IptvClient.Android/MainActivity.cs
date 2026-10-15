using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

[assembly: UsesFeature("android.hardware.touchscreen", Required = false)]
[assembly: UsesFeature("android.software.leanback", Required = false)]

namespace IptvClient.Android;

[Activity(
    Label = "IptvClient.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode
        | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Navigation)]
// Android TV launcher entry (the plain MainLauncher above covers phones).
[IntentFilter([Intent.ActionMain], Categories = ["android.intent.category.LEANBACK_LAUNCHER"])]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // No screen-saver / sleep while a stream is playing and nobody touches the remote.
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);

        // Back (button, gesture, TV remote): close the player first, only then leave the app.
        // Registered through the AndroidX dispatcher since gesture/predictive back on recent
        // Android never reaches OnKeyDown/OnBackPressed.
        OnBackPressedDispatcher.AddCallback(this, new BackCallback(this));
    }

    private sealed class BackCallback(MainActivity activity) : AndroidX.Activity.OnBackPressedCallback(true)
    {
        public override void HandleOnBackPressed()
        {
            if (App.BackHandler?.Invoke() == true)
            {
                return;
            }

            Enabled = false;
            activity.OnBackPressedDispatcher.OnBackPressed();
        }
    }

    // Once libVLC is rendering, its surface stops delivering touches to any view layered over it
    // (confirmed on device: tapping worked on the black idle surface, not once video played), so
    // the tap that reveals the player controls is caught here, before view dispatch.
    public override bool DispatchTouchEvent(MotionEvent? e)
    {
        if (e?.Action == MotionEventActions.Up)
        {
            App.ScreenTapped?.Invoke();
        }

        return base.DispatchTouchEvent(e);
    }
}
