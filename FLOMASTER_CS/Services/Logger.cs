using System;
using System.Collections.Generic;
using System.IO;

namespace FLOMASTER.Services
{
    public static class Logger
    {
        private static readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FLOMASTER");
        private static readonly string LogPath = Path.Combine(LogDir, "flomaster.log");

        // Legacy method (backward compatibility)
        public static void Log(string appName, string exePath, string ocioName, string args)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var user = Environment.UserName;
            var argsStr = string.IsNullOrEmpty(args) ? "-" : Sanitize(args);
            var line = $"{timestamp}|{user}|LAUNCH|{Sanitize(appName)}|{ocioName}|{argsStr}|{Sanitize(exePath)}";
            AppendLine(line);
        }

        // System log with level
        public static void Log(string source, string message, string level = "info")
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var line = $"{timestamp}|{level.ToUpper()}|{Sanitize(source)}|{Sanitize(message)}";
            AppendLine(line);
        }

        // Process exit code log
        public static void LogProcessExit(string appName, int exitCode, string exePath)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var status = exitCode == 0 ? "SUCCESS" : "FAILED";
            var line = $"{timestamp}|PROCESS|{Sanitize(appName)}|exit={exitCode}|{status}|{Sanitize(exePath)}";
            AppendLine(line);
        }

        public static List<string> GetLastEntries(int count = 30)
        {
            try
            {
                if (File.Exists(LogPath))
                {
                    var lines = File.ReadAllLines(LogPath);
                    return lines.Length > count ? new List<string>(lines[^count..]) : new List<string>(lines);
                }
            }
            catch { }
            return new();
        }

        private static void AppendLine(string line)
        {
            try
            {
                if (!Directory.Exists(LogDir))
                    Directory.CreateDirectory(LogDir);
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch { }
        }

        private static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return "-";
            return input.Replace("|", "/").Replace("\r", "").Replace("\n", " ");
        }
    }
}
