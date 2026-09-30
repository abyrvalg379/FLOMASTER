using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace FLOMASTER.Services
{
    /// <summary>
    /// Самолечение elevated-запуска. FLOMASTER, перезапущенный апдейтером СТАРОЙ версии
    /// (установщик выполняется из elevated PowerShell), остаётся с правами админа —
    /// все запущенные из него DCC наследуют admin-токен (вылеты Painter, блок drag&drop).
    /// Лечение: перезапуск себя через explorer.exe — тот форвардит запрос уже запущенному
    /// шеллу (medium IL), и ребёнок стартует без повышения.
    /// </summary>
    public static class ElevationGuard
    {
        private const int MarkerMaxAgeSeconds = 60;
        private static string MarkerPath => Path.Combine(Path.GetTempPath(), "FLOMASTER_delev.marker");

        public static bool IsElevated() =>
            new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        /// <summary>
        /// Если процесс elevated — перезапускает себя без повышения.
        /// </summary>
        /// <returns>true — перезапущены, вызывающий обязан завершить текущий процесс.
        /// false — запуск не elevated, перезапуск не удался или уже пытались (гвард цикла).</returns>
        public static bool EnsureNotElevated()
        {
            if (!IsElevated()) return false;

            // если только что перезапускались и мы ВСЁ ЕЩЁ elevated (нестандартный
            // elevated-шелл) — не зацикливаемся, работаем как есть
            if (IsMarkerFresh(MarkerPath, DateTime.Now))
            {
                Logger.Log("APP", "Still elevated after de-elevation attempt, running as-is", "warn");
                return false;
            }

            try
            {
                File.WriteAllText(MarkerPath, DateTime.Now.ToString("s"));
                var exe = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exe)) throw new InvalidOperationException("main module path unknown");
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{exe}\"") { UseShellExecute = true });
                Logger.Log("APP", "Running elevated: relaunching de-elevated via explorer", "warn");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log("APP", $"De-elevation relaunch failed: {ex.Message}, running elevated", "warn");
                return false;
            }
        }

        public static bool IsMarkerFresh(string path, DateTime now) =>
            File.Exists(path) &&
            (now - File.GetLastWriteTime(path)).TotalSeconds < MarkerMaxAgeSeconds;
    }
}
