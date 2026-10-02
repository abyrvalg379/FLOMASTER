using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FLOMASTER.Models;

namespace FLOMASTER.Services
{
    /// <summary>Чужой файл синка, который ещё не обработан этой машиной.</summary>
    public class SyncCandidate
    {
        public string FileName { get; init; } = "";
        public string Path { get; init; } = "";
        public string Label { get; init; } = "";   // MachineName из файла или имя файла
        public string Hash { get; init; } = "";
    }

    /// <summary>
    /// Папка-синк v2: каждая машина пишет свой FLOMASTER_&lt;MachineName&gt;.flomaster
    /// (формат SettingsExport, путь машино-специфичный — в payload не едет), чужие файлы
    /// предлагаются к импорту. Push — атомарный (tmp + move: облаку не уедет полфайла).
    /// Scan — по факту (старт/открытие дашборда), без FileSystemWatcher.
    /// </summary>
    public static class SyncService
    {
        private static readonly JsonSerializerOptions WriteOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static readonly JsonSerializerOptions ReadOpts = new() { PropertyNameCaseInsensitive = true };

        public static string OwnFileName() => FileNameFor(Environment.MachineName);

        /// <summary>Имя файла машины: только ASCII-буквы/цифры/-/_ (не-ASCII в имени файла
        /// папки облачного синка — тот же класс риска, что не-ASCII путь для OCIO), прочее → '-'.</summary>
        public static string FileNameFor(string machineName)
        {
            bool Safe(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_';
            var clean = new string((machineName ?? "pc").Select(c => Safe(c) ? c : '-').ToArray()).Trim('-');
            if (clean.Length == 0) clean = "pc";
            return $"FLOMASTER_{clean}.flomaster";
        }

        /// <summary>SettingsExport → JSON (единый источник формата для ручного Export и пуша в синк).</summary>
        public static string BuildPayload(SettingsExport export) =>
            JsonSerializer.Serialize(export, WriteOpts);

        public static SettingsExport? Parse(string json)
        {
            try
            {
                var export = JsonSerializer.Deserialize<SettingsExport>(json, ReadOpts);
                return export?.App == "FLOMASTER" ? export : null;
            }
            catch
            {
                return null;
            }
        }

        public static string Hash8(string content) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)))[..8].ToLowerInvariant();

        /// <summary>Своё состояние в папку синка. tmp + move — облако не увидит полфайла.</summary>
        public static void Push(string folder, string payload)
        {
            var target = Path.Combine(folder, OwnFileName());
            var tmp = target + ".tmp";
            File.WriteAllText(tmp, payload);
            File.Move(tmp, target, overwrite: true);
        }

        /// <summary>
        /// Чужие .flomaster, которых нет в seen или чей hash изменился. Свой файл,
        /// нечитаемые и уже обработанные — мимо.
        /// </summary>
        public static List<SyncCandidate> Scan(string folder, string ownFileName, Dictionary<string, string> seen)
        {
            var result = new List<SyncCandidate>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return result;

            foreach (var path in Directory.GetFiles(folder, "*.flomaster"))
            {
                var fileName = Path.GetFileName(path);
                if (string.Equals(fileName, ownFileName, StringComparison.OrdinalIgnoreCase)) continue;

                string json;
                try { json = File.ReadAllText(path); }
                catch { continue; } // облако ещё качает / залочен — предложим в следующий проход

                var hash = Hash8(json);
                if (seen.TryGetValue(fileName, out var known) && known == hash) continue;

                var export = Parse(json);
                if (export == null) continue; // не наш формат — не предлагаем

                result.Add(new SyncCandidate
                {
                    FileName = fileName,
                    Path = path,
                    Label = string.IsNullOrEmpty(export.MachineName) ? fileName : export.MachineName,
                    Hash = hash
                });
            }
            return result.OrderBy(c => c.Label, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
