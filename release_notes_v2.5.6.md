# FLOMASTER v2.5.6

Bugfix release — launched apps no longer inherit administrator rights, and lost OCIO configs repair themselves.

## Fixes

- **No more hidden admin launches** — after an auto-update FLOMASTER used to keep running with administrator rights, and every DCC started from it inherited the elevated token (crashes in Substance Painter with overlay tools, blocked drag & drop). Updates now relaunch the app without elevation, and on startup FLOMASTER self-heals: detecting admin rights, it quietly relaunches itself de-elevated
- **OCIO configs repair themselves** — a config entry whose .ocio file path was lost (for example, after copying just the exe to another machine) is re-linked to the bundled config on startup instead of silently launching apps without color management
- **Clearer diagnostics** — the log names the config missing its file and warns when FLOMASTER itself is running elevated
- Launched apps start in their own folder instead of inheriting FLOMASTER's working directory

## Upgrade

Install over the existing version (config is preserved), or check for updates on start — the app offers "Restart and install" by itself. If you are upgrading from 2.5.0–2.5.5 and the app opens with administrator rights once, it will fix itself on the next start.

**Full changelog:** see What's New in the README (EN/RU).


**Full Changelog**: https://github.com/abyrvalg379/FLOMASTER/compare/v2.5.5...v2.5.6
