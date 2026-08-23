using System;
using System.IO;
using System.Text.Json;
using FLOMASTER.Models;

namespace FLOMASTER.Services
{
    public static class ConfigManager
    {
        private static readonly string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FLOMASTER");
        private static readonly string ConfigPath = Path.Combine(ConfigDir, "launcher_config.json");

        public static Config Load()
        {
            if (!File.Exists(ConfigPath))
            {
                Logger.Log("Config", "No config file found, creating default", "info");
                var defaultConfig = GetDefault();
                Save(defaultConfig);
                return defaultConfig;
            }

            try
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config == null)
                {
                    Logger.Log("Config", "Config deserialized to null, using defaults", "warn");
                    return GetDefault();
                }

                NormalizePaths(config);
                Logger.Log("Config", $"Loaded: {config.Presets.Count} presets, theme={config.Theme}", "info");
                return config;
            }
            catch (JsonException ex)
            {
                Logger.Log("Config", $"JSON parse error: {ex.Message}", "error");
                BackupCorruptedConfig();
                return GetDefault();
            }
            catch (IOException ex)
            {
                Logger.Log("Config", $"IO error reading config: {ex.Message}", "error");
                return GetDefault();
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log("Config", $"Access denied: {ex.Message}", "error");
                return GetDefault();
            }
        }

        public static void Save(Config config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);

                File.WriteAllText(ConfigPath, json);
            }
            catch (IOException ex)
            {
                Logger.Log("Config", $"IO error saving config: {ex.Message}", "error");
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log("Config", $"Access denied saving config: {ex.Message}", "error");
            }
        }

        private static void BackupCorruptedConfig()
        {
            try
            {
                var backupPath = ConfigPath + $".backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                File.Copy(ConfigPath, backupPath, true);
                Logger.Log("Config", $"Corrupted config backed up to: {backupPath}", "warn");
            }
            catch (Exception ex)
            {
                Logger.Log("Config", $"Failed to backup corrupted config: {ex.Message}", "error");
            }
        }

        private static Config GetDefault()
        {
            var ocioPath = FindOcioConfig();

            return new Config
            {
                Theme = "blender",
                OcioConfigs = new()
                {
                    new() { Name = "ACES 1.2", Path = ocioPath }
                },
                DefaultOcio = "ACES 1.2",
                Presets = new(),
                RecentFiles = new(),
                ScanPaths = new(),
                AnimationEnabled = true,
                TopMostEnabled = false
            };
        }

        private static string FindOcioConfig()
        {
            var searchPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocio", "config.ocio"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "ocio", "config.ocio"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "ocio", "config.ocio")
            };

            foreach (var path in searchPaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    Logger.Log("Config", $"Found OCIO config at: {fullPath}", "info");
                    return fullPath;
                }
            }

            Logger.Log("Config", "No OCIO config found in search paths", "warn");
            return null;
        }

        private static void NormalizePaths(Config config)
        {
            foreach (var ocio in config.OcioConfigs)
            {
                if (!string.IsNullOrEmpty(ocio.Path) && !File.Exists(ocio.Path))
                {
                    Logger.Log("Config", $"OCIO path not found: {ocio.Path}, searching...", "warn");
                    var found = FindOcioConfig();
                    if (found != null)
                    {
                        ocio.Path = found;
                        Logger.Log("Config", $"OCIO path fixed to: {found}", "info");
                    }
                }
            }

            if (config.OcioConfigs.Count == 0)
            {
                var ocioPath = FindOcioConfig();
                if (ocioPath != null)
                {
                    config.OcioConfigs.Add(new() { Name = "ACES 1.2", Path = ocioPath });
                    config.DefaultOcio = "ACES 1.2";
                    Logger.Log("Config", "Added default OCIO config", "info");
                }
            }
        }
    }
}
