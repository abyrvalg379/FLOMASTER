using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
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
        private System.Windows.Forms.Screen _screen = System.Windows.Forms.Screen.PrimaryScreen
            ?? System.Windows.Forms.Screen.AllScreens[0];

        public OverlayWindow(MainViewModel viewModel, Window owner)
        {
            InitializeComponent();
            _vm = viewModel;
            _owner = owner;
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
            var b = _screen.Bounds;
            Left = b.Left; Top = b.Top; Width = b.Width; Height = b.Height;
        }

        private void SnapToMonitorIfChanged()
        {
            try
            {
                var center = new System.Drawing.Point(
                    (int)(Left + Width / 2), (int)(Top + Height / 2));
                var current = System.Windows.Forms.Screen.FromPoint(center);
                if (!current.Equals(_screen))
                {
                    _screen = current;
                    ApplyScreenBounds();
                }
            }
            catch { }
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
            var roots = _vm.OcioConfigs;
            foreach (var o in roots)
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

        // ---- Кнопки окна ----

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
