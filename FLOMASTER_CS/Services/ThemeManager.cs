using System.Collections.Generic;
using System.Windows.Media;
using FLOMASTER.Models;

namespace FLOMASTER.Services
{
    public static class ThemeManager
    {
        public static readonly Dictionary<string, ThemeColors> Themes = new()
        {
            ["blender"] = new()
            {
                Name = "Blender",
                Bg = "#161616",
                Panel = "#202020",
                Accent = "#E87D0D",
                Text = "#E0E0E0",
                Dim = "#858585",
                Border = "#303030"
            },
            ["maya"] = new()
            {
                Name = "Maya",
                Bg = "#1B232C",
                Panel = "#2A3642",
                Accent = "#3FD9E8",
                Text = "#EAF1F6",
                Dim = "#92A6B8",
                Border = "#3A4958"
            },
            ["houdini"] = new()
            {
                Name = "Houdini",
                Bg = "#1A181D",
                Panel = "#2C2A2E",
                Accent = "#FF4713",
                Text = "#E6E6E6",
                Dim = "#787779",
                Border = "#3D3B40"
            },
            ["nuke"] = new()
            {
                Name = "Nuke",
                Bg = "#262626",
                Panel = "#333333",
                Accent = "#C8C8C8",
                AccentText = "#1A1A1A",
                Text = "#E8E8E8",
                Dim = "#969696",
                Border = "#454545"
            },
            ["davinci"] = new()
            {
                Name = "DaVinci",
                Bg = "#121222",
                Panel = "#1E2340",
                Accent = "#FF4D6D",
                Text = "#EDEAF2",
                Dim = "#8E8CAB",
                Border = "#2C2C4A"
            },
            ["unreal"] = new()
            {
                Name = "Unreal",
                Bg = "#1C1F24",
                Panel = "#262A31",
                Accent = "#3D9BFF",
                Text = "#E0E0E0",
                Dim = "#8A919C",
                Border = "#33383F"
            },
            ["substance"] = new()
            {
                Name = "Substance",
                Bg = "#0D0F0D",
                Panel = "#161A16",
                Accent = "#76B900",
                Text = "#E0E0E0",
                Dim = "#8FA08F",
                Border = "#262B26"
            }
        };

        public static readonly List<string> ThemeOrder = new()
        {
            "blender", "maya", "houdini", "nuke", "davinci",
            "unreal", "substance"
        };

        public static ThemeColors GetTheme(string key)
        {
            return Themes.TryGetValue(key, out var theme) ? theme : Themes["blender"];
        }

        public static SolidColorBrush Brush(string color)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(color);
        }
    }
}
