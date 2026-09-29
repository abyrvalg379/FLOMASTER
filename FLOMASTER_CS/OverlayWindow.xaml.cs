using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FLOMASTER.Models;
using FLOMASTER.ViewModels;

namespace FLOMASTER
{
    /// <summary>
    /// Полноэкранный оверлей: плитки приложений (с иконками exe), профилей (монограммы)
    /// и последних файлов. Клик = действие + закрытие. Esc или повторный хоткей — закрыть.
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

        private readonly MainViewModel _vm;

        public OverlayWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _vm = viewModel;
            DataContext = this;

            BuildTiles();

            // пустые секции целиком скрыты — пустые состояния в fullscreen выглядят дырой
            ProfilesSection.Visibility = ProfileTiles.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            RecentSection.Visibility = RecentTiles.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        public ObservableCollection<OverlayTile> AppTiles { get; } = new();
        public ObservableCollection<OverlayTile> ProfileTiles { get; } = new();
        public ObservableCollection<OverlayTile> RecentTiles { get; } = new();

        private void BuildTiles()
        {
            foreach (var preset in _vm.Presets)
            {
                var fileName = SafeFileName(preset.Exe);
                AppTiles.Add(new OverlayTile
                {
                    Title = preset.Name,
                    Subtitle = fileName,
                    Icon = ExtractIcon(preset.Exe),
                    Payload = preset
                });
            }

            foreach (var profile in _vm.Profiles)
            {
                var letter = string.IsNullOrWhiteSpace(profile.Name) ? "?" : profile.Name.Trim().ToUpperInvariant()[0].ToString();
                ProfileTiles.Add(new OverlayTile
                {
                    Title = profile.Name,
                    Subtitle = $"{profile.PresetName} · {profile.OcioName}",
                    Monogram = letter,
                    Payload = profile
                });
            }

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
        }

        private static string SafeFileName(string? path)
        {
            try { return Path.GetFileName(path) ?? ""; } catch { return path ?? ""; }
        }

        private static string SafeDir(string? path)
        {
            try { return Path.GetDirectoryName(path) ?? ""; } catch { return ""; }
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
                    _vm.OpenRecentFile(file);
                    break;
            }
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }
    }
}
