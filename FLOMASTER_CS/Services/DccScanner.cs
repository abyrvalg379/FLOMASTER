using System;
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
            Logger.Log("Scanner", "Starting DCC scan...", "info");

            // Blender / K-Cycles
            ScanBlender(@"C:\Program Files\Blender Foundation", found);
            ScanBlender(@"C:\Program Files (x86)\Blender Foundation", found);
            ScanBlender(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Blender Foundation"), found);

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
            {
                found.Add(new() { Name = "DaVinci Resolve", Exe = davinciExe });
                Logger.Log("Scanner", "Found: DaVinci Resolve", "info");
            }

            // Substance 3D Painter
            var substanceExe = @"C:\Program Files\Adobe\Adobe Substance 3D Painter\Adobe Substance 3D Painter.exe";
            if (File.Exists(substanceExe))
            {
                found.Add(new() { Name = "Substance 3D Painter", Exe = substanceExe });
                Logger.Log("Scanner", "Found: Substance 3D Painter", "info");
            }

            // Custom scan paths
            if (customPaths != null)
            {
                foreach (var path in customPaths)
                {
                    if (Directory.Exists(path))
                    {
                        Logger.Log("Scanner", $"Scanning custom path: {path}", "info");
                        ScanCustomPath(path, found);
                    }
                    else
                    {
                        Logger.Log("Scanner", $"Custom path not found: {path}", "warn");
                    }
                }
            }

            Logger.Log("Scanner", $"Scan complete: {found.Count} applications found", "info");
            return found;
        }

        private static void ScanBlender(string basePath, List<Preset> found)
        {
            if (!Directory.Exists(basePath)) return;

            try
            {
                foreach (var dir in Directory.GetDirectories(basePath))
                {
                    var exe = Path.Combine(dir, "blender.exe");
                    if (File.Exists(exe))
                    {
                        var name = Path.GetFileName(dir).Trim();
                        found.Add(new() { Name = name, Exe = exe });
                        Logger.Log("Scanner", $"Found: {name} at {exe}", "info");
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log("Scanner", $"Access denied scanning {basePath}: {ex.Message}", "warn");
            }
            catch (IOException ex)
            {
                Logger.Log("Scanner", $"IO error scanning {basePath}: {ex.Message}", "warn");
            }
        }

        private static void ScanMaya(List<Preset> found)
        {
            var mayaBase = @"C:\Program Files\Autodesk";
            if (!Directory.Exists(mayaBase)) return;

            try
            {
                foreach (var dir in Directory.GetDirectories(mayaBase, "Maya*"))
                {
                    var exe = Path.Combine(dir, "bin", "maya.exe");
                    if (File.Exists(exe))
                    {
                        var year = Path.GetFileName(dir).Replace("Maya", "").Trim();
                        var name = string.IsNullOrEmpty(year) ? "Maya" : $"Maya {year}";
                        found.Add(new() { Name = name, Exe = exe });
                        Logger.Log("Scanner", $"Found: {name}", "info");
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log("Scanner", $"Access denied scanning Maya: {ex.Message}", "warn");
            }
        }

        private static void ScanHoudini(List<Preset> found)
        {
            var houdiniBase = @"C:\Program Files\Side Effects Software";
            if (!Directory.Exists(houdiniBase)) return;

            try
            {
                foreach (var dir in Directory.GetDirectories(houdiniBase, "Houdini*"))
                {
                    var exe = Path.Combine(dir, "bin", "houdini.exe");
                    if (File.Exists(exe))
                    {
                        var ver = Path.GetFileName(dir).Replace("Houdini ", "").Trim();
                        found.Add(new() { Name = $"Houdini {ver}", Exe = exe });
                        Logger.Log("Scanner", $"Found: Houdini {ver}", "info");
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log("Scanner", $"Access denied scanning Houdini: {ex.Message}", "warn");
            }
        }

        private static void ScanNuke(List<Preset> found)
        {
            var nukeBase = @"C:\Program Files";
            if (!Directory.Exists(nukeBase)) return;

            try
            {
                foreach (var dir in Directory.GetDirectories(nukeBase, "Nuke*"))
                {
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
                        Logger.Log("Scanner", $"Found: Nuke {ver}", "info");
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log("Scanner", $"Access denied scanning Nuke: {ex.Message}", "warn");
            }
        }

        private static void ScanUnrealEngine(List<Preset> found)
        {
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

                try
                {
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
                            {
                                found.Add(new() { Name = $"Unreal Engine {ver}", Exe = exe });
                                Logger.Log("Scanner", $"Found: Unreal Engine {ver}", "info");
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    Logger.Log("Scanner", $"Access denied scanning {ueBase}: {ex.Message}", "warn");
                }
            }
        }

        private static void ScanCustomPath(string basePath, List<Preset> found)
        {
            var knownExes = new Dictionary<string, string>
            {
                { "blender.exe", "Blender" },
                { "maya.exe", "Maya" },
                { "houdini.exe", "Houdini" },
                { "UnrealEditor.exe", "Unreal Engine" },
                { "Resolve.exe", "DaVinci Resolve" }
            };

            try
            {
                foreach (var dir in Directory.GetDirectories(basePath))
                {
                    foreach (var (exeName, appName) in knownExes)
                    {
                        var exe = Path.Combine(dir, exeName);
                        if (File.Exists(exe) && !found.Any(f => f.Exe == exe))
                        {
                            var folderName = Path.GetFileName(dir);
                            found.Add(new() { Name = $"{appName} ({folderName})", Exe = exe });
                            Logger.Log("Scanner", $"Found: {appName} in {folderName}", "info");
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log("Scanner", $"Access denied scanning custom path: {ex.Message}", "warn");
            }
            catch (IOException ex)
            {
                Logger.Log("Scanner", $"IO error scanning custom path: {ex.Message}", "warn");
            }
        }
    }
}
