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
        public ICommand ToggleNotesCommand { get; private set; }
        public ICommand OpenFullNotesCommand { get; private set; }
        public ICommand ClearRecentCommand { get; private set; }
        public ICommand SaveProfileCommand { get; private set; }
        public ICommand ApplyProfileCommand { get; private set; }
        public ICommand DeleteProfileCommand { get; private set; }
        public ICommand AddProjectRootCommand { get; private set; }
        public ICommand RemoveProjectRootCommand { get; private set; }
        public ICommand OpenProjectCommand { get; private set; }
        public ICommand ExportSettingsCommand { get; private set; }
        public ICommand ImportSettingsCommand { get; private set; }
        public ICommand SetSyncFolderCommand { get; private set; }
        public ICommand SyncImportCommand { get; private set; }

        private readonly IConfigStore _store;
        private readonly IOcioService _ocio;
        private readonly ILaunchService _launch;

        /// <summary>Ручной DI без контейнеров: composition root — MainWindow.</summary>
        public MainViewModel(IConfigStore store, IOcioService ocio, ILaunchService launch)
        {
            _store = store;
            _ocio = ocio;
            _launch = launch;
            _config = _store.Load();
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

            // папка-синк: любое изменение экспортируемого состояния — отложенный пуш
            Profiles.CollectionChanged += (_, _) => ScheduleSyncPush();
            ProjectRoots.CollectionChanged += (_, _) => ScheduleSyncPush();
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(SelectedTheme) or nameof(AnimationEnabled) or nameof(TopMostEnabled)
                    or nameof(CheckUpdatesEnabled) or nameof(HotkeyEnabled))
                    ScheduleSyncPush();
            };
            _syncPushTimer.Tick += (_, _) => { _syncPushTimer.Stop(); if (_syncDirty) PushSyncNow(); };

            // стартовый пуш своего файла + скан чужих (папка не задана — молча)
            CheckSyncAsync();

            // Init themes
            foreach (var key in ThemeManager.ThemeOrder)
                Themes.Add(ThemeManager.Themes[key].Name);

            // Init commands
            LaunchCommand = new RelayCommand(_ => LaunchSelectedApp());
            AddPresetCommand = new RelayCommand(_ => AddPreset());
            AddOcioCommand = new RelayCommand(_ => AddOcioConfig());
            RemoveOcioCommand = new RelayCommand(_ => RemoveOcioConfig(), _ => OcioConfigs.Count > 1 && SelectedOcio?.IsNoOcio != true);
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
            ToggleNotesCommand = new RelayCommand(_ => UpdateNotesVisible = !UpdateNotesVisible, _ => HasUpdateNotes);
            OpenFullNotesCommand = new RelayCommand(_ => OpenFullNotes(), _ => UpdateHtmlUrl != null);
            ClearRecentCommand = new RelayCommand(_ => ClearRecentFiles());
            SaveProfileCommand = new RelayCommand(_ => SaveProfile());
            ApplyProfileCommand = new RelayCommand<Profile>(p => ApplyProfile(p));
            DeleteProfileCommand = new RelayCommand<Profile>(p => DeleteProfile(p));
            AddProjectRootCommand = new RelayCommand(_ => AddProjectRoot());
            RemoveProjectRootCommand = new RelayCommand<string>(p => RemoveProjectRoot(p));
            OpenProjectCommand = new RelayCommand<string>(p => { if (p != null) OpenProjectFile(p); });
            ExportSettingsCommand = new RelayCommand(_ => ExportSettings());
            ImportSettingsCommand = new RelayCommand(_ => ImportSettings());
            SetSyncFolderCommand = new RelayCommand(_ => SetSyncFolder());
            SyncImportCommand = new RelayCommand(_ => ImportSyncedSettings(), _ => SyncPendingVisible);

            // Init state
            _selectedTheme = ThemeManager.GetTheme(_config.Theme).Name;
            _topMostEnabled = _config.TopMostEnabled;
            _animationEnabled = _config.AnimationEnabled;
            _autoStartEnabled = IsAutoStartEnabled();

            // выбор OCIO уже сделан в RefreshOcioConfigs (дефолт из конфига);
            // перебивать поле первым элементом нельзя — первым стоит псевдо-конфиг NO OCIO
            if (Presets.Count > 0)
                SelectedPreset = Presets.FirstOrDefault();

            StatusText = "Ready";
        }

        // ============ PROPERTIES ============

        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
        public string ArgsText { get => _argsText; set => SetProperty(ref _argsText, value); }
        public Preset SelectedPreset { get => _selectedPreset; set { if (SetProperty(ref _selectedPreset, value)) { RefreshQuickCommands(); RebuildRoleRows(); } } }
        public OcioConfig SelectedOcio
        {
            get => _selectedOcio;
            set
            {
                if (SetProperty(ref _selectedOcio, value))
                {
                    UpdateOcioRoles();
                    RebuildRoleRows();
                    OnPropertyChanged(nameof(NoOcioActive));
                }
            }
        }

        /// <summary>Выбран псевдо-конфиг NO OCIO: дашборд прячет роли и показывает «application defaults».</summary>
        public bool NoOcioActive => SelectedOcio?.IsNoOcio == true;

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
                            _store.Save(_config);
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
                    _store.Save(_config);
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
        private string? _updateHtmlUrl;
        private string _updateNotes = "";
        private bool _updateActive;
        private double _updateProgress;
        private bool _updateProgressIndeterminate;
        private bool _updateNotesVisible;

        /// <summary>Проверять обновления при старте (отключается в Settings).</summary>
        public bool CheckUpdatesEnabled
        {
            get => _config.CheckUpdates;
            set
            {
                _config.CheckUpdates = value;
                _store.Save(_config);
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

        /// <summary>Обновление обнаружено (баннер виден с момента обнаружения, не только на «ready»).</summary>
        public bool UpdateActive
        {
            get => _updateActive;
            private set => SetProperty(ref _updateActive, value);
        }

        /// <summary>Прогресс скачивания 0..1 (total неизвестен — бар в indeterminate).</summary>
        public double UpdateProgress
        {
            get => _updateProgress;
            private set => SetProperty(ref _updateProgress, value);
        }

        public bool UpdateProgressIndeterminate
        {
            get => _updateProgressIndeterminate;
            private set => SetProperty(ref _updateProgressIndeterminate, value);
        }

        /// <summary>Заметки релиза (body из GitHub API, почищенные от markdown-разметки).</summary>
        public string UpdateNotes
        {
            get => _updateNotes;
            private set { SetProperty(ref _updateNotes, value); OnPropertyChanged(nameof(HasUpdateNotes)); }
        }

        public bool HasUpdateNotes => !string.IsNullOrEmpty(UpdateNotes);

        /// <summary>Страница релиза на GitHub (линк «Full notes»), null — не прятать кнопку нельзя.</summary>
        public string? UpdateHtmlUrl
        {
            get => _updateHtmlUrl;
            private set => SetProperty(ref _updateHtmlUrl, value);
        }

        /// <summary>Раскрытые заметки «What's new» (панель в дашборде / инлайн в малом окне).</summary>
        public bool UpdateNotesVisible
        {
            get => _updateNotesVisible;
            private set => SetProperty(ref _updateNotesVisible, value);
        }

        /// <summary>Закрыть заметки извне (Esc, открытие другой шторки — взаимное исключение).</summary>
        public void CloseUpdateNotes() => UpdateNotesVisible = false;

        private async void StartUpdateCheck()
        {
            if (_checkingUpdates || !_config.CheckUpdates) return;
            _checkingUpdates = true;
            try
            {
                await Task.Delay(3000); // даём окну спокойно стартовать
                var (tag, url, isZip, notes, htmlUrl) = await UpdateService.CheckAsync();
                if (tag == null || url == null) { Logger.Log("Update", "Up to date", "info"); return; }

                _updateTag = tag;
                _updateUrl = url;
                _updatePath = Path.Combine(Path.GetTempPath(), isZip ? "FLOMASTER_update.zip" : "FLOMASTER_update.exe");
                UpdateHtmlUrl = htmlUrl;
                UpdateNotes = UpdateService.CleanupNotes(notes);
                UpdateProgress = 0;
                UpdateProgressIndeterminate = false;
                UpdateActive = true; // баннер сразу: видно, что качается, и есть что почитать
                UpdateInfoText = $"Update {tag}: downloading...";
                Logger.Log("Update", $"Downloading {tag}...", "info"); // прогресс и в лог: раньше скачивание молчало

                await UpdateService.DownloadAsync(url, _updatePath,
                    (done, total) =>
                    {
                        UpdateProgressIndeterminate = total <= 0;
                        if (total > 0)
                        {
                            UpdateProgress = Math.Min(1.0, (double)done / total);
                            UpdateInfoText = $"Update {tag}: {done * 100 / total}%";
                        }
                        else
                        {
                            UpdateInfoText = $"Update {tag}: downloading... {done / 1048576} MB";
                        }
                    });

                UpdateProgress = 1.0;
                UpdateProgressIndeterminate = false;
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

        private void OpenFullNotes()
        {
            if (string.IsNullOrEmpty(_updateHtmlUrl)) return;
            try
            {
                Process.Start(new ProcessStartInfo(_updateHtmlUrl) { UseShellExecute = true });
                Logger.Log("Update", $"Opened release page: {_updateHtmlUrl}", "info");
            }
            catch (Exception ex)
            {
                StatusText = $"Cannot open browser: {ex.Message}";
                Logger.Log("Update", $"Open release page failed: {ex.Message}", "warn");
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

        /// <summary>Оверлей поверх всех окон (переключается в Settings и в оверлее).</summary>
        public bool OverlayTopmost
        {
            get => _config.OverlayTopmost;
            set
            {
                _config.OverlayTopmost = value;
                _store.Save(_config);
                OnPropertyChanged();
                Logger.Log("Overlay", $"Topmost {(value ? "enabled" : "disabled")}", "info");
            }
        }

        /// <summary>Ctrl+Alt+F открывает полноэкранный дашборд (false — маленькое окно).</summary>
        public bool HotkeyOpensDashboard
        {
            get => _config.HotkeyOpensDashboard;
            set
            {
                _config.HotkeyOpensDashboard = value;
                _store.Save(_config);
                OnPropertyChanged();
                Logger.Log("Hotkey", $"Opens {(value ? "dashboard" : "window")}", "info");
            }
        }

        /// <summary>Приложение стартует с полноэкранного дашборда (false — маленькое окно).</summary>
        public bool StartupDashboard
        {
            get => _config.StartupDashboard;
            set
            {
                _config.StartupDashboard = value;
                _store.Save(_config);
                OnPropertyChanged();
                Logger.Log("Startup", $"UI: {(value ? "dashboard" : "window")}", "info");
            }
        }

        /// <summary>Монитор оверлея (DeviceName): пишется при закрытии оверлея, читается при открытии.</summary>
        public string OverlayScreenDeviceName
        {
            get => _config.OverlayScreenDeviceName;
            set
            {
                _config.OverlayScreenDeviceName = value ?? "";
                _store.Save(_config);
            }
        }

        /// <summary>Глобальный хоткей Ctrl+Alt+F (регистрация — в HotkeyService).</summary>
        public bool HotkeyEnabled
        {
            get => _config.HotkeyEnabled;
            set
            {
                _config.HotkeyEnabled = value;
                _store.Save(_config);
                OnPropertyChanged();
                StatusText = value ? "Global hotkey on (Ctrl+Alt+F)" : "Global hotkey off";
                Logger.Log("Hotkey", $"Global hotkey {(value ? "enabled" : "disabled")}", "info");
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
                    _store.Save(_config);
                }
            }
        }
        /// <summary>OCIO по умолчанию (трей/CLI). Хранится в конфиге именем; вычисляется из списка.</summary>
        public OcioConfig DefaultOcio
        {
            get => OcioConfigs.FirstOrDefault(o => o.Name == _config.DefaultOcio) ?? OcioConfigs.FirstOrDefault();
            set
            {
                if (value == null) return;
                _config.DefaultOcio = value.Name;
                _store.Save(_config);
                OnPropertyChanged();
                Logger.Log("OCIO", $"Default OCIO: {value.Name}", "info");
            }
        }

        // Theme names for selector
        public ObservableCollection<string> Themes { get; } = new();

        // Quick commands for selected app
        public ObservableCollection<QuickCommand> QuickCommands { get; } = new();

        // ============ METHODS ============

        private void LaunchSelectedApp()
        {
            if (SelectedPreset == null) { StatusText = "No app selected"; return; }

            var args = ArgsText?.Trim() ?? "";
            var error = _launch.Launch(SelectedPreset.Exe, SelectedPreset.Name, SelectedOcio, SelectedPreset.RoleOverrides, args);
            if (error != null)
            {
                StatusText = error == "App not found" ? error : $"Launch error: {error}";
                Logger.Log("Launch", error, "error");
                return;
            }

            Logger.Log(SelectedPreset.Name, SelectedPreset.Exe, SelectedOcio?.Name ?? "", args);
            var hint = OcioMissingHint();
            StatusText = string.IsNullOrEmpty(args) ? $"Launched: {SelectedPreset.Name}" : $"Launched: {SelectedPreset.Name} + {args}";
            if (hint != "") StatusText += " — " + hint;

            // Add to recent if file opened
            if (!string.IsNullOrEmpty(args) && args.Contains("\""))
            {
                var match = System.Text.RegularExpressions.Regex.Match(args, "\"([^\"]+)\"");
                if (match.Success && File.Exists(match.Groups[1].Value))
                    AddRecentFile(match.Groups[1].Value);
            }
        }

        /// <summary>Подсказка, когда у выбранного конфига нет файла: DCC уйдёт без OCIO молча (реальный кейс).
        /// Псевдо-конфиг NO OCIO — осознанный запуск без колор-менеджмента, это НЕ сломанная установка.</summary>
        private string OcioMissingHint() =>
            SelectedOcio != null && !SelectedOcio.IsNoOcio && string.IsNullOrEmpty(SelectedOcio.Path)
                ? $"OCIO '{SelectedOcio.Name}' has no config file (ocio\\ folder missing next to FLOMASTER.exe) — app runs WITHOUT color management, reinstall from the full zip"
                : "";

        private void AddPreset()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Select executable", Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*" };
            if (dialog.ShowDialog() != true) return;
            AddPresetFromExe(dialog.FileName);
        }

        /// <summary>Запуск пресета сразу с квик-командой: одноразовые аргументы, ArgsText не загрязняется.</summary>
        public void LaunchPresetWithCommand(Preset preset, string cmd)
        {
            SelectedPreset = preset;
            var saved = ArgsText;
            ArgsText = cmd;
            try { LaunchCommand.Execute(null); }
            finally { ArgsText = saved; }
        }

        public void AddPresetFromExe(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) { StatusText = "Executable not found"; return; }
            var defaultName = Path.GetFileNameWithoutExtension(exePath);
            var name = UiHelper.ShowInputDialog("New preset", "Preset name:", defaultName);
            if (string.IsNullOrWhiteSpace(name)) return;
            var preset = new Preset { Name = name, Exe = exePath };
            _config.Presets.Add(preset);
            _store.Save(_config);
            Presets.Add(preset);
            StatusText = $"Added: {name}";
        }

        /// <summary>Текущее переопределение роли пресета (null = значение из конфига). Для оверлея/пикера.</summary>
        public string? GetRoleOverride(string roleName) =>
            SelectedPreset?.RoleOverrides != null && SelectedPreset.RoleOverrides.TryGetValue(roleName, out var v) ? v : null;

        /// <summary>Colorspaces выбранного конфига (имя -> family). Для пикера ролей.</summary>
        public Dictionary<string, string> GetColorspaces()
        {
            var ocio = SelectedOcio;
            if (ocio == null || string.IsNullOrEmpty(ocio.Path) || !File.Exists(ocio.Path))
                return new Dictionary<string, string>();
            return _ocio.Parse(ocio.Path).colorspaces;
        }

        public bool IsProjectFile(string file) =>
            UiHelper.ProjectFileExtensions.Contains(Path.GetExtension(file).ToLowerInvariant());

        // расширение -> маркер семейства приложений (ищем в имени пресета и пути exe)
        private static readonly Dictionary<string, string> ExtToAppFamily = new()
        {
            [".blend"] = "blender",
            [".spp"] = "painter",
            [".ma"] = "maya",
            [".mb"] = "maya",
            [".hip"] = "houdini",
            [".hipl"] = "houdini",
            [".hipnc"] = "houdini",
            [".nk"] = "nuke",
        };

        /// <summary>
        /// Подбирает пресет под расширение файла (прощёлка .spp не уедет в блендер).
        /// Ищем маркер семейства в имени пресета и пути exe. Нет кандидата — null.
        /// </summary>
        public static Preset FindPresetForExtension(IEnumerable<Preset> presets, string file)
        {
            var ext = Path.GetExtension(file ?? "").ToLowerInvariant();
            if (!ExtToAppFamily.TryGetValue(ext, out var family)) return null;
            return presets.FirstOrDefault(p =>
            {
                var exe = (p.Exe ?? "").ToLowerInvariant();
                var name = (p.Name ?? "").ToLowerInvariant();
                return family == "painter"
                    ? exe.Contains("painter") || exe.Contains("substance") || name.Contains("substance")
                    : exe.Contains(family) || name.Contains(family);
            });
        }

        private void AddOcioConfig()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Select OCIO config", Filter = "OCIO config (*.ocio)|*.ocio|All files (*.*)|*.*" };
            if (dialog.ShowDialog() != true) return;
            var ocioPath = dialog.FileName;
            var defaultName = Path.GetFileNameWithoutExtension(Path.GetDirectoryName(ocioPath)) + " " + Path.GetFileNameWithoutExtension(ocioPath);
            var name = UiHelper.ShowInputDialog("New OCIO config", "OCIO config name:", defaultName);
            if (string.IsNullOrWhiteSpace(name)) return;
            if (_ocio.AddOcioConfig(_config, name, ocioPath))
            {
                _store.Save(_config);
                RefreshOcioConfigs();
                StatusText = $"Added OCIO: {name}";
            }
            else { StatusText = "OCIO config already exists or is invalid"; }
        }

        private void RemoveOcioConfig()
        {
            if (SelectedOcio == null || SelectedOcio.IsNoOcio || OcioConfigs.Count <= 1) return;
            var result = MessageBox.Show($"Remove \"{SelectedOcio.Name}\"?", "FLOMASTER", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            if (_ocio.RemoveOcioConfig(_config, SelectedOcio))
            {
                _store.Save(_config);
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
                    _store.Save(_config);
                    StatusText = $"Scan path added: {Path.GetFileName(path)}";
                }
            }
        }

        private void RescanApps()
        {
            _config.Presets.Clear();
            var scanned = DccScanner.Scan(_config.ScanPaths);
            foreach (var app in scanned) _config.Presets.Add(app);
            _store.Save(_config);
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

        public void OpenProjectFile(string filePath)
        {
            if (!File.Exists(filePath)) { StatusText = "File not found"; return; }

            // маршрутизация по расширению: .spp уходит в Painter, .blend — в Blender
            var target = FindPresetForExtension(Presets, filePath);
            var routed = target != null && !ReferenceEquals(target, SelectedPreset);
            if (target == null)
            {
                target = SelectedPreset;
                if (target == null) { StatusText = "Select an app first"; return; }
                if (!File.Exists(target.Exe)) { StatusText = "App not found"; return; }
            }
            else if (routed)
            {
                SelectedPreset = target; // видно в интерфейсе, чем открыли
            }
            if (!File.Exists(target.Exe)) { StatusText = "App not found"; return; }

            try
            {
                var psi = new ProcessStartInfo { FileName = target.Exe, UseShellExecute = false, Arguments = $"\"{filePath}\"" };
                _ocio.ApplyOcio(psi, SelectedOcio, target.Exe);
                Process.Start(psi);

                Logger.Log(target.Name, target.Exe, SelectedOcio?.Name ?? "", $"open: {filePath}");
                AddRecentFile(filePath);
                StatusText = routed
                    ? $"Opened: {Path.GetFileName(filePath)} → {target.Name}"
                    : $"Opened: {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                StatusText = $"Open error: {ex.Message}";
                Logger.Log("Open", ex.Message, "error");
            }
        }

        public void OpenRecentFile(string filePath)
        {
            if (!File.Exists(filePath)) { StatusText = "File not found"; RefreshRecentFiles(); return; }
            if (SelectedPreset == null) { StatusText = "Select an app first"; return; }
            if (!File.Exists(SelectedPreset.Exe)) { StatusText = "App not found"; return; }
            var ocio = SelectedOcio;
            var psi = new ProcessStartInfo { FileName = SelectedPreset.Exe, Arguments = $"\"{filePath}\"", UseShellExecute = false };
            _ocio.ApplyOcio(psi, ocio, SelectedPreset.Exe);
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
                _store.Save(_config);
                RefreshRecentFiles();
            }
        }

        private void ClearRecentFiles()
        {
            _config.RecentFiles.Clear();
            _store.Save(_config);
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

        /// <summary>Псевдо-конфиг «NO OCIO»: живёт только в VM-коллекции, в launcher_config не пишется.</summary>
        private static OcioConfig CreateNoOcioConfig() => new()
        {
            Name = ConfigManager.NoOcioName,
            Path = "",
            IsNoOcio = true
        };

        private void RefreshOcioConfigs()
        {
            OcioConfigs.Clear();
            OcioConfigs.Add(CreateNoOcioConfig());
            foreach (var o in _config.OcioConfigs) OcioConfigs.Add(o);
            // дефолт из конфига; записи нет — первый РЕАЛЬНЫЙ конфиг; конфигов нет вовсе — NO OCIO
            SelectedOcio = OcioConfigs.FirstOrDefault(o => o.Name == _config.DefaultOcio)
                           ?? _config.OcioConfigs.FirstOrDefault()
                           ?? OcioConfigs[0];
            var hint = OcioMissingHint();
            if (hint != "") StatusText = hint; // видно сразу при старте, не только после запуска
        }

        private void UpdateOcioRoles()
        {
            var ocio = SelectedOcio;
            if (ocio == null || string.IsNullOrEmpty(ocio.Path) || !File.Exists(ocio.Path))
            {
                OcioWarningsText = "";
                return;
            }
            var report = _ocio.Validate(ocio.Path);
            OcioWarningsText = report.WarningsLine;
        }

        /// <summary>Пересобирает строки ROLES: текущие значения пресета поверх выбранного конфига.</summary>
        private void RebuildRoleRows()
        {
            OcioRoleRows.Clear();
            var preset = SelectedPreset;
            var hasConfig = SelectedOcio != null && !string.IsNullOrEmpty(SelectedOcio.Path) && File.Exists(SelectedOcio.Path);
            if (!hasConfig) { UpdateOcioOverrideWarnings(); return; }

            var (roles, colorspaces) = _ocio.Parse(SelectedOcio.Path);
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

            _store.Save(_config);
            Logger.Log("OCIO", $"Preset '{preset.Name}': role {row.RoleName} -> {(name ?? "(config default)")}", "info");
            StatusText = $"Roles saved: {preset.Name}";
            RebuildRoleRows();
        }

        private void UpdateOcioOverrideWarnings()
        {
            if (SelectedOcio == null || string.IsNullOrEmpty(SelectedOcio.Path) || !File.Exists(SelectedOcio.Path))
            { OcioOverrideWarnings = ""; return; }
            var (_, colorspaces) = _ocio.Parse(SelectedOcio.Path);
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
            _store.Save(_config);
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
            var name = UiHelper.ShowInputDialog("New profile", "Profile name:", $"{SelectedPreset.Name} profile");
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

            _store.Save(_config);
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
            _store.Save(_config);
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
            _walkKey = null; // состав корней менялся — кэш обхода невалиден
            // держим выбор корня валидным; смена триггерит RebuildBrowserFiles
            if (SelectedBrowserRoot == null || !ProjectRoots.Contains(SelectedBrowserRoot))
                SelectedBrowserRoot = ProjectRoots.FirstOrDefault();
        }

        public List<string> GetProjectFiles(string root)
        {
            var acc = CollectProjectFilesCached(root, "");
            SortProjectFiles(acc, _config.ProjectsSort, _appSortOffset);
            return acc;
        }

        // ---- Кэш обхода дерева проектов. Чипы сортировки кликаются часто, а полный
// обход папки на UI-потоке — фриз («что-то просчитывается»), до двух раз на клик.
// Инвалидация: смена корня/поиска (ключ), правка списка корней, TTL 20 с
// (файл, сохранённый в DCC, появится не позже чем через 20 с). ----
        private string? _walkKey;
        private DateTime _walkTime = DateTime.MinValue;
        private List<string> _walkCache = new();

        private List<string> CollectProjectFilesCached(string root, string search)
        {
            var key = root + "\x1" + search;
            if (_walkKey != key || (DateTime.UtcNow - _walkTime).TotalSeconds >= 20)
            {
                var acc = new List<string>();
                if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
                    CollectProjectFiles(root, search, acc, 0);
                _walkKey = key;
                _walkCache = acc;
                _walkTime = DateTime.UtcNow;
            }
            return new List<string>(_walkCache);
        }

        /// <summary>Сортировка списка проектов: name — по имени файла (не по полному пути:
        /// путь ставил порядок папок выше имён — «идут чёрт знает как»), date — свежие сверху,
        /// app — группы по семейству DCC (blender/maya/houdini/nuke/painter, прочее в конец).
        /// appOffset — циклическая ротация семейств вправо (повторный клик по Apps):
        /// offset 1 ставит painter наверх, 2 — nuke, и т.д.; «прочее» в цикле не участвует.</summary>
        public static void SortProjectFiles(List<string> files, string mode, int appOffset = 0)
        {
            switch (mode)
            {
                case "date":
                    files.Sort((a, b) =>
                    {
                        int c = SafeMTimeUtc(b).CompareTo(SafeMTimeUtc(a)); // свежие сверху
                        if (c != 0) return c;
                        c = string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase);
                        return c != 0 ? c : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                    });
                    break;
                case "app":
                {
                    // цикл только по семействам, ПРЕДСТАВЛЕННЫМ в списке: иначе клики
                    // вращали пустые места и порядок менялся «через раз»
                    var ranks = BuildAppRanks(files, appOffset);
                    int Rank(string f) =>
                        ExtToAppFamily.TryGetValue(Path.GetExtension(f).ToLowerInvariant(), out var fam)
                            ? ranks[fam]
                            : ranks["other"];
                    files.Sort((a, b) =>
                    {
                        int c = Rank(a).CompareTo(Rank(b));
                        if (c != 0) return c;
                        c = string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase);
                        return c != 0 ? c : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                    });
                    break;
                }
                default: // name
                    files.Sort((a, b) =>
                    {
                        int c = string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase);
                        return c != 0 ? c : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                    });
                    break;
            }
        }

        private static DateTime SafeMTimeUtc(string file)
        {
            try { return File.GetLastWriteTimeUtc(file); } catch { return DateTime.MinValue; }
        }

        /// <summary>Ранги семейств для APP-сортировки. В цикле участвуют только семейства,
        /// представленные в списке (в каноническом порядке blender→maya→houdini→nuke→painter):
        /// папка с двумя семействами переворачивается КАЖДЫМ кликом, а не 1-м и 5-м.
        /// Цикл вправо: offset 1 ставит последнее присутствующее семейство наверху.
        /// «Прочее» всегда в конце, в цикле не участвует.</summary>
        private static Dictionary<string, int> BuildAppRanks(List<string> files, int appOffset)
        {
            var canonical = new[] { "blender", "maya", "houdini", "nuke", "painter" };
            var present = new HashSet<string>();
            foreach (var f in files)
                if (ExtToAppFamily.TryGetValue(Path.GetExtension(f).ToLowerInvariant(), out var fam))
                    present.Add(fam);

            var ordered = new List<string>();
            foreach (var fam in canonical)
                if (present.Contains(fam)) ordered.Add(fam);

            var ranks = new Dictionary<string, int>();
            // цикл вправо: смещение растёт вместе с каноническим индексом —
            // offset 1 ставит последнее присутствующее семейство (painter) на позицию 0
            for (int i = 0; i < ordered.Count; i++)
                ranks[ordered[i]] = (i + appOffset) % ordered.Count;
            ranks["other"] = int.MaxValue;
            return ranks;
        }

        /// <summary>Смещение цикла APP-сортировки. Сессионное (в конфиг не пишется):
        /// фича про «поставить нужное наверх в моменте», рестарт возвращает канон.</summary>
        private int _appSortOffset;

        /// <summary>Повторный клик по активному Apps: цикл семейств вправо —
        /// painter → nuke → houdini → maya → blender; нужная группа наверху в момент.</summary>
        public void CycleAppSort()
        {
            _appSortOffset = (_appSortOffset + 1) % 5;
            RebuildBrowserFiles();
            Logger.Log("Projects", $"App sort cycle: {_appSortOffset}/5", "info");
        }

        /// <summary>Режим сортировки проектов в дашборде; персистится в конфиге.</summary>
        public string ProjectSort
        {
            get => _config.ProjectsSort;
            set
            {
                var v = value == "date" || value == "app" ? value : "name";
                if (v == _config.ProjectsSort) return;
                _config.ProjectsSort = v;
                if (v == "app") _appSortOffset = 0; // каждое включение Apps — с канонического порядка
                _store.Save(_config);
                OnPropertyChanged();
                RebuildBrowserFiles();
                Logger.Log("Projects", $"Sort: {v}", "info");
            }
        }

        private void RebuildBrowserFiles()
        {
            BrowserFiles.Clear();
            var root = SelectedBrowserRoot;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;

            var acc = CollectProjectFilesCached(root, _browserSearchText?.Trim() ?? "");
            // distinct по пути: junctions/reparse в дереве двоят файлы
            acc = acc.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            SortProjectFiles(acc, _config.ProjectsSort, _appSortOffset);
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

        // ============ ПЕРЕНОС НАСТРОЕК МЕЖДУ МАШИНАМИ (v1: файл; профили/корни/тема; пути остаются машинными) ============

        private void ExportSettings()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export settings",
                    Filter = "FLOMASTER setup (*.flomaster)|*.flomaster|JSON (*.json)|*.json",
                    FileName = $"flomaster_setup_{DateTime.Now:yyyyMMdd}.flomaster"
                };
                if (dialog.ShowDialog() != true) return;

                File.WriteAllText(dialog.FileName, SyncService.BuildPayload(BuildSyncExport()));
                StatusText = $"Settings exported: {Path.GetFileName(dialog.FileName)}";
                Logger.Log("Sync", $"Exported {_config.Profiles.Count} profiles, {_config.ProjectRoots.Count} roots -> {dialog.FileName}", "info");
            }
            catch (Exception ex)
            {
                StatusText = $"Export error: {ex.Message}";
                Logger.Log("Sync", $"Export failed: {ex.Message}", "error");
            }
        }

        private void ImportSettings()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import settings",
                    Filter = "FLOMASTER setup (*.flomaster)|*.flomaster|JSON (*.json)|*.json"
                };
                if (dialog.ShowDialog() != true) return;

                var export = SyncService.Parse(File.ReadAllText(dialog.FileName));
                if (export == null) { StatusText = "Import: file is not a FLOMASTER setup"; return; }

                var (added, updated, rootsAdded) = ApplySyncedSettings(export);
                StatusText = $"Imported: {added} new, {updated} updated profiles, {rootsAdded} roots";
                Logger.Log("Sync", $"Imported: +{added}/~{updated} profiles, +{rootsAdded} roots, theme {export.Theme}", "info");
                ScheduleSyncPush(); // состояние изменилось — пусть уедет и в папку-синк
            }
            catch (Exception ex)
            {
                StatusText = $"Import error: {ex.Message}";
                Logger.Log("Sync", $"Import failed: {ex.Message}", "error");
            }
        }

        // ============ ПАПКА-СИНК v2: свой файл пишем сам, чужие предлагаем к импорту ============

        private bool _syncDirty;
        private bool _syncWarned; // спам-гвард: одна WARN на полосу недоступности папки
        private readonly List<SyncCandidate> _syncPending = new();
        private bool _syncPendingVisible;
        private string _syncPendingText = "";
        private readonly System.Windows.Threading.DispatcherTimer _syncPushTimer = new() { Interval = TimeSpan.FromSeconds(3) };

        /// <summary>Есть необработанные чужие файлы синка — чип импорта в MAINTENANCE.</summary>
        public bool SyncPendingVisible { get => _syncPendingVisible; private set => SetProperty(ref _syncPendingVisible, value); }
        public string SyncPendingText { get => _syncPendingText; private set => SetProperty(ref _syncPendingText, value); }

        /// <summary>Экспортируемый слепок — общий источник для ручного Export и пуша в папку-синк.</summary>
        private SettingsExport BuildSyncExport() => new()
        {
            App = "FLOMASTER",
            MachineName = Environment.MachineName,
            ExportedAt = DateTime.Now.ToString("s"),
            Theme = SelectedTheme,
            AnimationEnabled = AnimationEnabled,
            TopMostEnabled = TopMostEnabled,
            CheckUpdates = CheckUpdatesEnabled,
            HotkeyEnabled = HotkeyEnabled,
            Profiles = _config.Profiles.Select(p => new Profile
            {
                Name = p.Name, PresetName = p.PresetName, OcioName = p.OcioName, Args = p.Args
            }).ToList(),
            ProjectRoots = _config.ProjectRoots.ToList()
        };

        /// <summary>Мержит SettingsExport в текущее состояние: профили merge по имени, корни union, тема/галки.
        /// Общий путь ручного Import и импорта из папки-синка.</summary>
        private (int added, int updated, int rootsAdded) ApplySyncedSettings(SettingsExport export)
        {
            int overwritten = 0, added = 0;
            foreach (var p in export.Profiles ?? new List<Profile>())
            {
                var existing = _config.Profiles.FirstOrDefault(x => x.Name == p.Name);
                if (existing != null)
                {
                    existing.PresetName = p.PresetName;
                    existing.OcioName = p.OcioName;
                    existing.Args = p.Args;
                    overwritten++;
                }
                else
                {
                    _config.Profiles.Add(new Profile
                    {
                        Name = p.Name, PresetName = p.PresetName, OcioName = p.OcioName, Args = p.Args
                    });
                    added++;
                }
            }

            int rootsAdded = 0;
            foreach (var root in export.ProjectRoots ?? new List<string>())
                if (!_config.ProjectRoots.Contains(root))
                {
                    _config.ProjectRoots.Add(root);
                    rootsAdded++;
                }

            _store.Save(_config);
            RefreshProfiles();
            RefreshProjectRoots();

            if (!string.IsNullOrEmpty(export.Theme) && Themes.Contains(export.Theme))
                SelectedTheme = export.Theme;
            AnimationEnabled = export.AnimationEnabled;
            TopMostEnabled = export.TopMostEnabled;
            CheckUpdatesEnabled = export.CheckUpdates;
            HotkeyEnabled = export.HotkeyEnabled;
            _store.Save(_config);
            return (added, overwritten, rootsAdded);
        }

        /// <summary>Папка задана → отложенный пуш (3 с после последнего изменения, без дёрганья облака).</summary>
        private void ScheduleSyncPush()
        {
            if (string.IsNullOrEmpty(_config.SyncFolder)) return;
            _syncDirty = true;
            _syncPushTimer.Stop();
            _syncPushTimer.Start();
        }

        private void PushSyncNow()
        {
            _syncDirty = false;
            if (string.IsNullOrEmpty(_config.SyncFolder)) return;
            try
            {
                SyncService.Push(_config.SyncFolder, SyncService.BuildPayload(BuildSyncExport()));
                _syncWarned = false;
            }
            catch (Exception ex)
            {
                if (!_syncWarned)
                {
                    _syncWarned = true;
                    Logger.Log("Sync", $"Push failed, will retry on next change: {ex.Message}", "warn");
                    StatusText = "Sync: folder unavailable";
                }
            }
        }

        /// <summary>Чек папки синка: пуш своего + скан чужих. Старт и открытие дашборда; папка не задана — молча.</summary>
        public async void CheckSyncAsync()
        {
            if (string.IsNullOrEmpty(_config.SyncFolder)) return;
            try
            {
                var payload = SyncService.BuildPayload(BuildSyncExport());
                var folder = _config.SyncFolder;
                var candidates = await System.Threading.Tasks.Task.Run(() =>
                {
                    SyncService.Push(folder, payload);
                    return SyncService.Scan(folder, SyncService.OwnFileName(), _config.SyncSeen);
                });
                _syncWarned = false;
                SetSyncPending(candidates);
            }
            catch (Exception ex)
            {
                if (!_syncWarned)
                {
                    _syncWarned = true;
                    Logger.Log("Sync", $"Check failed: {ex.Message}", "warn");
                }
            }
        }

        private void SetSyncPending(List<SyncCandidate> candidates)
        {
            _syncPending.Clear();
            _syncPending.AddRange(candidates);
            SyncPendingVisible = _syncPending.Count > 0;
            if (_syncPending.Count == 0) { SyncPendingText = ""; return; }

            var sources = _syncPending.Select(c => c.Label).Distinct().ToList();
            SyncPendingText = sources.Count == 1
                ? $"Sync: {_syncPending.Count} file(s) from {sources[0]}"
                : $"Sync: {_syncPending.Count} file(s), {sources.Count} machines";
            Logger.Log("Sync", $"Pending sync imports: {string.Join(", ", _syncPending.Select(c => c.FileName))}", "info");
        }

        /// <summary>Импортирует всё накопленное из папки-синка (клик по чипу в MAINTENANCE).</summary>
        private void ImportSyncedSettings()
        {
            if (_syncPending.Count == 0) return;
            var labels = string.Join(", ", _syncPending.Select(c => c.Label).Distinct());
            try
            {
                int added = 0, updated = 0, roots = 0;
                foreach (var candidate in _syncPending)
                {
                    var export = SyncService.Parse(File.ReadAllText(candidate.Path));
                    if (export == null) continue;
                    var r = ApplySyncedSettings(export);
                    added += r.added; updated += r.updated; roots += r.rootsAdded;
                    _config.SyncSeen[candidate.FileName] = candidate.Hash;
                }
                _store.Save(_config);
                _syncPending.Clear();
                SyncPendingVisible = false;
                SyncPendingText = "";
                StatusText = $"Sync imported: +{added}/~{updated} profiles, +{roots} roots ({labels})";
                Logger.Log("Sync", $"Sync import done ({labels}): +{added}/~{updated} profiles, +{roots} roots", "info");
                PushSyncNow(); // своё состояние изменилось — перезаписать свой файл в папке
            }
            catch (Exception ex)
            {
                StatusText = $"Sync import error: {ex.Message}";
                Logger.Log("Sync", $"Sync import failed: {ex.Message}", "error");
            }
        }

        private void SetSyncFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select a folder synchronized between your machines (Dropbox, OneDrive, NAS). Profiles, project roots and theme travel through it automatically."
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            _config.SyncFolder = dialog.SelectedPath;
            _store.Save(_config);
            StatusText = $"Sync folder set: {Path.GetFileName(dialog.SelectedPath)}";
            Logger.Log("Sync", $"Sync folder set: {dialog.SelectedPath}", "info");
            CheckSyncAsync();
        }

        private void AddProjectRoot()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "Select a folder containing projects" };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var path = dialog.SelectedPath;
            if (_config.ProjectRoots.Contains(path)) { StatusText = "Project folder already added"; return; }
            _config.ProjectRoots.Add(path);
            _store.Save(_config);
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
            _store.Save(_config);
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

        public List<(string cmd, string desc)> GetCommandsForApp(string appName)
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
