# FLOMASTER

**OCIO Launcher** — A unified launch point for DCC applications with custom ACES 1.2 color space support.

---

## Quick Start

1. Download the latest release from [Releases](https://github.com/abyrvalg379/FLOMASTER/releases)
2. Extract `FLOMASTER_v2.0.zip`
3. Run `FLOMASTER.exe`
4. The launcher will automatically find installed DCC applications

### Requirements

- Windows 10/11
- .NET 8 Desktop Runtime (bundled in self-contained version)

---

## Features

### DCC Color Themes
5 color schemes inspired by popular software:

| Theme | Accent | Inspiration |
|-------|--------|-------------|
| **Blender** | Orange | Blender |
| **Maya** | Teal | Autodesk Maya |
| **Houdini** | Amber | SideFX Houdini |
| **Nuke** | Green | Foundry Nuke |
| **DaVinci** | Red | DaVinci Resolve |

### Auto-Scan
Finds: Blender, K-Cycles, Maya, Houdini, Nuke, DaVinci Resolve, Unreal Engine, Substance Painter

### Quick Commands
Expandable menu with launch arguments for each application.

### Recent Files
Recently opened files: `.blend`, `.spp`, `.ma`, `.hip`, `.nk`

### Drag & Drop
- `.exe` → create preset
- `.blend/.spp/.ma/.hip/.nk` → open in selected application

### Context Menu
Right-click files → "Open in FLOMASTER"

### Settings
- **Theme select** — choose color theme
- **Start with Windows** — auto-start
- **Always on top** — window above all others
- **Smooth animation** — animated panel expand/collapse
- **Default OCIO** — default config for tray launches
- **Scan paths** — custom folders for auto-scan
- **Shortcuts** — desktop shortcuts

---

## Structure

```
FLOMASTER/
├── FLOMASTER.exe             ← Application (self-contained)
├── flomaster.ico             ← Icon
├── flomaster_logo.png        ← Logo
├── launcher_config.json      ← Settings
├── LICENSE.txt               ← MIT License
├── README.md                 ← This file
└── ocio/                     ← ACES 1.2 config
    ├── config.ocio
    └── luts/
```

---

## OCIO Support

| Application | Support |
|-------------|---------|
| Blender | Native |
| Maya | Native |
| Houdini | Native |
| Nuke | Native |
| DaVinci Resolve | Native |
| Unreal Engine | Via `-ocio=` argument |
| Substance Painter | Native |

---

## Build from Source

```bash
cd FLOMASTER_CS
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## License

MIT License — see [LICENSE.txt](LICENSE.txt)

OCIO config — Academy of Motion Picture Arts and Sciences license. See ocio/LICENSE.md for details.


---

## 🔗 Related Tools

| Tool | Description |
|------|-------------|
| [STUKACH](https://github.com/abyrvalg379/STUKACH) | Pipeline asset validator for Blender |
| [LAMPOCHKA](https://github.com/abyrvalg379/LAMPOCHKA) | Scene light manager |
| [Switch_UDIM](https://github.com/abyrvalg379/Switch_UDIM) | Single ↔ UDIM texture switcher |
| [FLOMASTER](https://github.com/abyrvalg379/FLOMASTER) | OCIO launcher for DCC apps |
| [FILTER](https://github.com/abyrvalg379/FILTER) | Toggle visibility/selection by type, name, collection |
