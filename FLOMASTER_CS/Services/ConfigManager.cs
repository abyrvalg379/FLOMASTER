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
        // Зарезервированное имя канон-записи (сидинг с v2.0). Записи с этим именем
        // без явного флага — legacy-сидинг, мигрируют в IsCanon при загрузке.
        public const string CanonName = "ACES 1.2";

        // Резервированное имя псевдо-конфига «запуск без OCIO» (OcioConfig.IsNoOcio).
        // В launcher_config не хранится, но резервируется, чтобы пользовательский
        // конфиг с таким именем не конфликтовал с ним в списках/профилях.
        public const string NoOcioName = "NO OCIO";

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

                if (NormalizePaths(config)) Save(config); // починенный путь персистится, иначе чинится каждый старт
                Logger.Log("Config", $"Loaded: {config.Presets.Count} presets, theme={config.Theme}", "info");
                return config;
            }catch (JsonException ex)
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
                    new() { Name = CanonName, Path = ocioPath, IsCanon = true }
                },
                DefaultOcio = CanonName,
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

        /// <summary>
        /// Ремонт OCIO-путей (пустые и протухшие) + перепривязка канона. true — что-то изменено, нужно Save.
        /// Канон (IsCanon) следует за установкой: путь перепривязывается к ocio рядом с exe,
        /// потому что инсталлер/апдейтер синхронизируют именно эту папку. Сохранённый в
        /// launcher_config путь иначе законсервирует старую копию конфига навсегда.
        /// </summary>
        private bool NormalizePaths(Config config)
        {
            bool changed = false;
            var canonPath = FindOcioConfig();

            foreach (var ocio in config.OcioConfigs)
            {
                // миграция: «ACES 1.2» без флага — сидинг старых версий, это канон
                if (!ocio.IsCanon && ocio.Name == CanonName)
                {
                    ocio.IsCanon = true;
                    changed = true;
                }

                if (ocio.IsCanon)
                {
                    if (canonPath != null && !SameFile(ocio.Path, canonPath))
                    {
                        Logger.Log("Config", $"Canon '{ocio.Name}' follows shipped config: {canonPath} (was: {(string.IsNullOrEmpty(ocio.Path) ? "null" : ocio.Path)})", "info");
                        ocio.Path = canonPath;
                        changed = true;
                    }
                    else if (canonPath == null && !string.IsNullOrEmpty(ocio.Path) && File.Exists(ocio.Path))
                    {
                        // установка без папки ocio (exe-only апдейтер): сохранённый путь — единственный
                        // известный конфиг, оставляем; ApplyOcio предупредит, если и он протухнет
                        Logger.Log("Config", $"Canon '{ocio.Name}': shipped config not found next to exe, keeping {ocio.Path}", "warn");
                    }
                    continue;
                }

                // пользовательская запись: File.Exists(null/"") = false — чиним пустые
                // и протухшие пути (перепривязкой на канон установки)
                if (!File.Exists(ocio.Path))
                {
                    Logger.Log("Config", string.IsNullOrEmpty(ocio.Path)
                        ? $"OCIO '{ocio.Name}' has no file path, searching..."
                        : $"OCIO path not found: {ocio.Path}, searching...", "warn");
                    if (canonPath != null)
                    {
                        ocio.Path = canonPath;
                        changed = true;
                        Logger.Log("Config", $"OCIO path fixed to: {canonPath}", "info");
                    }
                }
            }

            if (config.OcioConfigs.Count == 0)
            {
                if (canonPath != null)
                {
                    config.OcioConfigs.Add(new() { Name = CanonName, Path = canonPath, IsCanon = true });
                    config.DefaultOcio = CanonName;
                    changed = true;
                    Logger.Log("Config", "Added default OCIO config", "info");
                }
            }

            return changed;
        }

        /// <summary>Тот же файл? Полные пути, без учёта регистра (Windows).</summary>
        private static bool SameFile(string? a, string b)
        {
            if (string.IsNullOrEmpty(a)) return false;
            try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
            catch { return string.Equals(a, b, StringComparison.Ordinal); }
        }
    }
}
