# -*- coding: utf-8 -*-
r"""FLOMASTER - Руководство пользователя (RU + EN). Генератор DOCX.

Хелперы стиля наследуют _gen_manual_ru.py семейства STUKACH/LAMPOCHKA/TOCHKA,
акцент - фирменный оранжевый FLOMASTER #E87D0D.
Запуск:  python _gen_manual.py
Выход:   work\docs\FLOMASTER_Manual_RU.docx + FLOMASTER_Manual_EN.docx
"""

from docx import Document
from docx.shared import Pt, RGBColor, Cm, Inches
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_ALIGN_VERTICAL
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

OUT_RU = r'D:\AI\ZCode\Project\FLOMASTER\work\docs\FLOMASTER_Manual_RU.docx'
OUT_EN = r'D:\AI\ZCode\Project\FLOMASTER\work\docs\FLOMASTER_Manual_EN.docx'

# ── фирменные цвета FLOMASTER ─────────────────────────────────────────────

ACCENT      = RGBColor(0xE8, 0x7D, 0x0D)   # оранжевый
ACCENT_SOFT = RGBColor(0xF2, 0xA2, 0x4B)   # светлее для H2
HDR_BG      = 'E87D0D'
ALT_ROW     = 'FDF4EA'
GREY        = RGBColor(0x55, 0x55, 0x55)


def _new_doc():
    doc = Document()
    for section in doc.sections:
        section.top_margin    = Inches(0.9)
        section.bottom_margin = Inches(0.9)
        section.left_margin   = Inches(1)
        section.right_margin  = Inches(1)

    normal = doc.styles['Normal']
    normal.font.name = 'Arial'
    normal.font.size = Pt(10)
    normal.paragraph_format.line_spacing = 1.3
    normal.paragraph_format.space_after = Pt(4)
    normal.paragraph_format.space_before = Pt(0)
    rpr = normal.element.get_or_add_rPr()
    rfonts = rpr.find(qn('w:rFonts'))
    rfonts.set(qn('w:cs'), 'Arial')

    for lvl, size in (('Heading 1', 15), ('Heading 2', 12.5)):
        st = doc.styles[lvl]
        st.font.name = 'Arial'
        st.font.size = Pt(size)
        st.font.bold = True
        st.font.color.rgb = ACCENT if lvl == 'Heading 1' else ACCENT_SOFT
        st.paragraph_format.space_before = Pt(14 if lvl == 'Heading 1' else 10)
        st.paragraph_format.space_after = Pt(5)
        st.paragraph_format.line_spacing = 1.15
        st.paragraph_format.keep_with_next = True
    return doc


# ── хелперы ────────────────────────────────────────────────────────────────

def set_cell_bg(cell, hex_color):
    tcPr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement('w:shd')
    shd.set(qn('w:val'), 'clear')
    shd.set(qn('w:color'), 'auto')
    shd.set(qn('w:fill'), hex_color)
    tcPr.append(shd)


def set_cell_borders(cell, color='CCCCCC'):
    tcPr = cell._tc.get_or_add_tcPr()
    tcB = OxmlElement('w:tcBorders')
    for side in ('top', 'left', 'bottom', 'right'):
        b = OxmlElement(f'w:{side}')
        b.set(qn('w:val'), 'single')
        b.set(qn('w:sz'), '4')
        b.set(qn('w:space'), '0')
        b.set(qn('w:color'), color)
        tcB.append(b)
    tcPr.append(tcB)


def cell_para(cell, text, bold=False, size=9, color=None):
    p_ = cell.paragraphs[0]
    p_.paragraph_format.space_before = Pt(2)
    p_.paragraph_format.space_after = Pt(2)
    p_.paragraph_format.line_spacing = 1.1
    run = p_.add_run(text)
    run.font.name = 'Arial'
    run.font.size = Pt(size)
    run.font.bold = bold
    if color:
        run.font.color.rgb = RGBColor(*color)


def add_table(doc, rows, col_widths_cm):
    table = doc.add_table(rows=0, cols=len(col_widths_cm))
    table.style = 'Table Grid'
    table.autofit = False
    tblPr = table._tbl.tblPr
    mar = OxmlElement('w:tblCellMar')
    for side, val in (('top', 40), ('left', 80), ('bottom', 40), ('right', 80)):
        el = OxmlElement(f'w:{side}')
        el.set(qn('w:w'), str(val))
        el.set(qn('w:type'), 'dxa')
        mar.append(el)
    tblPr.append(mar)

    for r_idx, row_data in enumerate(rows):
        row = table.add_row()
        big = len(rows) > 2
        trPr = row._tr.get_or_add_trPr()
        trPr.append(OxmlElement('w:cantSplit'))
        if big and r_idx == 0:
            trPr.append(OxmlElement('w:tblHeader'))

        glue = len(rows) <= 10 and r_idx < len(rows) - 1
        for c_idx, (text, w) in enumerate(zip(row_data, col_widths_cm)):
            cell = row.cells[c_idx]
            cell.width = Cm(w)
            set_cell_borders(cell)
            cell.vertical_alignment = WD_ALIGN_VERTICAL.TOP
            if r_idx == 0:
                set_cell_bg(cell, HDR_BG)
                cell_para(cell, text, bold=True, size=9, color=(0xFF, 0xFF, 0xFF))
            else:
                if r_idx % 2 == 0:
                    set_cell_bg(cell, ALT_ROW)
                cell_para(cell, text, bold=(c_idx == 0), size=9)
            if glue:
                for par in cell.paragraphs:
                    par.paragraph_format.keep_with_next = True

    sp = doc.add_paragraph()
    sp.paragraph_format.space_after = Pt(4)
    sp.paragraph_format.space_before = Pt(0)
    sp.paragraph_format.line_spacing = 1.0
    return table


def h1(doc, text):
    doc.add_heading(text, level=1)


def h2(doc, text):
    doc.add_heading(text, level=2)


def p(doc, text, bullet=False):
    par = doc.add_paragraph(style='List Bullet' if bullet else None)
    run = par.add_run(text)
    run.font.name = 'Arial'
    run.font.size = Pt(9.5)
    return par


def kv_note(doc, text):
    par = doc.add_paragraph()
    run = par.add_run(text)
    run.font.name = 'Arial'
    run.font.size = Pt(9)
    run.font.italic = True
    run.font.color.rgb = GREY


def add_toc_field(doc, hint):
    par = doc.add_paragraph()
    run = par.add_run()
    fb = OxmlElement('w:fldChar'); fb.set(qn('w:fldCharType'), 'begin')
    instr = OxmlElement('w:instrText'); instr.set(qn('xml:space'), 'preserve')
    instr.text = r'TOC \o "1-1" \h \z \u'
    fs = OxmlElement('w:fldChar'); fs.set(qn('w:fldCharType'), 'separate')
    t = OxmlElement('w:t'); t.text = hint
    fs.append(t)
    fe = OxmlElement('w:fldChar'); fe.set(qn('w:fldCharType'), 'end')
    r = run._r
    r.append(fb); r.append(instr); r.append(fs); r.append(fe)
    br = doc.add_paragraph()
    rb = br.add_run()
    pb = OxmlElement('w:br'); pb.set(qn('w:type'), 'page')
    rb._r.append(pb)


def add_footer_pagenum(doc):
    footer = doc.sections[0].footer
    par = footer.paragraphs[0]
    par.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = par.add_run()
    fb = OxmlElement('w:fldChar'); fb.set(qn('w:fldCharType'), 'begin')
    instr = OxmlElement('w:instrText'); instr.set(qn('xml:space'), 'preserve')
    instr.text = r'PAGE \* arabic \* MERGEFORMAT'
    fe = OxmlElement('w:fldChar'); fe.set(qn('w:fldCharType'), 'end')
    run._r.append(fb); run._r.append(instr); run._r.append(fe)
    run.font.name = 'Arial'
    run.font.size = Pt(9)
    run.font.color.rgb = GREY


def title_block(doc, name, subtitle, tagline, version):
    tp = doc.add_paragraph()
    tp.alignment = WD_ALIGN_PARAGRAPH.CENTER
    tp.paragraph_format.space_before = Pt(30)
    tp.paragraph_format.space_after = Pt(4)
    r = tp.add_run(name)
    r.font.name = 'Arial'; r.font.size = Pt(30); r.font.bold = True
    r.font.color.rgb = ACCENT

    t2 = doc.add_paragraph()
    t2.alignment = WD_ALIGN_PARAGRAPH.CENTER
    t2.paragraph_format.space_after = Pt(10)
    r = t2.add_run(subtitle)
    r.font.name = 'Arial'; r.font.size = Pt(18); r.font.bold = True
    r.font.color.rgb = ACCENT

    s1 = doc.add_paragraph()
    s1.alignment = WD_ALIGN_PARAGRAPH.CENTER
    s1.paragraph_format.space_after = Pt(4)
    r = s1.add_run(tagline)
    r.font.name = 'Arial'; r.font.size = Pt(11); r.font.color.rgb = GREY

    s2 = doc.add_paragraph()
    s2.alignment = WD_ALIGN_PARAGRAPH.CENTER
    s2.paragraph_format.space_after = Pt(20)
    r = s2.add_run(version)
    r.font.name = 'Arial'; r.font.size = Pt(10.5); r.font.color.rgb = GREY


# ════════════════════════════════════════════════════════════════════════════
# RU
# ════════════════════════════════════════════════════════════════════════════

def build_ru():
    doc = _new_doc()
    title_block(doc, 'FLOMASTER', 'Руководство пользователя',
                'Единая точка запуска DCC-приложений под OCIO-конфигом',
                'Версия 2.3 - Windows 10/11')

    p(doc, 'FLOMASTER - лаунчер для рабочих приложений трёхмерной графики: одна кнопка '
           'запускает Blender, Maya, Houdini, Nuke, DaVinci Resolve, Unreal Engine или '
           'Substance Painter с заранее заданным OCIO-конфигом цвета. Лаунчер сам находит '
           'установленные приложения, ведёт пресеты запуска, открывает файлы проектов и '
           'ведёт журнал каждого запуска. Один и тот же цветовой конфиг - во всех '
           'приложениях, без ручных переменных окружения.')

    kv_note(doc, 'github.com/abyrvalg379/FLOMASTER')

    h1(doc, 'Содержание')
    add_toc_field(doc, 'Оглавление: откройте документ в Word/LibreOffice и обновите поле (F9), '
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

    add_footer_pagenum(doc)
    doc.save(OUT_RU)
    print('saved:', OUT_RU)


# ════════════════════════════════════════════════════════════════════════════
# EN
# ════════════════════════════════════════════════════════════════════════════

def build_en():
    doc = _new_doc()
    title_block(doc, 'FLOMASTER', 'User Guide',
                'A single launch point for DCC applications under one OCIO config',
                'Version 2.3 - Windows 10/11')

    p(doc, 'FLOMASTER is a launcher for 3D graphics applications: one button starts Blender, '
           'Maya, Houdini, Nuke, DaVinci Resolve, Unreal Engine or Substance Painter with a '
           'predefined OCIO color config. The launcher finds installed applications '
           'automatically, keeps launch presets, opens project files and logs every launch. '
           'One color config - across all applications, with no manual environment setup.')

    kv_note(doc, 'github.com/abyrvalg379/FLOMASTER')

    h1(doc, 'Contents')
    add_toc_field(doc, 'Table of contents: open the document in Word/LibreOffice and refresh '
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

    add_footer_pagenum(doc)
    doc.save(OUT_EN)
    print('saved:', OUT_EN)


def _strip_trailing_empty_paras(doc):
    """Хвостовые пустые абзацы не должны плодить пустую последнюю страницу."""
    body = doc.element.body
    for el in list(body)[::-1]:
        if el.tag == qn('w:sectPr'):
            continue
        if el.tag == qn('w:p') and not ''.join(el.itertext()).strip():
            body.remove(el)
        else:
            break


if __name__ == '__main__':
    for build, out in ((build_ru, OUT_RU), (build_en, OUT_EN)):
        build()
        d = Document(out)
        _strip_trailing_empty_paras(d)
        d.save(out)
        print('cleaned:', out)
