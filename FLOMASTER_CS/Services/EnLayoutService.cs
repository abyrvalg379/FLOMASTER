using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using FLOMASTER.ViewModels;

namespace FLOMASTER.Services
{
    /// <summary>
    /// Управление фоновым сторожем раскладки DccEnLayout (опция «Auto EN keyboard layout»):
    /// извлекает вшитый в сборку exe в %LOCALAPPDATA%\FLOMASTER, ведёт автозапуск (HKCU Run)
    /// и процесс. Сторож живёт независимо от лаунчера — Dispose только отписывается.
    /// Список процессов и механика переключения — tools\DccEnLayout\DccEnLayout.cs.
    /// </summary>
    public class EnLayoutService : IDisposable
    {
        private const string ExeName = "DccEnLayout";
        private const string LegacyExeName = "HoudiniEnLayout"; // standalone-версия до встройки в лаунчер
        private readonly MainViewModel _vm;
        private bool _disposed;

        private static string TargetDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FLOMASTER");
        private static string TargetPath => Path.Combine(TargetDir, ExeName + ".exe");

        public EnLayoutService(MainViewModel vm)
        {
            _vm = vm;
            _vm.PropertyChanged += OnVmPropertyChanged;
            // старт: привести машину к конфигу (вкл — освежить exe и поднять, выкл — прибрать)
            Apply(_vm.EnLayoutEnabled);
        }

        private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.EnLayoutEnabled))
                Apply(_vm.EnLayoutEnabled);
        }

        private void Apply(bool enabled)
        {
            try
            {
                RemoveLegacy(); // двойной сторож не нужен: одиночная инсталляция прибирается тихо
                if (enabled) Enable();
                else Disable();
            }
            catch (Exception ex)
            {
                Logger.Log("EnLayout", $"apply {enabled} failed: {ex.Message}", "error");
                _vm.StatusText = "EN layout guard: error (see log)";
            }
        }

        private void Enable()
        {
            var bytes = ReadEmbeddedExe();
            if (bytes == null)
            {
                Logger.Log("EnLayout", "embedded DccEnLayout.exe not found", "error");
                _vm.StatusText = "EN layout guard: broken build (no embedded exe)";
                return;
            }

            Directory.CreateDirectory(TargetDir);
            // перезапись только при изменении байтов: бессмысленный рестарт бойкого сторожа не нужен
            var changed = !File.Exists(TargetPath) || !File.ReadAllBytes(TargetPath).SequenceEqual(bytes);
            if (changed)
            {
                KillProcess(ExeName); // exe может быть запущен — файл залочен
                File.WriteAllBytes(TargetPath, bytes);
            }

            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                key?.SetValue(ExeName, $"\"{TargetPath}\"");

            if (Process.GetProcessesByName(ExeName).Length == 0)
                Process.Start(new ProcessStartInfo(TargetPath) { UseShellExecute = true });

            Logger.Log("EnLayout",
                $"guard enabled ({(changed ? $"deployed {bytes.Length} b" : "up to date")})", "info");
        }

        private void Disable()
        {
            KillProcess(ExeName);
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                key?.DeleteValue(ExeName, false);
            if (File.Exists(TargetPath)) File.Delete(TargetPath);
            Logger.Log("EnLayout", "guard disabled", "info");
        }

        /// <summary>Standalone-инсталляции сторожа (HoudiniEnLayout и DccEnLayout из
        /// переносимого пакета): собственные HKCU Run-значения и папки в %LOCALAPPDATA%.
        /// Убить процесс и снять автозапуск, иначе два сторожа дерутся за одну раскладку.
        /// Run-значение у standalone-DccEnLayout то же имя — перетирается при Enable,
        /// здесь остаётся только папка.</summary>
        private static void RemoveLegacy()
        {
            KillProcess(LegacyExeName);
            // standalone-экземпляры DccEnLayout (запущены не из нашего %LOCALAPPDATA%\FLOMASTER)
            foreach (var p in Process.GetProcessesByName(ExeName))
                try { if (!string.Equals(p.MainModule?.FileName, TargetPath, StringComparison.OrdinalIgnoreCase)) p.Kill(); }
                catch { /* MainModule недоступен — не трогаем */ }
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                key?.DeleteValue(LegacyExeName, false);
                // значение DccEnLayout, указывающее вне нашей папки, — standalone: убрать
                // (Enable ниже перезапишет его каноническим путём)
                var v = key?.GetValue(ExeName) as string;
                if (v != null && !v.Contains(TargetDir, StringComparison.OrdinalIgnoreCase))
                    key?.DeleteValue(ExeName, false);
            }
            TryDeleteDirectory(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LegacyExeName));
            TryDeleteDirectory(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ExeName));
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch { /* залочено логом/процессом — не критично, автозапуска уже нет */ }
        }

        private static void KillProcess(string name)
        {
            foreach (var p in Process.GetProcessesByName(name))
                try { p.Kill(); } catch { /* уже умер */ }
        }

        private static byte[]? ReadEmbeddedExe()
        {
            try
            {
                var sri = Application.GetResourceStream(
                    new Uri("pack://application:,,,/Resources/DccEnLayout.exe"));
                if (sri == null) return null;
                using (sri.Stream)
                using (var ms = new MemoryStream())
                {
                    sri.Stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch { return null; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }
    }
}
