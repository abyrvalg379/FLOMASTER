using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FLOMASTER.Models;

namespace FLOMASTER.Services
{
    public static class OcioService
    {
        public static OcioConfig GetActiveOcio(Config config, object selectedItem)
        {
            if (selectedItem is OcioConfig ocio)
            {
                if (!string.IsNullOrEmpty(ocio.Path) && File.Exists(ocio.Path))
                    return ocio;
                Logger.Log("OCIO", $"OCIO path not found: {ocio.Path}", "warn");
            }
            return null;
        }

        public static void ApplyOcio(ProcessStartInfo psi, OcioConfig ocio, string exePath)
        {
            if (ocio == null || string.IsNullOrEmpty(ocio.Path))
            {
                Logger.Log("OCIO", "No OCIO config selected, launching without OCIO", "warn");
                return;
            }

            var isUnreal = exePath.ToLower().Contains("unreal");

            if (isUnreal)
            {
                var ocioArg = $"-ocio=\"{ocio.Path}\"";
                psi.Arguments = string.IsNullOrEmpty(psi.Arguments)
                    ? ocioArg
                    : $"{ocioArg} {psi.Arguments}";
                Logger.Log("OCIO", $"UE mode: added arg {ocioArg}", "info");
            }
            else
            {
                psi.EnvironmentVariables["OCIO"] = ocio.Path;
                Logger.Log("OCIO", $"Set OCIO={ocio.Path}", "info");
            }
        }

        public static bool AddOcioConfig(Config config, string name, string path)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
            {
                Logger.Log("OCIO", "Add failed: name or path is empty", "warn");
                return false;
            }

            if (!File.Exists(path))
            {
                Logger.Log("OCIO", $"Add failed: file not found: {path}", "warn");
                return false;
            }

            if (config.OcioConfigs.Any(o => o.Path == path))
            {
                Logger.Log("OCIO", $"Add failed: config already exists: {path}", "warn");
                return false;
            }

            config.OcioConfigs.Add(new() { Name = name, Path = path });
            Logger.Log("OCIO", $"Added config: {name} at {path}", "info");
            return true;
        }

        public static bool RemoveOcioConfig(Config config, OcioConfig selected)
        {
            if (selected == null) return false;

            if (config.OcioConfigs.Count <= 1)
            {
                Logger.Log("OCIO", "Cannot remove last OCIO config", "warn");
                return false;
            }

            config.OcioConfigs.RemoveAll(o => o.Name == selected.Name);

            if (config.DefaultOcio == selected.Name && config.OcioConfigs.Count > 0)
            {
                config.DefaultOcio = config.OcioConfigs[0].Name;
                Logger.Log("OCIO", $"Default OCIO changed to: {config.DefaultOcio}", "info");
            }

            Logger.Log("OCIO", $"Removed config: {selected.Name}", "info");
            return true;
        }

        public static OcioConfig GetDefaultOcio(Config config)
        {
            return config.OcioConfigs.FirstOrDefault(o => o.Name == config.DefaultOcio);
        }
    }
}
