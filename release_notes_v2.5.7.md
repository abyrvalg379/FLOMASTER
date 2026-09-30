# FLOMASTER v2.5.7

Bugfix release — updates deliver the OCIO config, Substance Painter gets correct project color defaults, and stale config paths heal themselves.

## Fixes

- **Updates ship the full package** — the auto-updater replaced only the exe, so installs without an `ocio` folder next to it launched apps with no color management, silently. Updates now download the complete package (exe + OCIO config) and sync the folder on install. Paths with spaces, Cyrillic characters and apostrophes are quoted safely
- **Substance Painter: correct project defaults** — the bundled OCIO config now defines Painter's bitmap import/export default color spaces, so new projects follow the fixed regulation (16-bit export in ACES - ACEScg, 8/16-bit textures and materials in sRGB) instead of falling back to the working color space for everything. Existing projects keep their own settings
- **Config paths can no longer go stale** — the canonical config entry follows the installation: on every start it re-links to the config shipped with the running build; legacy entries migrate automatically. Custom (user-added) configs are untouched
- **Clearer warnings** — STATUS warns when the selected config has no file; the log flags non-ASCII config paths (some apps silently ignore them) and logs download progress

## Upgrade

Install over the existing version (config is preserved), or let the app update itself. Note: machines on 2.5.6 or older receive this update as exe-only one last time — if you launch apps without color management after updating, reinstall once from the full **FLOMASTER_v2.5.7.zip** (run `install_flomaster.ps1`). From 2.5.7 on, updates carry the config themselves.

**Full Changelog**: https://github.com/abyrvalg379/FLOMASTER/compare/v2.5.6...v2.5.7
