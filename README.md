# IPTV Multi-Client

Client IPTV multiplateforme (Windows, Linux, Android — macOS envisagé plus tard)
pour lire une playlist M3U/M3U8 avec un temps de chargement de flux correct
(contrairement à VLC "nu" et aux addons Kodi disponibles).

## Stack

- **C# / .NET** (SDK épinglé via [`global.json`](global.json))
- **[Avalonia UI](https://avaloniaui.net/)** — framework XAML cross-platform (Windows/Linux/macOS/Android/iOS), choisi notamment pour son support Linux natif (contrairement à .NET MAUI)
- **[LibVLCSharp](https://github.com/videolan/libvlcsharp)** — binding officiel VideoLAN pour libVLC, avec packages natifs prêts à l'emploi par plateforme (dont Android)

## Structure du repo

```
IptvClient.slnx            # solution (format .slnx)
/src
  IptvClient.Core/         # logique partagée : parsing M3U, modèles, services (pas de dépendance Avalonia)
  IptvClient/               # ViewModels + Views Avalonia partagés (référence Core)
  IptvClient.Desktop/       # tête Windows/Linux/macOS
  IptvClient.Android/       # tête Android
  Directory.Packages.props  # versions de packages centralisées (CPM)
```

Têtes iOS/Browser du template `avalonia.xplat` retirées pour l'instant (hors
scope : Windows, Linux, Android en priorité, macOS gardé en réserve).

## Setup — environnement de dev

Ce projet est développé indifféremment depuis un laptop Linux et un PC fixe
Windows : la version du SDK est épinglée dans `global.json` pour que les deux
machines compilent identique.

### Linux

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version 10.0.100
```

Ajoute ensuite `~/.dotnet` à ton `PATH` (le script l'indique en fin d'exécution).

### Windows

```powershell
winget install Microsoft.DotNet.SDK.10
```

### Les deux plateformes

```bash
dotnet --version   # doit correspondre à global.json (10.0.x)
dotnet workload install android   # nécessaire uniquement sur la machine qui build la tête Android
```

⚠️ Si `dotnet` ne trouve aucun SDK alors que `~/.dotnet/sdk` en contient :
vérifie l'ordre du `PATH`, un `dotnet` système (`/usr/bin/dotnet`, souvent un
stub sans SDK fourni par un paquet distro) peut passer avant `~/.dotnet`.

**IDE** : VS Code + extensions *C# Dev Kit* et *Avalonia for VSCode* (identique
sur les deux OS). [JetBrains Rider](https://www.jetbrains.com/rider/) (gratuit
en usage non commercial) est une alternative avec un meilleur previewer XAML
si besoin plus tard.

### Lecture vidéo (LibVLCSharp) — dépendance runtime par OS

- **Windows** : géré automatiquement, le package NuGet `VideoLAN.LibVLC.Windows`
  embarque les binaires natifs.
- **Linux** : LibVLCSharp cherche `libvlc.so` via le système — **le VLC installé
  en Snap ne fonctionne pas** (confinement Snap, la lib n'est pas accessible aux
  autres apps). Installer la version apt en plus :
  ```bash
  sudo apt install libvlc-dev libvlc5 vlc-plugin-base
  ```
- **Android** : package NuGet `VideoLAN.LibVLC.Android` (à ajouter une fois le
  workload `android` installé — `dotnet add src/IptvClient.Android package VideoLAN.LibVLC.Android`).

## Configuration locale (playlist M3U)

Ce repo est **public** : aucune URL de playlist, aucun identifiant ne doit
être commité. La configuration réelle (URL M3U, éventuels identifiants) vit
dans un fichier local ignoré par git — voir `appsettings.Local.json` une fois
le projet scaffoldé (pattern déjà couvert par `.gitignore`).

## Licence

MIT (voir [LICENSE](LICENSE)) pour le code de ce dépôt. LibVLCSharp/libVLC
est sous LGPL-2.1 et utilisé en liaison dynamique — cela n'affecte pas la
licence du code présent ici.

## État du projet

- [x] Scaffolding repo (git, gitignore, editorconfig, licence)
- [x] Solution Avalonia (Core + UI partagée + Desktop head, build vérifié sur Linux)
- [x] LibVLCSharp référencé (UI + Desktop), packages natifs par OS documentés
- [ ] Tête Android buildable (workload à installer)
- [ ] Parsing M3U
- [ ] Lecture vidéo effective + tuning network-caching
- [ ] UI liste de chaînes / favoris
- [ ] EPG (XMLTV)
