## FLOMASTER v2.6.2

**Download `FLOMASTER_v2.6.2.zip`** — it contains everything (launcher + OCIO config). Unpack the whole folder and run `install_flomaster.ps1` (right-click → Run with PowerShell) for a Program Files install, or run `FLOMASTER.exe` from the folder.

- **Auto EN keyboard layout (Houdini, Maya)** — a new Settings option for the eternal pain: hotkeys stop firing when the keyboard is in a non-Latin layout (Houdini and Maya bind hotkeys to characters, not physical keys). When enabled, a tiny background guard (`DccEnLayout.exe`, ~9 KB, embedded into the launcher) switches the layout to English automatically whenever a Houdini or Maya window gets focus — within a second, no action required. Other apps are untouched; inside a DCC, Alt+Shift still switches to Russian for notes and holds until you re-enter the app. The guard is deployed to `%LOCALAPPDATA%\FLOMASTER`, runs from HKCU autostart and lives independently of the launcher (works with FLOMASTER closed). A pre-FLOMASTER standalone installation (`HoudiniEnLayout`) is adopted silently: old process killed, old autostart removed
- Off by default (deliberate: public release, non-Russian users gain nothing from a resident process). The preference travels with Settings Export; older exports don't carry the field and never silently flip your choice

Smoke 22/22, unit tests 140.
