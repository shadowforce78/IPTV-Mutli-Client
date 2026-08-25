using System;
using Android.Views;
using Android.Widget;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Platform;
using LibVLCSharp.Shared;

namespace IptvClient.Android;

/// <summary>
/// Android's video surface. LibVLCSharp.Avalonia.VideoView (used on desktop) doesn't support
/// Android at all — it unconditionally sets MediaPlayer.Hwnd, a Windows/Linux/macOS-only
/// concept that doesn't exist on the Android build of LibVLCSharp.Shared.MediaPlayer, so
/// constructing it there is an instant MissingMethodException (confirmed live on a physical
/// device). LibVLCSharp *does* ship a real Android video surface though, just not wired into
/// Avalonia: LibVLCSharp.Platforms.Android.VideoView, a SurfaceView that internally drives
/// libVLC's actual Android attach mechanism (Org.Videolan.Libvlc.AWindow +
/// MediaPlayer.SetAndroidContext) — confirmed present in the LibVLCSharp package we already
/// reference by inspecting its Android-targeted assembly directly (reflection, not docs —
/// scraped LibVLCSharp/MAUI/Forms docs describe several different eras of this API).
///
/// This wraps that control in Avalonia's own NativeControlHost, the documented way to embed a
/// native Android View: https://docs.avaloniaui.net/docs/platform-specific-guides/android/embed-native-views
/// (pattern taken from Avalonia's own ControlCatalog.Android sample, EmbedSample.Android.cs).
/// </summary>
public sealed class VlcVideoHost : NativeControlHost
{
    private readonly MediaPlayer _mediaPlayer;
    private readonly Action _onTap;
    private LibVLCSharp.Platforms.Android.VideoView? _videoView;
    private View? _tapOverlay;
    private DateTime _lastClickUtc = DateTime.MinValue;

    public VlcVideoHost(MediaPlayer mediaPlayer, Action onTap)
    {
        _mediaPlayer = mediaPlayer;
        _onTap = onTap;
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var context = (parent as AndroidViewControlHandle)?.View.Context
            ?? global::Android.App.Application.Context;

        _videoView = new LibVLCSharp.Platforms.Android.VideoView(context)
        {
            MediaPlayer = _mediaPlayer,
        };

        // A Click listener attached directly to the VideoView (a SurfaceView) only fires
        // *before* real video is attached — once MediaPlayer actually starts rendering into
        // it, libVLC's own AWindow/attachViews plumbing apparently takes over touch handling
        // for that surface and our listener stops firing (confirmed live: tapping worked on
        // the black idle surface, stopped the moment a channel started playing). A plain
        // transparent View stacked *on top* in a FrameLayout sidesteps this entirely — normal
        // Android touch dispatch follows View z-order regardless of what SurfaceView is doing
        // with its own rendering surface underneath, so this reliably intercepts taps in both
        // states.
        _tapOverlay = new View(context) { Clickable = true };
        _tapOverlay.Click += OnTapOverlayClick;

        var matchParent = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
        var layout = new FrameLayout(context);
        layout.AddView(_videoView, new FrameLayout.LayoutParams(matchParent));
        layout.AddView(_tapOverlay, new FrameLayout.LayoutParams(matchParent));

        return new AndroidViewControlHandle(layout);
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        if (_tapOverlay is not null)
        {
            _tapOverlay.Click -= OnTapOverlayClick;
            _tapOverlay = null;
        }

        if (_videoView is not null)
        {
            // Detaches from the MediaPlayer (VideoView's own setter tears down AWindow/
            // attachViews) without touching the MediaPlayer itself — MainViewModel owns that
            // for the app's whole lifetime, this control's job ends at the video surface.
            _videoView.MediaPlayer = null;
            _videoView = null;
        }

        base.DestroyNativeControlCore(control);
    }

    private void OnTapOverlayClick(object? sender, EventArgs e)
    {
        // A single physical tap was observed firing a click handler attached this way up to 4
        // times in a single burst (confirmed via adb logcat, back when it was attached to the
        // VideoView directly) — Avalonia appears to call CreateNativeControlCore more than
        // once for the same NativeControlHost without a matching DestroyNativeControlCore in
        // between on this device/setup. Debounced defensively; costs nothing since a real
        // human tap can't repeat this fast anyway.
        var now = DateTime.UtcNow;
        if ((now - _lastClickUtc).TotalMilliseconds < 400)
        {
            return;
        }

        _lastClickUtc = now;
        _onTap();
    }
}
