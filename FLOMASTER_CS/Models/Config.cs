using System.Collections.Generic;

namespace FLOMASTER.Models
{
    public class Config
    {
        public string Theme { get; set; } = "blender";
        public List<OcioConfig> OcioConfigs { get; set; } = new();
        public string DefaultOcio { get; set; } = "ACES 1.2";
        public List<Preset> Presets { get; set; } = new();
        public List<string> RecentFiles { get; set; } = new();
        public List<string> ScanPaths { get; set; } = new();
        public bool AnimationEnabled { get; set; } = true;
        public bool TopMostEnabled { get; set; } = false;
        public bool CheckUpdates { get; set; } = true;
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
}
