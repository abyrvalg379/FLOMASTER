using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using FLOMASTER.ViewModels;
using FLOMASTER.Services;
using WinForms = System.Windows.Forms;

namespace FLOMASTER
{
    public partial class MainWindow : Window
    {
        private WinForms.NotifyIcon _trayIcon;
        private const int BaseHeight = 500;

        public MainWindow()
        {
            InitializeComponent();

            // Set ViewModel as DataContext
            var viewModel = new MainViewModel();
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
                        viewModel.AddPresetFromExe(file);
                    else if (viewModel.IsProjectFile(file))
                        viewModel.OpenProjectFile(file);
                }
            };

            // Apply initial theme
            ApplyTheme(viewModel.SelectedTheme);

            // Update when properties change
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(viewModel.SelectedTheme))
                {
                    ApplyTheme(viewModel.SelectedTheme);
                }
                else if (e.PropertyName == nameof(viewModel.RecentPanelVisible) ||
                    e.PropertyName == nameof(viewModel.ArgsPanelVisible) ||
                    e.PropertyName == nameof(viewModel.SettingsPanelVisible))
                {
                    double h = BaseHeight;
                    if (viewModel.RecentPanelVisible) h += 200;
                    if (viewModel.ArgsPanelVisible) h += 200;
                    if (viewModel.SettingsPanelVisible) h += 200;
                    AnimateToHeight(h, viewModel.AnimationEnabled);
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
                        viewModel.StatusText = "No log entries yet";
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
                        var current = viewModel.ArgsText?.Trim() ?? "";
                        viewModel.ArgsText = string.IsNullOrEmpty(current) ? cmd.Cmd : $"{current} {cmd.Cmd}";
                        quickList.SelectedIndex = -1; // deselect
                    }
                };
            }

            // Setup tray
            SetupTray(viewModel);
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

        private void ApplyTheme(string themeName)
        {
            var t = ThemeManager.GetTheme(
                ThemeManager.ThemeOrder.FirstOrDefault(k => ThemeManager.Themes[k].Name == themeName) ?? "blender"
            );
            Resources["BgBrush"] = ThemeManager.Brush(t.Bg);
            Resources["PanelBrush"] = ThemeManager.Brush(t.Panel);
            Resources["AccentBrush"] = ThemeManager.Brush(t.Accent);
            Resources["AccentTextBrush"] = ThemeManager.Brush(string.IsNullOrEmpty(t.AccentText) ? "#FFFFFF" : t.AccentText);
            Resources["TextBrush"] = ThemeManager.Brush(t.Text);
            Resources["DimBrush"] = ThemeManager.Brush(t.Dim);
            Resources["BorderBrush"] = ThemeManager.Brush(t.Border);

            var accentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(t.Accent);
            Resources["AccentHoverBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x55, accentColor.R, accentColor.G, accentColor.B));
            Resources["AccentPressBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x77, accentColor.R, accentColor.G, accentColor.B));
            Resources["AccentLightBrush"] = new System.Windows.Media.SolidColorBrush(ShiftColor(accentColor, 1.18));
            Resources["AccentDarkBrush"] = new System.Windows.Media.SolidColorBrush(ShiftColor(accentColor, 0.82));

            // System color overrides for ComboBox dropdowns
            Resources[System.Windows.SystemColors.WindowBrushKey] = ThemeManager.Brush(t.Panel);
            Resources[System.Windows.SystemColors.WindowTextBrushKey] = ThemeManager.Brush(t.Text);
            Resources[System.Windows.SystemColors.ControlBrushKey] = ThemeManager.Brush(t.Panel);
            Resources[System.Windows.SystemColors.ControlTextBrushKey] = ThemeManager.Brush(t.Text);
            Resources[System.Windows.SystemColors.HighlightBrushKey] = ThemeManager.Brush(t.Accent);
            Resources[System.Windows.SystemColors.HighlightTextBrushKey] = ThemeManager.Brush(string.IsNullOrEmpty(t.AccentText) ? "#FFFFFF" : t.AccentText);
        }

        private static System.Windows.Media.Color ShiftColor(System.Windows.Media.Color c, double k)
        {
            return System.Windows.Media.Color.FromRgb(
                (byte)Math.Min(255, c.R * k),
                (byte)Math.Min(255, c.G * k),
                (byte)Math.Min(255, c.B * k));
        }

        private void SetupTray(MainViewModel viewModel)
        {
            _trayIcon = new WinForms.NotifyIcon();
            try
            {
                var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flomaster.ico");
                _trayIcon.Icon = File.Exists(icoPath)
                    ? new System.Drawing.Icon(icoPath)
                    : System.Drawing.SystemIcons.Application;
            }
            catch { _trayIcon.Icon = System.Drawing.SystemIcons.Application; }

            _trayIcon.Text = "FLOMASTER";
            _trayIcon.Visible = true;

            var menu = new WinForms.ContextMenuStrip();
            foreach (var p in viewModel.Presets)
            {
                var preset = p;
                var item = menu.Items.Add(p.Name);
                item.Click += (s, e) =>
                {
                    if (File.Exists(preset.Exe))
                    {
                        viewModel.SelectedPreset = preset;
                        viewModel.LaunchCommand.Execute(null);
                    }
                };
            }

            menu.Items.Add(new WinForms.ToolStripSeparator());
            var showItem = menu.Items.Add("Show FLOMASTER");
            showItem.Click += (s, e) => { Show(); WindowState = WindowState.Normal; Activate(); };
            var quitItem = menu.Items.Add("Quit");
            quitItem.Click += (s, e) => { _trayIcon.Visible = false; _trayIcon.Dispose(); Close(); };

            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, e) => { Show(); WindowState = WindowState.Normal; Activate(); };
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
        }
    }
}
