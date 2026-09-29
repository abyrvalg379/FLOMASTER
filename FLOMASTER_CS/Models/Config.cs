using System.Collections.Generic;

namespace FLOMASTER.Models
{
    public class Config
    {
        public string Theme { get; set; } = "blender";
        public List<OcioConfig> OcioConfigs { get; set; } = new();
        public string DefaultOcio { get; set; } = "ACES 1.2";
        public List<Preset> Presets { get; set; } = new();
        public List<Profile> Profiles { get; set; } = new();
        public List<string> RecentFiles { get; set; } = new();
        public List<string> ScanPaths { get; set; } = new();
        public List<string> ProjectRoots { get; set; } = new();
        public bool AnimationEnabled { get; set; } = true;
        public bool TopMostEnabled { get; set; } = false;
        public bool CheckUpdates { get; set; } = true;
        /// <summary>Глобальный хоткей Ctrl+Alt+F: показать/спрятать лаунчер поверх всего.</summary>
        public bool HotkeyEnabled { get; set; } = true;

        /// <summary>DeviceName монитора, на котором оверлей был закрыт последний раз.</summary>
        public string OverlayScreenDeviceName { get; set; } = "";

        /// <summary>Оверлей поверх всех окон (отключается, если мешает).</summary>
        public bool OverlayTopmost { get; set; } = true;
    }

    public class OcioConfig
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
    }

    public class Preset
    {
        public string Name { get; set; } = "";
        public string Exe { get; set; } = "";

        // Переопределения ролей OCIO на уровень запуска (роль -> colorspace).
        // Канонический .ocio никогда не модифицируется: при запуске собирается
        // вариант конфига в %APPDATA%\FLOMASTER\variants\ и OCIO указывает на него.
        public Dictionary<string, string>? RoleOverrides { get; set; }
    }

    /// <summary>
    /// Именованный слепок состояния запуска: пресет, аргументы, OCIO-конфиг.
    /// Ссылки по имени (пресет/конфиг могут редактироваться после создания профиля).
    /// Переопределения ролей не копируются — едут вместе с пресетом.
    /// </summary>
    public class Profile
    {
        public string Name { get; set; } = "";
        public string PresetName { get; set; } = "";
        public string OcioName { get; set; } = "";
        public string Args { get; set; } = "";
    }

    public class ThemeColors
    {
        public string Name { get; set; } = "";
        public string Bg { get; set; } = "";
        public string Panel { get; set; } = "";
        public string Accent { get; set; } = "";
        public string Text { get; set; } = "";
        public string Dim { get; set; } = "";
        public string Border { get; set; } = "";
        // Text color on accent-colored surfaces (light accents need dark text). Default white.
        public string AccentText { get; set; } = "#FFFFFF";
    }

    /// <summary>
    /// Файл переноса настроек между машинами. Машино-специфичное (пути exe, пути OCIO)
    /// сознательно НЕ экспортируется: пресеты матчатся по имени, пути остаются локальными.
    /// </summary>
    public class SettingsExport
    {
        public string App { get; set; } = "FLOMASTER";
        public string ExportedAt { get; set; } = "";
        public string Theme { get; set; } = "";
        public bool AnimationEnabled { get; set; } = true;
        public bool TopMostEnabled { get; set; } = false;
        public bool CheckUpdates { get; set; } = true;
        /// <summary>Глобальный хоткей Ctrl+Alt+F: показать/спрятать лаунчер поверх всего.</summary>
        public bool HotkeyEnabled { get; set; } = true;
        public List<Profile> Profiles { get; set; } = new();
        public List<string> ProjectRoots { get; set; } = new();
    }
}
