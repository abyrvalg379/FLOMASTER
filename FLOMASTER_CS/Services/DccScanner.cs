using System.Collections.Generic;
using System.IO;
using System.Linq;
using FLOMASTER.Models;

namespace FLOMASTER.Services
{
    public static class DccScanner
    {
        public static List<Preset> Scan(List<string> customPaths = null)
        {
            var found = new List<Preset>();

            // Blender / K-Cycles
            ScanBlender(@"C:\Program Files\Blender Foundation", found);
            ScanBlender(@"C:\Program Files (x86)\Blender Foundation", found);
            ScanBlender(Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Programs", "Blender Foundation"), found);

            // Custom scan paths - recursive scan for known executables
            if (customPaths != null)
            {
                foreach (var path in customPaths)
                {
                    if (Directory.Exists(path))
                        ScanCustomPath(path, found);
                }
            }

            // Maya
            ScanMaya(found);

            // Houdini
            ScanHoudini(found);

            // Nuke
            ScanNuke(found);

            // Unreal Engine
            ScanUnrealEngine(found);

            // DaVinci Resolve
            var davinciExe = @"C:\Program Files\Blackmagic Design\DaVinci Resolve\Resolve.exe";
            if (File.Exists(davinciExe))
                found.Add(new() { Name = "DaVinci Resolve", Exe = davinciExe });

            // Substance 3D Painter
            var substanceExe = @"C:\Program Files\Adobe\Adobe Substance 3D Painter\Adobe Substance 3D Painter.exe";
            if (File.Exists(substanceExe))
                found.Add(new() { Name = "Substance 3D Painter", Exe = substanceExe });

            return found;
        }

        private static void ScanBlender(string basePath, List<Preset> found)
        {
            if (!Directory.Exists(basePath)) return;

            foreach (var dir in Directory.GetDirectories(basePath))
            {
                var exe = Path.Combine(dir, "blender.exe");
                if (File.Exists(exe))
                {
                    var name = Path.GetFileName(dir).Trim();
                    found.Add(new() { Name = name, Exe = exe });
                }
            }
        }

        private static void ScanMaya(List<Preset> found)
        {
            var mayaBase = @"C:\Program Files\Autodesk";
            if (!Directory.Exists(mayaBase)) return;

            foreach (var dir in Directory.GetDirectories(mayaBase, "Maya*"))
            {
                var exe = Path.Combine(dir, "bin", "maya.exe");
                if (File.Exists(exe))
                {
                    var year = Path.GetFileName(dir).Replace("Maya", "");
                    found.Add(new() { Name = $"Maya {year}", Exe = exe });
                }
            }
        }

        private static void ScanHoudini(List<Preset> found)
        {
            var houdiniBase = @"C:\Program Files\Side Effects Software";
            if (!Directory.Exists(houdiniBase)) return;

            foreach (var dir in Directory.GetDirectories(houdiniBase, "Houdini*"))
            {
                var exe = Path.Combine(dir, "bin", "houdini.exe");
                if (File.Exists(exe))
                {
                    var ver = Path.GetFileName(dir).Replace("Houdini ", "").Trim();
                    found.Add(new() { Name = $"Houdini {ver}", Exe = exe });
                }
            }
        }

        private static void ScanNuke(List<Preset> found)
        {
            var nukeBase = @"C:\Program Files";
            if (!Directory.Exists(nukeBase)) return;

            foreach (var dir in Directory.GetDirectories(nukeBase, "Nuke*"))
            {
                // Look for main Nuke exe (NukeXX.X.exe), exclude helpers
                var nukeExe = Directory.GetFiles(dir, "Nuke*.exe")
                    .Where(f => !Path.GetFileName(f).Contains("Init") &&
                                !Path.GetFileName(f).Contains("Assistant") &&
                                !Path.GetFileName(f).Contains("Register"))
                    .OrderByDescending(f => f.Length)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(nukeExe))
                {
                    var ver = Path.GetFileName(dir).Replace("Nuke ", "").Trim();
                    found.Add(new() { Name = $"Nuke {ver}", Exe = nukeExe });
                }
            }
        }

        private static void ScanUnrealEngine(List<Preset> found)
        {
            // Scan multiple possible UE locations
            var uePaths = new[]
            {
                @"C:\Program Files\Epic Games",
                @"D:\Unreal",
                @"D:\Epic Games",
                @"E:\Unreal",
                @"E:\Epic Games"
            };

            foreach (var ueBase in uePaths)
            {
                if (!Directory.Exists(ueBase)) continue;

                foreach (var dir in Directory.GetDirectories(ueBase))
                {
                    var exe = Path.Combine(dir, "Engine", "Binaries", "Win64", "UnrealEditor.exe");
                    if (File.Exists(exe))
                    {
                        var folderName = Path.GetFileName(dir);
                        var ver = folderName
                            .Replace("UE_", "")
                            .Replace("UE-", "")
                            .Replace("UnrealEngine-", "")
                            .Replace("Unreal Engine ", "")
                            .Trim();
                        if (!found.Any(f => f.Exe == exe))
                            found.Add(new() { Name = $"Unreal Engine {ver}", Exe = exe });
                    }
                }
            }
        }

        private static void ScanCustomPath(string basePath, List<Preset> found)
        {
            // Known DCC executables to look for
            var knownExes = new Dictionary<string, string>
            {
                { "blender.exe", "Blender" },
                { "maya.exe", "Maya" },
                { "houdini.exe", "Houdini" },
                { "UnrealEditor.exe", "Unreal Engine" },
                { "Resolve.exe", "DaVinci Resolve" },
                { "Nuke*.exe", "Nuke" }
            };

            try
            {
                foreach (var dir in Directory.GetDirectories(basePath))
                {
                    foreach (var (exePattern, appName) in knownExes)
                    {
                        var files = Directory.GetFiles(dir, exePattern, SearchOption.AllDirectories);
                        foreach (var exe in files)
                        {
                            if (!found.Any(f => f.Exe == exe))
                            {
                                var folderName = Path.GetFileName(dir);
                                found.Add(new() { Name = $"{appName} ({folderName})", Exe = exe });
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }
}
