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

    public static class OcioService
    {
        public static OcioConfig GetActiveOcio(Config config, object selectedItem)
        {
            if (selectedItem is OcioConfig ocio)
            {
                if (!string.IsNullOrEmpty(ocio.Path) && File.Exists(ocio.Path))
                    return ocio;
                Logger.Log("OCIO", $"OCIO path not found: {ocio.Path}", "warn");
            }
            return null;
        }

        public static void ApplyOcio(ProcessStartInfo psi, OcioConfig ocio, string exePath)
        {
            if (ocio == null || string.IsNullOrEmpty(ocio.Path))
            {
                Logger.Log("OCIO", "No OCIO config selected, launching without OCIO", "warn");
                return;
            }

            var isUnreal = exePath.ToLower().Contains("unreal");

            if (isUnreal)
            {
                var ocioArg = $"-ocio=\"{ocio.Path}\"";
                psi.Arguments = string.IsNullOrEmpty(psi.Arguments)
                    ? ocioArg
                    : $"{ocioArg} {psi.Arguments}";
                Logger.Log("OCIO", $"UE mode: added arg {ocioArg}", "info");
            }
            else
            {
                psi.EnvironmentVariables["OCIO"] = ocio.Path;
                Logger.Log("OCIO", $"Set OCIO={ocio.Path}", "info");
            }
        }

        public static bool AddOcioConfig(Config config, string name, string path)
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

        public static bool RemoveOcioConfig(Config config, OcioConfig selected)
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

        public static OcioConfig GetDefaultOcio(Config config)
        {
            return config.OcioConfigs.FirstOrDefault(o => o.Name == config.DefaultOcio);
        }

        /// <summary>
        /// Разбирает config.ocio: секция roles + все объявленные colorspace.
        /// Предупреждает про отсутствующие критичные роли (дефолты Blender) и
        /// роли, цели которых не объявлены как colorspace (опечатки, битые конфиги).
        /// </summary>
        public static OcioValidationReport Validate(string ocioPath)
        {
            var report = new OcioValidationReport();
            try
            {
                var lines = File.ReadAllLines(ocioPath);
                var text = string.Join("\n", lines);

                var inRoles = false;
                foreach (var raw in lines)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    if (raw.StartsWith("roles:", StringComparison.Ordinal)) { inRoles = true; continue; }
                    if (inRoles && !char.IsWhiteSpace(raw[0])) break; // следующая top-level секция
                    if (!inRoles) continue;
                    var m = Regex.Match(raw, @"^\s+([A-Za-z_][\w_]*)\s*:\s*(.+?)\s*$");
                    if (m.Success) report.Roles.Add(new(m.Groups[1].Value, m.Groups[2].Value));
                }

                var colorspaces = new HashSet<string>(
                    Regex.Matches(text, @"^\s*name:\s*(.+?)\s*$", RegexOptions.Multiline)
                         .Cast<Match>().Select(mm => mm.Groups[1].Value));

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
                    if (!colorspaces.Contains(r.Value))
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
    }
}
