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
    }
}
