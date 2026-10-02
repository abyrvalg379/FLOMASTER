## FLOMASTER v2.6.0

**Download `FLOMASTER_v2.6.0.zip`** — it contains everything (launcher + OCIO config). Unpack the whole folder and run `install_flomaster.ps1` (right-click → Run with PowerShell) for a Program Files install, or run `FLOMASTER.exe` from the folder.

- **Launch without OCIO** — the NO OCIO chip starts any DCC with its own default color management: no OCIO env var, no `-ocio` for Unreal. Pin it in a profile ("Blender without ACES") or make it the startup default. A deliberate no-OCIO stays clearly distinct from a broken config — no scary warnings
- **Sync folder** — point FLOMASTER to a folder shared between your machines (Dropbox, OneDrive, NAS): profiles, project roots, theme and behaviour flags travel automatically. Your own state is pushed silently; other machines' changes are offered as an Import chip in MAINTENANCE and apply only on your click
- **Update banner, upgraded** — the banner appears as soon as an update is found (not only when it is ready): a live download progress bar with percent, and a "What's new" button that opens the release notes right in the app
- **Release notes panel** — on wide screens the notes open in a dedicated panel in the free space next to the columns (full-height reading, download progress inside); narrow windows fall back to the in-banner expansion
- **Dashboard ↔ widget switching** — one header button in each window (plus a tray item) swaps the fullscreen dashboard and the small window in place; the Ctrl+Alt+F rule follows the screen, so the hotkey always toggles what you see
- **Animated drawers** — the roles drawer, the log drawer and the update panel slide in smoothly; the "Launcher animation" toggle switches that off
- **App sort cycling** — click the active Apps chip again and the app groups rotate: all .spp files (or any other family) jump to the top in one click. Groups with no files don't waste your clicks

Manuals (RU + EN, DOCX/PDF) are attached below. Smoke 21/21, unit tests 113.
