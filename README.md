# FLOMASTER

![FLOMASTER](docs/cover.png)

[![Release](https://img.shields.io/github/v/release/abyrvalg379/FLOMASTER)](https://github.com/abyrvalg379/FLOMASTER/releases/latest)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D6?logo=windows11&logoColor=white)](https://github.com/abyrvalg379/FLOMASTER#quick-start)
[![smoke](https://img.shields.io/badge/smoke-21%2F21%20passing-brightgreen)](test/smoke_flomaster.py)

**OCIO Launcher** — a unified launch point for DCC applications with custom ACES 1.2 color space support.

*Документация на русском: [README.ru.md](README.ru.md)*

---

## Quick Start

1. Download the latest release from [Releases](https://github.com/abyrvalg379/FLOMASTER/releases/latest)
2. Extract the archive
3. Run `FLOMASTER.exe`
4. The launcher will automatically find installed DCC applications

### Requirements

- Windows 10/11
- .NET 8 Desktop Runtime (bundled in the self-contained build — no installation needed)

---

## Screenshots

![Role picker](docs/screenshot_roles.jpg)

| | |
|:---:|:---:|
| ![Main window](docs/screenshot_main.jpg) | ![Settings](docs/screenshot_settings.jpg) |

![Log window](docs/screenshot_log.jpg)

---

## What's New in v2.6.0

- **Launch without OCIO** — the NO OCIO chip (top of COLOR CONFIG) starts any DCC with its own default color management: no OCIO env var, no `-ocio` for Unreal. Pin it in a profile ("Blender without ACES") or make it the startup default. A deliberate no-OCIO stays clearly distinct from a broken config — no scary warnings
- **Sync folder** — point FLOMASTER to a folder shared between your machines (Dropbox, OneDrive, NAS): profiles, project roots, theme and behaviour flags travel automatically. Your own state is pushed silently; other machines' changes are offered as an Import chip in MAINTENANCE and apply only on your click
- **Update banner, upgraded** — the banner appears as soon as an update is found (not only when it is ready): a live download progress bar with percent, and a "What's new" button that opens the release notes right in the app
- **Release notes panel** — on wide screens the notes open in a dedicated panel in the free space next to the columns (full-height reading, download progress inside); narrow windows fall back to the in-banner expansion
- **Dashboard <-> widget switching** — one header button in each window (plus a tray item) swaps the fullscreen dashboard and the small window in place; the Ctrl+Alt+F rule follows the screen, so the hotkey always toggles what you see
- **Animated drawers** — the roles drawer, the log drawer and the update panel slide in smoothly; the "Launcher animation" toggle switches that off
- **App sort cycling** — click the active Apps chip again and the app groups rotate: all .spp files (or any other family) jump to the top in one click. Groups with no files don't waste your clicks

### [v2.5.8](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.5.8) — sorted projects, update banner
Project list sorting (A-Z / Date / Apps) and an explicit update-ready banner in both windows.

### [v2.5.7](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.5.7) — updates ship the config
Auto-updates download the full package (exe + OCIO config), Painter project defaults follow the fixed regulation, config paths can no longer go stale.

### [v2.5.6](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.5.6) — self-healing
No hidden admin launches after updates; lost OCIO config paths repair themselves on startup.

### [v2.5.5](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.5.5) — dashboard era
The dashboard became the full launcher UI: role picker and log drawers, per-tile quick launch commands, running-app chips, keyboard navigation, cleaner scanner, compact 2×N tile grid.

### [v2.5](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.5) — fullscreen dashboard
Fullscreen overlay dashboard (`Ctrl+Alt+F`): color config chips, application tiles with real exe icons, projects, profiles, OCIO roles, recent files and quick settings — over everything, draggable between monitors. Project file routing (.spp to Painter, .blend to Blender, .hip to Houdini). PerMonitorV2.

### [v2.4.2](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.4.2) — project file routing
An .spp opens in Substance Painter, a .blend in Blender, a .hip in Houdini — regardless of the selected app. Works in the Projects panel, Recent and drag & drop.

### [v2.4.1](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.4.1) — command line + multi-machine
- **Command line** — `--launch "Profile" [--project file]` and `--list-profiles`: headless, exit codes, same environment as a manual launch
- **Multi-machine** — export/import settings as a file; preset discovery scans the registry and Epic manifests
- **Global hotkey, themed title bar, Projects panel** — point it at project folders and open files without Explorer

### [v2.3.5](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.3.5) — Projects panel prototype + recent files fix
First take of the project browser; recent files click repaired.

### [v2.3.4](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.3.4) — launch profiles
Named app + OCIO + arguments snapshots with tray quick-launch; panels reordered.

### [v2.3.3](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.3.3) — OCIO role picker + auto-update
Per-preset role overrides with a family-grouped picker; auto-update check on start. Original config untouched.

### [v2.3.2](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.3.2) — OCIO config validation
Key roles shown and validated per config — warnings right in the UI and the log.

### [v2.3.1](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.3.1) — tray fixes
Tray menu updates with presets; missing exes warn instead of doing nothing.

### [v2.3](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.3) — pipeline-correct defaults
Explicit ACES roles in the bundled config: 8-bit images load as `raw`, float (EXR) as `acescg`.

<details>
<summary><b>What's New in v2.2</b></summary>

- **7 color themes** — Houdini and Nuke colors sampled from the real application UIs
- **Fully themed interface** — checkboxes, scrollbars and dropdown popups follow the selected theme, rounded corners everywhere
- **Smooth panel animation** — native WPF animation with easing (toggleable in Settings)
- **Extended drag & drop** — `.mb`, `.hipl`, `.hipnc` added
- **Window opens in the top-right corner** of the screen
- MVVM architecture under the hood

</details>

---

## Command Line

Launch without the window — profiles resolve to an app + OCIO config + arguments, so batch files and farms get the same environment as a manual launch:

```
FLOMASTER.exe --list-profiles
FLOMASTER.exe --launch "Project A" 
FLOMASTER.exe --launch "Project A" --project "E:\shows\A\shot_042.blend"
```

`--project` appends the file to the profile arguments (or substitutes a `{file}` placeholder if the profile has one). Exit codes: `0` launched, `2` profile/preset/file not found, `1` launch error — script-friendly.

---

## Features

### DCC Color Themes
7 color schemes inspired by the software they represent:

| Theme | Accent | Base |
|-------|--------|------|
| **Blender** | Orange | Dark graphite |
| **Maya** | Cyan | Blue-gray |
| **Houdini** | Red-orange | Dark, sampled from Houdini UI |
| **Nuke** | Light gray | Graphite — monochrome, like Nuke itself |
| **DaVinci** | Pink-red | Purple-navy |
| **Unreal** | Blue | Cool graphite |
| **Substance** | Green | Black |

Every control follows the theme: buttons, checkboxes, the scrollbar, dropdown menus and the launch button hover state.

### Auto-Scan
Finds: Blender, K-Cycles, Maya, Houdini, Nuke, DaVinci Resolve, Unreal Engine, Substance Painter. Custom scan paths can be added in Settings.

### Quick Commands
Expandable menu with launch arguments for each application.

### Recent Files
Recently opened files: `.blend`, `.spp`, `.ma`, `.mb`, `.hip`, `.hipl`, `.hipnc`, `.nk`

### Drag & Drop
- `.exe` → create a preset (name is asked)
- Project file → open in the selected application with OCIO applied

### Context Menu
Right-click files → "Open in FLOMASTER"

### System Tray
Minimizes to tray, quick launch with the default OCIO config.

### Logging
All launches are logged with timestamps, user, application, OCIO config and exit codes. View logs via the Log button.

### Settings
- **Theme** — color theme select
- **Start with Windows** — auto-start minimized to tray
- **Always on top** — window above all others
- **Smooth animation** — enable/disable panel animation
- **Default OCIO** — default config for tray launches
- **Scan paths** — custom folders for auto-scan
- **Shortcuts** — Start Menu and desktop shortcuts

---

## Structure

```
FLOMASTER/
├── FLOMASTER.exe             ← Application (self-contained)
├── flomaster.ico             ← Icon
├── flomaster_logo.png        ← Logo
├── LICENSE.txt               ← MIT License
├── README.md                 ← This file
├── README.ru.md              ← Russian README
└── ocio/                     ← ACES 1.2 config
    ├── config.ocio
    └── luts/
```

User data (auto-created in `%APPDATA%\FLOMASTER\`):
```
├── launcher_config.json      ← Settings
└── flomaster.log             ← Launch log
```

---

## OCIO Support

| Application | Support |
|-------------|---------|
| Blender | Native (OCIO env variable) |
| Maya | Native (OCIO env variable) |
| Houdini | Native (OCIO env variable) |
| Nuke | Native (OCIO env variable) |
| DaVinci Resolve | Native (OCIO env variable) |
| Substance Painter | Native (OCIO env variable) |
| Unreal Engine | Via `-ocio=` argument (does not read the env variable) |

---

## Build from Source

```bash
cd FLOMASTER_CS
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Requires the .NET 8 SDK. Output: a single ~155 MB self-contained executable.

---

## License

MIT License — see [LICENSE.txt](LICENSE.txt). Third-party attributions: [NOTICE.md](NOTICE.md)

OCIO config — Academy of Motion Picture Arts and Sciences license. See ocio/LICENSE.md for details.

---

## Related Tools

| Tool | Description |
|------|-------------|
| [STUKACH](https://github.com/abyrvalg379/STUKACH) | Pipeline asset validator for Blender |
| [LAMPOCHKA](https://github.com/abyrvalg379/LAMPOCHKA) | Scene light manager |
| [Switch_UDIM](https://github.com/abyrvalg379/Switch_UDIM) | Single ↔ UDIM texture switcher |
| [FILTER](https://github.com/abyrvalg379/FILTER) | Toggle visibility/selection by type, name, collection + bulk modifier management (apply, remove, diff) |
| [KARUSELKA](https://github.com/abyrvalg379/karuselka) | Fast camera turntable rig: orbit or object spin |
