using System;
using System.IO;
using System.Text.Json;
using FLOMASTER.Models;

namespace FLOMASTER.Services
{
    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_config.json");

        public static Config Load()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigPath);
                    var config = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (config != null)
                    {
                        NormalizePaths(config);
                        return config;
                    }
                }
                catch { }
            }
            return GetDefault();
        }

        public static void Save(Config config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            File.WriteAllText(ConfigPath, json);
        }

        private static Config GetDefault()
        {
            // Try multiple locations for OCIO config
            var ocioPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocio", "config.ocio"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "ocio", "config.ocio"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "ocio", "config.ocio")
            };

            string ocioPath = null;
            foreach (var p in ocioPaths)
            {
                if (File.Exists(p)) { ocioPath = Path.GetFullPath(p); break; }
            }

            return new Config
            {
                Theme = "blender",
                OcioConfigs = new()
                {
                    new() { Name = "ACES 1.2", Path = ocioPath }
                },
                DefaultOcio = "ACES 1.2",
                Presets = new(),
                RecentFiles = new()
            };
        }

        private static void NormalizePaths(Config config)
        {
            foreach (var ocio in config.OcioConfigs)
            {
                if (!string.IsNullOrEmpty(ocio.Path) && !File.Exists(ocio.Path))
                {
                    var bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocio", "config.ocio");
                    if (File.Exists(bundled)) ocio.Path = bundled;
                }
            }

            if (config.OcioConfigs.Count == 0)
            {
                var bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocio", "config.ocio");
                if (File.Exists(bundled))
                {
                    config.OcioConfigs.Add(new() { Name = "ACES 1.2", Path = bundled });
                    config.DefaultOcio = "ACES 1.2";
                }
            }
        }
    }
}
