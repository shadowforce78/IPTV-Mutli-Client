# IPTV Multi-Client

Client IPTV multiplateforme (Windows, Linux, Android — macOS envisagé plus tard)
pour lire une playlist M3U/M3U8 avec un temps de chargement de flux correct
(contrairement à VLC "nu" et aux addons Kodi disponibles).

## Stack

- **C# / .NET** (SDK épinglé via [`global.json`](global.json))
- **[Avalonia UI](https://avaloniaui.net/)** — framework XAML cross-platform (Windows/Linux/macOS/Android/iOS), choisi notamment pour son support Linux natif (contrairement à .NET MAUI)
- **[LibVLCSharp](https://github.com/videolan/libvlcsharp)** — binding officiel VideoLAN pour libVLC, avec packages natifs prêts à l'emploi par plateforme (dont Android)

## Structure du repo (prévue)

```
/src
  IptvClient.Core/        # logique partagée : parsing M3U, modèles, services
  IptvClient.UI/           # ViewModels + Views Avalonia partagés
  IptvClient.Desktop/      # tête Windows/Linux/macOS
  IptvClient.Android/      # tête Android
/docs
```

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
dotnet --version   # doit correspondre à global.json
dotnet workload install android   # nécessaire uniquement sur la machine qui build la tête Android
```

**IDE** : VS Code + extensions *C# Dev Kit* et *Avalonia for VSCode* (identique
sur les deux OS). [JetBrains Rider](https://www.jetbrains.com/rider/) (gratuit
en usage non commercial) est une alternative avec un meilleur previewer XAML
si besoin plus tard.

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
- [ ] Solution Avalonia (Core + Desktop head)
- [ ] Parsing M3U
- [ ] Lecture vidéo via LibVLCSharp + tuning network-caching
- [ ] UI liste de chaînes / favoris
- [ ] Tête Android
- [ ] EPG (XMLTV)
