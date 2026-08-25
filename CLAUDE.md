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

- `IptvClient.Core` — plain .NET library, no Avalonia reference. Intended
  home for M3U parsing, models, and services shared regardless of UI. Empty
  so far.
- `IptvClient` — shared Avalonia UI (Views/ViewModels/App), referencing
  `IptvClient.Core`. MVVM via CommunityToolkit.Mvvm (`ObservableObject`,
  `[ObservableProperty]`). `ViewLocator` resolves a View for a ViewModel by
  string-replacing `ViewModel` → `View` in the full type name via reflection
  — a new `FooViewModel` needs a `FooView` in the mirrored namespace or the
  locator renders a "Not Found" placeholder instead.
- `IptvClient.Desktop` — Windows/Linux/macOS entry point (`OutputType=WinExe`).
  Pulls in `VideoLAN.LibVLC.Windows` for the native libVLC binaries on Windows.
- `IptvClient.Android` — Android entry point (`net10.0-android`); not
  buildable until the `android` workload is installed locally. Still has the
  template's placeholder `ApplicationId` (`com.CompanyName.IptvClient`).

`App.axaml.cs` picks the shell by `ApplicationLifetime` type: classic
windowed `MainWindow` on desktop lifetimes, single-view `MainView` on
mobile/browser-style lifetimes — both seeded with `new MainViewModel()`.

**Video playback**: LibVLCSharp (libVLC), chosen over an mpv-based engine
(e.g. via Tauri) specifically because per-OS native binaries ship as ready
NuGet packages, at the cost of one manual runtime dependency per platform:

- Windows: handled by the `VideoLAN.LibVLC.Windows` package, nothing extra.
- Linux: **not** covered by NuGet. Needs system libVLC installed via the
  distro package manager (Debian/Ubuntu: `libvlc-dev libvlc5
  vlc-plugin-base`). A Snap-installed VLC does not work here — its libVLC is
  sandboxed and other processes can't dlopen it.
- Android: needs the `VideoLAN.LibVLC.Android` package, not yet added
  (blocked on the workload install above).

**Config**: nothing reads config yet, but the gitignore already reserves
`appsettings.Local.json` / `appsettings.*.local.json` / `**/config.local.json`
/ `secrets/` as the intended home for any real M3U playlist URL or
credentials — this repo is public, so real playlist data belongs in one of
those paths, never in a committed file.

## Multi-machine development

This project is developed from both a Linux laptop and a Windows desktop,
kept in sync purely through this git repo — a Claude Code session's file
access is local to the machine/directory it was started in, there is no
automatic cross-machine bridging. Pull before starting work on either
machine, push before switching to the other.
