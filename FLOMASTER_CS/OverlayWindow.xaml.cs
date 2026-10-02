using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FLOMASTER.Models;
using FLOMASTER.Services;
using FLOMASTER.ViewModels;

namespace FLOMASTER
{
    /// <summary>
    /// Полноэкранный дашборд: конфиг-чипы, плитки приложений, проекты, профили,
    /// роли, последние файлы, аргументы и быстрые настройки. Открывается на мониторе
    /// лаунчера; перетаскивается за шапку и подстраивается под мониторы.
    /// Esc или повторный хоткей — закрыть.
    /// </summary>
    public partial class OverlayWindow : Window, INotifyPropertyChanged
    {
        /// <summary>Плитка: полезная нагрузка (Preset/Profile/строка-файл) + данные отображения.</summary>
        public class OverlayTile : INotifyPropertyChanged
        {
            private Visibility _tileVisibility = Visibility.Visible;

            public event PropertyChangedEventHandler? PropertyChanged;

            public string Title { get; set; } = "";
            public string Subtitle { get; set; } = "";
            public ImageSource? Icon { get; set; }
            public string Monogram { get; set; } = "";
            public string Badge { get; set; } = "";
            public string Dir { get; set; } = "";
            public object? Payload { get; set; }

            /// <summary>Видимость плитки при поиске (INPC — без пересборки контейнеров, без дрожи).</summary>
            public Visibility TileVisibility
            {
                get => _tileVisibility;
                set { _tileVisibility = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TileVisibility))); }
            }
        }

        /// <summary>Чип выбора (конфиг/корень проектов).</summary>
        public class OverlayChip
        {
            public string Name { get; set; } = "";
            public string Path { get; set; } = "";
            public bool IsSelected { get; set; }

            /// <summary>Тултип чипа (null = без тултипа). Заполняет построитель чипов.</summary>
            public string? Hint { get; set; }
        }

        private readonly MainViewModel _vm;
        private readonly Window? _owner;
        private readonly UiController? _ui;
        private readonly System.Windows.Forms.Screen? _preferredScreen;
        private System.Windows.Forms.Screen _screen = System.Windows.Forms.Screen.PrimaryScreen
            ?? System.Windows.Forms.Screen.AllScreens[0];

        private readonly System.Windows.Threading.DispatcherTimer _searchDebounce;

        // INPC окна: живые тексты (статус/апдейт/варнинги) приходят из VM через прокси
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <param name="owner">Маленькое окно, если создано (старт с дашборда — null).</param>
        /// <param name="ui">Владелец окон — кнопка свапа дашборд↔виджет в шапке.</param>
        public OverlayWindow(MainViewModel viewModel, Window? owner,
            System.Windows.Forms.Screen? preferredScreen = null, UiController? ui = null)
        {
            InitializeComponent();
            _vm = viewModel;
            _ui = ui;
            _owner = owner;
            _preferredScreen = preferredScreen;
            DataContext = this;

            BuildAppAndProfileTiles();
            RebuildOcioChips();
            RebuildRootChips();
            RebuildProjectSortChips();
            RebuildProjectFiles();
            RebuildRecentTiles();
            UpdateSectionVisibility();
            RefreshRunningTiles();
            Closed += (_, _) => DisposeTrackedProcesses();

            // Дашборд — полноценный UI: мутации из дашборда же перерисовывают плитки
            _vm.Presets.CollectionChanged += (_, _) => RebuildAppTiles();
            _vm.OcioConfigs.CollectionChanged += (_, _) => RebuildOcioChips();
            _vm.ProjectRoots.CollectionChanged += (_, _) => { RebuildRootChips(); RebuildProjectFiles(); };
            _vm.Profiles.CollectionChanged += (_, _) => RebuildProfileTiles();
            _vm.RecentFiles.CollectionChanged += (_, _) => RebuildRecentTiles();

            // Живые тексты (статус, апдейт, варнинги) — прокси-свойства с INPC
            _vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.StatusText)) Raise(nameof(StatusText));
                else if (e.PropertyName == nameof(MainViewModel.UpdateInfoText)) Raise(nameof(UpdateInfoText));
                else if (e.PropertyName == nameof(MainViewModel.UpdateReady)) Raise(nameof(UpdateReady));
                else if (e.PropertyName == nameof(MainViewModel.UpdateActive)) Raise(nameof(UpdateActive));
                else if (e.PropertyName == nameof(MainViewModel.UpdateProgress)) Raise(nameof(UpdateProgress));
                else if (e.PropertyName == nameof(MainViewModel.UpdateProgressIndeterminate)) Raise(nameof(UpdateProgressIndeterminate));
                else if (e.PropertyName == nameof(MainViewModel.UpdateNotes)) Raise(nameof(UpdateNotes));
                else if (e.PropertyName == nameof(MainViewModel.HasUpdateNotes)) Raise(nameof(HasUpdateNotes));
                else if (e.PropertyName == nameof(MainViewModel.UpdateHtmlUrl)) Raise(nameof(UpdateHtmlUrl));
                else if (e.PropertyName == nameof(MainViewModel.SyncPendingVisible)) Raise(nameof(SyncPendingVisible));
                else if (e.PropertyName == nameof(MainViewModel.SyncPendingText)) Raise(nameof(SyncPendingText));
                else if (e.PropertyName == nameof(MainViewModel.UpdateNotesVisible))
                {
                    Raise(nameof(UpdateNotesVisible));
                    Raise(nameof(UpdatePanelVisible));
                    Raise(nameof(BannerNotesVisible));
                    if (_vm.UpdateNotesVisible)
                    {
                        // панель — член клуба шторок: взаимное исключение
                        LogDrawer.Visibility = Visibility.Collapsed;
                        RoleDrawer.Visibility = Visibility.Collapsed;
                    }
                }
                else if (e.PropertyName == nameof(MainViewModel.OcioWarningsText)) Raise(nameof(OcioWarningsText));
                else if (e.PropertyName == nameof(MainViewModel.NoOcioActive)) Raise(nameof(NoOcioActive));
                else if (e.PropertyName == nameof(MainViewModel.SelectedOcio)) RebuildOcioChips(); // выбор мог смениться вне чипа (профиль, старт)
            };

            PositionOnOwnerScreen();
            LocationChanged += (_, _) => SnapToMonitorIfChanged();
            StateChanged += (_, _) => { if (WindowState == WindowState.Normal) ApplyScreenBounds(); };
            // ширина свободной полосы под панель обновления зависит от фактического размера окна
            SizeChanged += (_, _) => ComputeUpdatePanelLayout();
            Loaded += (_, _) => ComputeUpdatePanelLayout();
            // панель обновления — член семьи шторок: появление с анимацией (биндинг видимости не трогаем)
            UpdatePanel.IsVisibleChanged += (_, e) =>
            {
                if (e.NewValue is true) AnimateDrawerIn(UpdatePanel, -80, 0, alreadyVisible: true); // выезд слева
            };

            // debounce поиска: 250 мс после последнего нажатия — без дрожи при быстром наборе
            _searchDebounce = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _searchDebounce.Tick += (_, _) =>
            {
                _searchDebounce.Stop();
                ApplySearchFilter(SearchBox.Text.Trim());
            };
            SearchBox.Focus();
        }

        // ---- Коллекции плиток/чипов ----

        public ObservableCollection<OverlayTile> AppTiles { get; } = new();
        public ObservableCollection<OverlayTile> ProfileTiles { get; } = new();
        public ObservableCollection<OverlayTile> RecentTiles { get; } = new();
        public ObservableCollection<OverlayTile> ProjectFileTiles { get; } = new();
        public ObservableCollection<OverlayTile> RunningTiles { get; } = new();
        public ObservableCollection<OverlayChip> OcioChips { get; } = new();
        public ObservableCollection<OverlayChip> RootChips { get; } = new();

        /// <summary>Найденные процессы DCC: живут до следующего скана, закрываем хэндлы.</summary>
        private readonly List<Process> _trackedProcesses = new();

        // ---- Прокси к ViewModel (DataContext оверлея — сам оверлей) ----

        public ObservableCollection<OcioRoleRow> RoleRows => _vm.OcioRoleRows;
        public ICommand ResetRolesCommand => _vm.ResetRolesCommand;
        public ObservableCollection<string> Themes => _vm.Themes;

        public string SelectedTheme
        {
            get => _vm.SelectedTheme;
            set => _vm.SelectedTheme = value;
        }

        public bool TopMostEnabled
        {
            get => _vm.TopMostEnabled;
            set => _vm.TopMostEnabled = value;
        }

        public bool HotkeyEnabled
        {
            get => _vm.HotkeyEnabled;
            set => _vm.HotkeyEnabled = value;
        }

        /// <summary>Оверлей поверх всех окон (наследует настройку лаунчера).</summary>
        public bool OverlayTopmost
        {
            get => _vm.OverlayTopmost;
            set => _vm.OverlayTopmost = value;
        }

        // ---- Прокси: полный функционал лаунчера в дашборде (команды живут в VM) ----

        public string VersionLabel => _vm.VersionLabel;
        public string OcioWarningsText => _vm.OcioWarningsText;
        public bool NoOcioActive => _vm.NoOcioActive;
        public string StatusText => _vm.StatusText;
        public string UpdateInfoText => _vm.UpdateInfoText;
        public bool UpdateReady => _vm.UpdateReady;
        public bool UpdateActive => _vm.UpdateActive;
        public double UpdateProgress => _vm.UpdateProgress;
        public bool UpdateProgressIndeterminate => _vm.UpdateProgressIndeterminate;
        public string UpdateNotes => _vm.UpdateNotes;
        public bool HasUpdateNotes => _vm.HasUpdateNotes;
        public string? UpdateHtmlUrl => _vm.UpdateHtmlUrl;
        public bool UpdateNotesVisible => _vm.UpdateNotesVisible;
        public ICommand ToggleNotesCommand => _vm.ToggleNotesCommand;
        public ICommand OpenFullNotesCommand => _vm.OpenFullNotesCommand;

        // ---- Папка-синк: чип импорта в MAINTENANCE ----

        public bool SyncPendingVisible => _vm.SyncPendingVisible;
        public string SyncPendingText => _vm.SyncPendingText;
        public ICommand SyncImportCommand => _vm.SyncImportCommand;
        public ICommand SetSyncFolderCommand => _vm.SetSyncFolderCommand;

        // ---- Панель обновления: занимает свободную полосу слева от центрированного грида ----

        /// <summary>Ширина полосы слева от грида (MaxWidth=1780, по центру). 0 — окно уже 1852.</summary>
        private double _stripWidth;

        private void ComputeUpdatePanelLayout()
        {
            double inner = Math.Max(0, ActualWidth - 72); // Margin 36+36 контентного грида
            double gridLeft = (ActualWidth - Math.Min(1780, inner)) / 2;
            _stripWidth = Math.Max(0, gridLeft - 36 - 12); // левый отступ панели + зазор до грида
            UpdatePanel.Width = _stripWidth > 0 ? _stripWidth : double.NaN;
            Raise(nameof(UpdatePanelVisible));
            Raise(nameof(BannerNotesVisible));
        }

        /// <summary>Панель: заметки открыты, апдейт активен и полоса достаточна —
        /// иначе панель налезла бы на колонки (узкое окно = фолбэк в баннер).</summary>
        public bool UpdatePanelVisible => _vm.UpdateNotesVisible && _vm.UpdateActive && _stripWidth >= 260;

        /// <summary>Фолбэк: на узком окне заметки раскрываются внутри баннера.</summary>
        public bool BannerNotesVisible => _vm.UpdateNotesVisible && !UpdatePanelVisible;

        public ObservableCollection<OcioConfig> OcioConfigs => _vm.OcioConfigs;

        public OcioConfig DefaultOcio
        {
            get => _vm.DefaultOcio;
            set => _vm.DefaultOcio = value;
        }

        public bool CheckUpdatesEnabled
        {
            get => _vm.CheckUpdatesEnabled;
            set => _vm.CheckUpdatesEnabled = value;
        }

        public bool AutoStartEnabled
        {
            get => _vm.AutoStartEnabled;
            set => _vm.AutoStartEnabled = value;
        }

        public bool AnimationEnabled
        {
            get => _vm.AnimationEnabled;
            set => _vm.AnimationEnabled = value;
        }

        /// <summary>Рулька: приложение стартует с дашборда (false — маленькое окно).</summary>
        public bool StartupDashboard
        {
            get => _vm.StartupDashboard;
            set => _vm.StartupDashboard = value;
        }

        public ICommand AddPresetCommand => _vm.AddPresetCommand;
        public ICommand AddOcioCommand => _vm.AddOcioCommand;
        public ICommand RemoveOcioCommand => _vm.RemoveOcioCommand;
        public ICommand RescanCommand => _vm.RescanCommand;
        public ICommand SaveProfileCommand => _vm.SaveProfileCommand;
        public ICommand DeleteProfileCommand => _vm.DeleteProfileCommand;
        public ICommand AddProjectRootCommand => _vm.AddProjectRootCommand;
        public ICommand RemoveProjectRootCommand => _vm.RemoveProjectRootCommand;
        public ICommand ClearRecentCommand => _vm.ClearRecentCommand;
        public ICommand AddScanPathCommand => _vm.AddScanPathCommand;
        public ICommand ExportSettingsCommand => _vm.ExportSettingsCommand;
        public ICommand ImportSettingsCommand => _vm.ImportSettingsCommand;
        public ICommand CreateShortcutCommand => _vm.CreateShortcutCommand;
        public ICommand CreateDesktopShortcutCommand => _vm.CreateDesktopShortcutCommand;
        public ICommand UpdateCommand => _vm.UpdateCommand;

        // ---- Мониторы: открытие на мониторе лаунчера, слежение за перетаскиванием ----

        private void PositionOnOwnerScreen()
        {
            // приоритет: монитор, где оверлей был закрыт в прошлый раз -> монитор лаунчера -> primary
            if (_preferredScreen != null &&
                System.Windows.Forms.Screen.AllScreens.Any(s => s.DeviceName == _preferredScreen.DeviceName))
            {
                _screen = _preferredScreen;
                ApplyScreenBounds();
                return;
            }
            try
            {
                if (_owner != null)
                    _screen = System.Windows.Forms.Screen.FromHandle(
                        new System.Windows.Interop.WindowInteropHelper(_owner).Handle);
            }
            catch { }
            _screen ??= System.Windows.Forms.Screen.PrimaryScreen
                ?? System.Windows.Forms.Screen.AllScreens[0];
            ApplyScreenBounds();
        }

        private void ApplyScreenBounds()
        {
            // WorkArea — минус панель задач: Normal-состояние оверлея = «всё видно, панель не перекрыта».
            // Bounds физические (PMv2); WPF-координаты — DIP: делим на масштаб целевого монитора
            var b = _screen.WorkingArea;
            double k = ScaleOf(_screen);
            Left = b.Left / k; Top = b.Top / k;
            Width = b.Width / k; Height = b.Height / k;
        }

        private void SnapToMonitorIfChanged()
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;

            var hmon = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
            if (hmon == IntPtr.Zero) return;

            // какой Screen соответствует текущему hmon — сравнением DeviceName
            System.Windows.Forms.Screen? match = null;
            try
            {
                var info = new MONITORINFOEX();
                info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(MONITORINFOEX));
                if (GetMonitorInfo(hmon, ref info))
                {
                    match = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(
                        s => s.DeviceName == info.szDevice);
                }
            }
            catch { }

            if (match != null && !match.Equals(_screen))
            {
                _screen = match;
                ApplyScreenBounds();
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [System.Runtime.InteropServices.DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmon, int type, out uint dpiX, out uint dpiY);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr hmon, ref MONITORINFOEX info);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(System.Drawing.Point pt, uint flags);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;
        private const int SW_RESTORE = 9;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        private static double ScaleOf(System.Windows.Forms.Screen screen)
        {
            try
            {
                var pt = new System.Drawing.Point(
                    screen.Bounds.Left + screen.Bounds.Width / 2,
                    screen.Bounds.Top + screen.Bounds.Height / 2);
                var hmon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
                if (hmon == IntPtr.Zero) return 1.0;
                if (GetDpiForMonitor(hmon, MDT_EFFECTIVE_DPI, out uint dx, out _) == 0 && dx > 0)
                    return dx / 96.0;
                return 1.0;
            }
            catch { return 1.0; }
        }

        // ---- Построение плиток ----

        private void BuildAppAndProfileTiles()
        {
            RebuildAppTiles();
            RebuildProfileTiles();
        }

        private void RebuildAppTiles()
        {
            AppTiles.Clear();
            foreach (var preset in _vm.Presets)
            {
                AppTiles.Add(new OverlayTile
                {
                    Title = preset.Name,
                    Subtitle = SafeFileName(preset.Exe),
                    Icon = ExtractIcon(preset.Exe),
                    Payload = preset
                });
            }
        }

        private void RebuildProfileTiles()
        {
            ProfileTiles.Clear();
            foreach (var profile in _vm.Profiles)
            {
                var letter = string.IsNullOrWhiteSpace(profile.Name)
                    ? "?"
                    : profile.Name.Trim().ToUpperInvariant()[0].ToString();
                ProfileTiles.Add(new OverlayTile
                {
                    Title = profile.Name,
                    Subtitle = $"{profile.PresetName} · {profile.OcioName}",
                    Monogram = letter,
                    Payload = profile
                });
            }
            ProfilesList.Visibility = _vm.Profiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ProfilesEmpty.Visibility = _vm.Profiles.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            ProfilesSection.Visibility = _vm.Profiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Иконка exe (System.Drawing 32x32 -> ImageSource). Не удалось — null, плитка без иконки.</summary>
        private static ImageSource? ExtractIcon(string? exePath)
        {
            try
            {
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return null;
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon == null) return null;
                var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            catch { return null; }
        }

        // ---- Чипы конфигов ----

        private void RebuildOcioChips()
        {
            OcioChips.Clear();
            foreach (var o in _vm.OcioConfigs)
                OcioChips.Add(new OverlayChip
                {
                    Name = o.Name,
                    Path = o.IsNoOcio ? "Application default color management" : (o.Path ?? ""),
                    IsSelected = _vm.SelectedOcio?.Name == o.Name
                });
        }

        // ---- Проекты: корни + файлы ----

        private void RebuildRootChips()
        {
            RootChips.Clear();
            foreach (var root in _vm.ProjectRoots)
                RootChips.Add(new OverlayChip
                {
                    Name = RootDisplayName(root),
                    Path = root,
                    IsSelected = root == _vm.SelectedBrowserRoot
                });
            ProjectsSection.Visibility = RootChips.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void RebuildProjectFiles()
        {
            ProjectFileTiles.Clear();
            var root = _vm.SelectedBrowserRoot;
            if (string.IsNullOrEmpty(root)) return;

            foreach (var f in _vm.GetProjectFiles(root))
            {
                ProjectFileTiles.Add(new OverlayTile
                {
                    Title = Path.GetFileName(f),
                    Subtitle = f,
                    Badge = Path.GetExtension(f).TrimStart('.').ToUpperInvariant(),
                    Dir = SafeDir(f),
                    Payload = f
                });
            }
            ProjectFilesFrame.Visibility = ProjectFileTiles.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            ProjectsEmpty.Visibility = ProjectFileTiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---- Сортировка проектов: A–Z / Date / Apps ----

        public ObservableCollection<OverlayChip> ProjectSortChips { get; } = new();

        private static readonly (string Key, string Label)[] ProjectSortModes =
        {
            ("name", "A\u2013Z"),
            ("date", "Date"),
            ("app", "Apps")
        };

        private void RebuildProjectSortChips()
        {
            ProjectSortChips.Clear();
            foreach (var (key, label) in ProjectSortModes)
                ProjectSortChips.Add(new OverlayChip
                {
                    Name = label,
                    Path = key,
                    IsSelected = _vm.ProjectSort == key,
                    Hint = key == "app" ? "Click again to cycle app groups" : "Sort projects"
                });
        }

        private void ProjectSortChip_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is string mode)
            {
                // повторный клик по активному Apps — цикл семейств, а не no-op
                if (mode == "app" && _vm.ProjectSort == "app") _vm.CycleAppSort();
                else _vm.ProjectSort = mode;
                RebuildProjectSortChips();
                RebuildProjectFiles();
            }
        }

        // ---- Recent ----

        private void RebuildRecentTiles()
        {
            RecentTiles.Clear();
            foreach (var file in _vm.RecentFiles)
            {
                RecentTiles.Add(new OverlayTile
                {
                    Title = Path.GetFileName(file),
                    Subtitle = file,
                    Badge = Path.GetExtension(file).TrimStart('.').ToUpperInvariant(),
                    Dir = SafeDir(file),
                    Payload = file
                });
            }
            RecentFrame.Visibility = RecentTiles.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            RecentSection.Visibility = RecentTiles.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateSectionVisibility()
        {
            RebuildRootChips();
            RebuildProjectFiles();
            RebuildRecentTiles();
            ProfilesList.Visibility = _vm.Profiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ProfilesEmpty.Visibility = _vm.Profiles.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            ProfilesSection.Visibility = _vm.Profiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---- RUNNING: запущенные инстансы DCC ----

        private void RefreshRunningTiles()
        {
            DisposeTrackedProcesses();
            RunningTiles.Clear();

            foreach (var proc in Process.GetProcesses())
            {
                var preset = MatchPreset(proc);
                if (preset == null) { proc.Dispose(); continue; }

                _trackedProcesses.Add(proc);
                string title = "";
                try { title = proc.MainWindowTitle ?? ""; } catch { }
                RunningTiles.Add(new OverlayTile
                {
                    Title = preset.Name,
                    Subtitle = string.IsNullOrEmpty(title)
                        ? $"{preset.Name} — pid {proc.Id}"
                        : $"{title} — pid {proc.Id}",
                    Icon = ExtractIcon(preset.Exe),
                    Payload = proc
                });
            }
            RunningList.Visibility = RunningTiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void DisposeTrackedProcesses()
        {
            foreach (var p in _trackedProcesses) { try { p.Dispose(); } catch { } }
            _trackedProcesses.Clear();
        }

        /// <summary>Процесс соответствует пресету, если путь exe совпадает с путём из пресета.</summary>
        private Preset? MatchPreset(Process proc)
        {
            string? path = null;
            try { path = proc.MainModule?.FileName; } catch { return null; }
            if (string.IsNullOrEmpty(path)) return null;

            foreach (var p in _vm.Presets)
            {
                if (string.IsNullOrWhiteSpace(p.Exe)) continue;
                string normalized;
                try { normalized = Path.GetFullPath(p.Exe); } catch { normalized = p.Exe; }
                if (string.Equals(normalized, path, StringComparison.OrdinalIgnoreCase)) return p;
            }
            return null;
        }

        private void FocusRunning(OverlayTile tile)
        {
            if (tile.Payload is not Process proc) return;
            if (proc.HasExited) { RefreshRunningTiles(); return; }
            try
            {
                // MainWindowHandle кэшируется в Process: если DCC ещё стартовала (сплэш без окна),
                // в кэше ноль — Refresh перечитывает, дальше fallback по EnumWindows
                proc.Refresh();
                var hwnd = proc.MainWindowHandle;
                if (hwnd == IntPtr.Zero) hwnd = FindProcessWindow(proc.Id);
                if (hwnd == IntPtr.Zero)
                {
                    _vm.StatusText = $"{proc.ProcessName} has no main window";
                    Logger.Log("Running", $"Focus failed: {proc.ProcessName} (pid {proc.Id}) has no main window", "warn");
                    return;
                }
                if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
                SetForegroundWindow(hwnd);
            }
            catch (Exception ex)
            {
                Logger.Log("Running", $"Focus failed: {ex.Message}", "warn");
                RefreshRunningTiles();
            }
        }

        /// <summary>Первое видимое top-level окно процесса (fallback, когда MainWindowHandle = 0).</summary>
        private static IntPtr FindProcessWindow(int pid)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out var windowPid);
                if (windowPid == pid && IsWindowVisible(hwnd))
                {
                    found = hwnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private void RunningChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: OverlayTile tile }) FocusRunning(tile);
        }

        private void RunningFocus_Menu(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: OverlayTile tile }) FocusRunning(tile);
        }

        private void RunningRestart_Menu(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: OverlayTile tile } ||
                tile.Payload is not Process proc) return;
            if (proc.HasExited) { RefreshRunningTiles(); return; }
            var preset = MatchPreset(proc);
            if (preset == null) return;

            // мягкое закрытие: приложение само спросит про несохранённую сцену; Kill не применяем
            bool closing;
            try
            {
                var hwnd = proc.MainWindowHandle;
                if (hwnd != IntPtr.Zero && IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
                closing = proc.CloseMainWindow();
            }
            catch { closing = false; }

            if (!closing || !proc.WaitForExit(10000))
            {
                _vm.StatusText = $"{preset.Name} is still open — restart aborted";
                Close();
                return;
            }
            _vm.SelectedPreset = preset;
            _vm.LaunchCommand.Execute(null);
            Close();
        }

        // ---- Утилиты ----

        private static string SafeFileName(string? path)
        {
            try { return Path.GetFileName(path) ?? ""; } catch { return path ?? ""; }
        }

        private static string SafeDir(string? path)
        {
            try { return Path.GetDirectoryName(path) ?? ""; } catch { return ""; }
        }

        private static string RootDisplayName(string root)
        {
            try
            {
                var name = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                return string.IsNullOrEmpty(name) ? root : name;
            }
            catch { return root; }
        }

        // ---- Поиск: фильтрует плитки всех колонок (через Visibility — без пересборки контейнеров) ----

        private void ApplySearchFilter(string query)
        {
            var q = query.ToLowerInvariant();

            ApplyTileFilter(AppTiles, q);
            ApplyTileFilter(ProfileTiles, q);
            ApplyTileFilter(ProjectFileTiles, q);
            ApplyTileFilter(RecentTiles, q);

            if (q == "")
            {
                // без запроса — эталонная видимость секций (по наличию данных)
                UpdateSectionVisibility();
                AppsSection.Visibility = Visibility.Visible;
                RolesSection.Visibility = Visibility.Visible;
                SearchHint.Visibility = Visibility.Visible;
                return;
            }

            // с запросом — секции без совпадений схлопываются, подсказки о настройке неуместны
            SearchHint.Visibility = Visibility.Collapsed;
            AppsSection.Visibility = VisibleCount(AppTiles) > 0 ? Visibility.Visible : Visibility.Collapsed;
            ProfilesSection.Visibility = VisibleCount(ProfileTiles) > 0 ? Visibility.Visible : Visibility.Collapsed;
            ProjectsSection.Visibility = VisibleCount(ProjectFileTiles) > 0 ? Visibility.Visible : Visibility.Collapsed;
            RecentSection.Visibility = VisibleCount(RecentTiles) > 0 ? Visibility.Visible : Visibility.Collapsed;
            RolesSection.Visibility = Visibility.Collapsed;
            ProjectsEmpty.Visibility = Visibility.Collapsed;
            RecentEmpty.Visibility = Visibility.Collapsed;
        }

        private static void ApplyTileFilter(ObservableCollection<OverlayTile> tiles, string q)
        {
            foreach (var t in tiles)
                t.TileVisibility = q == "" ||
                    t.Title.ToLowerInvariant().Contains(q) ||
                    t.Subtitle.ToLowerInvariant().Contains(q)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private static int VisibleCount(ObservableCollection<OverlayTile> collection)
        {
            var view = CollectionViewSource.GetDefaultView(collection);
            return view == null ? collection.Count : view.Cast<OverlayTile>().Count();
        }

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // placeholder гаснет сразу, фильтр — с дебаунсом (таймер перезапускается на каждое нажатие)
            SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            _searchDebounce.Stop();
            _searchDebounce.Start();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            var q = SearchBox.Text.Trim().ToLowerInvariant();

            // первый видимый кандидат: приложение -> профиль -> файл проекта -> recent
            foreach (var collection in new[] { AppTiles, ProfileTiles, ProjectFileTiles, RecentTiles })
            {
                var view = CollectionViewSource.GetDefaultView(collection);
                var first = view?.Cast<OverlayTile>().FirstOrDefault(t => t.TileVisibility == Visibility.Visible);
                if (first == null) continue;

                switch (first.Payload)
                {
                    case Preset preset:
                        _vm.SelectedPreset = preset;
                        _vm.LaunchCommand.Execute(null);
                        break;
                    case Profile profile:
                        _vm.ApplyProfile(profile);
                        _vm.LaunchCommand.Execute(null);
                        break;
                    case string file:
                        _vm.OpenProjectFile(file);
                        break;
                }
                Close();
                return;
            }
            e.Handled = true;
        }

        // ---- Клик по плитке/строке/роли/чипу ----

        private void Tile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: OverlayTile tile }) return;

            switch (tile.Payload)
            {
                case Preset preset:
                    _vm.SelectedPreset = preset;
                    _vm.LaunchCommand.Execute(null);
                    break;
                case Profile profile:
                    _vm.ApplyProfile(profile);
                    _vm.LaunchCommand.Execute(null);
                    break;
                case string file:
                    _vm.OpenProjectFile(file);
                    break;
            }
            Close();
        }

        // ---- Появление шторок: выезд + fade, уход мгновенный. ----
        // Уважает «Launcher animation» (BEHAVIOUR): галка выключена — появляются как раньше.

        /// <summary>Показать шторку с анимацией: сдвиг из (fromX, fromY) в ноль + fade 150мс, ease-out 200мс.
        /// alreadyVisible — для элементов с биндингом видимости (явный Set убил бы биндинг).</summary>
        private void AnimateDrawerIn(FrameworkElement el, double fromX, double fromY, bool alreadyVisible = false)
        {
            if (!alreadyVisible) el.Visibility = Visibility.Visible;
            if (!_vm.AnimationEnabled) return;

            var ease = new System.Windows.Media.Animation.CubicEase
                { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            var tt = new System.Windows.Media.TranslateTransform(fromX, fromY);
            el.RenderTransform = tt;
            var slideX = new System.Windows.Media.Animation.DoubleAnimation(fromX, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease };
            var slideY = new System.Windows.Media.Animation.DoubleAnimation(fromY, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease };
            var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            tt.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slideX);
            tt.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideY);
            el.BeginAnimation(OpacityProperty, fade);
            // самый долгий таймлайн гасит HoldEnd-хвосты: базовые Opacity=1, transform=null
            slideX.Completed += (_, _) =>
            {
                el.BeginAnimation(OpacityProperty, null);
                el.RenderTransform = null;
            };
        }

        private void UpdatePanelClose_Click(object sender, RoutedEventArgs e) => _vm.CloseUpdateNotes();

        private void RoleRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: OcioRoleRow row }) return;
            if (_vm.SelectedOcio == null || _vm.SelectedOcio.IsNoOcio) return; // пикать нечего — конфига нет

            // повторный клик по той же роли схлопывает шторку
            if (_expandedRole == row && RoleDrawer.Visibility == Visibility.Visible)
            {
                CollapseRoleDrawer();
                return;
            }

            LogDrawer.Visibility = Visibility.Collapsed; // шторки взаимно исключают друг друга
            _vm.CloseUpdateNotes();
            _expandedRole = row;
            RoleDrawerTitle.Text = $"ROLE: {row.RoleName}";
            RoleDrawerSearch.Text = "";
            _roleColorspaces = _vm.GetColorspaces()
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            RebuildRoleDrawerList("");
            AnimateDrawerIn(RoleDrawer, 480, 0); // выезд справа
            RoleDrawerSearch.Focus();
        }

        private OcioRoleRow? _expandedRole;
        private List<KeyValuePair<string, string>> _roleColorspaces = new();

        /// <summary>Раскрытые семейства живут между перестройками списка.</summary>
        private readonly HashSet<string> _expandedFamilies = new();

        private void RebuildRoleDrawerList(string filter)
        {
            var f = filter.Trim().ToLowerInvariant();
            RoleDrawerList.Items.Clear();
            RoleDrawerList.Items.Add(new RolePickerItem { Name = "(config default)", IsDefault = true });

            if (f != "")
            {
                // поиск: плоский список совпадений
                foreach (var kv in _roleColorspaces)
                {
                    if (!kv.Key.ToLowerInvariant().Contains(f)) continue;
                    RoleDrawerList.Items.Add(new RolePickerItem { Name = kv.Key, Family = kv.Value });
                }
                return;
            }

            // без поиска: дерево по family
            foreach (var g in _roleColorspaces
                .GroupBy(kv => string.IsNullOrEmpty(kv.Value) ? "(other)" : kv.Value)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var node = new RoleFamilyGroup { Family = g.Key, IsExpanded = _expandedFamilies.Contains(g.Key) };
                foreach (var kv in g.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                    node.Items.Add(new RolePickerItem { Name = kv.Key });
                RoleDrawerList.Items.Add(node);
            }
        }

        private void RoleFamilyHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: RoleFamilyGroup group }) return;
            group.IsExpanded = !group.IsExpanded;
            if (group.IsExpanded) _expandedFamilies.Add(group.Family);
            else _expandedFamilies.Remove(group.Family);
        }

        private void RoleDrawerSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            RebuildRoleDrawerList(RoleDrawerSearch.Text);
        }

        private void RolePickItem_Click(object sender, RoutedEventArgs e)
        {
            if (_expandedRole == null) return;
            if (sender is FrameworkElement { Tag: RolePickerItem item })
                _vm.ApplyRolePick(_expandedRole, item.IsDefault ? null : item.Name);
            CollapseRoleDrawer();
        }

        private void RoleDrawerClose_Click(object sender, RoutedEventArgs e) => CollapseRoleDrawer();

        private void CollapseRoleDrawer()
        {
            _expandedRole = null;
            RoleDrawer.Visibility = Visibility.Collapsed;
        }

        private void OcioChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: OverlayChip chip }) return;
            var match = _vm.OcioConfigs.FirstOrDefault(o => o.Name == chip.Name);
            if (match != null) _vm.SelectedOcio = match;
            RebuildOcioChips();
        }

        private void RootChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: OverlayChip chip }) return;
            var match = _vm.ProjectRoots.FirstOrDefault(r => r == chip.Path);
            if (match != null) _vm.SelectedBrowserRoot = match;
            RebuildRootChips();
            RebuildProjectFiles();
        }

        // ---- Полный функционал: drop, контекст-меню, лог ----

        /// <summary>Drag&drop как в маленьком окне: exe → новый пресет, проект-файл → открыть.</summary>
        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
            foreach (var file in files)
            {
                if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    _vm.AddPresetFromExe(file);
                else if (_vm.IsProjectFile(file))
                    _vm.OpenProjectFile(file);
            }
        }

        private void ProfileDelete_Menu(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: Profile profile })
                _vm.DeleteProfileCommand.Execute(profile);
        }

        private void RootRemove_Menu(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string root })
                _vm.RemoveProjectRootCommand.Execute(root);
        }

        /// <summary>Чип Log: тогглит шторку лога поверх низа колонок.</summary>
        private void LogBtn_Click(object sender, RoutedEventArgs e)
        {
            if (LogDrawer.Visibility == Visibility.Visible)
            {
                LogDrawer.Visibility = Visibility.Collapsed;
                return;
            }
            RoleDrawer.Visibility = Visibility.Collapsed; // шторки взаимно исключают друг друга
            _vm.CloseUpdateNotes();
            var entries = Logger.GetLastEntries(200);
            LogText.Text = entries.Count == 0 ? "No log entries yet." : string.Join(Environment.NewLine, entries);
            AnimateDrawerIn(LogDrawer, 0, 240); // подъём снизу
            LogText.ScrollToEnd();
        }

        private void LogDrawerClose_Click(object sender, RoutedEventArgs e) =>
            LogDrawer.Visibility = Visibility.Collapsed;

        private void DefaultOcioCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox combo) combo.IsDropDownOpen = false;
        }

        // ---- Клавиатурная навигация: стрелки по видимым плиткам ----

        private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match) return match;
                var deeper = FindVisualChild<T>(child);
                if (deeper != null) return deeper;
            }
            return null;
        }

        private Button? TileButtonFor(ItemsControl control, OverlayTile tile)
        {
            if (control?.ItemContainerGenerator.ContainerFromItem(tile) is System.Windows.Controls.ContentPresenter cp)
                return FindVisualChild<Button>(cp);
            return null;
        }

        /// <summary>Видимые плитки-кнопки в порядке чтения: running → apps → profiles → files → recent.</summary>
        private List<Button> NavButtons()
        {
            var result = new List<Button>();
            var pairs = new (ObservableCollection<OverlayTile> Items, ItemsControl? Host)[]
            {
                (RunningTiles, RunningList),
                (AppTiles, AppsList),
                (ProfileTiles, ProfilesList),
                (ProjectFileTiles, ProjectFilesList),
                (RecentTiles, RecentList)
            };
            foreach (var (items, host) in pairs)
            {
                if (host == null || host.Visibility != Visibility.Visible) continue;
                foreach (var tile in items)
                {
                    if (tile.TileVisibility != Visibility.Visible) continue;
                    if (TileButtonFor(host, tile) is { } b && b.Visibility == Visibility.Visible)
                        result.Add(b);
                }
            }
            return result;
        }

        private void MoveNavFocus(bool down)
        {
            if (ThemeCombo.IsDropDownOpen || DefaultOcioCombo.IsDropDownOpen) return;

            var buttons = NavButtons();
            if (buttons.Count == 0) return;

            var current = Keyboard.FocusedElement as Button;
            int idx = current != null ? buttons.IndexOf(current) : -1;
            if (idx < 0)
            {
                if (down) { buttons[0].Focus(); buttons[0].BringIntoView(); }
                else SearchBox.Focus();
                return;
            }

            int next = idx + (down ? 1 : -1);
            if (next < 0) { SearchBox.Focus(); return; }
            if (next >= buttons.Count) return;
            buttons[next].Focus();
            buttons[next].BringIntoView();
        }

        // ---- Кнопки окна ----

        /// <summary>ПКМ на плитке: меню квик-команд запуска + сохранение профиля. Собирается на открытие.</summary>
        private void AppTileMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu) return;
            if (menu.PlacementTarget is not FrameworkElement { DataContext: OverlayTile tile } ||
                tile.Payload is not Preset preset) return;

            menu.Items.Clear();

            foreach (var (cmd, desc) in _vm.GetCommandsForApp(preset.Name.ToLower()))
            {
                var item = new System.Windows.Controls.MenuItem { Header = $"Launch: {desc}" };
                item.Click += (_, _) =>
                {
                    _vm.LaunchPresetWithCommand(preset, cmd);
                    Close();
                };
                menu.Items.Add(item);
            }
            if (menu.Items.Count > 0)
                menu.Items.Add(new System.Windows.Controls.Separator());

            var save = new System.Windows.Controls.MenuItem { Header = "Save as profile" };
            save.Click += (_, _) =>
            {
                _vm.SelectedPreset = preset;
                _vm.SaveProfileCommand.Execute(null);
            };
            menu.Items.Add(save);
        }

        private void CaptionMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void SwitchLauncher_Click(object sender, RoutedEventArgs e) => _ui?.SwitchLauncher();

        private void CaptionMaximize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void CaptionClose_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // открытый дропдаун комбо гасит Esc сам — окно не закрываем
                if (ThemeCombo.IsDropDownOpen || DefaultOcioCombo.IsDropDownOpen) return;
                // слои: шторка лога → пикер ролей → окно
                if (LogDrawer.Visibility == Visibility.Visible)
                {
                    LogDrawer.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                    return;
                }
                if (RoleDrawer.Visibility == Visibility.Visible)
                {
                    CollapseRoleDrawer();
                    e.Handled = true;
                    return;
                }
                if (UpdatePanelVisible)
                {
                    _vm.CloseUpdateNotes();
                    e.Handled = true;
                    return;
                }
                Close();
                return;
            }
            if (e.Key == Key.Down) { MoveNavFocus(down: true); e.Handled = true; }
            else if (e.Key == Key.Up) { MoveNavFocus(down: false); e.Handled = true; }
        }
    }

    /// <summary>Элемент списка пикера: колорспейс (или запись сброса в конфиг).</summary>
    public class RolePickerItem
    {
        public string Name { get; init; } = "";
        public string Family { get; init; } = "";
        public bool IsDefault { get; init; }
    }

    /// <summary>Семейство колорспейсов в дереве шторки ролей (кастомный сворачиваемый узел).</summary>
    public class RoleFamilyGroup : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isExpanded;

        public string Family { get; init; } = "";
        public List<RolePickerItem> Items { get; } = new();

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new(nameof(IsExpanded)));
                PropertyChanged?.Invoke(this, new(nameof(ItemsVisibility)));
                PropertyChanged?.Invoke(this, new(nameof(Header)));
            }
        }

        public System.Windows.Visibility ItemsVisibility =>
            _isExpanded ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public string Header => $"{(_isExpanded ? "▾" : "▸")} {Family} ({Items.Count})";

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}
