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
- `IptvClient.Android` — Android entry point (`net10.0-android`); not
  buildable until the `android` workload is installed locally. Still has the
  template's placeholder `ApplicationId` (`com.CompanyName.IptvClient`).

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
Not wired up to the UI yet (channels can be browsed but not played) —
`LibVLCSharp` / `LibVLCSharp.Avalonia` are referenced by `IptvClient` but
unused so far.

- Windows: handled by the `VideoLAN.LibVLC.Windows` package, nothing extra.
- Linux: **not** covered by NuGet. Needs system libVLC installed via the
  distro package manager (Debian/Ubuntu: `libvlc-dev libvlc5
  vlc-plugin-base`). A Snap-installed VLC does not work here — its libVLC is
  sandboxed and other processes can't dlopen it.
- Android: needs the `VideoLAN.LibVLC.Android` package, not yet added
  (blocked on the workload install above).

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

## Multi-machine development

This project is developed from both a Linux laptop and a Windows desktop,
kept in sync purely through this git repo — a Claude Code session's file
access is local to the machine/directory it was started in, there is no
automatic cross-machine bridging. Pull before starting work on either
machine, push before switching to the other.
