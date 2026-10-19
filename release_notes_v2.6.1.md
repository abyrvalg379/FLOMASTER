## FLOMASTER v2.6.1

**Download `FLOMASTER_v2.6.1.zip`** — it contains everything (launcher + OCIO config). Unpack the whole folder and run `install_flomaster.ps1` (right-click → Run with PowerShell) for a Program Files install, or run `FLOMASTER.exe` from the folder.

- **NukeX support** — since Nuke 16, Nuke, NukeX and Studio ship as a single binary, so scanners that look for a separate NukeX executable find nothing. The auto-scan now adds a second **NukeX** tile that launches the same exe with `--nukex` and carries the native NukeX icon (exactly how Foundry's own desktop shortcut works). Legacy (≤ Nuke 15) NukeX installs are detected by their own executable
- **Per-preset launch arguments** — a preset can carry baked arguments; they always run ahead of your own — on tiles, quick commands, file routing, `--launch` from the command line and shortcuts created by FLOMASTER. Profiles keep storing only your arguments. Baked args are visible right on the tile subtitle (`Nuke17.1.exe · --nukex`), so lookalike tiles stay tellable and searchable
- **Nuke fixes** — the render-range quick command actually renders now (`-x "file" -F 1-100`), the app tile explains the one-time Project Settings → Color → OCIO switch in its tooltip, and the scanned tile is named "Nuke 17.1v2" instead of the stuttering "Nuke Nuke17.1v2"

Manuals (RU + EN, DOCX/PDF) are attached below. Smoke 22/22, unit tests 140.
