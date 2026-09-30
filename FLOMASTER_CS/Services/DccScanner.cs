using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using FLOMASTER.Models;

namespace FLOMASTER.Services
{
    public static class DccScanner
    {
        // Канонические префиксы имён для реестра/манифестов (нижний регистр).
        private static readonly string[] KnownPrefixes =
        {
            "blender", "autodesk maya", "maya", "sidefx houdini", "houdini",
            "nuke", "davinci resolve", "adobe substance 3d painter", "substance 3d painter",
            "unreal editor", "unreal engine"
        };

        /// <summary>NukeXX.Y.exe / NukeXX.YvN.exe / Nuke.exe — главный exe; Init/Assist/Register отсекается маской.</summary>
        public static bool IsNukeMainExe(string fileName)
        {
            return Regex.IsMatch(fileName ?? "", @"^Nuke(\d+(\.\d+)?)?(v\d+)?\.exe$", RegexOptions.IgnoreCase);
        }

        private static readonly Regex JunkExeRegex = new(
            @"(?i)(uninst|installer|setup|crashreport|crash_handler|error_report|vcredist|redist|directx|prereq)",
            RegexOptions.Compiled);

        /// <summary>
        /// Инсталляторы/деинсталляторы/репортеры — не DCC. Урок 2.5.3: реестр отдаёт
        /// DisplayIcon деинсталлятора (Uninstall Houdini.exe, Autodesk Installer.exe).
        /// </summary>
        public static bool IsJunkExe(string exePath)
        {
            var name = Path.GetFileName(exePath ?? "");
            return !string.IsNullOrEmpty(name) && JunkExeRegex.IsMatch(name);
        }

        /// <summary>Матчит DisplayName из реестра/манифеста на известное DCC-приложение.</summary>
        public static bool MatchesKnownApp(string displayName)
        {
            var name = (displayName ?? "").Trim().ToLowerInvariant();
            return KnownPrefixes.Any(name.StartsWith);
        }

        /// <summary>Читает Epic-манифест (*.item) и возвращает пресет, если это Unreal Engine.</summary>
        public static Preset FromEpicManifest(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var name = root.TryGetProperty("DisplayName", out var dn) ? dn.GetString() : null;
                var install = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() : null;
                var launchExe = root.TryGetProperty("LaunchExecutable", out var le) ? le.GetString() : null;
                if (string.IsNullOrEmpty(install) || string.IsNullOrEmpty(launchExe)) return null;
                if (!MatchesKnownApp(name)) return null;

                var exe = Path.Combine(install, launchExe.Replace('/', '\\'));
                return new Preset { Name = name.Trim(), Exe = exe };
            }
            catch { return null; }
        }

        public static List<Preset> Scan(List<string> customPaths = null)
        {
            var found = new List<Preset>();
            Logger.Log("Scanner", "Starting DCC scan...", "info");

            // 1) Реестр + Epic-манифесты: работает на любой машине без захардкоженных дисков
            ScanRegistry(found);
            ScanEpicManifests(found);

            // 2) Файловые эвристики — fallback (переносные установки, нестандартные пути)
            ScanBlender(@"C:\Program Files\Blender Foundation", found);
            ScanBlender(@"C:\Program Files (x86)\Blender Foundation", found);
            ScanBlender(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Blender Foundation"), found);

            ScanMaya(found);
            ScanHoudini(found);
            ScanNuke(found);
            ScanUnrealEngine(found);

            var davinciExe = @"C:\Program Files\Blackmagic Design\DaVinci Resolve\Resolve.exe";
            if (File.Exists(davinciExe) && !found.Any(f => string.Equals(f.Exe, davinciExe, StringComparison.OrdinalIgnoreCase)))
                found.Add(new() { Name = "DaVinci Resolve", Exe = davinciExe });

            var substanceExe = @"C:\Program Files\Adobe\Adobe Substance 3D Painter\Adobe Substance 3D Painter.exe";
            if (File.Exists(substanceExe) && !found.Any(f => string.Equals(f.Exe, substanceExe, StringComparison.OrdinalIgnoreCase)))
                found.Add(new() { Name = "Substance 3D Painter", Exe = substanceExe });

            if (customPaths != null)
            {
                foreach (var path in customPaths)
                {
                    if (Directory.Exists(path))
                    {
                        Logger.Log("Scanner", $"Scanning custom path: {path}", "info");
                        ScanCustomPath(path, found);
                    }
                    else
                    {
                        Logger.Log("Scanner", $"Custom path not found: {path}", "warn");
                    }
                }
            }

            // страховочный фильтр: никакой мусор из любого источника не доходит до пресетов
            foreach (var junk in found.Where(f => IsJunkExe(f.Exe)).ToList())
            {
                Logger.Log("Scanner", $"Skip junk exe: {junk.Exe}", "warn");
                found.Remove(junk);
            }

            // дедуп по exe: реестр может пересечься с эвристиками
            found = found
                .GroupBy(f => f.Exe, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            Logger.Log("Scanner", $"Scan complete: {found.Count} applications found", "info");
            return found;
        }

        /// <summary>HKLM Uninstall (+WOW6432Node): DisplayName/DisplayIcon известных DCC.</summary>
        private static void ScanRegistry(List<Preset> found)
        {
            try
            {
                var roots = new[]
                {
                    Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                    Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall")
                };

                foreach (var root in roots)
                {
                    if (root == null) continue;
                    using (root)
                    foreach (var keyName in root.GetSubKeyNames())
                    {
                        try
                        {
                            using var key = root.OpenSubKey(keyName);
                            var displayName = key?.GetValue("DisplayName") as string;
                            if (string.IsNullOrEmpty(displayName) || !MatchesKnownApp(displayName)) continue;

                            // DisplayIcon: "C:\path\app.exe,0" — до запятой путь к exe
                            var icon = (key.GetValue("DisplayIcon") as string)?.Split(',')[0].Trim('"', ' ');
                            string exe = null;
                            if (!string.IsNullOrEmpty(icon) && File.Exists(icon) && icon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                exe = icon;

                            // DisplayIcon часто показывает деинсталлятор/инсталлятор — канонический exe из InstallLocation
                            if (exe == null || IsJunkExe(exe))
                            {
                                exe = null;
                                var install = key.GetValue("InstallLocation") as string;
                                if (!string.IsNullOrEmpty(install))
                                {
                                    var candidates = new[] { "blender.exe", "maya.exe", "houdini.exe", "Resolve.exe", "Adobe Substance 3D Painter.exe", "UnrealEditor.exe" };
                                    exe = candidates.Select(c => Path.Combine(install, c)).FirstOrDefault(File.Exists) ?? "";
                                }
                            }

                            if (string.IsNullOrEmpty(exe) || IsJunkExe(exe)) continue;
                            if (found.Any(f => string.Equals(f.Exe, exe, StringComparison.OrdinalIgnoreCase))) continue;

                            var presetName = Regex.Replace(displayName.Trim(), @"\s*\(x64\)$", "", RegexOptions.IgnoreCase);
                            found.Add(new() { Name = presetName, Exe = exe });
                            Logger.Log("Scanner", $"Registry: {presetName} at {exe}", "info");
                        }
                        catch { /* битый ключ реестра — не повод падать */ }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Scanner", $"Registry scan failed: {ex.Message}", "warn");
            }
        }

        /// <summary>Epic Games Launcher манифесты: %ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item.</summary>
        private static void ScanEpicManifests(List<Preset> found)
        {
            try
            {
                var manifests = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Epic", "EpicGamesLauncher", "Data", "Manifests");
                if (!Directory.Exists(manifests)) return;

                foreach (var file in Directory.GetFiles(manifests, "*.item"))
                {
                    var preset = FromEpicManifest(File.ReadAllText(file));
                    if (preset != null && File.Exists(preset.Exe) &&
                        !found.Any(f => string.Equals(f.Exe, preset.Exe, StringComparison.OrdinalIgnoreCase)))
                    {
                        found.Add(preset);
                        Logger.Log("Scanner", $"Epic manifest: {preset.Name} at {preset.Exe}", "info");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Scanner", $"Epic manifest scan failed: {ex.Message}", "warn");
            }
        }

        private static void ScanBlender(string basePath, List<Preset> found)
        {
            if (!Directory.Exists(basePath)) return;

            try
            {
                foreach (var dir in Directory.GetDirectories(basePath))
                {
                    var exe = Path.Combine(dir, "blender.exe");
                    if (File.Exists(exe))
                    {
                        var name = Path.GetFileName(dir).Trim();
                        found.Add(new() { Name = name, Exe = exe });
                        Logger.Log("Scanner", $"Found: {name} at {exe}", "info");
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log("Scanner", $"Access denied scanning {basePath}: {ex.Message}", "warn");
            }
            catch (IOException ex)
            {
                Logger.Log("Scanner", $"IO error scanning {basePath}: {ex.Message}", "warn");
            }
        }

        private static void ScanMaya(List<Preset> found)
        {
            var mayaBase = @"C:\Program Files\Autodesk";
            if (!Directory.Exists(mayaBase)) return;

            try
            {
                foreach (var dir in Directory.GetDirectories(mayaBase, "Maya*"))
                {
                    var exe = Path.Combine(dir, "bin", "maya.exe");
                    if (File.Exists(exe))
                    {
                        var year = Path.GetFileName(dir).Replace("Maya", "").Trim();
                        var name = string.IsNullOrEmpty(year) ? "Maya" : $"Maya {year}";
                        found.Add(new() { Name = name, Exe = exe });
                        Logger.Log("Scanner", $"Found: {name}", "info");
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log("Scanner", $"Access denied scanning Maya: {ex.Message}", "warn");
            }
        }

        private static void ScanHoudini(List<Preset> found)
        {
            var houdiniBase = @"C:\Program Files\Side Effects Software";
            if (!Directory.Exists(houdiniBase)) return;

            try
            {
                foreach (var dir in Directory.GetDirectories(houdiniBase, "Houdini*"))
                {
                    var exe = Path.Combine(dir, "bin", "houdini.exe");
                    if (File.Exists(exe))
                    {
                        var ver = Path.GetFileName(dir).Replace("Houdini ", "").Trim();
                        found.Add(new() { Name = $"Houdini {ver}", Exe = exe });
                        Logger.Log("Scanner", $"Found: Houdini {ver}", "info");
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log("Scanner", $"Access denied scanning Houdini: {ex.Message}", "warn");
            }
        }

        private static void ScanNuke(List<Preset> found)
        {
            var nukeBase = @"C:\Program Files";
            if (!Directory.Exists(nukeBase)) return;

            try
            {
                foreach (var dir in Directory.GetDirectories(nukeBase, "Nuke*"))
                {
                    // маска главного exe: NukeXX.Y.exe (Init/Assist/Register отсекаются ей же)
                    var nukeExe = Directory.GetFiles(dir, "Nuke*.exe")
                        .Where(f => IsNukeMainExe(Path.GetFileName(f)))
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(nukeExe))
                    {
                        var ver = Path.GetFileName(dir).Replace("Nuke ", "").Trim();
                        found.Add(new() { Name = $"Nuke {ver}", Exe = nukeExe });
                        Logger.Log("Scanner", $"Found: Nuke {ver}", "info");
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log("Scanner", $"Access denied scanning Nuke: {ex.Message}", "warn");
            }
        }

        private static void ScanUnrealEngine(List<Preset> found)
        {
            var uePaths = new[]
            {
                @"C:\Program Files\Epic Games",
                @"D:\Unreal",
                @"D:\Epic Games",
                @"E:\Unreal",
                @"E:\Epic Games"
            };

            foreach (var ueBase in uePaths)
            {
                if (!Directory.Exists(ueBase)) continue;

                try
                {
                    foreach (var dir in Directory.GetDirectories(ueBase))
                    {
                        var exe = Path.Combine(dir, "Engine", "Binaries", "Win64", "UnrealEditor.exe");
                        if (File.Exists(exe))
                        {
                            var folderName = Path.GetFileName(dir);
                            var ver = folderName
                                .Replace("UE_", "")
                                .Replace("UE-", "")
                                .Replace("UnrealEngine-", "")
                                .Replace("Unreal Engine ", "")
                                .Trim();
                            if (!found.Any(f => f.Exe == exe))
                            {
                                found.Add(new() { Name = $"Unreal Engine {ver}", Exe = exe });
                                Logger.Log("Scanner", $"Found: Unreal Engine {ver}", "info");
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    Logger.Log("Scanner", $"Access denied scanning {ueBase}: {ex.Message}", "warn");
                }
            }
        }

        private static void ScanCustomPath(string basePath, List<Preset> found)
        {
            var knownExes = new Dictionary<string, string>
            {
                { "blender.exe", "Blender" },
                { "maya.exe", "Maya" },
                { "houdini.exe", "Houdini" },
                { "UnrealEditor.exe", "Unreal Engine" },
                { "Resolve.exe", "DaVinci Resolve" }
            };

            try
            {
                foreach (var dir in Directory.GetDirectories(basePath))
                {
                    foreach (var (exeName, appName) in knownExes)
                    {
                        var exe = Path.Combine(dir, exeName);
                        if (File.Exists(exe) && !found.Any(f => f.Exe == exe))
                        {
                            var folderName = Path.GetFileName(dir);
                            found.Add(new() { Name = $"{appName} ({folderName})", Exe = exe });
                            Logger.Log("Scanner", $"Found: {appName} in {folderName}", "info");
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log("Scanner", $"Access denied scanning custom path: {ex.Message}", "warn");
            }
            catch (IOException ex)
            {
                Logger.Log("Scanner", $"IO error scanning custom path: {ex.Message}", "warn");
            }
        }
    }
}
