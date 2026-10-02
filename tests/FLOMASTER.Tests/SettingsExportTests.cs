using System.IO;
using System.Text.Json;
using FLOMASTER.Models;
using Xunit;

namespace FLOMASTER.Tests
{
    /// <summary>Файл переноса настроек: сериализация/чтение без потерь.</summary>
    public class SettingsExportTests
    {
        [Fact]
        public void RoundTrip_KeepsProfilesRootsTheme()
        {
            var export = new SettingsExport
            {
                ExportedAt = "2026-09-29T23:59:00",
                Theme = "houdini",
                AnimationEnabled = false,
                TopMostEnabled = true,
                CheckUpdates = false,
                Profiles = new()
                {
                    new Profile { Name = "Show A", PresetName = "Blender 5.2", OcioName = "ACES 1.2", Args = "--factory-startup" },
                    new Profile { Name = "SP bake", PresetName = "Substance 3D Painter", OcioName = "ACES 1.2", Args = "" }
                },
                ProjectRoots = new() { @"E:\0.Project\Work\Pipeline\STL", @"D:\projects" }
            };

            var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(export, options);
            var loaded = JsonSerializer.Deserialize<SettingsExport>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.Equal("houdini", loaded.Theme);
            Assert.False(loaded.AnimationEnabled);
            Assert.True(loaded.TopMostEnabled);
            Assert.False(loaded.CheckUpdates);
            Assert.Equal(2, loaded.Profiles.Count);
            Assert.Equal("Blender 5.2", loaded.Profiles[0].PresetName);
            Assert.Equal("--factory-startup", loaded.Profiles[0].Args);
            Assert.Equal(2, loaded.ProjectRoots.Count);
            Assert.Equal(@"D:\projects", loaded.ProjectRoots[1]);
        }
    }
}
