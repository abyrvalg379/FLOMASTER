using System;
using System.IO;
using System.Text.Json;
using FLOMASTER.Models;

namespace FLOMASTER.Services
{
    /// <summary>Хранилище конфига. Папка инъектится: тесты работают на temp, приложение — на %APPDATA%.</summary>
    public interface IConfigStore
    {
        Config Load();
        void Save(Config config);
    }

    public class ConfigManager : IConfigStore
    {
        private readonly string _dir;
        private string ConfigPath => Path.Combine(_dir, "launcher_config.json");

        public ConfigManager() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FLOMASTER")) { }

        public ConfigManager(string dir)
        {
            _dir = dir;
        }

        public Config Load()
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

        public void Save(Config config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (!Directory.Exists(_dir))
                    Directory.CreateDirectory(_dir);

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

        private void BackupCorruptedConfig()
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

        private Config GetDefault()
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

        private void NormalizePaths(Config config)
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
