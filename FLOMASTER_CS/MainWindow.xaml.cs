using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using FLOMASTER.ViewModels;
using FLOMASTER.Services;

namespace FLOMASTER
{
    /// <summary>
    /// Маленькое окно лаунчера. Трей, глобальный хоткей и создание оверлея живут на уровне
    /// приложения (TrayService / HotkeyService / UiController) — окно может вообще не
    /// создаваться (старт с дашборда). Закрытие = скрыть в трей; настоящий выход — Quit.
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int BaseHeight = 560; // свёрнутое окно вмещает весь стек вкладок + ARGUMENTS (32 из них — шапка)

        private readonly MainViewModel _viewModel;
        private readonly UiController? _ui;

        public MainWindow(MainViewModel viewModel, UiController? ui = null)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _ui = ui;
            DataContext = viewModel;

            // Open in top-right corner of the screen
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = SystemParameters.WorkArea.Right - Width;
            Top = SystemParameters.WorkArea.Top;

            // Drag & drop: exe → new preset, project file → open in selected app
            Drop += (s, e) =>
            {
                if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files) return;
                foreach (var file in files)
                {
                    if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        _viewModel.AddPresetFromExe(file);
                    else if (_viewModel.IsProjectFile(file))
                        _viewModel.OpenProjectFile(file);
                }
            };

            // Раскрытие панелей анимирует высоту окна (тема применяется на уровне App)
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.RecentPanelVisible) ||
                    e.PropertyName == nameof(_viewModel.ArgsPanelVisible) ||
                    e.PropertyName == nameof(_viewModel.SettingsPanelVisible) ||
                    e.PropertyName == nameof(_viewModel.RolesPanelVisible) ||
                    e.PropertyName == nameof(_viewModel.ProfilesPanelVisible) ||
                    e.PropertyName == nameof(_viewModel.ProjectsPanelVisible))
                {
                    double h = BaseHeight;
                    if (_viewModel.RecentPanelVisible) h += 200;
                    if (_viewModel.ArgsPanelVisible) h += 200;
                    if (_viewModel.SettingsPanelVisible) h += 200;
                    if (_viewModel.RolesPanelVisible) h += 220;
                    if (_viewModel.ProfilesPanelVisible) h += 180;
                    if (_viewModel.ProjectsPanelVisible) h += 220;
                    AnimateToHeight(h, _viewModel.AnimationEnabled);
                }
            };

            // Close dropdowns on selection
            var appCombo = (System.Windows.Controls.ComboBox)FindName("AppCombo");
            var ocioCombo = (System.Windows.Controls.ComboBox)FindName("OcioCombo");
            var themeCombo = (System.Windows.Controls.ComboBox)FindName("ThemeCombo");

            if (appCombo != null)
                appCombo.SelectionChanged += (s, e) => { appCombo.IsDropDownOpen = false; };
            if (ocioCombo != null)
                ocioCombo.SelectionChanged += (s, e) => { ocioCombo.IsDropDownOpen = false; };
            if (themeCombo != null)
                themeCombo.SelectionChanged += (s, e) => { themeCombo.IsDropDownOpen = false; };

            // Set window icon
            try
            {
                var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flomaster.ico");
                if (File.Exists(icoPath))
                    Icon = BitmapFrame.Create(new Uri(icoPath, UriKind.Absolute));
            }
            catch { }

            // Log button - show popup window
            var logBtn = (System.Windows.Controls.Button)FindName("LogBtn");
            if (logBtn != null)
            {
                logBtn.Click += (s, e) =>
                {
                    var entries = Logger.GetLastEntries(50);
                    if (entries.Count == 0)
                    {
                        _viewModel.StatusText = "No log entries yet";
                        return;
                    }
                    var logContent = string.Join(Environment.NewLine, entries);
                    var logWindow = UiHelper.CreateLogWindow(logContent, this);
                    logWindow.ShowDialog();
                };
            }

            // Quick commands click handler
            var quickList = (System.Windows.Controls.ListBox)FindName("QuickCommandsList");
            if (quickList != null)
            {
                quickList.SelectionChanged += (s, e) =>
                {
                    if (quickList.SelectedItem is QuickCommand cmd)
                    {
                        var current = _viewModel.ArgsText?.Trim() ?? "";
                        _viewModel.ArgsText = string.IsNullOrEmpty(current) ? cmd.Cmd : $"{current} {cmd.Cmd}";
                        quickList.SelectedIndex = -1; // deselect
                    }
                };
            }

            // Recent files click handler: открыть файл выбранным приложением
            var recentList = (System.Windows.Controls.ListBox)FindName("RecentList");
            if (recentList != null)
            {
                recentList.SelectionChanged += (s, e) =>
                {
                    if (recentList.SelectedItem is string file)
                    {
                        _viewModel.OpenRecentFile(file);
                        recentList.SelectedIndex = -1; // deselect
                    }
                };
            }

            // WindowStyle=None отключает системное скругление Windows 11 — DWM-атрибут + клип бордера
            SourceInitialized += (s, e) => UiHelper.RoundCorners(this);
            Loaded += (s, e) => ClipRootBorder();
            SizeChanged += (s, e) => ClipRootBorder();
        }

        private void RolePick_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not ViewModels.OcioRoleRow row) return;
            var ocio = _viewModel.SelectedOcio;
            if (ocio == null || string.IsNullOrEmpty(ocio.Path) || !File.Exists(ocio.Path)) return;

            string? current = null;
            if (_viewModel.SelectedPreset?.RoleOverrides != null &&
                _viewModel.SelectedPreset.RoleOverrides.TryGetValue(row.RoleName, out var v))
                current = v;

            var picker = UiHelper.CreateColorspacePicker(this, row.RoleName, current, _viewModel.GetColorspaces(),
                name => _viewModel.ApplyRolePick(row, name));
            picker.ShowDialog();
        }

        private void AnimateToHeight(double target, bool animate)
        {
            target = Math.Min(target, MaxHeight);
            BeginAnimation(Window.HeightProperty, null);

            if (!animate || Math.Abs(Height - target) < 1)
            {
                Height = target;
                return;
            }

            var anim = new DoubleAnimation(Height, target, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(Window.HeightProperty, anim);
        }

        /// <summary>Клип контента по скруглению корневого бордера (после AllowsTransparency).</summary>
        private void ClipRootBorder()
        {
            RootBorder.Clip = new System.Windows.Media.RectangleGeometry(
                new Rect(0, 0, RootBorder.ActualWidth, RootBorder.ActualHeight), 10, 10);
        }

        private void CaptionMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void SwitchLauncher_Click(object sender, RoutedEventArgs e) => _ui?.SwitchLauncher();

        private void CaptionClose_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // лаунчер живёт в трее: крестик прячет окно, настоящий выход — Quit (App.IsExiting)
            if (!App.IsExiting)
            {
                e.Cancel = true;
                Hide();
            }
        }
    }
}
