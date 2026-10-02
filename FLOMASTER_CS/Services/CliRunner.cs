using System;
using System.IO;
using System.Linq;
using FLOMASTER.Models;

namespace FLOMASTER.Services
{
    /// <summary>
    /// CLI-режим лаунчера: работает без окна поверх тех же сервисов.
    ///   FLOMASTER.exe --list-profiles
    ///   FLOMASTER.exe --launch "Профиль" [--project "файл"]
    /// Коды возврата: 0 — успех, 2 — не найден профиль/пресет/файл, 1 — ошибка запуска.
    /// </summary>
    public static class CliRunner
    {
        public static int Run(string[] args)
        {
            try
            {
                if (args.Contains("--list-profiles"))
                    return ListProfiles(new ConfigManager().Load());

                var launchIdx = Array.IndexOf(args, "--launch");
                if (launchIdx < 0 || launchIdx + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Usage: FLOMASTER.exe --launch \"Profile\" [--project \"file\"] | --list-profiles");
                    return 1;
                }
                var profileName = args[launchIdx + 1];

                var projectIdx = Array.IndexOf(args, "--project");
                var project = projectIdx >= 0 && projectIdx + 1 < args.Length ? args[projectIdx + 1] : null;

                return LaunchProfile(profileName, project);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("CLI error: " + ex.Message);
                return 1;
            }
        }

        private static int ListProfiles(Config config)
        {
            if (config.Profiles.Count == 0)
            {
                Console.WriteLine("No profiles defined.");
                return 0;
            }
            foreach (var p in config.Profiles)
                Console.WriteLine($"{p.Name}\t{p.PresetName}\t{p.OcioName}\t{p.Args}");
            return 0;
        }

        private static int LaunchProfile(string profileName, string project)
        {
            var config = new ConfigManager().Load();

            var profile = config.Profiles.FirstOrDefault(p => p.Name == profileName)
                          ?? config.Profiles.FirstOrDefault(p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
            {
                Console.Error.WriteLine($"Profile '{profileName}' not found. Use --list-profiles.");
                return 2;
            }

            var preset = config.Presets.FirstOrDefault(p => p.Name == profile.PresetName);
            if (preset == null)
            {
                Console.Error.WriteLine($"Preset '{profile.PresetName}' (profile '{profile.Name}') not found.");
                return 2;
            }

            // «NO OCIO» — псевдо-конфиг без OCIO: в config.OcioConfigs его нет, резолвим явно
            var ocio = profile.OcioName == ConfigManager.NoOcioName
                ? new OcioConfig { Name = ConfigManager.NoOcioName, IsNoOcio = true }
                : config.OcioConfigs.FirstOrDefault(o => o.Name == profile.OcioName)
                  ?? config.OcioConfigs.FirstOrDefault(o => o.Name == config.DefaultOcio);
            if (ocio == null && !string.IsNullOrEmpty(profile.OcioName))
                Console.Error.WriteLine($"OCIO '{profile.OcioName}' not found, launching without OCIO.");

            var args = profile.Args ?? "";
            if (!string.IsNullOrEmpty(project))
            {
                if (!File.Exists(project))
                {
                    Console.Error.WriteLine($"Project file not found: {project}");
                    return 2;
                }
                args = args.Contains("{file}")
                    ? args.Replace("{file}", $"\"{project}\"")
                    : (string.IsNullOrEmpty(args) ? $"\"{project}\"" : $"{args} \"{project}\"");
            }

            var error = new LaunchService(new OcioService())
                .Launch(preset.Exe, preset.Name, ocio, preset.RoleOverrides, args);
            if (error != null)
            {
                Console.Error.WriteLine("Launch failed: " + error);
                return 1;
            }

            Console.WriteLine($"Launched: {preset.Name} (profile '{profile.Name}') OCIO='{ocio?.Name ?? "-"}' args='{args}'");
            Logger.Log("CLI", $"Launched profile '{profile.Name}': {preset.Name}, OCIO '{ocio?.Name ?? "-"}', args '{args}'", "info");
            return 0;
        }
    }
}
