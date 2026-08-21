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
                Bg = "#303030",
                Panel = "#383838",
                Accent = "#E87D0D",
                Text = "#E0E0E0",
                Dim = "#999999",
                Border = "#4A4A4A"
            },
            ["maya"] = new()
            {
                Name = "Maya",
                Bg = "#3D3D4E",
                Panel = "#4A4A5A",
                Accent = "#6EC6D4",
                Text = "#E0E0E0",
                Dim = "#999999",
                Border = "#555568"
            },
            ["houdini"] = new()
            {
                Name = "Houdini",
                Bg = "#2B2B2B",
                Panel = "#383838",
                Accent = "#F9A028",
                Text = "#E0E0E0",
                Dim = "#999999",
                Border = "#4A4A4A"
            },
            ["nuke"] = new()
            {
                Name = "Nuke",
                Bg = "#3B3B3B",
                Panel = "#454545",
                Accent = "#8CC63F",
                Text = "#E0E0E0",
                Dim = "#999999",
                Border = "#555555"
            },
            ["davinci"] = new()
            {
                Name = "DaVinci",
                Bg = "#1A1A2E",
                Panel = "#16213E",
                Accent = "#E94560",
                Text = "#E0E0E0",
                Dim = "#7A7A9A",
                Border = "#2A2A44"
            }
        };

        public static readonly List<string> ThemeOrder = new()
        {
            "blender", "maya", "houdini", "nuke", "davinci"
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
