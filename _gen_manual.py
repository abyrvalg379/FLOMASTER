# -*- coding: utf-8 -*-
r"""FLOMASTER - Руководство пользователя (RU + EN). Генератор DOCX. Версия V2.4.

Стиль - общий модуль _docstyle.py (язык обложек семьи), акцент FLOMASTER #E87D0D.
Запуск:  python _gen_manual.py
Затем:   python finish_toc.py FLOMASTER_Manual_RU   (и то же для _EN)
Выход:   work\docs\FLOMASTER_Manual_RU.docx/_EN.docx (+ .pdf после finish_toc)
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
    return ds.p(doc, text, bullet=bullet, italic=italic, grey=False) if False else ds.p(doc, text, bullet=bullet, italic=italic, grey=grey)


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
    doc = ds.new_doc('FLOMASTER', 'Руководство пользователя', 'V2.4  -  WINDOWS 10/11')

    p(doc, 'FLOMASTER - лаунчер для рабочих приложений трёхмерной графики: одна кнопка '
           'запускает Blender, Maya, Houdini, Nuke, DaVinci Resolve, Unreal Engine или '
           'Substance Painter с заранее заданным OCIO-конфигом цвета. Лаунчер сам находит '
           'установленные приложения, ведёт пресеты и профили запуска, открывает файлы '
           'проектов, работает из командной строки и ведёт журнал каждого запуска. Один и '
           'тот же цветовой конфиг - во всех приложениях, без ручных переменных окружения.')

    kv_note(doc, 'github.com/abyrvalg379/FLOMASTER')

    h1(doc, 'Содержание')
    ds.toc_field(doc, 'Оглавление: откройте документ в Word/LibreOffice и обновите поле (F9), '
                       'чтобы заполнить номера страниц.')

    h1(doc, '1. О программе')
    p(doc, 'Ключевые возможности:', bullet=False)
    for b in (
        'автоскан установленных приложений: реестр Windows, манифесты Epic Games и файловый поиск — Blender, K-Cycles, Maya, Houdini, Nuke, DaVinci Resolve, Unreal Engine, Substance Painter;',
        'пресеты запуска: своё имя, свой exe, свои аргументы запуска;',
        'профили запуска: именованные связки «приложение + OCIO-конфиг + аргументы», переключение проектов одним нажатием;',
        'панель Projects: браузер файлов проектов по вашим папкам, открытие прямо в лаунчере;',
        'роли OCIO: переопределение цветовых ролей на уровне пресета без правки конфига;',
        'командная строка для батников и ферм;',
        'перенос настроек между машинами файлом;',
        '7 цветовых тем, включая шапку окна, и глобальный хоткей Ctrl+Alt+F;',
        'системный трей с быстрым запуском пресетов и профилей;',
        'журнал запусков и автообновление с GitHub.',
    ):
        p(doc, b, bullet=True)

    h1(doc, '2. Установка и обновление')
    for b in (
        'Скачайте последний релиз: github.com/abyrvalg379/FLOMASTER → Releases → Latest.',
        'Распакуйте архив в удобную папку (например, C:\\Program Files\\FLOMASTER).',
        'Запустите FLOMASTER.exe. Сборка самодостаточная: .NET 8 входит в комплект, ничего доустанавливать не нужно.',
        'Окно открывается в правом верхнем углу экрана. Пользовательские данные (настройки и журнал) создаются в %APPDATA%\\FLOMASTER\\.',
        'Автообновление: при старте лаунчер проверяет GitHub на новую версию, скачивает её в фоне и показывает в Settings кнопку «Restart and install» — замена происходит в один клик (один запрос UAC). Проверку можно отключить галкой Check for updates on start.',
        'Ручное обновление: закройте лаунчер (в том числе из трея), замените файлы, запустите заново. Настройки и журнал живут отдельно от программы и переживают обновление.',
    ):
        p(doc, b, bullet=True)

    h1(doc, '3. Быстрый старт')
    for b in (
        'Запустите FLOMASTER — автоскан найдёт установленные приложения.',
        'Выберите приложение в списке APPLICATION и нажмите Launch — оно стартует с OCIO-конфигом.',
        'Проверьте в приложении палитру цветовых пространств (в Blender — Color Management в свойствах сцены).',
        'Раскройте панель Projects, добавьте папку с проектами кнопкой «+» и откройте файл кликом — он запустится в выбранном приложении.',
        'Нажмите Save current as profile в панели Profiles — текущая связка «приложение + конфиг + аргументы» сохранена под именем.',
        'Ctrl+Alt+F прячет и возвращает лаунчер поверх любого приложения.',
    ):
        p(doc, b, bullet=True)

    h1(doc, '4. Пресеты и автоскан')
    h2(doc, '4.1 Автоскан')
    p(doc, 'При старте лаунчер ищет установленные приложения: сначала в реестре Windows и в '
           'манифестах Epic Games Launcher, затем файловым поиском в стандартных папках. '
           'Нестандартная установка (портативная сборка, своя версия) — добавьте папку в '
           'Settings → Scan paths, и карточка появится после следующего скана.')
    h2(doc, '4.2 Свои пресеты')
    p(doc, 'Пресет - это пара «имя + путь к exe». Два способа создать:')
    for b in (
        'перетащите exe приложения прямо в окно лаунчера - имя спросится;',
        'создайте пресет вручную и укажите путь к exe.',
    ):
        p(doc, b, bullet=True)
    p(doc, 'У каждого пресета свои аргументы запуска (панель Quick commands) - так держат '
           'несколько версий одного приложения или особые ключи запуска.')

    h1(doc, '5. Профили запуска')
    p(doc, 'Профиль - именованный слепок состояния запуска: приложение + OCIO-конфиг + '
           'аргументы. Это единица переключения между задачами: «проект А» и «проект Б» отличаются '
           'одним нажатием, а не перенастройкой трёх полей.')
    for b in (
        'настройте приложение, OCIO-конфиг и аргументы, раскройте панель Profiles и нажмите Save current as profile;',
        'клик по профилю возвращает всю связку целиком (профили ссылаются на пресет и конфиг по имени — их правки подхватываются);',
        'крестик удаляет профиль (с подтверждением);',
        'в меню трея есть секция PROFILES — запуск профиля прямо из трея, без окна.',
    ):
        p(doc, b, bullet=True)
    p(doc, 'Типовой набор: по профилю на проект (разные OCIO-конфиги), на версию приложения, на '
           'фоновый рендер (аргументы запекания). Профили переезжают между машинами вместе с '
           'настройками — см. раздел «Перенос на другую машину».')

    h1(doc, '6. Панель Projects')
    p(doc, 'Панель Projects — браузер файлов проектов внутри лаунчера: вместо копания в '
           'проводнике вы открываете нужный файл одним кликом.')
    for b in (
        'кнопкой «+» добавьте папку, где хранятся проекты (корень); «−» удаляет выбранный корень;',
        'раскройте панель Projects: список показывает все проектные файлы корня рекурсивно, с относительными путями;',
        'поле поиска фильтрует список по имени файла;',
        'клик по файлу открывает его текущим приложением с OCIO-конфигом и добавляет в Recent.',
    ):
        p(doc, b, bullet=True)
    p(doc, 'В списке только родные форматы DCC: .blend, .spp, .ma, .mb, .hip, .hipl, .hipnc, '
           '.nk. Экспортные форматы (.fbx, .obj, .stl) сознательно не показываются — это '
           'выпечка, а не проекты.')

    h1(doc, '7. Файлы проектов')
    p(doc, 'Перетащите файл проекта на окно лаунчера — файл откроется в выбранном приложении с '
           'применённым OCIO-конфигом. Те же файлы доступны в списке Recent — клик переоткрывает '
           'их без проводника. Поддерживаются: .blend, .spp, .ma, .mb, .hip, .hipl, .hipnc, .nk.')

    h1(doc, '8. Роли OCIO')
    p(doc, 'Панель Roles показывает, во что превращаются ключевые цветовые роли выбранного '
           'конфига (scene_linear, data, default_byte и другие) — и позволяет переопределить их '
           'на уровень пресета.')
    for b in (
        'клик по строке роли открывает пикер: colorspaces сгруппированы по family, как меню Color Space в Blender, есть поиск;',
        'переопределения хранятся в конфиге лаунчера, а при запуске собирается вариант конфига — оригинальный .ocio не изменяется никогда;',
        'Reset возвращает все роли пресета к значениям конфига;',
        'под селектором конфига — строка валидации: отсутствующие критичные роли и битые ссылки подсвечиваются заранее.',
    ):
        p(doc, b, bullet=True)

    h1(doc, '9. Как устроен OCIO')
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

    h1(doc, '10. Темы и шапка окна')
    p(doc, 'Семь цветовых схем - по одной на приложение из пайплайна. Тема меняется в Settings '
           'и красит весь интерфейс: кнопки, галочки, скроллбар, выпадающие меню, полосу '
           'заголовка окна и состояние кнопки Launch при наведении.')
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
    p(doc, 'Полоса заголовка окна — собственная, в цвете темы: слева имя приложения, справа '
           'кнопки свернуть и закрыть. Перетаскивание окна — за полосу заголовка.')

    h1(doc, '11. Настройки')
    add_table(doc, [
        ('Параметр', 'Что делает'),
        ('Check for updates on start', 'проверка автообновления при старте'),
        ('Theme', 'цветовая тема интерфейса'),
        ('Start with Windows', 'автозапуск свёрнутым в трей вместе с Windows'),
        ('Always on top', 'окно поверх всех'),
        ('Smooth animation', 'включить/выключить анимацию панелей'),
        ('Global hotkey (Ctrl+Alt+F)', 'глобальный хоткей: показать/спрятать лаунчер поверх всего'),
        ('Default OCIO', 'конфиг по умолчанию для запусков из трея'),
        ('Scan paths', 'дополнительные папки для автоскана'),
        ('SYNC', 'экспорт/импорт настроек для переноса на другую машину'),
        ('Shortcuts', 'ярлыки в меню Пуск и на рабочем столе'),
    ], [6.0, 11.0])

    h1(doc, '12. Трей и журнал')
    p(doc, 'Закрытие окна сворачивает лаунчер в системный трей - он продолжает работать. Меню '
           'трея: быстрый запуск пресетов, секция PROFILES (запуск профилей одной кнопкой) и '
           'показ окна.')
    p(doc, 'Кнопка Log открывает окно журнала сбоку от лаунчера (той же высоты, левее или '
           'правее - где есть место). В журнале каждый запуск: время, пользователь, приложение, '
           'конфиг OCIO, код возврата. Строка «Set OCIO=...» - маркер того, что конфиг реально '
           'передан. Файл журнала - %APPDATA%\\FLOMASTER\\flomaster.log.')

    h1(doc, '13. Командная строка')
    p(doc, 'Лаунчер работает без окна - для батников, ферм и скриптов:')
    p(doc, 'FLOMASTER.exe --list-profiles', bullet=False, italic=True)
    p(doc, 'FLOMASTER.exe --launch "Профиль"', bullet=False, italic=True)
    p(doc, 'FLOMASTER.exe --launch "Профиль" --project "D:\\projects\\A\\shot_042.blend"', bullet=False, italic=True)
    p(doc, 'Профиль разворачивается в приложение + OCIO-конфиг + аргументы - та же среда, что и '
           'ручной запуск. --project добавляет файл к аргументам профиля (или подставляется '
           'вместо плейсхолдера {file}, если он есть в аргументах профиля).')
    add_table(doc, [
        ('Код возврата', 'Значение'),
        ('0', 'запущено успешно'),
        ('1', 'ошибка запуска или использования'),
        ('2', 'не найден профиль, пресет или файл проекта'),
    ], [4.6, 12.4])
    p(doc, 'Вывод идёт в консоль, из которой запущен exe. Переменные окружения и OCIO передаются '
           'точно так же, как при запуске из окна.')

    h1(doc, '14. Перенос на другую машину')
    p(doc, 'Настройки переносятся файлом: Settings → SYNC → Export settings сохраняет профили, '
           'корни проектов, тему и опции в файл flomaster_setup_дата.flomaster. На другой машине: '
           'установите лаунчер, нажмите Import settings и выберите этот файл.')
    for b in (
        'профили импортируются по имени: новые добавляются, существующие обновляются;',
        'корни проектов сливаются без дублей;',
        'пути к exe остаются машинными: пресеты матчатся по имени, поэтому после импорта проверьте, что приложения найдены (Rescan);',
    ):
        p(doc, b, bullet=True)
    p(doc, 'Автоскан на новой машине подхватит установленные приложения из реестра и манифестов '
           'Epic — как правило, пресеты собираются сами.')

    h1(doc, '15. Файлы и папки')
    add_table(doc, [
        ('Путь', 'Содержимое'),
        ('папка установки', 'FLOMASTER.exe, flomaster.ico, ocio/ (конфиг ACES 1.2 + luts/), LICENSE.txt'),
        ('%APPDATA%\\FLOMASTER\\', 'launcher_config.json (настройки), flomaster.log (журнал), variants/ (варианты конфига под роли)'),
    ], [5.6, 11.4])
    p(doc, 'Программа и пользовательские данные разделены намеренно: переустановка и обновление '
           'не трогают настройки, пресеты и журнал.')

    h1(doc, '16. Сценарии')
    for t in (
        ('Единый цвет на старте дня',
         'Окно лаунчера → Launch у нужного приложения. Все приложения дня стартуют с одним и тем же '
         'конфигом - передавать файлы между ними можно без цветовых сюрпризов.'),
        ('Переключение между проектами',
         'Настройте профили по одному на проект и переключайте их в панели Profiles одним нажатием. '
         'Из трея профиль запускается сразу, без окна лаунчера.'),
        ('Открыть файл проекта не выходя из задачи',
         'Панель Projects → корень → клик по файлу. Поиск по имени находит файл в глубине папок.'),
        ('Текстуры: что откроется и в чём',
         'Float-картинки (EXR) открываются как acescg - цветовой цикл корректен сразу. 8-битные - как '
         'raw: для служебных карт (нормали, маски, roughness) это нужный дефолт без ручных кликов. '
         'Цветовой 8-битной карте назначьте colorspace вручную, если она открылась как raw.'),
        ('Фоновый рендер из батника',
         'Сохраните профиль с аргументами запекания и запускайте: FLOMASTER.exe --launch "Рендер" '
         '--project "сцена.blend". Код возврата 0 — процесс ушёл.'),
        ('Несколько версий одного приложения',
         'Создайте по пресету на версию (перетаскивание exe + имя) - и запускайте нужную одним '
         'нажатием; аргументы версии живут в Quick Commands пресета.'),
    ):
        h2(doc, t[0])
        p(doc, t[1])

    h1(doc, '17. Решение проблем')
    add_table(doc, [
        ('Симптом', 'Причина', 'Что делать'),
        ('Приложение не появилось после установки', 'нестандартная папка установки', 'добавьте папку в Settings → Scan paths или создайте пресет перетаскиванием exe'),
        ('Ctrl+Alt+F ничего не делает', 'комбинация занята другим приложением', 'смотрите warn в журнале; освободите комбинацию или отключите/включите галку Global hotkey заново'),
        ('Профиль не запускается из командной строки, код 2', 'профиль/пресет/файл не найдены', 'проверьте имя профиля через --list-profiles; пресеты матчатся по имени'),
        ('Файл проекта не виден в панели Projects', 'показываются только родные форматы DCC (.blend, .spp, .ma, .mb, .hip, .hipl, .hipnc, .nk)', 'используйте поиск по имени или перетащите файл в окно напрямую'),
        ('Unreal запустился не с тем цветом', 'Unreal не читает переменную OCIO', 'не убирайте аргумент -ocio= в аргументах пресета - лаунчер передаёт конфиг именно им'),
        ('Ошибка «enum "Non-Color" not found» при импорте', 'комплектный конфиг не содержит colorspace с именем Non-Color (есть алиас raw)', 'импортируйте вне этого OCIO-конфига либо добавьте в конфиг colorspace/алиас с таким именем'),
        ('Цветовая 8-битная карта открылась как raw', 'роль default_byte в конфиге = raw (дефолт под служебные карты)', 'назначьте этой карте colorspace вручную: 8-битному цвету - sRGB-пространство, float - acescg'),
        ('Окно журнала не видно', 'открывается сбоку от лаунчера, той же высоты', 'посмотрите левее/правее окна лаунчера'),
        ('Настройки и журнал «пропали» после переустановки', 'они живут не в папке программы', 'ищите в %APPDATA%\\FLOMASTER\\ - обновления их не трогают'),
        ('После переноса настроек пресеты указывают не туда', 'пути exe машинно-специфичны и не переносятся', 'выполните Rescan на новой машине — пресеты найдутся в реестре'),
    ], [4.6, 5.4, 7.0])

    _save(doc, OUT_RU)


# ════════════════════════════════════════════════════════════════════════════
# EN
# ════════════════════════════════════════════════════════════════════════════

def build_en():
    ds.H1_REGISTRY.clear()
    doc = ds.new_doc('FLOMASTER', 'User Guide', 'V2.4  -  WINDOWS 10/11')

    p(doc, 'FLOMASTER is a launcher for 3D graphics applications: one button starts Blender, '
           'Maya, Houdini, Nuke, DaVinci Resolve, Unreal Engine or Substance Painter with a '
           'predefined OCIO color config. The launcher finds installed applications '
           'automatically, keeps launch presets and profiles, opens project files, runs from '
           'the command line and logs every launch. One color config - across all applications, '
           'with no manual environment setup.')

    kv_note(doc, 'github.com/abyrvalg379/FLOMASTER')

    h1(doc, 'Contents')
    ds.toc_field(doc, 'Table of contents: open the document in Word/LibreOffice and refresh '
                       'the field (F9) to fill in page numbers.')

    h1(doc, '1. About FLOMASTER')
    p(doc, 'Key features:', bullet=False)
    for b in (
        'auto-scan of installed applications from the Windows registry, Epic Games manifests and file search — Blender, K-Cycles, Maya, Houdini, Nuke, DaVinci Resolve, Unreal Engine, Substance Painter;',
        'launch presets: your own name, exe and launch arguments;',
        'launch profiles: named app + OCIO config + arguments bundles, one-click project switching;',
        'Projects panel: a project file browser over your own folders, opening files right in the launcher;',
        'OCIO roles: per-preset role overrides without touching the config;',
        'a command line for batch files and farms;',
        'settings transfer between machines via a file;',
        '7 color themes including the window title bar, and a global Ctrl+Alt+F hotkey;',
        'system tray with quick launch of presets and profiles;',
        'launch log and auto-update from GitHub.',
    ):
        p(doc, b, bullet=True)

    h1(doc, '2. Installation and updates')
    for b in (
        'Download the latest release: github.com/abyrvalg379/FLOMASTER → Releases → Latest.',
        'Extract the archive to a folder of your choice (e.g. C:\\Program Files\\FLOMASTER).',
        'Run FLOMASTER.exe. The build is self-contained: .NET 8 is bundled, nothing else to install.',
        'The window opens in the top-right corner of the screen. User data (settings and the log) is created in %APPDATA%\\FLOMASTER\\.',
        'Auto-update: on start the launcher checks GitHub for a new version, downloads it in the background and shows a "Restart and install" button in Settings — one click, one UAC prompt. Disable it with the Check for updates on start checkbox.',
        'Manual update: close the launcher (including the tray), replace the files, start it again. Settings and the log live apart from the program and survive updates.',
    ):
        p(doc, b, bullet=True)

    h1(doc, '3. Quick start')
    for b in (
        'Start FLOMASTER — the auto-scan finds installed applications.',
        'Pick an application in the APPLICATION list and click Launch — it starts with the OCIO config.',
        'Check the color spaces in the application (in Blender — the Color Management tab in scene properties).',
        'Expand the Projects panel, add a project folder with the "+" button and open a file with a click — it starts in the selected application.',
        'Press Save current as profile in the Profiles panel — the current app + config + arguments bundle is saved under a name.',
        'Ctrl+Alt+F hides and restores the launcher above any application.',
    ):
        p(doc, b, bullet=True)

    h1(doc, '4. Presets and auto-scan')
    h2(doc, '4.1 Auto-scan')
    p(doc, 'On start the launcher looks for installed applications: first in the Windows registry '
           'and Epic Games Launcher manifests, then by file search in standard locations. '
           'Something installed in a non-standard folder (portable builds, custom versions)? '
           'Add the folder in Settings → Scan paths and the card appears on the next scan.')
    h2(doc, '4.2 Custom presets')
    p(doc, 'A preset is a pair of "name + path to exe". Two ways to create one:')
    for b in (
        'drag the application exe right into the launcher window - you will be asked for a name;',
        'create a preset manually and point it to the exe.',
    ):
        p(doc, b, bullet=True)
    p(doc, 'Each preset carries its own launch arguments (the Quick commands panel) - '
           'that is how you keep several versions of one application or special launch keys.')

    h1(doc, '5. Launch profiles')
    p(doc, 'A profile is a named snapshot of a launch state: application + OCIO config + '
           'arguments. It is the unit of task switching: "Project A" and "Project B" differ by one '
           'click instead of re-tuning three fields.')
    for b in (
        'set the application, OCIO config and arguments, expand the Profiles panel and press Save current as profile;',
        'clicking a profile restores the whole bundle (profiles reference the preset and config by name — edits are picked up);',
        'the cross deletes a profile (with confirmation);',
        'the tray menu has a PROFILES section — launch a profile straight from the tray, no window needed.',
    ):
        p(doc, b, bullet=True)
    p(doc, 'A typical set: one profile per project (different OCIO configs), one per application '
           'version, one for background rendering (bake arguments). Profiles travel between '
           'machines with your settings — see "Moving to another machine".')

    h1(doc, '6. Projects panel')
    p(doc, 'The Projects panel is a project file browser inside the launcher: instead of digging '
           'through the file manager you open the file you need with one click.')
    for b in (
        'add the folder where your projects live with the "+" button (a root); "−" removes the selected root;',
        'expand the Projects panel: the list shows every project file under the root recursively, with relative paths;',
        'the search field filters the list by file name;',
        'clicking a file opens it with the current application, OCIO config applied, and adds it to Recent.',
    ):
        p(doc, b, bullet=True)
    p(doc, 'Only native DCC formats are listed: .blend, .spp, .ma, .mb, .hip, .hipl, .hipnc, '
           '.nk. Export formats (.fbx, .obj, .stl) are deliberately hidden — those are exports, '
           'not projects.')

    h1(doc, '7. Project files')
    p(doc, 'Drag a project file onto the launcher window — it opens in the selected application '
           'with the OCIO config applied. The same files are available in the Recent list — a '
           'click reopens them without the file manager. Supported: .blend, .spp, .ma, .mb, '
           '.hip, .hipl, .hipnc, .nk.')

    h1(doc, '8. OCIO roles')
    p(doc, 'The Roles panel shows what the key color roles of the selected config resolve to '
           '(scene_linear, data, default_byte and others) — and lets you override them per preset.')
    for b in (
        'clicking a role row opens the picker: colorspaces grouped by family, like the Color Space menu in Blender, with search;',
        'overrides are stored in the launcher config, and a derived config is built at launch — the original .ocio file is never modified;',
        'Reset returns all roles of the preset to the config values;',
        'under the config selector a validation line highlights missing critical roles and broken references in advance.',
    ):
        p(doc, b, bullet=True)

    h1(doc, '9. How OCIO support works')
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

    h1(doc, '10. Themes and the window chrome')
    p(doc, 'Seven color schemes - one per application in the pipeline. Switch the theme in '
           'Settings; the entire interface follows: buttons, checkboxes, the scrollbar, dropdown '
           'menus, the window title strip and the Launch button hover state.')
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
    p(doc, 'The title strip is drawn by the launcher itself, in the theme colors: the application '
           'name on the left, minimize and close buttons on the right. Drag the window by the strip.')

    h1(doc, '11. Settings')
    add_table(doc, [
        ('Setting', 'What it does'),
        ('Check for updates on start', 'auto-update check at launch'),
        ('Theme', 'interface color theme'),
        ('Start with Windows', 'auto-start minimized to tray with Windows'),
        ('Always on top', 'window above all others'),
        ('Smooth animation', 'enable/disable panel animation'),
        ('Global hotkey (Ctrl+Alt+F)', 'global hotkey: show/hide the launcher above everything'),
        ('Default OCIO', 'default config for tray launches'),
        ('Scan paths', 'extra folders for the auto-scan'),
        ('SYNC', 'export/import settings to move them to another machine'),
        ('Shortcuts', 'Start Menu and desktop shortcuts'),
    ], [6.0, 11.0])

    h1(doc, '12. Tray and log')
    p(doc, 'Closing the window minimizes the launcher to the system tray - it keeps running. The '
           'tray menu offers quick launch of presets, a PROFILES section (one-click profile '
           'launches) and the window itself.')
    p(doc, 'The Log button opens the log window beside the launcher (same height, to the left '
           'or right - wherever it fits). Every launch is logged: timestamp, user, application, '
           'OCIO config, exit code. A "Set OCIO=..." line marks that the config was actually '
           'passed. The log file lives at %APPDATA%\\FLOMASTER\\flomaster.log.')

    h1(doc, '13. Command line')
    p(doc, 'The launcher runs without a window - for batch files, farms and scripts:')
    p(doc, 'FLOMASTER.exe --list-profiles', bullet=False, italic=True)
    p(doc, 'FLOMASTER.exe --launch "Profile"', bullet=False, italic=True)
    p(doc, 'FLOMASTER.exe --launch "Profile" --project "D:\\projects\\A\\shot_042.blend"', bullet=False, italic=True)
    p(doc, 'A profile resolves to application + OCIO config + arguments - the same environment as '
           'a manual launch. --project appends the file to the profile arguments (or substitutes '
           'the {file} placeholder if the profile has one).')
    add_table(doc, [
        ('Exit code', 'Meaning'),
        ('0', 'launched successfully'),
        ('1', 'launch or usage error'),
        ('2', 'profile, preset or project file not found'),
    ], [4.6, 12.4])
    p(doc, 'Output goes to the console the exe was started from. Environment variables and OCIO '
           'are passed exactly like a window launch.')

    h1(doc, '14. Moving to another machine')
    p(doc, 'Settings travel as a file: Settings → SYNC → Export settings saves profiles, project '
           'roots, theme and options into a flomaster_setup_date.flomaster file. On another '
           'machine: install the launcher, press Import settings and pick the file.')
    for b in (
        'profiles are imported by name: new ones are added, existing ones updated;',
        'project roots merge without duplicates;',
        'exe paths stay machine-local: presets match by name, so after importing check that applications are found (Rescan);',
    ):
        p(doc, b, bullet=True)
    p(doc, 'The auto-scan on a fresh machine picks up installed applications from the registry '
           'and Epic manifests — as a rule, presets assemble themselves.')

    h1(doc, '15. Files and folders')
    add_table(doc, [
        ('Path', 'Contents'),
        ('installation folder', 'FLOMASTER.exe, flomaster.ico, ocio/ (ACES 1.2 config + luts/), LICENSE.txt'),
        ('%APPDATA%\\FLOMASTER\\', 'launcher_config.json (settings), flomaster.log (log), variants/ (per-role config variants)'),
    ], [5.6, 11.4])
    p(doc, 'The program and user data are deliberately separated: reinstalling and updating '
           'never touch settings, presets or the log.')

    h1(doc, '16. Workflows')
    for t in (
        ('One color from the start of the day',
         'Launcher window → Launch on the application you need. Everything you start that day '
         'runs on the same config - files travel between applications without color surprises.'),
        ('Switching between projects',
         'Set up one profile per project and switch them in the Profiles panel with a single click. '
         'From the tray a profile launches right away, without the launcher window.'),
        ('Open a project file without leaving the task',
         'Projects panel → root → click the file. Name search finds files deep inside folders.'),
        ('Textures: what opens and where',
         'Float images (EXR) open as acescg - the color cycle is correct right away. 8-bit images '
         'open as raw: the right default for data maps (normals, masks, roughness) with no manual '
         'clicks. Assign a colorspace manually to a color 8-bit map if it opened as raw.'),
        ('Background render from a batch file',
         'Save a profile with bake arguments and run: FLOMASTER.exe --launch "Render" '
         '--project "scene.blend". Exit code 0 — the process is on its way.'),
        ('Several versions of one application',
         'Create one preset per version (drag the exe + name) and start the one you need with a '
         'click; the version arguments live in the preset Quick commands.'),
    ):
        h2(doc, t[0])
        p(doc, t[1])

    h1(doc, '17. Troubleshooting')
    add_table(doc, [
        ('Symptom', 'Cause', 'What to do'),
        ('Application missing after installation', 'non-standard install folder', 'add the folder in Settings → Scan paths or create a preset by dragging the exe'),
        ('Ctrl+Alt+F does nothing', 'the combination is taken by another application', 'check for a warn line in the log; free the combination or toggle the Global hotkey checkbox off and on'),
        ('Profile does not start from the command line, exit code 2', 'profile/preset/file not found', 'check the profile name with --list-profiles; presets match by name'),
        ('Project file is not visible in the Projects panel', 'only native DCC formats are listed (.blend, .spp, .ma, .mb, .hip, .hipl, .hipnc, .nk)', 'use name search or drag the file into the window directly'),
        ('Unreal started with the wrong color', 'Unreal does not read the OCIO variable', 'keep the -ocio= argument in the preset arguments - that is how the launcher passes the config'),
        ('Error «enum "Non-Color" not found» on import', 'the bundled config has no colorspace named Non-Color (the raw alias exists)', 'import outside this OCIO config, or add a colorspace/alias with that name to the config'),
        ('A color 8-bit map opened as raw', 'the default_byte role in the config is raw (the default for data maps)', 'assign a colorspace manually to that map: sRGB space for 8-bit color, acescg for float'),
        ('Log window is nowhere to be seen', 'it opens beside the launcher, at the same height', 'look to the left/right of the launcher window'),
        ('Settings and log "disappeared" after reinstall', 'they do not live in the program folder', 'look in %APPDATA%\\FLOMASTER\\ - updates never touch them'),
        ('After moving settings the presets point to wrong places', 'exe paths are machine-specific and are not exported', 'run Rescan on the new machine — presets will be found in the registry'),
    ], [4.6, 5.4, 7.0])

    _save(doc, OUT_EN)


if __name__ == '__main__':
    build_ru()
    build_en()
