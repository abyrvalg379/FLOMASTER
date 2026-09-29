# FLOMASTER

![FLOMASTER](docs/cover.png)

[![Release](https://img.shields.io/github/v/release/abyrvalg379/FLOMASTER)](https://github.com/abyrvalg379/FLOMASTER/releases/latest)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D6?logo=windows11&logoColor=white)](https://github.com/abyrvalg379/FLOMASTER/blob/main/README.ru.md#быстрый-старт)
[![smoke](https://img.shields.io/badge/smoke-19%2F19%20passing-brightgreen)](test/smoke_flomaster.py)

**OCIO-лаунчер** — единая точка запуска DCC-приложений с поддержкой кастомного цветового пространства ACES 1.2.

*English documentation: [README.md](README.md)*

---

## Быстрый старт

1. Скачай последний релиз со страницы [Releases](https://github.com/abyrvalg379/FLOMASTER/releases/latest)
2. Распакуй архив
3. Запусти `FLOMASTER.exe`
4. Лаунчер автоматически найдёт установленные DCC-приложения

### Требования

- Windows 10/11
- .NET 8 Desktop Runtime (входит в self-contained сборку — устанавливать ничего не нужно)

---

## Скриншоты

![Пикер ролей](docs/screenshot_roles.jpg)

| | |
|:---:|:---:|
| ![Главное окно](docs/screenshot_main.jpg) | ![Настройки](docs/screenshot_settings.jpg) |

![Окно лога](docs/screenshot_log.jpg)

---

## Что нового в v2.3.5

- **Панель Projects** — укажите папки с проектами и открывайте файлы без копания в проводнике: плоский список всех файлов с относительными путями и поиском по имени; клик открывает текущим пресетом с OCIO и наполняет Recent
- **Встроена в лаунчер** — то же окно, та же тема, те же подсветки при наведении, та же анимация раскрытия; корни хранятся в конфиге аддитивно
- **Только родные форматы** — `.blend .spp .ma .mb .hip .hipl .hipnc .nk`; экспорт и выпечка (`.fbx .obj .stl`) в список не попадают сознательно

### [v2.3.4](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.3.4) — профили запуска + починенный Recent
Именованные слепки «приложение + OCIO + аргументы» с быстрым запуском из трея; клик по файлу в Recent починен (обработчик был молча потерян); панели переставлены Roles → Profiles → Recent → Settings → Quick → Arguments.

### [v2.3.3](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.3.3) — пикер ролей OCIO + автообновление
Переопределение ролей на пресет с пикером-деревом по family как в Blender; проверка обновлений при старте и установка в один клик. Оригинальный конфиг неприкосновенен.

### [v2.3.2](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.3.2) — валидация OCIO-конфига
Ключевые роли показываются и проверяются: нет `default_byte` или `data`, роли ссылаются на неизвестные colorspace — предупреждения прямо в UI и логе.

### [v2.3.1](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.3.1) — фиксы трея
Меню трея обновляется вместе с пресетами; пункты с отсутствующим exe предупреждают вместо молчаливого игнора.

### [v2.3](https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.3) — пайплайн-корректные дефолты
Явные роли ACES в комплектном конфиге: 8-bit грузится как `raw`, float (EXR) — как `acescg`, строчные interchange-алиасы; исправлен старый регресс sRGB-фикса в Blender 5.1+.

<details>
<summary><b>Новое в v2.2</b></summary>

- **7 цветовых тем** — цвета Houdini и Nuke сняты пипеткой с реальных интерфейсов
- **Полностью темизированный интерфейс** — чекбоксы, скроллбар и выпадающие меню следуют выбранной теме, везде скруглённые углы
- **Плавная анимация панелей** — нативная WPF-анимация с easing (отключается в настройках)
- **Расширенный drag & drop** — добавлены `.mb`, `.hipl`, `.hipnc`
- **Окно открывается в правом верхнем углу** экрана
- Внутри — MVVM-архитектура

</details>

---

## Возможности

### Цветовые темы DCC
7 схем, вдохновлённых софтом, который они представляют:

| Тема | Акцент | База |
|------|--------|------|
| **Blender** | Оранжевый | Тёмный графит |
| **Maya** | Циановый | Сине-серая |
| **Houdini** | Красно-оранжевый | Тёмная, снята с интерфейса Houdini |
| **Nuke** | Светло-серый | Графит — монохром, как сам Nuke |
| **DaVinci** | Розово-красный | Фиолетово-синяя |
| **Unreal** | Синий | Холодный графит |
| **Substance** | Зелёный | Чёрная |

Каждый контрол следует теме: кнопки, чекбоксы, скроллбар, выпадающие меню и подсветка кнопки запуска.

### Автоскан
Находит: Blender, K-Cycles, Maya, Houdini, Nuke, DaVinci Resolve, Unreal Engine, Substance Painter. Свои папки сканирования добавляются в настройках.

### Быстрые команды
Раскрывающееся меню с аргументами запуска для каждого приложения.

### Недавние файлы
Недавно открытые файлы: `.blend`, `.spp`, `.ma`, `.mb`, `.hip`, `.hipl`, `.hipnc`, `.nk`

### Drag & Drop
- `.exe` → создать пресет (спросит имя)
- Файл проекта → открыть выбранным приложением с применённым OCIO

### Контекстное меню
ПКМ по файлу → «Open in FLOMASTER»

### Системный трей
Сворачивается в трей, быстрый запуск с OCIO-конфигом по умолчанию.

### Логирование
Все запуски логируются: время, пользователь, приложение, OCIO-конфиг, коды выхода. Просмотр — кнопкой Log.

### Настройки
- **Тема** — выбор цветовой темы
- **Запуск с Windows** — автостарт, свёрнутым в трей
- **Поверх всех окон** — окно выше остальных
- **Плавная анимация** — включить/отключить анимацию панелей
- **OCIO по умолчанию** — конфиг для запусков из трея
- **Папки сканирования** — свои папки для автоскана
- **Ярлыки** — в меню «Пуск» и на рабочем столе

---

## Структура

```
FLOMASTER/
├── FLOMASTER.exe             ← Приложение (self-contained)
├── flomaster.ico             ← Иконка
├── flomaster_logo.png        ← Логотип
├── LICENSE.txt               ← Лицензия MIT
├── README.md                 ← Английский README
├── README.ru.md              ← Этот файл
└── ocio/                     ← Конфиг ACES 1.2
    ├── config.ocio
    └── luts/
```

Пользовательские данные (создаются в `%APPDATA%\FLOMASTER\`):
```
├── launcher_config.json      ← Настройки
└── flomaster.log             ← Лог запусков
```

---

## Поддержка OCIO

| Приложение | Поддержка |
|------------|-----------|
| Blender | Нативно (переменная окружения OCIO) |
| Maya | Нативно (переменная окружения OCIO) |
| Houdini | Нативно (переменная окружения OCIO) |
| Nuke | Нативно (переменная окружения OCIO) |
| DaVinci Resolve | Нативно (переменная окружения OCIO) |
| Substance Painter | Нативно (переменная окружения OCIO) |
| Unreal Engine | Через аргумент `-ocio=` (env-переменную не читает) |

---

## Сборка из исходников

```bash
cd FLOMASTER_CS
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Требуется .NET 8 SDK. Результат — один self-contained exe ~155 МБ.

---

## Лицензия

MIT — см. [LICENSE.txt](LICENSE.txt). Сторонние лицензии: [NOTICE.md](NOTICE.md)

OCIO-конфиг — лицензия Academy of Motion Picture Arts and Sciences. Подробности в ocio/LICENSE.md.

---

## Связанные инструменты

| Инструмент | Описание |
|------------|----------|
| [STUKACH](https://github.com/abyrvalg379/STUKACH) | Проверка ассетов пайплайна в Blender |
| [LAMPOCHKA](https://github.com/abyrvalg379/LAMPOCHKA) | Менеджер света в сцене |
| [Switch_UDIM](https://github.com/abyrvalg379/Switch_UDIM) | Переключатель текстур Single ↔ UDIM |
| [FILTER](https://github.com/abyrvalg379/FILTER) | Видимость/выделение по типу, имени, коллекции + массовое управление модификаторами |
