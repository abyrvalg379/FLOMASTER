# -*- coding: utf-8 -*-
r"""FLOMASTER - Руководство пользователя (RU + EN). Генератор DOCX.

Хелперы стиля наследуют _gen_manual_ru.py семейства STUKACH/LAMPOCHKA/TOCHKA,
акцент - фирменный оранжевый FLOMASTER #E87D0D.
Запуск:  python _gen_manual.py
Выход:   work\docs\FLOMASTER_Manual_RU.docx + FLOMASTER_Manual_EN.docx
"""

import json

import _docstyle as ds

OUT_RU = r'D:\AI\ZCode\Project\FLOMASTER\work\docs\FLOMASTER_Manual_RU.docx'
OUT_EN = r'D:\AI\ZCode\Project\FLOMASTER\work\docs\FLOMASTER_Manual_EN.docx'


def h1(doc, text):
    return ds.h1(doc, text)


def h2(doc, text):
    return ds.h2(doc, text)


def p(doc, text, bullet=False, italic=False, grey=False):
    return ds.p(doc, text, bullet=bullet, italic=italic, grey=grey)


def kv_note(doc, text):
    return ds.kv(doc, text)


def add_table(doc, rows, widths, sev_col=None):
    return ds.add_table(doc, rows, widths, sev_col=sev_col)


def _save(doc, out):
    ds.footer(doc.sections[1], 'FLOMASTER')
    ds.strip_tail(doc)
    doc.save(out)
    h1s = [t for t in ds.H1_REGISTRY if t.lower() not in ('содержание', 'contents')]
    json.dump(h1s, open(out.replace('.docx', '.h1.json'), 'w', encoding='utf-8'),
              ensure_ascii=False)
    print('saved:', out)

def build_ru():
    ds.H1_REGISTRY.clear()
    doc = ds.new_doc('FLOMASTER', 'Руководство пользователя', 'V2.3  -  WINDOWS 10/11')

    p(doc, 'FLOMASTER - лаунчер для рабочих приложений трёхмерной графики: одна кнопка '
           'запускает Blender, Maya, Houdini, Nuke, DaVinci Resolve, Unreal Engine или '
           'Substance Painter с заранее заданным OCIO-конфигом цвета. Лаунчер сам находит '
           'установленные приложения, ведёт пресеты запуска, открывает файлы проектов и '
           'ведёт журнал каждого запуска. Один и тот же цветовой конфиг - во всех '
           'приложениях, без ручных переменных окружения.')

    kv_note(doc, 'github.com/abyrvalg379/FLOMASTER')

    h1(doc, 'Содержание')
    ds.toc_field(doc, 'Оглавление: откройте документ в Word/LibreOffice и обновите поле (F9), '
                       'чтобы заполнить номера страниц.')

    h1(doc, '1. О программе')
    p(doc, 'Ключевые возможности:', bullet=False)
    for b in (
        'автоскан установленных приложений: Blender, K-Cycles, Maya, Houdini, Nuke, DaVinci Resolve, Unreal Engine, Substance Painter;',
        'пресеты запуска: своё имя, свой exe, свои аргументы запуска;',
        'открытие файлов проектов (.blend, .spp, .ma, .mb, .hip, .hipl, .hipnc, .nk) с применением OCIO;',
        '7 цветовых тем в стиле запускаемых приложений;',
        'drag-and-drop: exe превращается в пресет, файл проекта запускается в выбранном приложении;',
        'системный трей с быстрым запуском под конфигом по умолчанию;',
        'журнал запусков: время, пользователь, приложение, конфиг, код возврата;',
        'комплектный ACES 1.2 OCIO-конфиг со строчными алиасами acescg / raw.',
    ):
        p(doc, b, bullet=True)

    h1(doc, '2. Установка')
    for b in (
        'Скачайте последний релиз: github.com/abyrvalg379/FLOMASTER → Releases → Latest.',
        'Распакуйте архив в удобную папку (например, C:\\Program Files\\FLOMASTER).',
        'Запустите FLOMASTER.exe. Сборка самодостаточная: .NET 8 входит в комплект, ничего доустанавливать не нужно.',
        'Окно открывается в правом верхнем углу экрана. Пользовательские данные (настройки и журнал) создаются в %APPDATA%\\FLOMASTER\\.',
        'Обновление: закройте лаунчер (в том числе из трея), замените файлы, запустите заново. Настройки и журнал живут отдельно от программы и переживают обновление.',
    ):
        p(doc, b, bullet=True)

    h1(doc, '3. Быстрый старт')
    for b in (
        'Запустите FLOMASTER - автоскан найдёт установленные приложения, карточки появятся в окне.',
        'Нажмите Launch на карточке - приложение стартует с OCIO-конфигом лаунчера.',
        'Проверьте в приложении, что палитра цветов пространств соответствует конфигу (в Blender - вкладка Color Management в свойствах сцены).',
        'Перетащите файл проекта (.blend, .ma, .hip...) на карточку - файл откроется в этом приложении под тем же конфигом.',
        'Настройте под себя: тема и пути сканирования - в Settings, конфиг по умолчанию для трея - там же.',
    ):
        p(doc, b, bullet=True)

    h1(doc, '4. Пресеты и автоскан')
    h2(doc, '4.1 Автоскан')
    p(doc, 'При старте лаунчер ищет установленные приложения в стандартных местах. Если что-то '
           'стоит в нестандартной папке (портативные сборки, свои версии) - добавьте папку в '
           'Settings → Scan paths, и карточка появится после следующего скана.')
    h2(doc, '4.2 Свои пресеты')
    p(doc, 'Пресет - это пара «имя + путь к exe». Два способа создать:')
    for b in (
        'перетащите exe приложения прямо в окно лаунчера - имя спросится;',
        'создайте пресет вручную и укажите путь к exe.',
    ):
        p(doc, b, bullet=True)
    p(doc, 'У каждого пресета свои аргументы запуска (меню Quick Commands на карточке) - так '
           'держат несколько версий одного приложения или особые ключи запуска.')

    h1(doc, '5. Файлы проектов')
    p(doc, 'Перетащите файл проекта на карточку приложения - файл откроется в нём с применённым '
           'OCIO-конфигом. Поддерживаются: .blend, .spp, .ma, .mb, .hip, .hipl, .hipnc, .nk. '
           'Последние открытые файлы доступны в списке Recent. Так же работает пункт контекстного '
           'меню Windows «Open in FLOMASTER» - правый клик по файлу проекта.')

    h1(doc, '6. Как устроен OCIO')
    p(doc, 'Большинство приложений читают путь к конфигу из переменной окружения OCIO - лаунчер '
           'проставляет её при запуске. Unreal Engine переменную не читает: ему путь передаётся '
           'аргументом -ocio="путь" - лаунчер делает это сам.')
    add_table(doc, [
        ('Приложение', 'Способ'),
        ('Blender, Maya, Houdini, Nuke, DaVinci Resolve, Substance Painter', 'переменная окружения OCIO'),
        ('Unreal Engine', 'аргумент -ocio='),
    ], [9.6, 7.4])
    p(doc, 'В комплекте - ACES 1.2 конфиг (папка ocio/ рядом с exe). В нём заведены строчные '
           'алиасы acescg и raw: это interchange-имена ACES, одинаковые во всех приложениях и '
           'конфигах. Скрипты и пресеты, прописывающие colorspace строкой acescg или raw, '
           'переживают смену конфига - длинные имена вида «ACES - ACEScg» уникальны для '
           'конкретного конфига. Трансформы у алиаса и длинного имени идентичны.')
    p(doc, 'Роли по умолчанию в комплектном конфиге настроены под типовой цикл текстур: '
           '8-битные изображения открываются как raw, float-изображения (EXR) - как acescg.')

    h1(doc, '7. Темы')
    p(doc, 'Семь цветовых схем - по одной на приложение из пайплайна. Тема меняется в Settings '
           'и красит весь интерфейс: кнопки, галочки, скроллбар, выпадающие меню и состояние '
           'кнопки Launch при наведении.')
    add_table(doc, [
        ('Тема', 'Акцент', 'База'),
        ('Blender', 'оранжевый', 'тёмный графит'),
        ('Maya', 'циан', 'сине-серый'),
        ('Houdini', 'красно-оранжевый', 'тёмный (снят с реального UI)'),
        ('Nuke', 'светло-серый', 'графит, монохром'),
        ('DaVinci', 'розово-красный', 'фиолетово-синий'),
        ('Unreal', 'синий', 'холодный графит'),
        ('Substance', 'зелёный', 'чёрный'),
    ], [4.6, 5.6, 6.8])

    h1(doc, '8. Настройки')
    add_table(doc, [
        ('Параметр', 'Что делает'),
        ('Theme', 'цветовая тема интерфейса'),
        ('Start with Windows', 'автозапуск свёрнутым в трей вместе с Windows'),
        ('Always on top', 'окно поверх всех'),
        ('Smooth animation', 'включить/выключить анимацию панелей'),
        ('Default OCIO', 'конфиг по умолчанию для запусков из трея'),
        ('Scan paths', 'дополнительные папки для автоскана'),
        ('Shortcuts', 'ярлыки в меню Пуск и на рабочем столе'),
    ], [6.0, 11.0])

    h1(doc, '9. Трей и журнал')
    p(doc, 'Закрытие окна сворачивает лаунчер в системный трей - он продолжает работать. Из '
           'меню трея доступен быстрый запуск приложений под конфигом по умолчанию.')
    p(doc, 'Кнопка Log открывает окно журнала сбоку от лаунчера (той же высоты, левее или '
           'правее - где есть место). В журнале каждый запуск: время, пользователь, приложение, '
           'конфиг OCIO, код возврата. Строка «Set OCIO=...» - маркер того, что конфиг реально '
           'передан. Файл журнала - %APPDATA%\\FLOMASTER\\flomaster.log.')

    h1(doc, '10. Файлы и папки')
    add_table(doc, [
        ('Путь', 'Содержимое'),
        ('папка установки', 'FLOMASTER.exe, flomaster.ico, ocio/ (конфиг ACES 1.2 + luts/), LICENSE.txt'),
        ('%APPDATA%\\FLOMASTER\\', 'launcher_config.json (настройки), flomaster.log (журнал)'),
    ], [5.6, 11.4])
    p(doc, 'Программа и пользовательские данные разделены намеренно: переустановка и обновление '
           'не трогают настройки, пресеты и журнал.')

    h1(doc, '11. Сценарии')
    for t in (
        ('Единый цвет на старте дня',
         'Окно лаунчера → Launch у нужного приложения. Все приложения дня стартуют с одним и тем же '
         'конфигом - передавать файлы между ними можно без цветовых сюрпризов.'),
        ('Текстуры: что откроется и в чём',
         'Float-картинки (EXR) открываются как acescg - цветовой цикл корректен сразу. 8-битные - как '
         'raw: для служебных карт (нормали, маски, roughness) это нужный дефолт без ручных кликов. '
         'Цветовой 8-битной карте назначьте colorspace вручную, если она открылась как raw.'),
        ('Открыть сцену в другом приложении',
         'Перетащите файл сцены на карточку нужного приложения - или воспользуйтесь Recent, чтобы '
         'переоткрыть последний файл, не ища его в проводнике.'),
        ('Несколько версий одного приложения',
         'Создайте по пресету на версию (перетаскивание exe + имя) - и запускайте нужную одним '
         'нажатием; аргументы версии живут в Quick Commands пресета.'),
    ):
        h2(doc, t[0])
        p(doc, t[1])

    h1(doc, '12. Решение проблем')
    add_table(doc, [
        ('Симптом', 'Причина', 'Что делать'),
        ('Приложение не появилось после установки', 'нестандартная папка установки', 'добавьте папку в Settings → Scan paths или создайте пресет перетаскиванием exe'),
        ('Unreal запустился не с тем цветом', 'Unreal не читает переменную OCIO', 'не убирайте аргумент -ocio= в аргументах пресета - лаунчер передаёт конфиг именно им'),
        ('Ошибка «enum "Non-Color" not found» при импорте', 'комплектный конфиг не содержит colorspace с именем Non-Color (есть алиас raw)', 'импортируйте вне этого OCIO-конфига либо добавьте в конфиг colorspace/алиас с таким именем'),
        ('Цветовая 8-битная карта открылась как raw', 'роль default_byte в конфиге = raw (дефолт под служебные карты)', 'назначьте этой карте colorspace вручную: 8-битному цвету - sRGB-пространство, float - acescg'),
        ('Окно журнала не видно', 'открывается сбоку от лаунчера, той же высоты', 'посмотрите левее/правее окна лаунчера'),
        ('Настройки и журнал «пропали» после переустановки', 'они живут не в папке программы', 'ищите в %APPDATA%\\FLOMASTER\\ - обновления их не трогают'),
        ('Не заменяется exe при обновлении', 'лаунчер запущен (в том числе в трее) и файл занят', 'закройте FLOMASTER полностью, затем замените файлы'),
    ], [4.6, 5.4, 7.0])

    _save(doc, OUT_RU)


# ════════════════════════════════════════════════════════════════════════════
# EN
# ════════════════════════════════════════════════════════════════════════════

def build_en():
    ds.H1_REGISTRY.clear()
    doc = ds.new_doc('FLOMASTER', 'User Guide', 'V2.3  -  WINDOWS 10/11')

    p(doc, 'FLOMASTER is a launcher for 3D graphics applications: one button starts Blender, '
           'Maya, Houdini, Nuke, DaVinci Resolve, Unreal Engine or Substance Painter with a '
           'predefined OCIO color config. The launcher finds installed applications '
           'automatically, keeps launch presets, opens project files and logs every launch. '
           'One color config - across all applications, with no manual environment setup.')

    kv_note(doc, 'github.com/abyrvalg379/FLOMASTER')

    h1(doc, 'Contents')
    ds.toc_field(doc, 'Table of contents: open the document in Word/LibreOffice and refresh '
                       'the field (F9) to fill in page numbers.')

    h1(doc, '1. About FLOMASTER')
    p(doc, 'Key features:', bullet=False)
    for b in (
        'auto-scan of installed applications: Blender, K-Cycles, Maya, Houdini, Nuke, DaVinci Resolve, Unreal Engine, Substance Painter;',
        'launch presets: your own name, exe and launch arguments;',
        'project file opening (.blend, .spp, .ma, .mb, .hip, .hipl, .hipnc, .nk) with OCIO applied;',
        '7 color themes styled after the applications they launch;',
        'drag and drop: an exe becomes a preset, a project file opens in the chosen application;',
        'system tray with quick launch under the default config;',
        'launch log: timestamp, user, application, config, exit code;',
        'bundled ACES 1.2 OCIO config with lowercase acescg / raw aliases.',
    ):
        p(doc, b, bullet=True)

    h1(doc, '2. Installation')
    for b in (
        'Download the latest release: github.com/abyrvalg379/FLOMASTER → Releases → Latest.',
        'Extract the archive to a folder of your choice (e.g. C:\\Program Files\\FLOMASTER).',
        'Run FLOMASTER.exe. The build is self-contained: .NET 8 is bundled, nothing else to install.',
        'The window opens in the top-right corner of the screen. User data (settings and the log) is created in %APPDATA%\\FLOMASTER\\.',
        'Updating: close the launcher (including the tray), replace the files, start it again. Settings and the log live apart from the program and survive updates.',
    ):
        p(doc, b, bullet=True)

    h1(doc, '3. Quick start')
    for b in (
        'Start FLOMASTER - the auto-scan finds installed applications and their cards appear in the window.',
        'Click Launch on a card - the application starts with the launcher OCIO config.',
        'Check the color spaces in the application (in Blender - the Color Management tab in scene properties).',
        'Drag a project file (.blend, .ma, .hip...) onto a card - it opens in that application under the same config.',
        'Make it yours: theme and scan paths are in Settings, the default tray config is there too.',
    ):
        p(doc, b, bullet=True)

    h1(doc, '4. Presets and auto-scan')
    h2(doc, '4.1 Auto-scan')
    p(doc, 'On start the launcher looks for installed applications in standard locations. '
           'Something installed in a non-standard folder (portable builds, custom versions)? '
           'Add the folder in Settings → Scan paths and the card appears on the next scan.')
    h2(doc, '4.2 Custom presets')
    p(doc, 'A preset is a pair of "name + path to exe". Two ways to create one:')
    for b in (
        'drag the application exe right into the launcher window - you will be asked for a name;',
        'create a preset manually and point it to the exe.',
    ):
        p(doc, b, bullet=True)
    p(doc, 'Each preset carries its own launch arguments (the Quick Commands menu on the card) - '
           'that is how you keep several versions of one application or special launch keys.')

    h1(doc, '5. Project files')
    p(doc, 'Drag a project file onto an application card - it opens there with the OCIO config '
           'applied. Supported: .blend, .spp, .ma, .mb, .hip, .hipl, .hipnc, .nk. Recently opened '
           'files are available in the Recent list. The Windows context menu entry "Open in '
           'FLOMASTER" (right-click on a project file) works the same way.')

    h1(doc, '6. How OCIO support works')
    p(doc, 'Most applications read the config path from the OCIO environment variable - the '
           'launcher sets it on launch. Unreal Engine ignores the variable: it receives the path '
           'as a -ocio="path" argument - the launcher does that for you.')
    add_table(doc, [
        ('Application', 'Method'),
        ('Blender, Maya, Houdini, Nuke, DaVinci Resolve, Substance Painter', 'OCIO environment variable'),
        ('Unreal Engine', '-ocio= argument'),
    ], [9.6, 7.4])
    p(doc, 'The bundled ACES 1.2 config (the ocio/ folder next to the exe) defines lowercase '
           'aliases acescg and raw: interchange ACES names identical across applications and '
           'configs. Scripts and presets that set a colorspace by the acescg or raw string '
           'survive config changes - long names like "ACES - ACEScg" are unique to one config. '
           'The transforms behind an alias and its long name are identical.')
    p(doc, 'Default roles in the bundled config are tuned for a typical texture cycle: 8-bit '
           'images open as raw, float images (EXR) - as acescg.')

    h1(doc, '7. Themes')
    p(doc, 'Seven color schemes - one per application in the pipeline. Switch the theme in '
           'Settings; the entire interface follows: buttons, checkboxes, the scrollbar, dropdown '
           'menus and the Launch button hover state.')
    add_table(doc, [
        ('Theme', 'Accent', 'Base'),
        ('Blender', 'orange', 'dark graphite'),
        ('Maya', 'cyan', 'blue-gray'),
        ('Houdini', 'red-orange', 'dark (sampled from the real UI)'),
        ('Nuke', 'light gray', 'graphite, monochrome'),
        ('DaVinci', 'pink-red', 'purple-navy'),
        ('Unreal', 'blue', 'cool graphite'),
        ('Substance', 'green', 'black'),
    ], [4.6, 5.6, 6.8])

    h1(doc, '8. Settings')
    add_table(doc, [
        ('Setting', 'What it does'),
        ('Theme', 'interface color theme'),
        ('Start with Windows', 'auto-start minimized to tray with Windows'),
        ('Always on top', 'window above all others'),
        ('Smooth animation', 'enable/disable panel animation'),
        ('Default OCIO', 'default config for tray launches'),
        ('Scan paths', 'extra folders for the auto-scan'),
        ('Shortcuts', 'Start Menu and desktop shortcuts'),
    ], [6.0, 11.0])

    h1(doc, '9. Tray and log')
    p(doc, 'Closing the window minimizes the launcher to the system tray - it keeps running. '
           'The tray menu offers quick launch of applications under the default config.')
    p(doc, 'The Log button opens the log window beside the launcher (same height, to the left '
           'or right - wherever it fits). Every launch is logged: timestamp, user, application, '
           'OCIO config, exit code. A "Set OCIO=..." line marks that the config was actually '
           'passed. The log file lives at %APPDATA%\\FLOMASTER\\flomaster.log.')

    h1(doc, '10. Files and folders')
    add_table(doc, [
        ('Path', 'Contents'),
        ('installation folder', 'FLOMASTER.exe, flomaster.ico, ocio/ (ACES 1.2 config + luts/), LICENSE.txt'),
        ('%APPDATA%\\FLOMASTER\\', 'launcher_config.json (settings), flomaster.log (log)'),
    ], [5.6, 11.4])
    p(doc, 'The program and user data are deliberately separated: reinstalling and updating '
           'never touch settings, presets or the log.')

    h1(doc, '11. Workflows')
    for t in (
        ('One color from the start of the day',
         'Launcher window → Launch on the application you need. Everything you start that day '
         'runs on the same config - files travel between applications without color surprises.'),
        ('Textures: what opens and where',
         'Float images (EXR) open as acescg - the color cycle is correct right away. 8-bit images '
         'open as raw: the right default for data maps (normals, masks, roughness) with no manual '
         'clicks. Assign a colorspace manually to a color 8-bit map if it opened as raw.'),
        ('Open a scene in another application',
         'Drag the scene file onto the application card - or use Recent to reopen the last file '
         'without hunting for it in the file manager.'),
        ('Several versions of one application',
         'Create one preset per version (drag the exe + name) and start the one you need with a '
         'click; the version arguments live in the preset Quick Commands.'),
    ):
        h2(doc, t[0])
        p(doc, t[1])

    h1(doc, '12. Troubleshooting')
    add_table(doc, [
        ('Symptom', 'Cause', 'What to do'),
        ('Application missing after installation', 'non-standard install folder', 'add the folder in Settings → Scan paths or create a preset by dragging the exe'),
        ('Unreal started with the wrong color', 'Unreal does not read the OCIO variable', 'keep the -ocio= argument in the preset arguments - that is how the launcher passes the config'),
        ('Error «enum "Non-Color" not found» on import', 'the bundled config has no colorspace named Non-Color (the raw alias exists)', 'import outside this OCIO config, or add a colorspace/alias with that name to the config'),
        ('A color 8-bit map opened as raw', 'the default_byte role in the config is raw (the default for data maps)', 'assign a colorspace manually to that map: sRGB space for 8-bit color, acescg for float'),
        ('Log window is nowhere to be seen', 'it opens beside the launcher, at the same height', 'look to the left/right of the launcher window'),
        ('Settings and log "disappeared" after reinstall', 'they do not live in the program folder', 'look in %APPDATA%\\FLOMASTER\\ - updates never touch them'),
        ('Cannot replace the exe when updating', 'the launcher is running (including the tray) and the file is locked', 'close FLOMASTER completely, then replace the files'),
    ], [4.6, 5.4, 7.0])

    _save(doc, OUT_EN)


if __name__ == '__main__':
    build_ru()
    build_en()
