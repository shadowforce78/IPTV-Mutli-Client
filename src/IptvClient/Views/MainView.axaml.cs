using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;
using IptvClient.ViewModels;

namespace IptvClient.Views;

public partial class MainView : UserControl
{
    private readonly DispatcherTimer _cursorPollTimer;
    private MainViewModel? _viewModel;
    private POINT? _lastCursorPos;
    private IntPtr _windowHandle;

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // VideoView hosts a real native child window (NativeControlHost) — Windows delivers
        // mouse-move messages straight to that HWND, bypassing Avalonia's own input pipeline
        // entirely, so PointerMoved on VideoArea never fires while the cursor is over the
        // video pixels themselves (only over the letterboxed edges / control bar, which are
        // genuinely Avalonia-rendered). Polling GetCursorPos is the pragmatic workaround —
        // Windows-only for now, matching where this app is actually being run/tested; it just
        // degrades to "no auto-reveal while hovering the video" on other platforms rather than
        // throwing, since OperatingSystem.IsWindows() gates every use of it below.
        _cursorPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _cursorPollTimer.Tick += OnCursorPollTick;

        // LibVLCSharp.Avalonia.VideoView unconditionally sets MediaPlayer.Hwnd, a
        // Windows/Linux/macOS-only concept — the Android build of LibVLCSharp.Shared doesn't
        // have that member at all, so constructing VideoView there is an instant
        // MissingMethodException on launch (confirmed on a physical device). Android needs its
        // own video surface (libVLC's attachViews-on-a-native-View mechanism, not built yet —
        // see CLAUDE.md); for now it just gets an empty placeholder instead of crashing.
        if (!OperatingSystem.IsAndroid())
        {
            var videoView = new LibVLCSharp.Avalonia.VideoView();
            videoView.Bind(LibVLCSharp.Avalonia.VideoView.MediaPlayerProperty, new Binding(nameof(MainViewModel.MediaPlayer)));
            VideoHost.Content = videoView;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _windowHandle = TopLevel.GetTopLevel(this)?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (OperatingSystem.IsWindows())
        {
            _cursorPollTimer.Start();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _cursorPollTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnCursorPollTick(object? sender, EventArgs e)
    {
        if (!GetCursorPos(out var pos))
        {
            return;
        }

        var moved = _lastCursorPos is not { } last || last.X != pos.X || last.Y != pos.Y;
        _lastCursorPos = pos;

        // Without this, moving the mouse over some *other* focused app would still reveal our
        // controls, since GetCursorPos reports the cursor's screen position regardless of
        // which window — or which process — is actually underneath it.
        if (moved && _windowHandle != IntPtr.Zero && GetForegroundWindow() == _windowHandle)
        {
            _viewModel?.NotifyPointerActivity();
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsFullscreen) && _viewModel is not null)
        {
            ApplyFullscreen(_viewModel.IsFullscreen);
        }
    }

    private void OnVideoAreaPointerMoved(object? sender, PointerEventArgs e) => _viewModel?.NotifyPointerActivity();

    // Fullscreen is done at the Avalonia Window level — collapse the browsing UI and let the
    // video row fill the window — rather than via MediaPlayer.Fullscreen/ToggleFullscreen():
    // libVLC's own fullscreen is meant for a video output it fully owns, and ours is embedded
    // as a NativeControlHost inside this app's window, so driving it from here keeps behavior
    // consistent instead of handing the video surface off to libVLC's own window management.
    private void ApplyFullscreen(bool isFullscreen)
    {
        var rows = RootGrid.RowDefinitions;
        rows[0].Height = isFullscreen ? new GridLength(0) : GridLength.Auto; // status bar
        rows[2].Height = isFullscreen ? new GridLength(0) : GridLength.Auto; // splitter
        rows[3].Height = isFullscreen ? new GridLength(0) : new GridLength(5, GridUnitType.Star); // tabs

        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.WindowState = isFullscreen ? WindowState.FullScreen : WindowState.Normal;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
