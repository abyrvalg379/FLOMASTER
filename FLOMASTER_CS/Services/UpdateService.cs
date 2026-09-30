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

        /// <summary>
        /// Чистое сравнение: тег релиза (v2.4.1 / 2.4.1) новее локальной версии?
        /// Сравнение по Major.Minor.Build (ревизия сборки не участвует).
        /// Неразборчивый тег — обновления нет.
        /// </summary>
        private static Version Normalize3(Version v)
        {
            // "2.5" и "2" парсятся с Build/Minor = -1 — добиваем нулями до трёх компонентов
            if (v.Build < 0) return new Version(v.Major, Math.Max(v.Minor, 0), 0);
            return new Version(Math.Max(v.Major, 0), v.Minor, v.Build);
        }

        public static bool IsUpdateAvailable(string tag, Version current)
        {
            var clean = (tag ?? "").TrimStart('v');
            if (!Version.TryParse(clean, out var parsed)) return false;

            // обе стороны режутся до Major.Minor.Build — ревизия сборки не участвует
            var remote = Normalize3(parsed);
            var local = Normalize3(current);
            return remote > local;
        }

        /// <summary>
        /// Тег и URL на обновление из вложений последнего релиза, если он новее текущего.
        /// Качаем версионированный ZIP (exe + ocio): точечная замена одного exe оставляла
        /// машины без папки ocio — SP запускался без env var молча (реальный кейс, 01.10).
        /// Fallback на exe-вложение, если ZIP в релизе нет.
        /// </summary>
        public static async Task<(string? tag, string? url, bool isZip)> CheckAsync()
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("FLOMASTER");
            var json = await Http.GetStringAsync(ReleasesApi).ConfigureAwait(false);

            var tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([^\"]+)\"").Groups[1].Value;
            if (!IsUpdateAvailable(tag, CurrentVersion)) return (null, null, false);

            var (url, isZip) = SelectUpdateAsset(json);
            return url == null ? (null, null, false) : ("v" + tag, url, isZip);
        }

        /// <summary>
        /// Выбор вложения обновления: версионированный ZIP (FLOMASTER_v*.zip) важнее exe.
        /// Автобандл FLOMASTER_Windows.zip и source-архивы (…/archive/refs/tags/*.zip) не берём.
        /// </summary>
        public static (string? url, bool isZip) SelectUpdateAsset(string releaseJson)
        {
            var zip = Regex.Match(releaseJson, "\"browser_download_url\"\\s*:\\s*\"([^\"]*/FLOMASTER_v[^\"]*\\.zip)\"");
            if (zip.Success) return (zip.Groups[1].Value, true);

            var exe = Regex.Match(releaseJson, "\"browser_download_url\"\\s*:\\s*\"([^\"]*/FLOMASTER\\.exe)\"").Groups[1].Value;
            return exe == "" ? ((string?)null, false) : (exe, false);
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
        /// Запускает повышенный (UAC) установщик: ждёт завершения приложения, заменяет exe,
        /// синхронизирует папку ocio (из ZIP-вложения), подчищает бэкап и стартует новую версию.
        /// Текущий процесс завершается. НОВАЯ версия стартуется через explorer.exe, а не напрямую:
        /// PowerShell под UAC elevated, и прямой Start-Process оставил бы FLOMASTER с правами
        /// админа — все запущенные из него DCC наследуют admin-токен (вылеты Painter, блок drag&drop).
        /// explorer.exe форвардит запрос уже запущенному шеллу (medium IL) — ребёнок без повышения.
        /// </summary>
        public static void ApplyDownloadedUpdate(string downloadedPath, string exePath)
        {
            var isZip = downloadedPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            var extractDir = Path.Combine(Path.GetTempPath(), "FLOMASTER_update");

            // robocopy /MIR делает target идентичным source: подхватывают и обновления канон-конфига
            string body;
            if (isZip)
            {
                body =
                    "$tmp=" + Ps(extractDir) + ";" +
                    "if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force };" +
                    "Expand-Archive " + Ps(downloadedPath) + " $tmp -Force;" +
                    "Copy-Item (Join-Path $tmp 'FLOMASTER.exe') $exe -Force;" +
                    "robocopy (Join-Path $tmp 'ocio') (Join-Path (Split-Path $exe) 'ocio') /MIR /NFL /NDL /NJH /NJS /NP;" +
                    "Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue;" +
                    "Remove-Item $zip -ErrorAction SilentlyContinue;";
            }
            else
            {
                // запасной путь: в релизе нет ZIP — точечная замена exe как раньше
                body = "Copy-Item $new $exe -Force;" +
                       "Remove-Item $new -ErrorAction SilentlyContinue;";
            }

            var script =
                "$exe=" + Ps(exePath) + ";" +
                "$zip=" + Ps(downloadedPath) + ";" +
                "$new=" + Ps(downloadedPath) + ";" +
                "Get-Process FLOMASTER -ErrorAction SilentlyContinue | Stop-Process -Force;" +
                "Start-Sleep 2;" +
                body +
                "explorer.exe $exe";
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

        /// <summary>PS-одинарные кавычки: пути с пробелами/кириллицей/апострофами не рвут скрипт.</summary>
        private static string Ps(string s) => "'" + (s ?? "").Replace("'", "''") + "'";
    }
}
