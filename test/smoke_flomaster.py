#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
FLOMASTER smoke test (gate-скрипт, по образцу STUKACH smoke).

Что проверяет (17 чеков):
  01 csproj: AssemblyVersion == FileVersion
  02 XAML Title (vX.Y) синхронна csproj
  03 XAML hex-гигиена: hex-цвета только на строках-определениях кистей
     (x:Key / SystemColors) — урок v2.2 про захардкоженный синий hover
  04 Настроечные галки (*Enabled) имеют логику в сеттере (не мёртвый binding)
  05 Все Command="{Binding X}" из XAML существуют в ViewModel
  06 Тем ровно 7, ThemeOrder == ключи Themes
  07 Канонический ocio/config.ocio: роли scene_linear=acescg, data/reference/default_byte=raw
  08 Канонический конфиг: строчные алиасы raw/acescg существуют как colorspace
  09 ocio-бандл: luts не пуст, файлов 94
  10-12 Синхронизация конфига: out/vX.Y, Program Files, FLOMASTER_CS/ocio == канон
      (FLOMASTER_CS/ocio не в git — исторически протухает, отсюда отдельный чек)
  13 Версии exe: publish == out == Program Files == csproj
  14 Релизный zip: размер вменяемый, содержит exe и канонический конфиг, без вложенных zip
  15 Гигиена: нет файла `dist` в корне репо, .gitignore держит голый `dist`
  20 Юнит-тесты: dotnet test (OcioService + ConfigStore)
  16 Launch smoke: publish-exe стартует, жив через 5 с, конфиг %APPDATA% валиден
      (если FLOMASTER уже запущен — SKIP, чужой трей не трогаем)
  17 git status (только WARN, репо живое)

Запуск:
  python smoke_flomaster.py            # быстрая проверка артефактов (без сборки)
  python smoke_flomaster.py --build    # сначала dotnet publish в publish/, потом все чеки

Выход: 0 если все чеки PASS/SKIP/WARN без FAIL, 1 иначе.
"""

import json
import os
import re
import subprocess
import sys
import time
import zipfile
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(r"D:\AI\ZCode\Project\FLOMASTER")
WORK = ROOT / "work"                 # корень git-репо
CS = WORK / "FLOMASTER_CS"
PUBLISH = ROOT / "publish"
OUT = ROOT / "out"
PF = Path(r"C:\Program Files\FLOMASTER")
APPDATA_DIR = Path(os.environ["APPDATA"]) / "FLOMASTER"
OCIO_FILES_EXPECTED = 94
ZIP_SIZE_MIN, ZIP_SIZE_MAX = 100 * 2**20, 250 * 2**20

results = []


def maybe_build():
    """--build: dotnet publish в publish/. MSB4018 роняет publish 1-2 раза
    подряд разово (грабля из memory) — ретраим до 3, стабильное падение = FAIL."""
    if "--build" not in sys.argv:
        return
    section("dotnet publish --build")
    for attempt in (1, 2, 3):
        print(f"  attempt {attempt}/3 ...")
        out, code = run_cmd(
            ["dotnet", "publish", "-c", "Release", "-r", "win-x64",
             "--self-contained", "true", "-p:PublishSingleFile=true",
             "-p:IncludeNativeLibrariesForSelfExtract=true",
             "-o", str(PUBLISH)],
            timeout=600, cwd=CS)
        if code == 0:
            record("00 dotnet publish", "PASS", f"attempt {attempt}")
            return
        if "MSB4018" not in out and "error" not in out.lower():
            break
    record("00 dotnet publish", "FAIL", out.strip().splitlines()[-1][:120] if out.strip() else f"exit={code}")


def record(name, status, detail=""):
    results.append((name, status, detail))
    print(f"[{status:4}] {name}" + (f" -- {detail}" if detail else ""))


def section(title):
    print(f"\n--- {title} ---")


def run_cmd(args, timeout=30, cwd=None):
    """Байтовый capture + replace: консоль Windows может отдать cp866/cp1251."""
    r = subprocess.run(args, capture_output=True, timeout=timeout, cwd=cwd)
    return r.stdout.decode("utf-8", "replace"), r.returncode


def normalize_ocio(b):
    """Сравнение без шума: снять BOM и разницу CRLF/LF."""
    if b.startswith(b"\xef\xbb\xbf"):
        b = b[3:]
    return b.replace(b"\r\n", b"\n")


def read(p):
    return Path(p).read_text(encoding="utf-8", errors="replace")


def exe_file_version(path):
    """FileVersion через PowerShell (ctypes-парсинг PE не нужен)."""
    out, _ = run_cmd(["powershell", "-NoProfile", "-Command",
                      f"(Get-Item -LiteralPath '{path}').VersionInfo.FileVersion"])
    return out.strip()


def latest_out_dir():
    """Свежая out\vX.Y[.Z] — версии любой глубины, сравнение кортежами int."""
    best, best_ver = None, ()
    for d in OUT.glob("v*.*"):
        m = re.fullmatch(r"v(\d+(?:\.\d+)*)", d.name)
        if m and d.is_dir():
            ver = tuple(int(x) for x in m.group(1).split("."))
            if ver > best_ver:
                best, best_ver = d, ver
    return best


# ---------------------------------------------------------------- csproj/XAML

maybe_build()
section("Код: версии и XAML-гигиена")

csproj = read(CS / "FLOMASTER.csproj")
m_asm = re.search(r"<AssemblyVersion>([\d.]+)</AssemblyVersion>", csproj)
m_file = re.search(r"<FileVersion>([\d.]+)</FileVersion>", csproj)
if m_asm and m_file:
    if m_asm.group(1) == m_file.group(1):
        record("01 csproj AssemblyVersion==FileVersion", "PASS", m_asm.group(1))
        csproj_ver = m_asm.group(1)
    else:
        record("01 csproj AssemblyVersion==FileVersion", "FAIL",
               f"{m_asm.group(1)} != {m_file.group(1)}")
        csproj_ver = m_asm.group(1)
else:
    record("01 csproj AssemblyVersion==FileVersion", "FAIL", "теги не найдены")
    csproj_ver = None

xaml = read(CS / "MainWindow.xaml")
m_title = re.search(r'Title="FLOMASTER v([\d.]+)"', xaml)
if csproj_ver and m_title:
    # Title-версия должна быть префиксом csproj: v2.3 ↔ 2.3.x.y и v2.3.1 ↔ 2.3.1.0
    t_parts = m_title.group(1).split(".")
    c_parts = csproj_ver.split(".")
    ok = len(t_parts) <= len(c_parts) and t_parts == c_parts[:len(t_parts)]
    record("02 XAML Title синхронна csproj", "PASS" if ok else "FAIL",
           f"Title v{m_title.group(1)} vs csproj {csproj_ver}")
else:
    record("02 XAML Title синхронна csproj", "FAIL", "Title не распознан")

# 02b: в XAML не должно быть захардкоженных версий в Run (урок v2.3.1: шапка окна
# осталась "v2.3" после бампа 2.3.1 — версия в UI только через бинд VersionLabel)
hard_ver = [ln for ln in xaml.splitlines()
            if re.search(r'<Run Text="[^"]*v\d+\.\d+', ln)]
record("02b XAML: версии без хардкода в Run", "FAIL" if hard_ver else "PASS",
       hard_ver[0].strip()[:70] if hard_ver else "версия в шапке через бинд VersionLabel")

HEX_RE = re.compile(r"#[0-9A-Fa-f]{6}\b")
bad_hex = []
for line_no, line in enumerate(xaml.splitlines(), 1):
    if HEX_RE.search(line) and "x:Key" not in line and "SystemColors" not in line:
        bad_hex.append((line_no, line.strip()[:70]))
if bad_hex:
    record("03 XAML hex-гигиена", "FAIL",
           f"{len(bad_hex)} hex вне определений кистей, напр. строка {bad_hex[0][0]}: {bad_hex[0][1]}")
else:
    record("03 XAML hex-гигиена", "PASS",
           f"{len(HEX_RE.findall(xaml))} hex, все в ключевых строках кистей")

vm = read(CS / "ViewModels" / "MainViewModel.cs")
enabled_bound = set(re.findall(r'IsChecked="\{Binding (\w+Enabled)[,}]', xaml))
dead, ok_setters = [], []
for prop in sorted(enabled_bound):
    pm = re.search(rf"public bool {prop}\b(.*?)(?=\n        public |\n    }})", vm, re.S)
    if not pm:
        dead.append((prop, "свойство не найдено в VM"))
        continue
    body = pm.group(1)
    if "if (SetProperty" in body and re.search(r"if \(SetProperty\(ref \w+, value\)\)\s*\{\s*\}", body):
        dead.append((prop, "пустой if-блок: сеттер без логики (урок TopMost)"))
    else:
        ok_setters.append(prop)
if enabled_bound and not dead:
    record("04 Настроечные галки имеют логику в сеттере", "PASS", ", ".join(ok_setters))
elif enabled_bound:
    record("04 Настроечные галки имеют логику в сеттере", "FAIL", "; ".join(f"{p}: {r}" for p, r in dead))
else:
    record("04 Настроечные галки имеют логику в сеттере", "WARN", "галок *Enabled в XAML не найдено")

cmd_bindings = set(re.findall(r'Command="\{Binding (?:DataContext\.)?(\w+)', xaml))
vm_cmds = set(re.findall(r"public ICommand (\w+)", vm))
orphan = sorted(cmd_bindings - vm_cmds)
if orphan:
    record("05 Command-биндинги резолвятся в VM", "FAIL", "нет в VM: " + ", ".join(orphan))
else:
    record("05 Command-биндинги резолвятся в VM", "PASS", f"{len(cmd_bindings)} команд")

# ------------------------------------------------------------------- Темы

tm = read(CS / "Services" / "ThemeManager.cs")
theme_keys = set(re.findall(r'\["(\w+)"\]\s*=\s*new\(\)', tm))
theme_order = re.findall(r'"(\w+)"', re.search(r"ThemeOrder = new\(\).*?\{(.*?)\};", tm, re.S).group(1))
if len(theme_keys) == 7 and set(theme_order) == theme_keys and len(theme_order) == 7:
    record("06 Тем 7, ThemeOrder==Themes", "PASS", ", ".join(sorted(theme_keys)))
else:
    record("06 Тем 7, ThemeOrder==Themes", "FAIL",
           f"keys={len(theme_keys)} {sorted(theme_keys)}; order={theme_order}")

# ---------------------------------------------------------------- OCIO

section("OCIO-конфиг")

canon = WORK / "ocio" / "config.ocio"
canon_bytes = canon.read_bytes()
canon_text = canon_bytes.decode("utf-8", errors="replace")

role_checks = {
    "scene_linear: acescg": r"^\s*scene_linear:\s*acescg\s*$",
    "data: raw": r"^\s*data:\s*raw\s*$",
    "reference: raw": r"^\s*reference:\s*raw\s*$",
    "default_byte: raw": r"^\s*default_byte:\s*raw\s*$",
}
missing_roles = [k for k, pat in role_checks.items() if not re.search(pat, canon_text, re.M)]
if missing_roles:
    record("07 Канонический ocio: роли v2.3", "FAIL", "нет: " + ", ".join(missing_roles))
else:
    record("07 Канонический ocio: роли v2.3", "PASS", "scene_linear=acescg, data/reference/default_byte=raw")

alias_names = set(n.strip() for n in re.findall(r"^\s*name:\s*(.+?)\s*$", canon_text, re.M))
need_aliases = {"raw", "acescg"}
keep_long = {"ACES - ACEScg", "Utility - Raw"}
if need_aliases <= alias_names and keep_long <= alias_names:
    record("08 Канонический ocio: алиасы raw/acescg", "PASS",
           f"всего colorspace: {len(alias_names)}")
else:
    record("08 Канонический ocio: алиасы raw/acescg", "FAIL",
           f"нет: {sorted((need_aliases | keep_long) - alias_names)}")

ocio_dir = WORK / "ocio"
lut_files = list((ocio_dir / "luts").glob("*")) if (ocio_dir / "luts").is_dir() else []
total_ocio = sum(1 for p in ocio_dir.rglob("*") if p.is_file())
if lut_files and total_ocio == OCIO_FILES_EXPECTED:
    record("09 ocio-бандл: luts и состав", "PASS", f"{total_ocio} файлов, luts: {len(lut_files)}")
else:
    record("09 ocio-бандл: luts и состав", "FAIL",
           f"файлов {total_ocio} (ожидалось {OCIO_FILES_EXPECTED}), luts: {len(lut_files)}")


def sync_check(num, label, path):
    if not path.is_file():
        record(num + " " + label, "FAIL", f"{path} не найден")
        return
    raw = path.read_bytes()
    if raw == canon_bytes:
        record(num + " " + label, "PASS", "байт-в-байт с каноном")
    elif normalize_ocio(raw) == normalize_ocio(canon_bytes):
        record(num + " " + label, "PASS", "совпадает по содержимому (разница только BOM/CRLF)")
    else:
        record(num + " " + label, "FAIL", f"ОТЛИЧАЕТСЯ от {canon}")


sync_check("10", "Sync: out/vX.Y ocio == канон", latest_out_dir() / "ocio" / "config.ocio")
sync_check("11", "Sync: Program Files ocio == канон", PF / "ocio" / "config.ocio")
sync_check("12", "Sync: FLOMASTER_CS/ocio == канон", CS / "ocio" / "config.ocio")

# ------------------------------------------------------------ exe + релизный zip

section("Сборка и деплой")

deploy_points = {"publish": PUBLISH / "FLOMASTER.exe", "out": latest_out_dir() / "FLOMASTER.exe", "Program Files": PF / "FLOMASTER.exe"}
if csproj_ver:
    mism, absents = [], []
    for label, p in deploy_points.items():
        if not p.is_file():
            absents.append(label)
            continue
        fv = exe_file_version(p)
        if fv != csproj_ver:
            mism.append(f"{label}={fv}")
    if absents == ["publish"]:
        record("13 Версии exe синхронны", "WARN", "publish\\ отсутствует — пересобери (--build)")
    elif absents:
        record("13 Версии exe синхронны", "FAIL", "нет exe: " + ", ".join(absents))
    elif mism:
        record("13 Версии exe синхронны", "FAIL", "; ".join(mism) + f" (ожидалось {csproj_ver})")
    else:
        record("13 Версии exe синхронны", "PASS", f"все == {csproj_ver}")
else:
    record("13 Версии exe синхронны", "FAIL", "csproj версия не распознана")

out_dir = latest_out_dir()
zips = sorted(out_dir.glob("FLOMASTER_v*.zip")) if out_dir else []
if not zips:
    record("14 Релизный zip", "FAIL", f"в {out_dir} нет FLOMASTER_v*.zip")
else:
    zp = zips[-1]
    size = zp.stat().st_size
    problems = []
    if not (ZIP_SIZE_MIN <= size <= ZIP_SIZE_MAX):
        problems.append(f"размер {size / 2**20:.0f} МБ вне {ZIP_SIZE_MIN // 2**20}-{ZIP_SIZE_MAX // 2**20} МБ (урок zip-в-zip)")
    with zipfile.ZipFile(zp) as z:
        names = z.namelist()
        if not any(n.endswith("FLOMASTER.exe") for n in names):
            problems.append("нет FLOMASTER.exe в корне")
        if any(n.lower().endswith(".zip") for n in names):
            problems.append("вложенный zip внутри")
        m_cfg = [n for n in names if re.fullmatch(r"(ocio/)?config\.ocio", n.replace("\\", "/"))]
        if m_cfg:
            if normalize_ocio(z.read(m_cfg[0])) != normalize_ocio(canon_bytes):
                problems.append(f"ocio в zip ({m_cfg[0]}) != канон")
        else:
            problems.append("нет ocio/config.ocio")
    record("14 Релизный zip", "FAIL" if problems else "PASS",
           "; ".join(problems) if problems else f"{zp.name}, {size / 2**20:.0f} МБ, конфиг канонический")

# ------------------------------------------------------------------ Гигиена

dist_file = WORK / "dist"
gi = WORK / ".gitignore"
gi_has_dist = gi.is_file() and re.search(r"^dist\s*$", gi.read_text(encoding="utf-8", errors="replace"), re.M) is not None
if dist_file.exists() or not gi_has_dist:
    parts = []
    if dist_file.exists():
        parts.append("мусорный файл dist в корне репо (урок v2.2)")
    if not gi_has_dist:
        parts.append(".gitignore не содержит голый 'dist'")
    record("15 Гигиена репо", "FAIL", "; ".join(parts))
else:
    record("15 Гигиена репо", "PASS", "dist отсутствует, .gitignore закрыт")

git_out, _ = run_cmd(["git", "status", "--porcelain"], cwd=WORK)
if git_out.strip():
    first = git_out.strip().splitlines()[:3]
    record("17 git status", "WARN", f"{len(git_out.strip().splitlines())} изменений, напр.: {first}")
else:
    record("17 git status", "PASS", "чисто")

# ------------------------------------------------------------ Юнит-тесты

section("Юнит-тесты")

tests_dir = WORK / "tests" / "FLOMASTER.Tests"
if not tests_dir.exists():
    record("20 Юнит-тесты dotnet test", "FAIL", "нет tests/FLOMASTER.Tests")
else:
    out, code = run_cmd(["dotnet", "test", str(tests_dir), "--nologo", "-v", "q"], timeout=240, cwd=WORK)
    if code == 0:
        import re as _re
        m = _re.search(r"пройдено\s+(\d+),\s+пропущено", out) or _re.search(r"Passed!.*?(\d+) passed", out)
        record("20 Юнит-тесты dotnet test", "PASS", f"{m.group(1)} passed" if m else "exit 0")
    else:
        tail = "; ".join(out.strip().splitlines()[-3:])
        record("20 Юнит-тесты dotnet test", "FAIL", tail or f"exit {code}")

# ------------------------------------------------------------ Launch smoke

section("Launch smoke")

cfg_json_ok = False
try:
    json.loads((APPDATA_DIR / "launcher_config.json").read_text(encoding="utf-8-sig"))
    cfg_json_ok = True
except Exception:
    pass
record("16a %APPDATA% конфиг валиден", "PASS" if cfg_json_ok else "FAIL", str(APPDATA_DIR / "launcher_config.json"))

task_out, _ = run_cmd(["tasklist", "/FI", "IMAGENAME eq FLOMASTER.exe"])
already_running = "FLOMASTER.exe" in task_out
launch_exe = PUBLISH / "FLOMASTER.exe"
if not launch_exe.is_file():
    launch_exe = deploy_points["out"]
if already_running:
    record("16b Запуск publish-exe", "SKIP", "FLOMASTER уже запущен (пользовательский трей) — launch-тест пропущен")
elif not launch_exe.is_file():
    record("16b Запуск publish-exe", "FAIL", "нет exe ни в publish\\, ни в out\\")
else:
    proc = subprocess.Popen([str(launch_exe)], cwd=str(launch_exe.parent))
    time.sleep(5)
    alive = proc.poll() is None
    if alive:
        subprocess.run(["taskkill", "/PID", str(proc.pid)], capture_output=True)  # graceful WM_CLOSE
        time.sleep(2)
        if proc.poll() is None:
            proc.kill()
        record("16b Запуск publish-exe", "PASS", f"{launch_exe.name} жив через 5 с, закрыт корректно")
    else:
        record("16b Запуск publish-exe", "FAIL", f"процесс упал, exit={proc.returncode}")

# ------------------------------------------------------------------- Итог

fails = [n for n, s, _ in results if s == "FAIL"]
warns = [n for n, s, _ in results if s == "WARN"]
skips = [n for n, s, _ in results if s == "SKIP"]
total = len(results)
passed = total - len(fails)
print(f"\n===== SMOKE: {passed}/{total} "
      f"(FAIL: {len(fails)}, WARN: {len(warns)}, SKIP: {len(skips)}) =====")
for n, s, d in results:
    if s == "FAIL":
        print(f"  FAIL  {n}: {d}")
sys.exit(1 if fails else 0)
