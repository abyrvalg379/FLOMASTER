using System;
using System.Collections.Generic;
using System.IO;
using FLOMASTER.Models;
using FLOMASTER.Services;
using Xunit;

namespace FLOMASTER.Tests
{
    /// <summary>Герметичные тесты хранилища конфига: temp-папка, битый JSON, аддитивность.</summary>
    public class ConfigManagerTests : IDisposable
    {
        private readonly string _dir;

        public ConfigManagerTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "flm_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        private string ConfigPath => Path.Combine(_dir, "launcher_config.json");

        [Fact]
        public void Load_NoFile_CreatesDefaultAndSaves()
        {
            var store = new ConfigManager(_dir);
            var config = store.Load();

            Assert.NotNull(config);
            Assert.Equal("blender", config.Theme);
            Assert.True(File.Exists(ConfigPath)); // дефолт сразу сохранён
        }

        [Fact]
        public void Load_BrokenJson_BackupsAndReturnsDefault()
        {
            File.WriteAllText(ConfigPath, "{ not valid json at all");

            var store = new ConfigManager(_dir);
            var config = store.Load();

            Assert.Equal("blender", config.Theme);
            var backups = Directory.GetFiles(_dir, "launcher_config.json.backup_*");
            Assert.NotEmpty(backups);
        }

        [Fact]
        public void Load_NullJson_ReturnsDefault()
        {
            File.WriteAllText(ConfigPath, "null");

            var config = new ConfigManager(_dir).Load();
            Assert.NotNull(config);
            Assert.Equal("blender", config.Theme);
        }

        [Fact]
        public void SaveLoad_RoundTripsNewFields()
        {
            var config = new Config
            {
                Theme = "houdini",
                AnimationEnabled = false,
                TopMostEnabled = true,
                CheckUpdates = false,
                ProjectRoots = new() { @"E:\0.Project\Work\VVERH\STL" },
                RecentFiles = new() { @"E:\somewhere\shot_042.blend" },
                Profiles = new()
                {
                    new Profile { Name = "Show A", PresetName = "Blender 5.2", OcioName = "ACES 1.2", Args = "--factory-startup" }
                },
                Presets = new()
                {
                    new Preset
                    {
                        Name = "Blender 5.2",
                        Exe = @"C:\apps\blender.exe",
                        RoleOverrides = new Dictionary<string, string> { { "scene_linear", "acescg" } }
                    }
                }
            };

            var store = new ConfigManager(_dir);
            store.Save(config);
            var loaded = store.Load();

            Assert.Equal("houdini", loaded.Theme);
            Assert.False(loaded.AnimationEnabled);
            Assert.True(loaded.TopMostEnabled);
            Assert.False(loaded.CheckUpdates);
            Assert.Equal(@"E:\0.Project\Work\VVERH\STL", loaded.ProjectRoots[0]);
            Assert.Single(loaded.Profiles);
            Assert.Equal("Show A", loaded.Profiles[0].Name);
            Assert.Equal("--factory-startup", loaded.Profiles[0].Args);
            Assert.Single(loaded.Presets);
            Assert.Equal("acescg", loaded.Presets[0].RoleOverrides["scene_linear"]);
            Assert.Equal(@"E:\somewhere\shot_042.blend", loaded.RecentFiles[0]);
        }

        [Fact]
        public void SaveLoad_OldConfigWithoutNewKeys_LoadsAsEmpty()
        {
            // конфиг времён v2.2: без profiles/projectRoots/checkUpdates — аддитивность
            File.WriteAllText(ConfigPath,
                "{\n  \"theme\": \"maya\",\n  \"presets\": [],\n  \"recentFiles\": [],\n  \"scanPaths\": []\n}");

            var loaded = new ConfigManager(_dir).Load();

            Assert.Equal("maya", loaded.Theme);
            Assert.Empty(loaded.Profiles);
            Assert.Empty(loaded.ProjectRoots);
            Assert.True(loaded.CheckUpdates); // initializer-дефолт
        }

        [Fact]
        public void Load_EmptyOcioPath_FixedFromBaseDirectoryAndPersisted()
        {
            // регресс: сидинг без ocio рядом с exe дал Path = null, NormalizePaths его
            // не чинил — «ACES 1.2» вечно запускалась без OCIO (кейс стороннего пользователя)
            var ocioDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocio");
            var ocioFile = Path.Combine(ocioDir, "config.ocio");
            bool created = !File.Exists(ocioFile);
            Directory.CreateDirectory(ocioDir);
            if (created) File.WriteAllText(ocioFile, "ocio_profile_version: 1\nroles:\n  scene_linear: raw\n");

            try
            {
                File.WriteAllText(ConfigPath,
                    "{\n  \"theme\": \"maya\",\n  \"ocioConfigs\": [ { \"name\": \"ACES 1.2\", \"path\": null } ],\n  \"defaultOcio\": \"ACES 1.2\",\n  \"presets\": [],\n  \"recentFiles\": [],\n  \"scanPaths\": []\n}");

                var loaded = new ConfigManager(_dir).Load();

                Assert.False(string.IsNullOrEmpty(loaded.OcioConfigs[0].Path));

                // починка персистится: в json на диске путь уже не null
                var json = File.ReadAllText(ConfigPath);
                Assert.DoesNotContain("null", json);
            }
            finally
            {
                if (created) try { Directory.Delete(ocioDir, true); } catch { }
            }
        }
    }
}
