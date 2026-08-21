using System;
using System.Collections.Generic;
using System.IO;

namespace FLOMASTER.Services
{
    public static class Logger
    {
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flomaster.log");

        public static void Log(string appName, string exePath, string ocioName, string args)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var user = Environment.UserName;
            var argsStr = string.IsNullOrEmpty(args) ? "-" : args;
            var line = $"{timestamp}|{user}|{appName}|{ocioName}|{argsStr}|{exePath}";

            try
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch { }
        }

        public static List<string> GetLastEntries(int count = 30)
        {
            if (File.Exists(LogPath))
            {
                var lines = File.ReadAllLines(LogPath);
                return lines.Length > count ? new List<string>(lines[^count..]) : new List<string>(lines);
            }
            return new();
        }
    }
}
