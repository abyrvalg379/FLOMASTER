# FLOMASTER

**OCIO Launcher** — единая точка запуска для DCC-приложений с кастомным цветовым пространством ACES 1.2.

---

## Быстрый старт

1. Скачай последний релиз
2. Запусти `FLOMASTER.exe`
3. Лаунчер автоматически найдёт установленные DCC-приложения

### Требования

- Windows 10/11
- .NET 8 Desktop Runtime (встроен в self-contained версию)

---

## Возможности

### Темы в стиле DCC
5 цветовых схем, вдохновлённых популярным софтом:

| Тема | Акцент | Вдохновение |
|------|--------|-------------|
| **Blender** | Оранжевый | Blender |
| **Maya** | Бирюзовый | Autodesk Maya |
| **Houdini** | Янтарный | SideFX Houdini |
| **Nuke** | Зелёный | Foundry Nuke |
| **DaVinci** | Красный | DaVinci Resolve |

### Автоскан
Находит: Blender, K-Cycles, Maya, Houdini, Nuke, DaVinci Resolve, Unreal Engine, Substance Painter

### Quick Commands
Раскривающееся меню с аргументами запуска для каждого приложения.

### Recent Files
Последние открытые файлы: `.blend`, `.spp`, `.ma`, `.hip`, `.nk`

### Drag & Drop
- `.exe` → создать пресет
- `.blend/.spp/.ma/.hip/.nk` → открыть в выбранном приложении

### Контекстное меню
ПКМ по файлам → "Open in FLOMASTER"

### Settings
- **Theme select** — выбор темы
- **Start with Windows** — автозапуск
- **Always on top** — окно поверх всех
- **Smooth animation** — плавное раскрытие панелей
- **Default OCIO** — дефолтный конфиг для запуска из трея
- **Scan paths** — кастомные папки для автоскана
- **Shortcuts** — ярлыки на рабочий стол

---

## Структура

```
FLOMASTER/
├── FLOMASTER.exe             ← Приложение (self-contained)
├── flomaster.ico             ← Иконка
├── flomaster_logo.png        ← Логотип
├── launcher_config.json      ← Настройки
├── LICENSE.txt               ← MIT License
├── README.md                 ← Этот файл
└── ocio/                     ← ACES 1.2 конфиг
    ├── config.ocio
    └── luts/
```

---

## Поддержка OCIO

| Приложение | Поддержка |
|-----------|-----------|
| Blender | ✅ нативно |
| Maya | ✅ нативно |
| Houdini | ✅ нативно |
| Nuke | ✅ нативно |
| DaVinci Resolve | ✅ нативно |
| Unreal Engine | ✅ через аргумент `-ocio=` |
| Substance Painter | ✅ нативно |

---

## Сборка из исходников

```bash
cd FLOMASTER_CS
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## Лицензия

MIT License — см. [LICENSE.txt](LICENSE.txt)

OCIO конфиг — лицензия Academy of Motion Picture Arts and Sciences
