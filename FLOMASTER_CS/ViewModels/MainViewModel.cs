using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using FLOMASTER.Models;
using FLOMASTER.Services;

namespace FLOMASTER.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private Config _config;
        private string _statusText = "Ready";
        private string _argsText = "";
        private Preset _selectedPreset;
        private OcioConfig _selectedOcio;
        private string _selectedTheme;
        private bool _recentPanelVisible;
        private bool _argsPanelVisible;
        private bool _settingsPanelVisible;
        private bool _autoStartEnabled;
        private bool _topMostEnabled;
        private bool _animationEnabled;
        private OcioConfig _defaultOcio;

        // Collections
        public ObservableCollection<Preset> Presets { get; } = new();
        public ObservableCollection<OcioConfig> OcioConfigs { get; } = new();
        public ObservableCollection<string> LogEntries { get; } = new();
        public ObservableCollection<string> RecentFiles { get; } = new();

        // Commands
        public ICommand LaunchCommand { get; private set; }
        public ICommand AddPresetCommand { get; private set; }
        public ICommand AddOcioCommand { get; private set; }
        public ICommand RemoveOcioCommand { get; private set; }
        public ICommand ClearArgsCommand { get; private set; }
        public ICommand AddScanPathCommand { get; private set; }
        public ICommand RescanCommand { get; private set; }
        public ICommand ViewLogCommand { get; private set; }
        public ICommand CreateShortcutCommand { get; private set; }
        public ICommand CreateDesktopShortcutCommand { get; private set; }
        public ICommand OpenRecentFileCommand { get; private set; }
        public ICommand QuickCommandCommand { get; private set; }
        public ICommand ClearRecentCommand { get; private set; }

        public MainViewModel()
        {
            _config = ConfigManager.Load();
            Logger.Log("ViewModel", "Config loaded", "info");

            if (_config.Presets.Count == 0)
                RescanApps();

            RefreshPresets();
            RefreshOcioConfigs();
            RefreshRecentFiles();

            // Init themes
            foreach (var key in ThemeManager.ThemeOrder)
                Themes.Add(ThemeManager.Themes[key].Name);

            // Init commands
            LaunchCommand = new RelayCommand(_ => LaunchSelectedApp());
            AddPresetCommand = new RelayCommand(_ => AddPreset());
            AddOcioCommand = new RelayCommand(_ => AddOcioConfig());
            RemoveOcioCommand = new RelayCommand(_ => RemoveOcioConfig(), _ => OcioConfigs.Count > 1);
            ClearArgsCommand = new RelayCommand(_ => ArgsText = "");
            AddScanPathCommand = new RelayCommand(_ => AddScanPath());
            RescanCommand = new RelayCommand(_ => RescanApps());
            ViewLogCommand = new RelayCommand(_ => RefreshLogEntries());
            CreateShortcutCommand = new RelayCommand(_ => CreateShortcut());
            CreateDesktopShortcutCommand = new RelayCommand(_ => CreateDesktopShortcut());
            OpenRecentFileCommand = new RelayCommand<string>(file => OpenRecentFile(file));
            QuickCommandCommand = new RelayCommand<string>(cmd => AddQuickCommand(cmd));
            ClearRecentCommand = new RelayCommand(_ => ClearRecentFiles());

            // Init state
            _selectedTheme = ThemeManager.GetTheme(_config.Theme).Name;
            _topMostEnabled = _config.TopMostEnabled;
            _animationEnabled = _config.AnimationEnabled;
            _autoStartEnabled = IsAutoStartEnabled();

            if (OcioConfigs.Count > 0)
                _selectedOcio = OcioConfigs.FirstOrDefault();
            if (Presets.Count > 0)
                SelectedPreset = Presets.FirstOrDefault();

            StatusText = "Ready";
        }

        // ============ PROPERTIES ============

        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
        public string ArgsText { get => _argsText; set => SetProperty(ref _argsText, value); }
        public Preset SelectedPreset { get => _selectedPreset; set { if (SetProperty(ref _selectedPreset, value)) RefreshQuickCommands(); } }
        public OcioConfig SelectedOcio { get => _selectedOcio; set => SetProperty(ref _selectedOcio, value); }
        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (SetProperty(ref _selectedTheme, value) && value != null)
                {
                    // Find theme key from name
                    foreach (var key in ThemeManager.ThemeOrder)
                    {
                        if (ThemeManager.Themes[key].Name == value)
                        {
                            _config.Theme = key;
                            ConfigManager.Save(_config);
                            Logger.Log("Theme", $"Changed to: {value}", "info");
                            break;
                        }
                    }
                }
            }
        }
        public bool RecentPanelVisible { get => _recentPanelVisible; set => SetProperty(ref _recentPanelVisible, value); }
        public bool ArgsPanelVisible { get => _argsPanelVisible; set => SetProperty(ref _argsPanelVisible, value); }
        public bool SettingsPanelVisible { get => _settingsPanelVisible; set => SetProperty(ref _settingsPanelVisible, value); }
        public bool AutoStartEnabled
        {
            get => _autoStartEnabled;
            set
            {
                if (SetProperty(ref _autoStartEnabled, value))
                {
                    SetAutoStart(value);
                    StatusText = value ? "Auto-start enabled" : "Auto-start disabled";
                }
            }
        }

        public bool TopMostEnabled
        {
            get => _topMostEnabled;
            set
            {
                if (SetProperty(ref _topMostEnabled, value))
                {
                    _config.TopMostEnabled = value;
                    ConfigManager.Save(_config);
                    StatusText = value ? "Always on top" : "Normal mode";
                }
            }
        }

        public bool AnimationEnabled
        {
            get => _animationEnabled;
            set
            {
                if (SetProperty(ref _animationEnabled, value))
                {
                    _config.AnimationEnabled = value;
                    ConfigManager.Save(_config);
                }
            }
        }
        public OcioConfig DefaultOcio { get => _defaultOcio; set => SetProperty(ref _defaultOcio, value); }

        // Theme names for selector
        public ObservableCollection<string> Themes { get; } = new();

        // Quick commands for selected app
        public ObservableCollection<QuickCommand> QuickCommands { get; } = new();

        // ============ METHODS ============

        private void LaunchSelectedApp()
        {
            if (SelectedPreset == null) { StatusText = "No app selected"; return; }
            if (!File.Exists(SelectedPreset.Exe)) { StatusText = "App not found"; return; }

            try
            {
                var ocio = SelectedOcio;
                var psi = new ProcessStartInfo { FileName = SelectedPreset.Exe, UseShellExecute = false };
                var args = ArgsText?.Trim() ?? "";
                if (!string.IsNullOrEmpty(args)) psi.Arguments = args;
                OcioService.ApplyOcio(psi, ocio, SelectedPreset.Exe);
                Process.Start(psi);

                Logger.Log(SelectedPreset.Name, SelectedPreset.Exe, ocio?.Name ?? "", args);
                StatusText = string.IsNullOrEmpty(args) ? $"Launched: {SelectedPreset.Name}" : $"Launched: {SelectedPreset.Name} + {args}";

                // Add to recent if file opened
                if (!string.IsNullOrEmpty(args) && args.Contains("\""))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(args, @"""([^""]+)""");
                    if (match.Success && File.Exists(match.Groups[1].Value))
                        AddRecentFile(match.Groups[1].Value);
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Launch error: {ex.Message}";
                Logger.Log("Launch", ex.Message, "error");
            }
        }

        private void AddPreset()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Select executable", Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*" };
            if (dialog.ShowDialog() != true) return;
            AddPresetFromExe(dialog.FileName);
        }

        public void AddPresetFromExe(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) { StatusText = "Executable not found"; return; }
            var defaultName = Path.GetFileNameWithoutExtension(exePath);
            var name = Microsoft.VisualBasic.Interaction.InputBox("Preset name:", "FLOMASTER", defaultName);
            if (string.IsNullOrWhiteSpace(name)) return;
            var preset = new Preset { Name = name, Exe = exePath };
            _config.Presets.Add(preset);
            ConfigManager.Save(_config);
            Presets.Add(preset);
            StatusText = $"Added: {name}";
        }

        private static readonly string[] ProjectExtensions =
            { ".blend", ".spp", ".ma", ".mb", ".hip", ".hipl", ".hipnc", ".nk" };

        public bool IsProjectFile(string file) =>
            ProjectExtensions.Contains(Path.GetExtension(file).ToLowerInvariant());

        public void OpenProjectFile(string file)
        {
            if (!File.Exists(file)) { StatusText = "File not found"; return; }
            if (SelectedPreset == null) { StatusText = "Select an app first"; return; }
            if (!File.Exists(SelectedPreset.Exe)) { StatusText = "App not found"; return; }

            try
            {
                var psi = new ProcessStartInfo { FileName = SelectedPreset.Exe, UseShellExecute = false, Arguments = $"\"{file}\"" };
                OcioService.ApplyOcio(psi, SelectedOcio, SelectedPreset.Exe);
                Process.Start(psi);

                Logger.Log(SelectedPreset.Name, SelectedPreset.Exe, SelectedOcio?.Name ?? "", $"open: {file}");
                AddRecentFile(file);
                StatusText = $"Opened: {Path.GetFileName(file)}";
            }
            catch (Exception ex)
            {
                StatusText = $"Open error: {ex.Message}";
                Logger.Log("Open", ex.Message, "error");
            }
        }

        private void AddOcioConfig()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Select OCIO config", Filter = "OCIO config (*.ocio)|*.ocio|All files (*.*)|*.*" };
            if (dialog.ShowDialog() != true) return;
            var ocioPath = dialog.FileName;
            var defaultName = Path.GetFileNameWithoutExtension(Path.GetDirectoryName(ocioPath)) + " " + Path.GetFileNameWithoutExtension(ocioPath);
            var name = Microsoft.VisualBasic.Interaction.InputBox("OCIO config name:", "FLOMASTER", defaultName);
            if (string.IsNullOrWhiteSpace(name)) return;
            if (OcioService.AddOcioConfig(_config, name, ocioPath))
            {
                ConfigManager.Save(_config);
                RefreshOcioConfigs();
                StatusText = $"Added OCIO: {name}";
            }
            else { StatusText = "OCIO config already exists or is invalid"; }
        }

        private void RemoveOcioConfig()
        {
            if (SelectedOcio == null || OcioConfigs.Count <= 1) return;
            var result = MessageBox.Show($"Remove \"{SelectedOcio.Name}\"?", "FLOMASTER", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            if (OcioService.RemoveOcioConfig(_config, SelectedOcio))
            {
                ConfigManager.Save(_config);
                RefreshOcioConfigs();
                StatusText = $"Removed: {SelectedOcio.Name}";
            }
        }

        private void AddScanPath()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "Select folder containing DCC applications" };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var path = dialog.SelectedPath;
                if (!_config.ScanPaths.Contains(path))
                {
                    _config.ScanPaths.Add(path);
                    ConfigManager.Save(_config);
                    StatusText = $"Scan path added: {Path.GetFileName(path)}";
                }
            }
        }

        private void RescanApps()
        {
            _config.Presets.Clear();
            var scanned = DccScanner.Scan(_config.ScanPaths);
            foreach (var app in scanned) _config.Presets.Add(app);
            ConfigManager.Save(_config);
            RefreshPresets();
            StatusText = $"Found {scanned.Count} applications";
            Logger.Log("Rescan", $"Found {scanned.Count} applications", "info");
        }

        private void RefreshLogEntries()
        {
            LogEntries.Clear();
            foreach (var entry in Logger.GetLastEntries(50))
                LogEntries.Add(entry);
            StatusText = $"Log: {LogEntries.Count} entries";
        }

        private void CreateShortcut()
        {
            if (SelectedPreset == null || !File.Exists(SelectedPreset.Exe)) { StatusText = "App not found"; return; }
            try
            {
                var ocio = SelectedOcio;
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var lnkDir = Path.Combine(desktop, "FLOMASTER");
                Directory.CreateDirectory(lnkDir);
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic shortcut = shell.CreateShortcut(Path.Combine(lnkDir, $"{SelectedPreset.Name}.lnk"));
                shortcut.TargetPath = SelectedPreset.Exe;
                shortcut.WorkingDirectory = Path.GetDirectoryName(SelectedPreset.Exe);
                shortcut.IconLocation = $"{SelectedPreset.Exe},0";
                if (ocio != null && !string.IsNullOrEmpty(ocio.Path) && File.Exists(ocio.Path))
                {
                    var batPath = Path.Combine(lnkDir, $"{SelectedPreset.Name}.bat");
                    File.WriteAllText(batPath, $"@echo off\r\nset \"OCIO={ocio.Path}\"\r\nstart \"\" \"{SelectedPreset.Exe}\"");
                    shortcut.TargetPath = batPath;
                }
                shortcut.Save();
                StatusText = $"Shortcut: {SelectedPreset.Name}";
            }
            catch (Exception ex) { StatusText = $"Shortcut error: {ex.Message}"; }
        }

        private void CreateDesktopShortcut()
        {
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FLOMASTER.exe");
                var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flomaster.ico");
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic shortcut = shell.CreateShortcut(Path.Combine(desktop, "FLOMASTER.lnk"));
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                shortcut.IconLocation = File.Exists(icoPath) ? $"{icoPath},0" : $"{exePath},0";
                shortcut.Description = "FLOMASTER - OCIO Launcher";
                shortcut.Save();
                StatusText = "Desktop shortcut created";
            }
            catch (Exception ex) { StatusText = $"Shortcut error: {ex.Message}"; }
        }

        private void OpenRecentFile(string filePath)
        {
            if (!File.Exists(filePath)) { StatusText = "File not found"; RefreshRecentFiles(); return; }
            if (SelectedPreset == null || !File.Exists(SelectedPreset.Exe)) return;
            var ocio = SelectedOcio;
            var psi = new ProcessStartInfo { FileName = SelectedPreset.Exe, Arguments = $"\"{filePath}\"", UseShellExecute = false };
            OcioService.ApplyOcio(psi, ocio, SelectedPreset.Exe);
            Process.Start(psi);
            Logger.Log(SelectedPreset.Name, SelectedPreset.Exe, ocio?.Name ?? "", $"open: {filePath}");
            StatusText = $"Opened: {Path.GetFileName(filePath)}";
        }

        private void AddQuickCommand(string cmd)
        {
            var current = ArgsText?.Trim() ?? "";
            ArgsText = string.IsNullOrEmpty(current) ? cmd : $"{current} {cmd}";
        }

        private void AddRecentFile(string filePath)
        {
            if (!_config.RecentFiles.Contains(filePath))
            {
                _config.RecentFiles.Insert(0, filePath);
                if (_config.RecentFiles.Count > 20) _config.RecentFiles.RemoveAt(_config.RecentFiles.Count - 1);
                ConfigManager.Save(_config);
                RefreshRecentFiles();
            }
        }

        private void ClearRecentFiles()
        {
            _config.RecentFiles.Clear();
            ConfigManager.Save(_config);
            RefreshRecentFiles();
            StatusText = "Recent files cleared";
        }

        private void SetAutoStart(bool enabled)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;
                if (enabled)
                {
                    var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FLOMASTER.exe");
                    if (File.Exists(exePath)) key.SetValue("FLOMASTER", $"\"{exePath}\"");
                }
                else { key.DeleteValue("FLOMASTER", false); }
            }
            catch (Exception ex) { Logger.Log("AutoStart", ex.Message, "error"); }
        }

        private bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue("FLOMASTER") != null;
            }
            catch { return false; }
        }

        private void RefreshPresets()
        {
            Presets.Clear();
            foreach (var p in _config.Presets) Presets.Add(p);
        }

        private void RefreshOcioConfigs()
        {
            OcioConfigs.Clear();
            foreach (var o in _config.OcioConfigs) OcioConfigs.Add(o);
            if (OcioConfigs.Count > 0)
                SelectedOcio = OcioConfigs.FirstOrDefault(o => o.Name == _config.DefaultOcio) ?? OcioConfigs[0];
        }

        private void RefreshRecentFiles()
        {
            RecentFiles.Clear();
            foreach (var f in _config.RecentFiles.Where(f => File.Exists(f)).Distinct().Take(20))
                RecentFiles.Add(f);
        }

        private void RefreshQuickCommands()
        {
            QuickCommands.Clear();
            if (SelectedPreset == null) return;
            foreach (var (cmd, desc) in GetCommandsForApp(SelectedPreset.Name.ToLower()))
                QuickCommands.Add(new QuickCommand { Cmd = cmd, Desc = desc });
        }

        private List<(string cmd, string desc)> GetCommandsForApp(string appName)
        {
            if (appName.Contains("blender") || appName.Contains("k-cycles"))
                return new() { ("--factory-startup", "Clean start (no addons)"), ("--debug-gpu", "GPU diagnostics"), ("--debug-cycles", "Cycles debug"), ("-b \"{file}\" -o \"{out}\" -f 1", "Background render"), ("--python-expr \"import bpy; bpy.context.scene.render.resolution_percentage = 25\"", "Quick preview (25%)") };
            if (appName.Contains("maya"))
                return new() { ("-batch", "Batch mode (no GUI)"), ("-command \"cmds.polyCube()\"", "Execute MEL command"), ("-proj \"{path}\"", "Set project path") };
            if (appName.Contains("houdini"))
                return new() { ("-batch", "Batch mode (no GUI)"), ("-foreground", "Don't fork process") };
            if (appName.Contains("nuke"))
                return new() { ("-t", "Terminal mode (no GUI)"), ("-F 1-100", "Frame range") };
            if (appName.Contains("unreal"))
                return new() { ("-game", "Standalone game mode"), ("-log", "Show log window"), ("-renderoffscreen", "Render without GUI"), ("-nullrhi", "No rendering (headless)") };
            return new();
        }
    }

    public class QuickCommand
    {
        public string Cmd { get; set; }
        public string Desc { get; set; }
    }
}
