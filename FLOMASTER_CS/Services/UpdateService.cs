using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FLOMASTER.Services
{
    /// <summary>Проверка и установка обновлений с GitHub Releases (только встроенные средства).</summary>
    public static class UpdateService
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
        private const string ReleasesApi = "https://api.github.com/repos/abyrvalg379/FLOMASTER/releases/latest";

        public static Version CurrentVersion =>
            Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

        /// <summary>Тег и URL на FLOMASTER.exe из вложений последнего релиза, если он новее текущего.</summary>
        public static async Task<(string? tag, string? exeUrl)> CheckAsync()
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("FLOMASTER");
            var json = await Http.GetStringAsync(ReleasesApi).ConfigureAwait(false);

            var tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([^\"]+)\"").Groups[1].Value;
            if (!Version.TryParse(tag, out var remote)) return (null, null);

            var cur = CurrentVersion;
            var local = new Version(Math.Max(cur.Major, 0), cur.Minor, cur.Build);
            if (remote <= local) return (null, null);

            // обновляем точечной заменой exe, поэтому качаем именно exe-вложение
            var url = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*/FLOMASTER\\.exe)\"").Groups[1].Value;
            return url == "" ? ((string?)null, (string?)null) : ("v" + tag, url);
        }

        public static async Task DownloadAsync(string url, string destPath, Action<long, long>? progress = null)
        {
            using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? -1;
            await using var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using var dst = File.Create(destPath);
            var buf = new byte[1 << 16];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false)) > 0)
            {
                await dst.WriteAsync(buf, 0, n).ConfigureAwait(false);
                done += n;
                progress?.Invoke(done, total);
            }
        }

        /// <summary>Убирает бэкап прошлой установки (best effort: Program Files без админа — просто лежит).</summary>
        public static void CleanupOldInstall()
        {
            try
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName;
                if (exe != null && File.Exists(exe + ".old")) File.Delete(exe + ".old");
            }
            catch { /* не критично */ }
        }

        /// <summary>
        /// Запускает повышенный (UAC) установщик: ждёт завершения приложения,
        /// заменяет exe, подчищает бэкап и стартует новую версию. Текущий процесс завершается.
        /// </summary>
        public static void ApplyDownloadedUpdate(string downloadedPath, string exePath)
        {
            var script =
                "$exe='" + exePath + "';" +
                "$new='" + downloadedPath + "';" +
                "Get-Process FLOMASTER -ErrorAction SilentlyContinue | Stop-Process -Force;" +
                "Start-Sleep 2;" +
                "Copy-Item $new $exe -Force;" +
                "Remove-Item $new -ErrorAction SilentlyContinue;" +
                "Start-Process $exe";
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

            var psi = new ProcessStartInfo("powershell",
                "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded)
            {
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true
            };
            Process.Start(psi);
        }
    }
}
