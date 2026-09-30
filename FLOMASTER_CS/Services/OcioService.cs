using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FLOMASTER.Models;

namespace FLOMASTER.Services
{
    public class OcioValidationReport
    {
        public List<KeyValuePair<string, string>> Roles { get; } = new();
        public List<string> Warnings { get; } = new();

        // Роли, влияющие на пайплайн-дефолты DCC (Blender: float/8-bit дефолты, is_data)
        private static readonly string[] Shown = { "reference", "scene_linear", "rendering", "data", "default_byte", "texture_paint" };

        public string RolesLine =>
            string.Join("   ", Roles.Where(r => Shown.Contains(r.Key)).Select(r => $"{r.Key}={Short(r.Value)}"));

        public string WarningsLine => Warnings.Count == 0 ? "" : "! " + string.Join("   ! ", Warnings);

        private static string Short(string v) => v.Length <= 26 ? v : v[..23] + "...";
    }

    public interface IOcioService
    {
        (List<KeyValuePair<string, string>> roles, Dictionary<string, string> colorspaces) Parse(string ocioPath);
        OcioValidationReport Validate(string ocioPath);
        string? BuildVariant(string basePath, Dictionary<string, string> overrides, string presetName);
        void ApplyOcio(ProcessStartInfo psi, OcioConfig ocio, string exePath, string? variantPath = null);
        bool AddOcioConfig(Config config, string name, string path);
        bool RemoveOcioConfig(Config config, OcioConfig selected);
    }

    public class OcioService : IOcioService
    {
        public void ApplyOcio(ProcessStartInfo psi, OcioConfig ocio, string exePath, string? variantPath = null)
        {
            if (ocio == null)
            {
                Logger.Log("OCIO", "No OCIO config selected, launching without OCIO", "warn");
                return;
            }
            if (string.IsNullOrEmpty(ocio.Path))
            {
                // seeded/imported запись без файла: NormalizePaths чинит это при старте,
                // но если добрались сюда — конфиг в списке есть, а .ocio не указан
                Logger.Log("OCIO", $"Config '{ocio.Name}' has no .ocio file path — launching WITHOUT color management. " +
                    "Reinstall from the full release zip: ocio\\ folder must sit next to FLOMASTER.exe", "warn");
                return;
            }

            // вариант конфига (переопределения ролей пресета) вместо канонического файла
            var ocioPath = string.IsNullOrEmpty(variantPath) ? ocio.Path : variantPath;
            if (!string.IsNullOrEmpty(variantPath))
                Logger.Log("OCIO", $"Using variant config: {variantPath}", "info");

            var isUnreal = exePath.ToLower().Contains("unreal");

            if (isUnreal)
            {
                var ocioArg = $"-ocio=\"{ocioPath}\"";
                psi.Arguments = string.IsNullOrEmpty(psi.Arguments)
                    ? ocioArg
                    : $"{ocioArg} {psi.Arguments}";
                Logger.Log("OCIO", $"UE mode: added arg {ocioArg}", "info");
            }
            else
            {
                psi.EnvironmentVariables["OCIO"] = ocioPath;
                Logger.Log("OCIO", $"Set OCIO={ocioPath}", "info");
                if (ocioPath.Any(c => c > 127))
                    Logger.Log("OCIO", "OCIO path contains non-ASCII characters — some DCC (Substance Painter) " +
                        "may silently ignore the config; keep FLOMASTER in an ASCII path, e.g. C:\\Program Files\\FLOMASTER", "warn");
            }
        }

        public bool AddOcioConfig(Config config, string name, string path)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
            {
                Logger.Log("OCIO", "Add failed: name or path is empty", "warn");
                return false;
            }

            if (!File.Exists(path))
            {
                Logger.Log("OCIO", $"Add failed: file not found: {path}", "warn");
                return false;
            }

            if (config.OcioConfigs.Any(o => o.Path == path))
            {
                Logger.Log("OCIO", $"Add failed: config already exists: {path}", "warn");
                return false;
            }

            config.OcioConfigs.Add(new() { Name = name, Path = path });
            Logger.Log("OCIO", $"Added config: {name} at {path}", "info");
            return true;
        }

        public bool RemoveOcioConfig(Config config, OcioConfig selected)
        {
            if (selected == null) return false;

            if (config.OcioConfigs.Count <= 1)
            {
                Logger.Log("OCIO", "Cannot remove last OCIO config", "warn");
                return false;
            }

            config.OcioConfigs.RemoveAll(o => o.Name == selected.Name);

            if (config.DefaultOcio == selected.Name && config.OcioConfigs.Count > 0)
            {
                config.DefaultOcio = config.OcioConfigs[0].Name;
                Logger.Log("OCIO", $"Default OCIO changed to: {config.DefaultOcio}", "info");
            }

            Logger.Log("OCIO", $"Removed config: {selected.Name}", "info");
            return true;
        }

        /// <summary>
        /// Разбирает config.ocio: секция roles + все объявленные colorspace (имя -> family).
        /// Общий парсер для Validate, UI-настройки ролей и пикера с группировкой по family.
        /// </summary>
        public (List<KeyValuePair<string, string>> roles, Dictionary<string, string> colorspaces) Parse(string ocioPath)
        {
            var lines = File.ReadAllLines(ocioPath);
            var roles = new List<KeyValuePair<string, string>>();
            var inRoles = false;
            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                if (raw.StartsWith("roles:", StringComparison.Ordinal)) { inRoles = true; continue; }
                if (inRoles && !char.IsWhiteSpace(raw[0])) break; // следующая top-level секция
                if (!inRoles) continue;
                var m = Regex.Match(raw, @"^\s+([A-Za-z_][\w_]*)\s*:\s*(.+?)\s*$");
                if (m.Success) roles.Add(new(m.Groups[1].Value, m.Groups[2].Value));
            }

            var text = string.Join("\n", lines);
            var colorspaces = new Dictionary<string, string>();
            // блоки colorspace: имя из "name:", family — последний "family:" перед следующим name
            string? currentName = null, currentFamily = null;
            foreach (var raw in lines)
            {
                var mn = Regex.Match(raw, @"^\s*-?\s*name:\s*(.+?)\s*$");
                if (mn.Success)
                {
                    if (currentName != null) colorspaces[currentName] = currentFamily ?? "";
                    currentName = mn.Groups[1].Value;
                    currentFamily = null;
                    continue;
                }
                var mf = Regex.Match(raw, @"^\s*family:\s*(.+?)\s*$");
                if (mf.Success && currentName != null) currentFamily = mf.Groups[1].Value;
            }
            if (currentName != null) colorspaces[currentName] = currentFamily ?? "";
            return (roles, colorspaces);
        }

        /// <summary>
        /// Проверяет конфиг: критичные роли, дефолты Blender, битые ссылки ролей.
        /// </summary>
        public OcioValidationReport Validate(string ocioPath)
        {
            var report = new OcioValidationReport();
            try
            {
                var (parsedRoles, colorspaces) = Parse(ocioPath);
                report.Roles.AddRange(parsedRoles);
                var cs = new HashSet<string>(colorspaces.Keys);

                if (report.Roles.Count == 0)
                {
                    report.Warnings.Add("no roles section found - is this an OCIO config?");
                    return report;
                }

                foreach (var req in new[] { "reference", "scene_linear" })
                    if (!report.Roles.Any(r => r.Key == req))
                        report.Warnings.Add($"missing required role '{req}'");

                if (!report.Roles.Any(r => r.Key == "default_byte"))
                    report.Warnings.Add("no default_byte: 8-bit images fall back to texture_paint (Blender 5.1+)");
                if (!report.Roles.Any(r => r.Key == "data"))
                    report.Warnings.Add("no data role: is_data textures have no target");

                foreach (var r in report.Roles)
                    if (!cs.Contains(r.Value))
                        report.Warnings.Add($"role {r.Key} -> colorspace '{r.Value}' not found");

                Logger.Log("OCIO",
                    $"Validated '{ocioPath}': {report.Roles.Count} roles, {report.Warnings.Count} warnings",
                    report.Warnings.Count > 0 ? "warn" : "info");
            }
            catch (Exception ex)
            {
                report.Warnings.Add("read failed: " + ex.Message);
                Logger.Log("OCIO", $"Validate failed: {ex.Message}", "warn");
            }
            return report;
        }

        /// <summary>
        /// Собирает вариант конфига для пресета: basePath, в котором блок roles
        /// дополнен/изменён согласно overrides. Оригинальный файл не трогается,
        /// результат пишется в %APPDATA%\FLOMASTER\variants\.
        /// Возвращает путь варианта или null (нет переопределений / нечего применять).
        /// </summary>
        public string? BuildVariant(string basePath, Dictionary<string, string> overrides, string presetName)
        {
            try
            {
                var (baseRoles, colorspaces) = Parse(basePath);
                var cs = new HashSet<string>(colorspaces.Keys);

                // отбрасываем переопределения, ссылающиеся на необъявленные colorspace
                var applied = new Dictionary<string, string>();
                foreach (var (role, target) in overrides)
                {
                    if (!cs.Contains(target))
                    {
                        Logger.Log("OCIO", $"Variant '{presetName}': drop {role} -> '{target}' (colorspace not in config)", "warn");
                        continue;
                    }
                    var baseVal = baseRoles.FirstOrDefault(r => r.Key == role).Value;
                    if (baseVal == target) continue; // совпадает с базой - правка не нужна
                    applied[role] = target;
                }
                if (applied.Count == 0) return null;

                string text = File.ReadAllText(basePath);
                string eol = text.Contains("\r\n") ? "\r\n" : "\n";
                var lines = text.Replace("\r\n", "\n").Split('\n').ToList();

                var outLines = new List<string>();
                var inRoles = false;
                var inserted = new HashSet<string>();
                foreach (var raw in lines)
                {
                    var line = raw;
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        if (line.StartsWith("roles:", StringComparison.Ordinal)) inRoles = true;
                        else if (inRoles && !char.IsWhiteSpace(line[0])) inRoles = false;
                    }
                    if (inRoles)
                    {
                        var m = Regex.Match(line, @"^(\s+)([A-Za-z_][\w_]*)\s*:\s*(.+?)\s*$");
                        if (m.Success && applied.TryGetValue(m.Groups[2].Value, out var newVal))
                        {
                            line = $"{m.Groups[1].Value}{m.Groups[2].Value}: {newVal}";
                            inserted.Add(m.Groups[2].Value);
                        }
                    }
                    outLines.Add(line);
                }
                // отсутствующие в базе роли дописываем в конец roles-блока (2 пробела)
                for (int i = outLines.Count - 1; i >= 0; i--)
                {
                    if (outLines[i].StartsWith("roles:", StringComparison.Ordinal))
                    {
                        var missing = applied.Where(kv => !inserted.Contains(kv.Key)).ToList();
                        for (int j = missing.Count - 1; j >= 0; j--)
                            outLines.Insert(i + 1, $"  {missing[j].Key}: {missing[j].Value}");
                        break;
                    }
                }

                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FLOMASTER", "variants");
                Directory.CreateDirectory(dir);
                var slug = new string(presetName.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
                var hashInput = string.Join(";", applied.Select(kv => $"{kv.Key}={kv.Value}")) + "|" + File.GetLastWriteTimeUtc(basePath).Ticks;
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(hashInput)))[..8];
                var variantPath = Path.Combine(dir, $"{slug}-{hash}.ocio");
                File.WriteAllText(variantPath, string.Join(eol, outLines));
                Logger.Log("OCIO", $"Variant for '{presetName}': {string.Join(", ", applied.Select(kv => $"{kv.Key}->{kv.Value}"))} -> {variantPath}", "info");
                return variantPath;
            }
            catch (Exception ex)
            {
                Logger.Log("OCIO", $"Variant build failed: {ex.Message}", "warn");
                return null;
            }
        }
    }
}
