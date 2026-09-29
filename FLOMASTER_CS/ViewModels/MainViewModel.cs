using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
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
        private bool _rolesPanelVisible;
        private bool _profilesPanelVisible;
        private bool _projectsPanelVisible;
        private string _browserSearchText = "";
        private string _selectedBrowserRoot;
        private bool _autoStartEnabled;
        private bool _topMostEnabled;
        private bool _animationEnabled;
        private OcioConfig _defaultOcio;
        private string _ocioWarningsText = "";
        private string _ocioOverrideWarnings = "";

        // Роли, значимые для пайплайна (порядок = порядок строк в UI)
        private static readonly string[] RoleUiOrder = { "scene_linear", "rendering", "data", "default_byte", "texture_paint", "reference" };

        // Collections
        public ObservableCollection<Preset> Presets { get; } = new();
        public ObservableCollection<OcioConfig> OcioConfigs { get; } = new();
        public ObservableCollection<Profile> Profiles { get; } = new();
        public ObservableCollection<string> ProjectRoots { get; } = new();
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
        public ICommand ResetRolesCommand { get; private set; }
        public ICommand UpdateCommand { get; private set; }
        public ICommand ClearRecentCommand { get; private set; }
        public ICommand SaveProfileCommand { get; private set; }
        public ICommand ApplyProfileCommand { get; private set; }
        public ICommand DeleteProfileCommand { get; private set; }
        public ICommand AddProjectRootCommand { get; private set; }
        public ICommand RemoveProjectRootCommand { get; private set; }
        public ICommand OpenProjectCommand { get; private set; }

        public MainViewModel()
        {
            _config = ConfigManager.Load();
            Logger.Log("ViewModel", "Config loaded", "info");
            UpdateService.CleanupOldInstall();
            StartUpdateCheck();

            if (_config.Presets.Count == 0)
                RescanApps();

            RefreshPresets();
            RefreshOcioConfigs();
            RefreshRecentFiles();
            RefreshProfiles();
            RefreshProjectRoots();

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
            ResetRolesCommand = new RelayCommand(_ => ResetRoleOverrides(), _ => SelectedPreset?.RoleOverrides is { Count: > 0 });
            UpdateCommand = new RelayCommand(_ => ApplyUpdate(), _ => UpdateReady);
            ClearRecentCommand = new RelayCommand(_ => ClearRecentFiles());
            SaveProfileCommand = new RelayCommand(_ => SaveProfile());
            ApplyProfileCommand = new RelayCommand<Profile>(p => ApplyProfile(p));
            DeleteProfileCommand = new RelayCommand<Profile>(p => DeleteProfile(p));
            AddProjectRootCommand = new RelayCommand(_ => AddProjectRoot());
            RemoveProjectRootCommand = new RelayCommand<string>(p => RemoveProjectRoot(p));
            OpenProjectCommand = new RelayCommand<string>(p => { if (p != null) OpenProjectFile(p); });

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
        public Preset SelectedPreset { get => _selectedPreset; set { if (SetProperty(ref _selectedPreset, value)) { RefreshQuickCommands(); RebuildRoleRows(); } } }
        public OcioConfig SelectedOcio { get => _selectedOcio; set { if (SetProperty(ref _selectedOcio, value)) { UpdateOcioRoles(); RebuildRoleRows(); } } }

        // Валидация выбранного OCIO: ключевые роли + предупреждения (строка для UI)
        public string OcioWarningsText { get => _ocioWarningsText; set => SetProperty(ref _ocioWarningsText, value); }

        // Переопределения ролей пресета (строки ROLES в главном окне)
        public ObservableCollection<OcioRoleRow> OcioRoleRows { get; } = new();
        public string OcioOverrideWarnings { get => _ocioOverrideWarnings; set => SetProperty(ref _ocioOverrideWarnings, value); }

        public string VersionLabel =>
            "v" + string.Join(".", (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version
                                    ?? new Version(2, 3, 2)).ToString().Split('.').Take(3));
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
        public bool RolesPanelVisible { get => _rolesPanelVisible; set => SetProperty(ref _rolesPanelVisible, value); }
        public bool ProfilesPanelVisible { get => _profilesPanelVisible; set => SetProperty(ref _profilesPanelVisible, value); }
        public bool ProjectsPanelVisible { get => _projectsPanelVisible; set => SetProperty(ref _projectsPanelVisible, value); }
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

        // ——— Обновления ———

        private string _updateInfoText = "";
        private bool _updateReady;
        private bool _checkingUpdates;
        private string? _updateTag;
        private string? _updateUrl;
        private string? _updatePath;

        /// <summary>Проверять обновления при старте (отключается в Settings).</summary>
        public bool CheckUpdatesEnabled
        {
            get => _config.CheckUpdates;
            set
            {
                _config.CheckUpdates = value;
                ConfigManager.Save(_config);
                OnPropertyChanged();
                Logger.Log("Update", $"Auto-check {(value ? "enabled" : "disabled")}", "info");
                if (value) StartUpdateCheck();
            }
        }

        /// <summary>Строка статуса обновления для Settings (пустая = не показывать).</summary>
        public string UpdateInfoText
        {
            get => _updateInfoText;
            set => SetProperty(ref _updateInfoText, value);
        }

        /// <summary>Обновление скачано и готово к установке.</summary>
        public bool UpdateReady
        {
            get => _updateReady;
            set => SetProperty(ref _updateReady, value);
        }

        private async void StartUpdateCheck()
        {
            if (_checkingUpdates || !_config.CheckUpdates) return;
            _checkingUpdates = true;
            try
            {
                await Task.Delay(3000); // даём окну спокойно стартовать
                var (tag, url) = await UpdateService.CheckAsync();
                if (tag == null || url == null) { Logger.Log("Update", "Up to date", "info"); return; }

                _updateTag = tag;
                _updateUrl = url;
                _updatePath = Path.Combine(Path.GetTempPath(), "FLOMASTER_update.exe");
                UpdateInfoText = $"Update {tag}: downloading...";

                await UpdateService.DownloadAsync(url, _updatePath,
                    (done, total) => UpdateInfoText = total > 0
                        ? $"Update {tag}: {done * 100 / total}%"
                        : $"Update {tag}: downloading...");

                UpdateInfoText = $"Update {tag} ready — restart to install";
                UpdateReady = true;
                StatusText = $"Update {tag} ready";
                Logger.Log("Update", $"Update {tag} downloaded, ready to install", "info");
            }
            catch (Exception ex)
            {
                Logger.Log("Update", $"Check failed: {ex.Message}", "warn");
            }
            finally
            {
                _checkingUpdates = false;
            }
        }

        private void ApplyUpdate()
        {
            if (_updatePath == null || !File.Exists(_updatePath)) return;
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe)) { StatusText = "Update failed: exe path"; return; }
            Logger.Log("Update", $"Applying {_updateTag}, exit for elevated installer", "info");
            UpdateService.ApplyDownloadedUpdate(_updatePath, exe);
            Application.Current.Shutdown();
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

                // переопределения ролей пресета -> вариант конфига в %APPDATA% (канон не трогается)
                string? variantPath = null;
                if (ocio != null && SelectedPreset.RoleOverrides is { Count: > 0 })
                    variantPath = OcioService.BuildVariant(ocio.Path, SelectedPreset.RoleOverrides, SelectedPreset.Name);
                OcioService.ApplyOcio(psi, ocio, SelectedPreset.Exe, variantPath);
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

        public bool IsProjectFile(string file) =>
            UiHelper.ProjectFileExtensions.Contains(Path.GetExtension(file).ToLowerInvariant());

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

        public void OpenRecentFile(string filePath)
        {
            if (!File.Exists(filePath)) { StatusText = "File not found"; RefreshRecentFiles(); return; }
            if (SelectedPreset == null) { StatusText = "Select an app first"; return; }
            if (!File.Exists(SelectedPreset.Exe)) { StatusText = "App not found"; return; }
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

        private void UpdateOcioRoles()
        {
            var ocio = SelectedOcio;
            if (ocio == null || string.IsNullOrEmpty(ocio.Path) || !File.Exists(ocio.Path))
            {
                OcioWarningsText = "";
                return;
            }
            var report = OcioService.Validate(ocio.Path);
            OcioWarningsText = report.WarningsLine;
        }

        /// <summary>Пересобирает строки ROLES: текущие значения пресета поверх выбранного конфига.</summary>
        private void RebuildRoleRows()
        {
            OcioRoleRows.Clear();
            var preset = SelectedPreset;
            var hasConfig = SelectedOcio != null && !string.IsNullOrEmpty(SelectedOcio.Path) && File.Exists(SelectedOcio.Path);
            if (!hasConfig) { UpdateOcioOverrideWarnings(); return; }

            var (roles, colorspaces) = OcioService.Parse(SelectedOcio.Path);
            foreach (var role in RoleUiOrder)
            {
                var effective = roles.FirstOrDefault(r => r.Key == role).Value;
                var row = new OcioRoleRow { RoleName = role };
                var ov = preset?.RoleOverrides;
                if (ov != null && ov.TryGetValue(role, out var v))
                {
                    row.IsOverridden = true;
                    row.DisplayText = $"{role}: {v}";
                }
                else
                {
                    row.DisplayText = $"{role}: {(effective ?? "(not in config)")}   (config)";
                }
                OcioRoleRows.Add(row);
            }
            UpdateOcioOverrideWarnings();
        }

        /// <summary>Применяет выбор из пикера. name = null — вернуть значение из конфига.</summary>
        public void ApplyRolePick(OcioRoleRow row, string? name)
        {
            if (SelectedPreset == null || row == null) return;
            var preset = SelectedPreset;
            var ov = preset.RoleOverrides ??= new Dictionary<string, string>();

            if (name == null) ov.Remove(row.RoleName);
            else ov[row.RoleName] = name;
            if (ov.Count == 0) preset.RoleOverrides = null;

            ConfigManager.Save(_config);
            Logger.Log("OCIO", $"Preset '{preset.Name}': role {row.RoleName} -> {(name ?? "(config default)")}", "info");
            StatusText = $"Roles saved: {preset.Name}";
            RebuildRoleRows();
        }

        private void UpdateOcioOverrideWarnings()
        {
            if (SelectedOcio == null || string.IsNullOrEmpty(SelectedOcio.Path) || !File.Exists(SelectedOcio.Path))
            { OcioOverrideWarnings = ""; return; }
            var (_, colorspaces) = OcioService.Parse(SelectedOcio.Path);
            var warns = new List<string>();
            var ov = SelectedPreset?.RoleOverrides;
            if (ov != null)
                foreach (var kv in ov)
                    if (!colorspaces.ContainsKey(kv.Value))
                        warns.Add($"{kv.Key} -> '{kv.Value}' not in config, override will be dropped at launch");
            OcioOverrideWarnings = warns.Count == 0 ? "" : "! " + string.Join("   ! ", warns);
        }

        private void ResetRoleOverrides()
        {
            if (SelectedPreset == null) return;
            SelectedPreset.RoleOverrides = null;
            ConfigManager.Save(_config);
            RebuildRoleRows();
            Logger.Log("OCIO", $"Preset '{SelectedPreset.Name}': role overrides reset", "info");
            StatusText = $"Roles reset: {SelectedPreset.Name}";
        }

        // ============ ПРОФИЛИ: слепок {пресет, аргументы, OCIO}; применение заполняет состояние окна ============

        private void RefreshProfiles()
        {
            Profiles.Clear();
            foreach (var p in _config.Profiles) Profiles.Add(p);
        }

        /// <summary>Сохраняет текущее состояние окна (пресет + OCIO + аргументы) как именованный профиль.</summary>
        private void SaveProfile()
        {
            if (SelectedPreset == null) { StatusText = "No app selected"; return; }
            var name = Microsoft.VisualBasic.Interaction.InputBox("Profile name:", "FLOMASTER", $"{SelectedPreset.Name} profile");
            if (string.IsNullOrWhiteSpace(name)) return;

            var existing = _config.Profiles.FirstOrDefault(p => p.Name == name);
            if (existing != null)
            {
                var result = MessageBox.Show($"Profile \"{name}\" already exists. Overwrite?", "FLOMASTER",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;
            }

            var profile = existing ?? new Profile { Name = name };
            profile.PresetName = SelectedPreset.Name;
            profile.OcioName = SelectedOcio?.Name ?? "";
            profile.Args = ArgsText?.Trim() ?? "";
            if (existing == null) _config.Profiles.Add(profile);

            ConfigManager.Save(_config);
            RefreshProfiles();
            StatusText = $"Profile saved: {name}";
            Logger.Log("Profile", $"Saved '{name}': {profile.PresetName}, OCIO '{profile.OcioName}', args '{profile.Args}'", "info");
        }

        /// <summary>Применяет профиль: выставляет пресет, OCIO и аргументы. Запуск — штатным Launch.</summary>
        public void ApplyProfile(Profile profile)
        {
            if (profile == null) return;
            var preset = Presets.FirstOrDefault(p => p.Name == profile.PresetName);
            if (preset == null)
            {
                StatusText = $"Profile '{profile.Name}': app '{profile.PresetName}' not found";
                Logger.Log("Profile", $"'{profile.Name}': preset '{profile.PresetName}' not found", "warn");
                return;
            }
            var ocio = OcioConfigs.FirstOrDefault(o => o.Name == profile.OcioName);
            if (ocio == null && !string.IsNullOrEmpty(profile.OcioName))
                Logger.Log("Profile", $"'{profile.Name}': OCIO '{profile.OcioName}' not found, keeping current", "warn");

            SelectedPreset = preset;
            if (ocio != null) SelectedOcio = ocio;
            ArgsText = profile.Args ?? "";

            StatusText = $"Profile applied: {profile.Name}";
            Logger.Log("Profile", $"Applied '{profile.Name}': {preset.Name}, OCIO '{ocio?.Name ?? "(current)"}', args '{profile.Args}'", "info");
        }

        private void DeleteProfile(Profile profile)
        {
            if (profile == null) return;
            var result = MessageBox.Show($"Delete profile \"{profile.Name}\"?", "FLOMASTER",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            _config.Profiles.RemoveAll(p => p.Name == profile.Name);
            ConfigManager.Save(_config);
            RefreshProfiles();
            StatusText = $"Profile deleted: {profile.Name}";
            Logger.Log("Profile", $"Deleted '{profile.Name}'", "info");
        }

        // ============ КОРНИ ПРОЕКТОВ для панели Projects (read-only, файловые операции вне скоупа) ============

        // Файлы выбранного корня (плоский рекурсивный список, RelPath = путь от корня для метки)
        public ObservableCollection<BrowserFile> BrowserFiles { get; } = new();

        /// <summary>Выбранный в панели Projects корень.</summary>
        public string SelectedBrowserRoot
        {
            get => _selectedBrowserRoot;
            set { if (SetProperty(ref _selectedBrowserRoot, value)) RebuildBrowserFiles(); }
        }

        /// <summary>Поиск по имени внутри выбранного корня (пусто = все файлы).</summary>
        public string BrowserSearchText
        {
            get => _browserSearchText;
            set { if (SetProperty(ref _browserSearchText, value)) RebuildBrowserFiles(); }
        }

        private void RefreshProjectRoots()
        {
            ProjectRoots.Clear();
            foreach (var r in _config.ProjectRoots) ProjectRoots.Add(r);
            // держим выбор корня валидным; смена триггерит RebuildBrowserFiles
            if (SelectedBrowserRoot == null || !ProjectRoots.Contains(SelectedBrowserRoot))
                SelectedBrowserRoot = ProjectRoots.FirstOrDefault();
        }

        private void RebuildBrowserFiles()
        {
            BrowserFiles.Clear();
            var root = SelectedBrowserRoot;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;

            var acc = new List<string>();
            CollectProjectFiles(root, _browserSearchText?.Trim() ?? "", acc, 0);
            // distinct по пути: junctions/reparse в дереве двоят файлы
            acc = acc.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            acc.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var f in acc)
            {
                string rel;
                try { rel = Path.GetRelativePath(root, f); } catch { rel = Path.GetFileName(f); }
                BrowserFiles.Add(new BrowserFile { FullPath = f, RelPath = rel });
            }
        }

        private static void CollectProjectFiles(string dir, string filter, List<string> acc, int depth)
        {
            if (depth > 6 || acc.Count >= 300) return;
            try
            {
                foreach (var f in Directory.EnumerateFiles(dir))
                    if (UiHelper.ProjectFileExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    {
                        if (filter.Length == 0 || Path.GetFileName(f).Contains(filter, StringComparison.OrdinalIgnoreCase))
                        {
                            acc.Add(f);
                            if (acc.Count >= 300) return;
                        }
                    }
                foreach (var d in Directory.EnumerateDirectories(dir))
                {
                    bool skip;
                    try
                    {
                        var attr = File.GetAttributes(d);
                        // ReparsePoint (junction/symlink) пропускаем: зацикливание и дубли файлов
                        skip = attr.HasFlag(FileAttributes.ReparsePoint) ||
                               attr.HasFlag(FileAttributes.Hidden) || attr.HasFlag(FileAttributes.System);
                    }
                    catch { skip = true; }
                    if (!skip) CollectProjectFiles(d, filter, acc, depth + 1);
                }
            }
            catch (UnauthorizedAccessException) { }
        }

        private void AddProjectRoot()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "Select a folder containing projects" };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var path = dialog.SelectedPath;
            if (_config.ProjectRoots.Contains(path)) { StatusText = "Project folder already added"; return; }
            _config.ProjectRoots.Add(path);
            ConfigManager.Save(_config);
            RefreshProjectRoots();
            StatusText = $"Project folder added: {Path.GetFileName(path)}";
            Logger.Log("Browser", $"Project root added: {path}", "info");
        }

        private void RemoveProjectRoot(string? path)
        {
            if (path == null || !_config.ProjectRoots.Contains(path)) return;
            var result = MessageBox.Show($"Remove project folder \"{path}\"?", "FLOMASTER",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            _config.ProjectRoots.Remove(path);
            ConfigManager.Save(_config);
            RefreshProjectRoots();
            StatusText = "Project folder removed";
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

    /// <summary>Строка списка Projects: полный путь для открытия, относительный для метки.</summary>
    public class BrowserFile
    {
        public string FullPath { get; set; } = "";
        public string RelPath { get; set; } = "";
    }
}
