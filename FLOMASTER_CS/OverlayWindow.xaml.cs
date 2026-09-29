using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
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
    public partial class OverlayWindow : Window
    {
        /// <summary>Плитка: полезная нагрузка (Preset/Profile/строка-файл) + данные отображения.</summary>
        public class OverlayTile
        {
            public string Title { get; set; } = "";
            public string Subtitle { get; set; } = "";
            public ImageSource? Icon { get; set; }
            public string Monogram { get; set; } = "";
            public string Badge { get; set; } = "";
            public string Dir { get; set; } = "";
            public object? Payload { get; set; }
        }

        /// <summary>Чип выбора (конфиг/корень проектов).</summary>
        public class OverlayChip
        {
            public string Name { get; set; } = "";
            public string Path { get; set; } = "";
            public bool IsSelected { get; set; }
        }

        private readonly MainViewModel _vm;
        private readonly Window? _owner;
        private readonly System.Windows.Forms.Screen? _preferredScreen;
        private System.Windows.Forms.Screen _screen = System.Windows.Forms.Screen.PrimaryScreen
            ?? System.Windows.Forms.Screen.AllScreens[0];

        public OverlayWindow(MainViewModel viewModel, Window owner,
            System.Windows.Forms.Screen? preferredScreen = null)
        {
            InitializeComponent();
            _vm = viewModel;
            _owner = owner;
            _preferredScreen = preferredScreen;
            DataContext = this;

            BuildAppAndProfileTiles();
            RebuildOcioChips();
            RebuildRootChips();
            RebuildProjectFiles();
            RebuildRecentTiles();
            UpdateSectionVisibility();

            PositionOnOwnerScreen();
            LocationChanged += (_, _) => SnapToMonitorIfChanged();
            StateChanged += (_, _) => { if (WindowState == WindowState.Normal) ApplyScreenBounds(); };

            SearchBox.Focus();
        }

        // ---- Коллекции плиток/чипов ----

        public ObservableCollection<OverlayTile> AppTiles { get; } = new();
        public ObservableCollection<OverlayTile> ProfileTiles { get; } = new();
        public ObservableCollection<OverlayTile> RecentTiles { get; } = new();
        public ObservableCollection<OverlayTile> ProjectFileTiles { get; } = new();
        public ObservableCollection<OverlayChip> OcioChips { get; } = new();
        public ObservableCollection<OverlayChip> RootChips { get; } = new();

        // ---- Прокси к ViewModel (DataContext оверлея — сам оверлей) ----

        public ObservableCollection<OcioRoleRow> RoleRows => _vm.OcioRoleRows;
        public ICommand ResetRolesCommand => _vm.ResetRolesCommand;
        public ICommand ClearArgsCommand => _vm.ClearArgsCommand;
        public ObservableCollection<string> Themes => _vm.Themes;
        public ObservableCollection<QuickCommand> QuickCommands => _vm.QuickCommands;

        public string ArgsText
        {
            get => _vm.ArgsText ?? "";
            set => _vm.ArgsText = value;
        }

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

        // ---- Мониторы: открытие на мониторе лаунчера, слежение за перетаскиванием ----

        private void PositionOnOwnerScreen()
        {
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
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(System.Drawing.Point pt, uint flags);
        [System.Runtime.InteropServices.DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmon, int type, out uint dpiX, out uint dpiY);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr hmon, ref MONITORINFOEX info);
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;

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
                    Path = o.Path ?? "",
                    IsSelected = _vm.SelectedOcio?.Name == o.Name
                });
        }

        private void OcioChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: OverlayChip chip }) return;
            var match = _vm.OcioConfigs.FirstOrDefault(o => o.Name == chip.Name);
            if (match != null) _vm.SelectedOcio = match;
            RebuildOcioChips();
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
            ProjectsEmpty.Visibility = ProjectFileTiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RootChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: OverlayChip chip }) return;
            var match = _vm.ProjectRoots.FirstOrDefault(r => r == chip.Path);
            if (match != null) _vm.SelectedBrowserRoot = match;
            RebuildRootChips();
            RebuildProjectFiles();
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
            RecentSection.Visibility = RecentTiles.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateSectionVisibility()
        {
            RebuildRootChips();
            RebuildProjectFiles();
            RebuildRecentTiles();
            ProfilesList.Visibility = _vm.Profiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ProfilesEmpty.Visibility = _vm.Profiles.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
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

        // ---- Поиск: фильтрует плитки всех колонок ----

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var q = SearchBox.Text.Trim().ToLowerInvariant();
            SearchHint.Visibility = q == "" ? Visibility.Visible : Visibility.Collapsed;

            ApplyFilter(CollectionViewSource.GetDefaultView(AppTiles),
                o => q == "" || Match((OverlayTile)o, q));
            AppsSection.Visibility = VisibleCount(AppTiles) > 0 || q == "" ? Visibility.Visible : Visibility.Collapsed;

            ApplyFilter(CollectionViewSource.GetDefaultView(ProjectFileTiles),
                o => q == "" || Match((OverlayTile)o, q));
            ProjectFilesList.Visibility = VisibleCount(ProjectFileTiles) > 0 || q == "" ? Visibility.Visible : Visibility.Collapsed;

            ApplyFilter(CollectionViewSource.GetDefaultView(RecentTiles),
                o => q == "" || Match((OverlayTile)o, q));
            RecentList.Visibility = VisibleCount(RecentTiles) > 0 || q == "" ? Visibility.Visible : Visibility.Collapsed;
        }

        private static bool Match(OverlayTile tile, string q) =>
            tile.Title.ToLowerInvariant().Contains(q) || tile.Subtitle.ToLowerInvariant().Contains(q);

        private static void ApplyFilter(System.ComponentModel.ICollectionView view, Predicate<object> filter)
        {
            if (view == null) return;
            view.Filter = filter;
            view.Refresh();
        }

        private static int VisibleCount(ObservableCollection<OverlayTile> collection)
        {
            var view = CollectionViewSource.GetDefaultView(collection);
            return view == null ? collection.Count : view.Cast<OverlayTile>().Count();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            // первый видимый кандидат: приложение -> файл проекта -> recent
            foreach (var collection in new[] { AppTiles, ProjectFileTiles, RecentTiles })
            {
                var view = CollectionViewSource.GetDefaultView(collection);
                var first = view.Cast<OverlayTile>().FirstOrDefault();
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

        // ---- Клик по плитке/строке/роли ----

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

        private void RoleRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: OcioRoleRow row }) return;
            if (_vm.SelectedOcio == null) return;

            var picker = UiHelper.CreateColorspacePicker(
                this, row.RoleName, _vm.GetRoleOverride(row.RoleName),
                _vm.GetColorspaces(),
                name => _vm.ApplyRolePick(row, name));
            picker.ShowDialog();
        }


        private void CaptionMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void CaptionMaximize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void CaptionClose_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }
    }
}
