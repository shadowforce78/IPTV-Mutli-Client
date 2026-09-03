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

    // Linux/X11 side of the same cursor-poll workaround (see TryInitX11/IsOurAppFocused).
    private IntPtr _x11Display;
    private IntPtr _x11Root;
    private IntPtr _netWmPidAtom;
    private (int X, int Y)? _lastX11CursorPos;

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
        // MissingMethodException on launch (confirmed on a physical device). Android gets its
        // video surface from App.AndroidVideoHostFactory instead (see OnDataContextChanged —
        // it needs the MediaPlayer instance up front, which isn't available yet here).
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
        else if (OperatingSystem.IsLinux())
        {
            // Same airspace problem as Windows (see the class comment above), same cursor-poll
            // workaround in spirit, but X11 has no analogue of GetForegroundWindow to gate it
            // with — see IsOurAppFocused for what that actually took to get right.
            TryInitX11();
            if (_x11Display != IntPtr.Zero)
            {
                _cursorPollTimer.Start();
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _cursorPollTimer.Stop();
        if (_x11Display != IntPtr.Zero)
        {
            XCloseDisplay(_x11Display);
            _x11Display = IntPtr.Zero;
        }
        base.OnDetachedFromVisualTree(e);
    }

    private void OnCursorPollTick(object? sender, EventArgs e)
    {
        if (OperatingSystem.IsWindows())
        {
            OnWindowsCursorPollTick();
        }
        else if (OperatingSystem.IsLinux())
        {
            OnLinuxCursorPollTick();
        }
    }

    private void OnWindowsCursorPollTick()
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

    private void TryInitX11()
    {
        try
        {
            _x11Display = XOpenDisplay(IntPtr.Zero);
            if (_x11Display != IntPtr.Zero)
            {
                _x11Root = XDefaultRootWindow(_x11Display);
                _netWmPidAtom = XInternAtom(_x11Display, "_NET_WM_PID", onlyIfExists: 0);
            }
        }
        catch (DllNotFoundException)
        {
            // No libX11 to load at all — e.g. a pure-Wayland session with no XWayland. Degrades
            // to "no auto-reveal while hovering the video" on Linux, same as every other platform
            // this poll doesn't cover, rather than throwing.
            _x11Display = IntPtr.Zero;
        }
    }

    private void OnLinuxCursorPollTick()
    {
        if (XQueryPointer(_x11Display, _x11Root, out _, out _, out var rootX, out var rootY, out _, out _, out _) == 0)
        {
            return;
        }

        var moved = _lastX11CursorPos is not { } last || last.X != rootX || last.Y != rootY;
        _lastX11CursorPos = (rootX, rootY);

        if (moved && IsOurAppFocused())
        {
            _viewModel?.NotifyPointerActivity();
        }
    }

    // X11 has nothing like GetForegroundWindow, so this can't just compare XIDs the way the
    // Windows path compares HWNDs — confirmed by testing against two real running apps on this
    // machine (not guessed): for our own Avalonia window, XGetInputFocus happens to return our
    // top-level's own XID directly, but for a GTK terminal window checked the same way,
    // XGetInputFocus returned some other, unrelated window's XID entirely (XQueryTree showed it
    // parented straight to the root, nothing to do with the terminal's own top-level) — a
    // toolkit-internal focus-proxy window, not something TryGetPlatformHandle()/_windowHandle
    // would ever match. Since that means a plain XID match can't be trusted to generalize across
    // toolkits/window managers, walk up from the focused window looking for a _NET_WM_PID
    // property instead: every WM-managed top-level carries one (set by the toolkit, read by the
    // window manager), so the first ancestor that has one at all tells us which process actually
    // owns the focus, regardless of how many focus-proxy windows sit in between.
    private bool IsOurAppFocused()
    {
        if (XGetInputFocus(_x11Display, out var focus, out _) == 0 || focus == IntPtr.Zero)
        {
            return false;
        }

        var ourPid = (uint)Environment.ProcessId;
        var window = focus;
        for (var depth = 0; depth < 10 && window != IntPtr.Zero && window != _x11Root; depth++)
        {
            if (TryGetNetWmPid(window, out var pid))
            {
                return pid == ourPid;
            }

            if (XQueryTree(_x11Display, window, out _, out var parent, out var children, out _) == 0)
            {
                break;
            }

            if (children != IntPtr.Zero)
            {
                XFree(children);
            }

            window = parent;
        }

        return false;
    }

    private bool TryGetNetWmPid(IntPtr window, out uint pid)
    {
        pid = 0;
        var status = XGetWindowProperty(_x11Display, window, _netWmPidAtom, 0, 1, delete: 0, AnyPropertyType,
            out var actualType, out _, out var itemCount, out _, out var prop);

        if (status != 0 || prop == IntPtr.Zero)
        {
            return false;
        }

        var found = actualType != IntPtr.Zero && itemCount.ToInt64() > 0;
        if (found)
        {
            pid = (uint)Marshal.ReadInt32(prop);
        }

        XFree(prop);
        return found;
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

            // Android's video surface needs the MediaPlayer instance up front (unlike
            // LibVLCSharp.Avalonia.VideoView on desktop, which just binds to it), so this is
            // deferred to here rather than the constructor, where DataContext isn't set yet.
            if (OperatingSystem.IsAndroid() && VideoHost.Content is null && App.AndroidVideoHostFactory is { } factory)
            {
                var viewModel = _viewModel;
                VideoHost.Content = factory(viewModel.MediaPlayer, () => viewModel.ToggleControlsVisibility());
            }
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

    private const int AnyPropertyType = 0;

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr displayName);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XInternAtom(IntPtr display, string atomName, int onlyIfExists);

    [DllImport("libX11.so.6")]
    private static extern int XQueryPointer(IntPtr display, IntPtr w, out IntPtr root, out IntPtr child,
        out int rootX, out int rootY, out int winX, out int winY, out uint mask);

    [DllImport("libX11.so.6")]
    private static extern int XGetInputFocus(IntPtr display, out IntPtr focusReturn, out int revertToReturn);

    [DllImport("libX11.so.6")]
    private static extern int XQueryTree(IntPtr display, IntPtr w, out IntPtr rootReturn, out IntPtr parentReturn,
        out IntPtr childrenReturn, out uint nchildrenReturn);

    [DllImport("libX11.so.6")]
    private static extern int XGetWindowProperty(IntPtr display, IntPtr w, IntPtr property, long longOffset,
        long longLength, int delete, IntPtr reqType, out IntPtr actualTypeReturn, out int actualFormatReturn,
        out IntPtr nitemsReturn, out IntPtr bytesAfterReturn, out IntPtr propReturn);

    [DllImport("libX11.so.6")]
    private static extern int XFree(IntPtr data);
}
