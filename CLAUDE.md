# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Cross-platform IPTV client (plays an M3U/M3U8 playlist) targeting Windows,
Linux and Android (macOS reserved for later). Built to replace slow-loading
VLC/Kodi-addon playback with LibVLCSharp under tuned buffering settings. See
[README.md](README.md) for setup instructions and project status.

## Commands

SDK version is pinned in `global.json` (10.0.100, `rollForward: latestFeature`)
— `dotnet --version` must resolve to a matching SDK before anything below works.

```bash
# Build everything the current machine can build (fails on Android head
# unless the android workload is installed — see below)
dotnet build IptvClient.slnx

# Build/run just the desktop head (works without any extra workload)
dotnet build src/IptvClient.Desktop/IptvClient.Desktop.csproj
dotnet run --project src/IptvClient.Desktop

# Android head requires the workload first, then builds/runs like any other head
dotnet workload install android
dotnet build src/IptvClient.Android/IptvClient.Android.csproj
```

There are no test projects yet, and no separate lint command — style comes
from `.editorconfig` plus the default Roslyn analyzers that run as part of
`dotnet build`.

On Linux, if `dotnet` reports no SDKs found despite `~/.dotnet/sdk` having
some installed, a distro-packaged `/usr/bin/dotnet` stub is shadowing it —
`~/.dotnet` must come *before* the system path entries in `PATH`.

## Architecture

**Central Package Management**: all package versions live in
`src/Directory.Packages.props`; individual `.csproj` files only declare bare
`<PackageReference Include="X" />` (no version). Adding a dependency to any
project touches both files.

**Head/shared-core layout** (from the Avalonia `avalonia.xplat` template,
with the iOS and Browser heads removed as out of scope):

- `IptvClient.Core` — plain .NET library, no Avalonia reference. M3U parsing,
  models, config loading. See "M3U parsing" below.
- `IptvClient` — shared Avalonia UI (Views/ViewModels/App), referencing
  `IptvClient.Core`. MVVM via CommunityToolkit.Mvvm (`ObservableObject`,
  `[ObservableProperty]` on partial properties, `On<Prop>Changed` partial
  hooks). `ViewLocator` resolves a View for a ViewModel by string-replacing
  `ViewModel` → `View` in the full type name via reflection — a new
  `FooViewModel` needs a `FooView` in the mirrored namespace or the locator
  renders a "Not Found" placeholder instead. **Implicit usings are OFF** in
  this project (unlike Core) — every file needs explicit `using`s.
- `IptvClient.Desktop` — Windows/Linux/macOS entry point (`OutputType=WinExe`).
  Pulls in `VideoLAN.LibVLC.Windows` for the native libVLC binaries on Windows.
  Also where the git-ignored `appsettings.Local.json` lives (see Config below).
- `IptvClient.Android` — Android entry point (`net10.0-android`). Still has
  the template's placeholder `ApplicationId` (`com.CompanyName.IptvClient`).
  Not buildable yet — see "Android build" below for exactly what's missing
  and in what order to add it.

`App.axaml.cs` picks the shell by `ApplicationLifetime` type: classic
windowed `MainWindow` on desktop lifetimes, single-view `MainView` on
mobile/browser-style lifetimes — both seeded with one shared `MainViewModel`.
It also loads `appsettings.Local.json` and, if it sets a playlist URL, fires
`MainViewModel.LoadPlaylistAsync` fire-and-forget before the window is shown.

**M3U parsing** (`IptvClient.Core/M3u/`, `IptvClient.Core/Models/`):

- `M3uParser.Parse(string|TextReader)` / `ParseAsync(Stream)` — hand-rolled
  parser for the extended M3U format (`#EXTM3U`, `#EXTINF:` with
  `key="value"` attributes). The tricky bit is splitting the `#EXTINF` line:
  the display name is taken from *after the comma that follows the last
  attribute's closing quote*, not the first comma in the line — attribute
  values and the display name can both legitimately contain commas (real
  provider data does this). Verified against a real 26k-entry provider
  playlist: 538ms parse time, 0 crashes, only 5 silently-dropped malformed
  entries (two consecutive `#EXTINF:` with no URL between them — a defect in
  that source file itself, not a parser bug).
- Attribute keys are matched case-insensitively (`tvg-ID` vs `tvg-id` both
  seen in the wild). `Channel.Attributes` keeps the full raw bag;
  `TvgId`/`TvgName`/`TvgLogo`/`GroupTitle` are convenience accessors into it.
  Unrecognized directive lines between `#EXTINF` and the URL (`#EXTVLCOPT`,
  `#EXTGRP`, `#KODIPROP`, ...) are preserved verbatim in `Channel.ExtraTags`
  rather than dropped — real playlist tested against didn't use any of these,
  so they're unexercised in practice so far.
- `ContentClassifier.Classify(Channel)` splits Live vs Movie vs Series by
  looking for a `/movie/` or `/series/` segment in the stream URL's path —
  the convention Xtream Codes reseller panels use (confirmed against real
  data: 8611 movies, 15752 series episodes, 2404 live, matching the panel's
  own bouquet counts exactly). This is a heuristic, not part of the M3U spec:
  a playlist without that URL shape classifies everything as Live.
- Real IPTV reseller playlists are **not** just live channels — the one
  tested against was 91% VOD (movies/series organized into genre groups
  like "Drame" with 10k+ entries) with live TV as a minority of ~15
  `BOUQUET ...` groups. This is why the UI splits Live/VOD into separate tabs
  instead of one flat list (see below) — a flat list would be swamped by the
  VOD catalog.
- `tvg-logo` is not always an http(s) URL — some providers inline it as a
  `data:image/...;base64,...` URI (seen on ~0.6% of channels in real data,
  up to ~19KB per line). Handled in the UI layer, not the parser (see
  Images below); the parser just stores whatever string is there.

**Video playback**: LibVLCSharp (libVLC), chosen over an mpv-based engine
(e.g. via Tauri) specifically because per-OS native binaries ship as ready
NuGet packages, at the cost of one manual runtime dependency per platform.
Wired up and working on Windows (double-click a channel in either tab to
play it).

- Windows: handled by the `VideoLAN.LibVLC.Windows` package, nothing extra.
- Linux: **not** covered by NuGet. Needs system libVLC installed via the
  distro package manager (Debian/Ubuntu: `libvlc-dev libvlc5
  vlc-plugin-base`). A Snap-installed VLC does not work here — its libVLC is
  sandboxed and other processes can't dlopen it.
- Android: `VideoLAN.LibVLC.Android` is referenced and `Core.Initialize()`
  is called. Playback goes through a *different* control than desktop's
  `LibVLCSharp.Avalonia.VideoView` (which doesn't support Android at all) —
  see `VlcVideoHost` under "Android build" below for the real mechanism and
  why.

`LibVLCSharp.Shared.Core.Initialize()` is called once, in
`IptvClient.Desktop/Program.cs`, before `BuildAvaloniaApp()` — it loads the
native libvlc binaries and must run before any `LibVLC` instance is
constructed. Each head's `Program`-equivalent needs its own call to this
(Android's hasn't been added yet, tracked in "Android build" below).
Fully-qualified as `LibVLCSharp.Shared.Core.Initialize()` at the call site —
the bare name `Core` resolves to our own `IptvClient.Core` namespace first
(enclosing-namespace lookup beats a `using` import), a real trap given the
project's own top-level project is *also* named Core.

`MainViewModel` owns one `LibVLC`/`MediaPlayer` pair for the app's whole
lifetime (`--network-caching=1500 --no-video-title-show`; the caching value
is a first guess, not measured against real playback — revisit if streams
stutter or take too long to start). `PlayChannel(Channel)` does
`MediaPlayer.Play(new Media(_libVlc, new Uri(channel.Url)))`, disposing the
`Media` right after `Play()` returns (libVLC's own docs pattern — the player
holds its own native reference once playing). All `MediaPlayer` events
(`Playing`, `TimeChanged`, `VolumeChanged`, ...) fire on a libvlc-internal
thread; every handler hops back via `Dispatcher.UIThread.Post` before
touching an `[ObservableProperty]`.

**Player controls & video overlay** (`IptvClient/Views/PlayerControlsView.*`,
`MainView.axaml(.cs)`):

- Transport: play/pause, ±10s skip (no-ops when `MediaPlayer.IsSeekable` is
  false — most live streams), mute, a 0–100 volume slider, a scrub/progress
  slider bound to `PositionSeconds`/`DurationSeconds` (synced from
  `MediaPlayer.TimeChanged`/`LengthChanged`, with a `_isSyncingPosition`
  guard flag so mirroring the player's own position doesn't itself trigger a
  seek), audio/subtitle track pickers (`MediaPlayer.AudioTrackDescription` /
  `SpuDescription`, selecting via `SetAudioTrack`/`SetSpu`), and an aspect
  ratio picker (`AspectRatioOption.All` → `MediaPlayer.AspectRatio`, a plain
  "W:H" string libVLC takes directly, null = source default).
- Icons are hand-drawn `Path`/`Polygon` geometry (play triangle, pause bars,
  chevron skip icons, a speaker cone + waves/X, corner-bracket fullscreen
  icons) — deliberately simple shapes (M/L line commands only, no arcs) since
  there's no icon font in this project and no live XAML previewer in this
  environment to iteratively check a fancier glyph. Two-state icons (e.g.
  play vs pause) are two overlaid `Path`s whose `IsVisible` bindings read
  companion negated properties on `MainViewModel` (`IsPaused`, `IsUnmuted`,
  `IsWindowed`) rather than a `!Binding` operator, to keep it unambiguous.
- **Controls live in a `Popup`, not a row of the layout.** `VideoView` is a
  `NativeControlHost` — Windows delivers its native child HWND's paint
  ahead of *any* Avalonia-rendered sibling sharing the same window, no
  matter the XAML z-order ("airspace": a real native child window always
  wins over software/Skia-rendered content in the same top-level window).
  A first attempt drawing the control bar as an Avalonia `Border` layered
  over `VideoView` in the same `Grid` cell was confirmed invisible by
  screenshot — not a timing issue, the content literally never painted.
  The fix: host the controls in a `Popup` (`ControlsPopup` in
  `MainView.axaml`), which *is* a genuine separate top-level window and so
  can paint over the native surface. `PlacementTarget="{Binding #VideoArea}"`,
  `Placement="AnchorAndGravity"`, `PlacementAnchor="Bottom"` anchors to the
  video's bottom edge; `PlacementGravity="Top"` makes the popup grow
  *upward* from that anchor so it overlaps the video from below instead of
  appearing as a separate block underneath it — gravity here means "which
  direction the popup extends from the anchor", not "which edge of the
  popup touches it" (confirmed by screenshot; got this backwards once —
  `PlacementGravity="Bottom"` instead pushed it down into the channel list).
  `Width="{Binding #VideoArea.Bounds.Width}"` keeps it in sync with the
  video's width as the window resizes.
- Auto-hide: `MainViewModel.IsControlsVisible` (default true) drives the
  popup content's `Opacity` (via `BoolToOpacityConverter` +
  `DoubleTransition`, not `IsVisible` — fading, and never resizing anything
  since the video sits in its own row unaffected by the popup). A
  `DispatcherTimer` (`_hideControlsTimer`, 3s) hides it while playing;
  `NotifyPointerActivity()` (called on `PointerMoved` over the video/popup,
  and once when a channel starts) shows it again and restarts the timer.
  Paused always keeps it visible (`OnIsPlayingChanged(false)` stops the
  timer and forces `IsControlsVisible = true`).
- **Hovering the video itself doesn't fire `PointerMoved`** — same airspace
  issue: Windows delivers mouse-move straight to `VideoView`'s native HWND,
  bypassing Avalonia's input pipeline, so a normal event handler on the
  containing `Grid` never sees it (only the letterboxed edges and the popup
  itself, which *are* genuinely Avalonia-rendered, fire it normally).
  Workaround in `MainView.axaml.cs`: a `DispatcherTimer` polls
  `user32!GetCursorPos` every 200ms and calls `NotifyPointerActivity()` on
  movement — Windows-only, gated by `OperatingSystem.IsWindows()`, so it
  degrades to "no auto-reveal while hovering the video" rather than
  throwing on Linux/Android. **This polls the cursor's absolute screen
  position, not anything scoped to our window** — an early version revealed
  the controls on mouse movement over *any* focused app, not just this one.
  Fixed by caching this window's native handle
  (`TopLevel.GetTopLevel(this)?.TryGetPlatformHandle()?.Handle`) in
  `OnAttachedToVisualTree` and only calling `NotifyPointerActivity()` when
  `user32!GetForegroundWindow()` equals it. Worth remembering as a class of
  bug if this poll is ever extended: anything driven by absolute
  screen/cursor state needs an explicit "is it actually ours" check that
  per-control Avalonia events get for free.
- Fullscreen (`MainViewModel.IsFullscreen`, toggled from the popup) is done
  at the `Window` level in `MainView.axaml.cs` (`WindowState.FullScreen` +
  collapsing the status/splitter/tabs rows to zero height), **not** via
  `MediaPlayer.Fullscreen`/`ToggleFullscreen()` — that libVLC API assumes it
  owns the video output's window, which isn't true here (`VideoView` is
  embedded in ours), so driving our own `Window` keeps behavior predictable
  instead of handing the surface off to libVLC's own window management.

**Visual design** (`App.axaml`, all `Views/*.axaml`): a small hand-rolled
token set — `AppBackgroundBrush`/`AppSurfaceBrush`/`AppSurfaceElevatedBrush`/
`AppBorderBrush`/`AppAccentBrush`/`AppTextSecondaryBrush` — defined in
`Application.Resources` and consumed via `{StaticResource ...}` everywhere,
rather than ad-hoc hex literals scattered through each view (an earlier pass
had exactly that — `#33FFFFFF`, `#CC000000`, `#141414`, ... inconsistently
across files). `RequestedThemeVariant="Dark"` is forced regardless of the OS
setting — deliberate for a video-playback app. Global `Style` selectors in
`App.axaml` round `ListBoxItem`/`TextBox`/`ComboBox` (`CornerRadius`,
consistent `Padding`/`Margin`) so the channel/group lists and inputs don't
need per-view overrides; `Button.transport`/`Button.transport.icon` give the
player's icon buttons a flat, fixed-size (40×40, fully circular) look.
`ChannelListView` lays the group list and channel list out as two separate
rounded "cards" (`AppSurfaceBrush`, `CornerRadius="12"`) with a real gap
between them instead of one continuous panel. The video `Border` itself is
deliberately left square — rounding a `NativeControlHost`'s container is
unreliable (the native child's own rectangular paint doesn't respect the
parent's clip), so corner-rounding stays on the chrome around it rather than
the video surface.

**Config**: `IptvClient.Core/Configuration/AppSettingsLoader.Load(path)`
reads a small JSON file (`{ "playlistUrl": "..." }`, camelCase via
`JsonSerializerDefaults.Web`) and returns defaults if it's missing — no
`Microsoft.Extensions.Configuration` dependency, just `System.Text.Json`.
The desktop head points this at `appsettings.Local.json` next to the exe
(`AppContext.BaseDirectory`); Android will need a different settings source
when that head is built out, since there's no equivalent "next to the exe"
location there. The gitignore reserves `appsettings.Local.json` /
`appsettings.*.local.json` / `**/config.local.json` / `secrets/` for this —
this repo is public, real playlist URLs/credentials must never land in a
committed file. `IptvClient.Desktop.csproj` copies
`appsettings.Local.json` to the output dir only `Condition="Exists(...)"`,
so a fresh clone without the file still builds.

**Channel browsing UI** (`IptvClient/ViewModels/`, `IptvClient/Views/`):

- `MainViewModel.LoadPlaylistAsync(source)` fetches+parses (via
  `PlaylistLoader`, which accepts an http(s) URL or a local file path),
  splits the result through `ContentClassifier`, and feeds two independent
  `ChannelListViewModel` instances (`Live`, `Vod`) — the same view-model
  class serves both tabs, they just get different channel lists.
- `ChannelListViewModel` groups its channels by `group-title` (sorted
  largest-group-first), and exposes a selected group + free-text search
  (search scans all channels by name when non-empty, ignoring the selected
  group). **Perf note**: `Items` is a plain `[ObservableProperty]`
  (`IReadOnlyList<Channel>`), replaced wholesale on every filter change —
  not an `ObservableCollection` mutated item-by-item. With groups up to 10k+
  entries (real "Drame" genre group), per-item `Add` would fire that many
  collection-changed events; a single property-changed swap plus the
  `ListBox`'s default virtualization is what keeps this responsive.
- `MainView.axaml` is just a `TabControl` with two `ChannelListView`
  instances (`DataContext="{Binding Live}"` / `"{Binding Vod}"`).
  `ChannelListView` is the reusable group-list + search-box + item-list
  layout, shared by both tabs.
- **Responsive: side-by-side cards on a wide window, drill-down panels on a
  narrow one** (built for Android in portrait, where the original
  desktop-shaped two-column layout genuinely didn't fit — groups and channel
  names were both illegibly cramped). `ChannelListView.axaml.cs` measures
  its own width (`SizeChanged` + an initial check on `DataContextChanged`,
  700px breakpoint) and sets `ChannelListViewModel.IsNarrowLayout`; every
  other piece of the adaptive behavior — which panel is visible, the shared
  search box's `Grid.Column`/`ColumnSpan`, the back button — is *derived*
  from that plus `IsShowingItems` inside the view-model (`IsGroupsPanelVisible`,
  `ItemsColumn`, `ShowBackButton`, etc.), not decided in XAML, so the view
  stays a thin measurement layer. Tapping a group in narrow mode navigates
  to the items panel (`OnSelectedGroupChanged` sets `IsShowingItems = true`)
  — except the auto-selected *default* group `SetItems` picks on load, which
  must not itself trigger navigation (a `_suppressNavigation` flag brackets
  that one assignment) or the app would always open on the items panel
  instead of the groups list. The search box is shared between both panels
  and both layouts and "overrides" whichever is showing — typing a query
  jumps straight to filtered results even from the groups panel
  (`OnSearchTextChanged` also sets `IsShowingItems = true` when non-empty).
  In wide layout none of this matters: `IsGroupsPanelVisible`/
  `IsItemsPanelVisible` both collapse to `!IsNarrowLayout`, i.e. always true,
  reproducing the original always-both-visible desktop behavior exactly.

**Images**: `AsyncImageLoader.Avalonia` (v3.8.0, needs `Avalonia >= 12.0.0`)
provides the `Image` control's `asyncImageLoader:ImageLoader.Source`
attached property (async fetch + RAM cache, keyed by URL). Verify its real
API by reflection/build rather than trusting scraped docs if this ever needs
revisiting — a web search for this package's README returned a mix of the
current 3.x API and an unreleased 4.0-preview pipeline-builder API that
doesn't exist in 3.8.0; the ground truth was the actual installed DLL.
`IptvClient/Imaging/LogoImageLoader.cs` subclasses `RamCachedWebImageLoader`
to also special-case `data:` URIs (decode base64 directly, no network) —
set as the global default via `ImageLoader.AsyncImageLoader = new
LogoImageLoader()` in `App.axaml.cs Initialize()`. The base64 decode path
was verified correct in isolation (a real extracted `data:image/jpeg;...`
payload decodes to a valid 225×225 `Bitmap` via a headless
`AppBuilder...SetupWithoutStarting()` harness — this is the way to
exercise Avalonia `Bitmap` construction without a running window). Its
on-screen rendering inside the actual list, specifically, was **not**
re-confirmed after that check due to the GUI-automation issue noted below —
worth a quick manual look before relying on it further.

## Android build

Status as of 2026-08-25 on the Windows machine, verified live on a physical
device (`adb shell screencap`, not just "it built"): **builds, installs,
launches, loads and browses the real playlist, adapts its layout to a phone
in portrait, and plays real video with audio through `VlcVideoHost`**
(confirmed by the user directly: "ça joue bien la vidéo"). **Known open
issue**: the on-screen controls overlay could not be reliably shown/hidden
by tapping the video once real playback started (see the tap-to-reveal
writeup above) — a fix (transparent tap-catching overlay `View` stacked over
the `VideoView`) is written but not yet redeployed/confirmed on this device;
the last deploy attempt was blocked by the MIUI "Install via USB" prompt.
Fullscreen specifically has therefore also never been confirmed working on
Android, since it was only reachable through the buggy tap-to-reveal path.

**Toolchain setup**, in the order that actually worked (each step's error
message names the next missing piece — that's the fastest way to
sequence this on a fresh machine rather than front-loading every install):

1. `android` workload — `dotnet workload install android` (was already
   present here: `dotnet workload list` showed `android 36.1.69/10.0.100`).
   Provides the MSBuild tooling that *drives* the SDK, not the SDK itself.
2. Android SDK platform + build-tools — no `ANDROID_HOME` and no
   `%LOCALAPPDATA%\Android\Sdk` here despite step 1, build fails with
   `XA5300: Le répertoire Android SDK est introuvable`. Went with the
   lightweight `cmdline-tools` (not full Android Studio): download
   `commandlinetools-win-*_latest.zip` from
   `https://developer.android.com/studio#command-line-tools-only`, but
   **the zip's own top-level folder must be renamed/nested one level
   deeper** — `sdkmanager` refuses to run unless the tools sit at
   `<sdk_root>\cmdline-tools\latest\bin\...`, not
   `<sdk_root>\cmdline-tools\bin\...` (the zip extracts to the latter
   directly). Set `ANDROID_HOME`/`ANDROID_SDK_ROOT` to `<sdk_root>` and put
   `cmdline-tools\latest\bin` + `platform-tools` on `PATH`
   ([Environment]::SetEnvironmentVariable(..., "User") persists it, but the
   *current* shell/process needs it re-set inline too — registry changes
   from `setx`/`SetEnvironmentVariable` don't retroactively reach an
   already-running shell).
3. `sdkmanager` needs a JDK — `ERROR: JAVA_HOME is not set and no 'java'
   command could be found`. None was present on this machine. Installed the
   Microsoft Build of OpenJDK 17 (zip, not the installer, so no admin
   prompt: `https://aka.ms/download-jdk/microsoft-jdk-17.0.20.1-windows-x64.zip`),
   set `JAVA_HOME` to the extracted `jdk-17.0.20.1+1` folder.
4. `sdkmanager --licenses`, then install packages. **The exact platform
   version matters**: the workload's manifest wants API level `36`
   specifically — `platforms;android-36` — installing `platforms;android-36.1`
   (which *does* show up in `sdkmanager --list` and looks newer) does
   **not** satisfy it; the build still fails with `XA5207: Android.jar
   introuvable ... attendu dans ...\platforms\android-36\android.jar`.
   The reliable way to get the exactly-right set without guessing version
   strings by hand: let MSBuild resolve it itself —
   `dotnet build src/IptvClient.Android -t:InstallAndroidDependencies
   -f net10.0-android -p:AndroidSdkDirectory=<sdk_root>
   -p:AcceptAndroidSDKLicenses=True` — this is what actually got `android-36`
   (plus `build-tools;36.1.0`) installed correctly here.
5. `dotnet build src/IptvClient.Android/IptvClient.Android.csproj` then
   succeeds and produces `bin\Debug\net10.0-android\*.apk` (a `-Signed.apk`
   too, debug-signed).

**Deploying to a physical device** (`adb devices` must show it first — USB
debugging enabled, cable plugged in): `dotnet build
src/IptvClient.Android/IptvClient.Android.csproj -t:Run` installs *and*
launches it. Hit `XA0129: Erreur de déploiement
« files/.__override__/.../System.Private.Uri.dll »` here — .NET Android's
"fast deploy" (pushes managed assemblies to the device separately from the
APK, for faster iterative deploys) failed outright on this device/setup.
Symptom if you skip fixing this and just install the resulting APK anyway:
**the app installs fine but crashes instantly on open** — fast deploy had
left it without its own assemblies. Fix: add
`-p:EmbedAssembliesIntoApk=true` to bundle them directly into the APK
instead (slower deploy, but reliable — use it as the default for this
project until fast-deploy is revisited).

**`LibVLCSharp.Avalonia.VideoView` does not work on Android — confirmed
live on a physical device, not a guess.** Its constructor unconditionally
sets `MediaPlayer.Hwnd` (a Windows/Linux/macOS windowing concept);
reflecting the Android-targeted `LibVLCSharp.dll` (`net10.0-android36.0`)
shows its `MediaPlayer` has no `Hwnd`, `NsObject`, or `XWindow` member *at
all*. Constructing `VideoView` on Android is an immediate
`MissingMethodException`, caught live via `adb logcat` as `FATAL EXCEPTION:
main ... [System.MissingMethodException]: Method not found: void
LibVLCSharp.Shared.MediaPlayer.set_Hwnd(intptr)`, right as `MainView` first
attaches to the visual tree (app opens and is gone within about a second).
An independent source hit the exact same wall
(monsalma.net's Avalonia-video writeup) and worked around it by dropping
LibVLCSharp entirely on Android in favor of ExoPlayer — a viable path, but
a much bigger one (a second, unrelated media-player API to adapt the whole
`MainViewModel`/`PlayerControlsView` binding surface to).

**The actual fix keeps LibVLCSharp for both heads.** LibVLCSharp *does*
ship a real Android video surface — it's just not the `LibVLCSharp.Avalonia`
package. `LibVLCSharp.Platforms.Android.VideoView` (a `SurfaceView`
subclass) lives directly inside the core `LibVLCSharp.dll` for the Android
target — confirmed by inspecting the actual Android-targeted assembly via
`System.Reflection.MetadataLoadContext` (needed instead of a normal
`AssemblyLoadContext`/`Assembly.LoadFrom`, since `Mono.Android.dll` and
friends are *reference*-only assemblies that throw `BadImageFormatException`
if you try to load them for execution outside a real Android runtime —
`MetadataLoadContext` is built exactly for inspecting assemblies without
executing them; point its `PathAssemblyResolver` at the LibVLCSharp NuGet
package's `net10.0-android36.0` lib folder plus the `Microsoft.Android.Ref.*`
and `Microsoft.NETCore.App.Ref` packs under `dotnet/packs` for
`Mono.Android.dll` / `System.Private.CoreLib.dll` etc.). Confirmed API:
`.ctor(Context)` and a settable `MediaPlayer` property that internally
drives libVLC's real Android attach mechanism (`Org.Videolan.Libvlc.AWindow`
+ `MediaPlayer.SetAndroidContext`) — none of which the app needs to touch
directly.

`IptvClient.Android/VlcVideoHost.cs` wraps that in Avalonia's own
`NativeControlHost` — the documented way to embed a native Android `View`
(https://docs.avaloniaui.net/docs/platform-specific-guides/android/embed-native-views,
pattern taken from Avalonia's own `ControlCatalog.Android` sample,
`EmbedSample.Android.cs`): `CreateNativeControlCore` casts the `parent`
handle to `AndroidViewControlHandle` to get its `.View.Context`, constructs
`new LibVLCSharp.Platforms.Android.VideoView(context) { MediaPlayer = ... }`,
wraps it together with a tap-catching overlay (see below) in a `FrameLayout`,
and returns `new AndroidViewControlHandle(layout)`.

**Wiring it into the shared `MainView` without breaking desktop**: the
shared `IptvClient` project can't reference `VlcVideoHost` directly — it's
Android-only, Desktop wouldn't compile against it. `App.AndroidVideoHostFactory`
(a static `Func<MediaPlayer, Action, Control>?` on `App`, alongside the existing
`SettingsOverride` for the same reason) is the indirection: Android's
`Application.CustomizeAppBuilder` sets it to `(mp, onTap) => new VlcVideoHost(mp, onTap)`
before Avalonia finishes starting; `MainView`'s video `Border` holds an
empty named `ContentControl` (`VideoHost`) instead of a XAML-declared
`VideoView`, and `MainView.axaml.cs` fills it two different ways depending
on platform — desktop constructs `LibVLCSharp.Avalonia.VideoView` directly
in the constructor (bound to `MediaPlayer` via `Binding`, same as always);
Android calls the factory from `OnDataContextChanged` instead, since
`VlcVideoHost` needs the actual `MediaPlayer` instance up front (its
constructor parameter) rather than a lazy binding, and the constructor
runs before `DataContext` is set. The `Action` parameter is "the user tapped
the video surface", routed to `MainViewModel.ToggleControlsVisibility()` —
native Android touch on a `SurfaceView` doesn't bubble into Avalonia's own
pointer pipeline any more than Windows mouse-move does (same airspace class
of issue, touch flavor), so `VlcVideoHost` wires a real Android click
listener and calls this directly instead.

**Tapping the video to reveal controls stopped working specifically once
real playback started — confirmed live, not guessed.** A `Click` listener
attached directly to the `VideoView` (the `SurfaceView` libVLC renders into)
fired fine while the surface was idle/black (no media loaded yet), but
silently stopped firing the moment a channel/movie actually started playing
— found by the user isolating the exact repro (idle tap works, post-play tap
doesn't). Working theory: once `MediaPlayer` actually attaches via libVLC's
own `AWindow`/`attachViews` mechanism, something in that path takes over
touch handling for the surface, ahead of our listener. **Fix**: don't rely
on the `VideoView`'s own touch behavior at all — `CreateNativeControlCore`
now returns a `FrameLayout` containing the `VideoView` plus a second, plain
transparent Android `View` stacked on top (`Clickable = true`, its own
`Click` listener calling `_onTap`). Normal Android touch dispatch follows
View z-order regardless of what a `SurfaceView` is doing internally with its
own rendering surface, so this overlay reliably catches taps in both idle
and playing states rather than depending on the `SurfaceView`'s own
click behavior. (Separately, a single physical tap was also observed
firing the click handler up to 4 times in one burst — suspected
`CreateNativeControlCore` being called more than once without a matching
`DestroyNativeControlCore` on this device/setup — debounced defensively at
400ms in the handler regardless of root cause.) **This fix is written but
not yet confirmed on-device** — the redeploy needed to verify it was
blocked by the MIUI "Install via USB" confirmation dialog (see below); pick
up by redeploying and re-testing tap-to-reveal both before and after
starting playback.

**Config on Android**: no "next to the exe" file location exists there, so
`appsettings.Local.json` is instead bundled as a raw Android asset
(`IptvClient.Android/Assets/appsettings.Local.json`, referenced via an
`<AndroidAsset>` item in the csproj, same `Condition="Exists(...)"` guard as
the desktop head's copy so a fresh clone without it still builds) and read
through `Context.Assets.Open(...)` in `Application.cs` — a completely
different API from `System.IO.File`. `AppSettingsLoader` gained a
`Load(Stream)` overload for this (the existing `Load(string path)` now just
opens a `FileStream` and delegates to it). Wired up via a new
`App.SettingsOverride` static settable on the shared `App` class: Android's
`Application.CustomizeAppBuilder` sets it (reading the asset) before Avalonia
finishes starting; `App.OnFrameworkInitializationCompleted` prefers it over
the file-path lookup when set, so desktop is completely unaffected. Same
real playlist URL as the desktop copy, so same git-ignore concern — verified
`git check-ignore` on this path specifically, not just assumed the existing
pattern still matched from a new directory.

**Android blocks cleartext (`http://`) traffic by default since API 28** —
the very first real playlist load attempt on device failed with a bare
"Connection failure", no further detail, because the playlist URL is plain
`http://` (typical for IPTV reseller panels — not something this app
controls). Fixed with `android:usesCleartextTraffic="true"` on the
`<application>` element in `AndroidManifest.xml`. App-wide rather than a
scoped Network Security Config allowlist — deliberate, since basically any
playlist/stream URL this app is fed is liable to be plain HTTP.

The `ApplicationId` placeholder (`com.CompanyName.IptvClient`) should also
get a real value at some point, independent of whether it blocks a build.

## Testing the app visually on Windows

`SetForegroundWindow` called from an external automation script (PowerShell)
is unreliable here — Windows blocks a background process from stealing
focus, so it can silently no-op while leaving some *other* window (e.g. an
editor) focused. Blindly sending clicks/keystrokes after calling it once
caused real input (a search string) to land in an unrelated window in this
project's history. If driving the app's UI via simulated input again:
verify `GetForegroundWindow() == target hwnd` right before every click/key
send and abort otherwise — don't assume `SetForegroundWindow`'s return value
or `ShowWindow` guarantee focus actually moved.

## Testing the app visually on Android

`adb shell screencap` / `adb pull` / `adb shell input tap|text|keyevent`
against a physical device (`adb devices` must list it first — USB debugging
on) is a reliable substitute for the Windows GUI-automation issue above, but
carries its own two gotchas, both hit live in this project:

- **Git Bash mangles `/sdcard/...`-style paths before `adb` ever sees them.**
  MSYS path conversion rewrites any argument that looks like a Unix absolute
  path into a Windows path, which corrupts on-device paths like
  `/sdcard/screen.png` and makes `adb shell screencap ...`/`adb pull ...`
  fail (`screencap` prints its own usage text — from `adb`'s point of view
  the command looked malformed). Fix: prefix the command with
  `MSYS_NO_PATHCONV=1`.
- **`adb shell input keyevent 4` (system Back) can exit the app entirely,
  not just dismiss whatever's on top of it.** Sent once intending only to
  dismiss an on-screen keyboard, it instead popped the app's entire
  back-stack and landed on a completely unrelated app that happened to be
  underneath in the device's task history (confirmed via screenshot) — the
  same class of "stray input on the user's real device" risk as the
  Windows `SetForegroundWindow` incident below, just triggered by app
  navigation state instead of window focus. Avoid `keyevent 4` unless the
  app's back-stack depth at that moment is actually known; screenshot
  before and after any Android input sequence rather than trusting
  previously-recorded coordinates or assuming a key event stayed inside
  the app.

## Testing the app visually on Windows

This project is developed from both a Linux laptop and a Windows desktop,
kept in sync purely through this git repo — a Claude Code session's file
access is local to the machine/directory it was started in, there is no
automatic cross-machine bridging. Pull before starting work on either
machine, push before switching to the other.
