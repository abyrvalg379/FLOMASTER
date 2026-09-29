using System;
using System.IO;
using System.Runtime.InteropServices;
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
        private const int BaseHeight = 560; // свёрнутое окно вмещает весь стек вкладок + ARGUMENTS (32 из них — шапка)

        // ---- Глобальный хоткей Ctrl+Alt+F: показать/спрятать лаунчер поверх всего ----
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        private const int HOTKEY_ID = 0xF10;
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000, VK_F = 0x46;
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private static readonly IntPtr HwndNotTopmost = new IntPtr(-2);
        private const uint SwpNomove = 0x2, SwpNosize = 0x1, SwpShowwindow = 0x40;
        private bool _hotkeyRegistered;

        public MainWindow()
        {
            InitializeComponent();

            // Set ViewModel as DataContext (composition root: ручной DI без контейнеров)
            var ocio = new OcioService();
            _ocioService = ocio;
            var viewModel = new MainViewModel(new ConfigManager(), ocio, new LaunchService(ocio));
            DataContext = viewModel;
            _viewModel = viewModel;

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
                else if (e.PropertyName == nameof(viewModel.HotkeyEnabled))
                {
                    ApplyHotkey(viewModel.HotkeyEnabled);
                }
                else if (e.PropertyName == nameof(viewModel.RecentPanelVisible) ||
                    e.PropertyName == nameof(viewModel.ArgsPanelVisible) ||
                    e.PropertyName == nameof(viewModel.SettingsPanelVisible) ||
                    e.PropertyName == nameof(viewModel.RolesPanelVisible) ||
                    e.PropertyName == nameof(viewModel.ProfilesPanelVisible) ||
                    e.PropertyName == nameof(viewModel.ProjectsPanelVisible))
                {
                    double h = BaseHeight;
                    if (viewModel.RecentPanelVisible) h += 200;
                    if (viewModel.ArgsPanelVisible) h += 200;
                    if (viewModel.SettingsPanelVisible) h += 200;
                    if (viewModel.RolesPanelVisible) h += 220;
                    if (viewModel.ProfilesPanelVisible) h += 180;
                    if (viewModel.ProjectsPanelVisible) h += 220;
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

            // Recent files click handler: открыть файл выбранным приложением
            var recentList = (System.Windows.Controls.ListBox)FindName("RecentList");
            if (recentList != null)
            {
                recentList.SelectionChanged += (s, e) =>
                {
                    if (recentList.SelectedItem is string file)
                    {
                        viewModel.OpenRecentFile(file);
                        recentList.SelectedIndex = -1; // deselect
                    }
                };
            }

            // Global hotkey: регистрация требует hwnd
            SourceInitialized += (s, e) => ApplyHotkey(viewModel.HotkeyEnabled);

            // WindowStyle=None отключает системное скругление Windows 11 — DWM-атрибут + клип бордера
            SourceInitialized += (s, e) => UiHelper.RoundCorners(this);
            Loaded += (s, e) => ClipRootBorder();
            SizeChanged += (s, e) => ClipRootBorder();

            // Setup tray
            SetupTray(viewModel);
        }

        private void ApplyHotkey(bool enabled)
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            if (_hotkeyRegistered)
            {
                UnregisterHotKey(handle, HOTKEY_ID);
                _hotkeyRegistered = false;
            }
            if (enabled)
            {
                // MOD_NOREPEAT: автоповтор клавиатуры не дёргает тоггл
                _hotkeyRegistered = RegisterHotKey(handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_F);
                if (_hotkeyRegistered)
                {
                    var source = System.Windows.Interop.HwndSource.FromHwnd(handle);
                    source?.AddHook(WndProc);
                    Logger.Log("Hotkey", "Registered Ctrl+Alt+F", "info");
                }
                else Logger.Log("Hotkey", "RegisterHotKey failed (combination busy?)", "warn");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleWindow();
                handled = true;
            }
            return IntPtr.Zero;
        }

        /// <summary>Активно и видно — спрятать; иначе показать и поднять поверх всего.</summary>
        private void ToggleWindow()
        {
            Logger.Log("Hotkey", $"Toggle: visible={IsVisible} active={IsActive}", "info");
            if (IsVisible)
            {
                Hide();
                return;
            }
            Show();
            WindowState = WindowState.Normal;
            Activate();
            // всплытие поверх DCC: Win32-topmost на мгновение, WPF-биндинг Topmost не трогаем
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpShowwindow);
            SetWindowPos(handle, HwndNotTopmost, 0, 0, 0, 0, SwpNomove | SwpNosize);
        }

        private MainViewModel? _viewModel;
        private OcioService? _ocioService;

        private void RolePick_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not ViewModels.OcioRoleRow row) return;
            var ocio = _viewModel?.SelectedOcio;
            if (ocio == null || string.IsNullOrEmpty(ocio.Path) || !File.Exists(ocio.Path)) return;
            if (_viewModel == null) return;

            var (_, colorspaces) = _ocioService.Parse(ocio.Path);
            string? current = null;
            if (_viewModel.SelectedPreset?.RoleOverrides != null &&
                _viewModel.SelectedPreset.RoleOverrides.TryGetValue(row.RoleName, out var v))
                current = v;

            var picker = UiHelper.CreateColorspacePicker(this, row.RoleName, current, colorspaces,
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

        private void ApplyTheme(string themeName)
        {
            var t = ThemeManager.GetTheme(
                ThemeManager.ThemeOrder.FirstOrDefault(k => ThemeManager.Themes[k].Name == themeName) ?? "blender"
            );
            Application.Current.Resources["BgBrush"] = ThemeManager.Brush(t.Bg);
            Application.Current.Resources["PanelBrush"] = ThemeManager.Brush(t.Panel);
            Application.Current.Resources["AccentBrush"] = ThemeManager.Brush(t.Accent);
            Application.Current.Resources["AccentTextBrush"] = ThemeManager.Brush(string.IsNullOrEmpty(t.AccentText) ? "#FFFFFF" : t.AccentText);
            Application.Current.Resources["TextBrush"] = ThemeManager.Brush(t.Text);
            Application.Current.Resources["DimBrush"] = ThemeManager.Brush(t.Dim);
            Application.Current.Resources["BorderBrush"] = ThemeManager.Brush(t.Border);

            var accentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(t.Accent);
            Application.Current.Resources["AccentHoverBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x55, accentColor.R, accentColor.G, accentColor.B));
            Application.Current.Resources["AccentPressBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x77, accentColor.R, accentColor.G, accentColor.B));
            Application.Current.Resources["AccentLightBrush"] = new System.Windows.Media.SolidColorBrush(ShiftColor(accentColor, 1.18));
            Application.Current.Resources["AccentDarkBrush"] = new System.Windows.Media.SolidColorBrush(ShiftColor(accentColor, 0.82));

            // System color overrides for ComboBox dropdowns
            Application.Current.Resources[System.Windows.SystemColors.WindowBrushKey] = ThemeManager.Brush(t.Panel);
            Application.Current.Resources[System.Windows.SystemColors.WindowTextBrushKey] = ThemeManager.Brush(t.Text);
            Application.Current.Resources[System.Windows.SystemColors.ControlBrushKey] = ThemeManager.Brush(t.Panel);
            Application.Current.Resources[System.Windows.SystemColors.ControlTextBrushKey] = ThemeManager.Brush(t.Text);
            Application.Current.Resources[System.Windows.SystemColors.HighlightBrushKey] = ThemeManager.Brush(t.Accent);
            Application.Current.Resources[System.Windows.SystemColors.HighlightTextBrushKey] = ThemeManager.Brush(string.IsNullOrEmpty(t.AccentText) ? "#FFFFFF" : t.AccentText);
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

            _trayIcon.ContextMenuStrip = new WinForms.ContextMenuStrip();
            RebuildTrayMenu(viewModel);
            viewModel.Presets.CollectionChanged += (s, e) => RebuildTrayMenu(viewModel);
            viewModel.Profiles.CollectionChanged += (s, e) => RebuildTrayMenu(viewModel);

            _trayIcon.DoubleClick += (s, e) => { Show(); WindowState = WindowState.Normal; Activate(); };
        }

        private void RebuildTrayMenu(MainViewModel viewModel)
        {
            var menu = _trayIcon.ContextMenuStrip;
            if (menu == null) return;
            menu.Items.Clear();

            foreach (var preset in viewModel.Presets)
            {
                var item = menu.Items.Add(preset.Name);
                item.Click += (s, e) =>
                {
                    if (!File.Exists(preset.Exe))
                    {
                        Logger.Log("Tray", $"Preset exe not found: {preset.Exe}", "warn");
                        viewModel.StatusText = $"{preset.Name}: exe not found";
                        return;
                    }
                    viewModel.SelectedPreset = preset;
                    viewModel.LaunchCommand.Execute(null);
                };
            }

            // Профили: применение состояния + запуск тем же путём, что и Launch
            if (viewModel.Profiles.Count > 0)
            {
                menu.Items.Add(new WinForms.ToolStripSeparator());
                var header = menu.Items.Add("PROFILES");
                header.Enabled = false;

                foreach (var profile in viewModel.Profiles)
                {
                    var item = menu.Items.Add(profile.Name);
                    item.Click += (s, e) =>
                    {
                        viewModel.ApplyProfile(profile);
                        var exe = viewModel.SelectedPreset?.Exe;
                        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                        {
                            Logger.Log("Tray", $"Profile '{profile.Name}': exe not found: {exe}", "warn");
                            return;
                        }
                        viewModel.LaunchCommand.Execute(null);
                    };
                }
            }

            menu.Items.Add(new WinForms.ToolStripSeparator());
            var showItem = menu.Items.Add("Show FLOMASTER");
            showItem.Click += (s, e) => { Show(); WindowState = WindowState.Normal; Activate(); };
            var quitItem = menu.Items.Add("Quit");
            quitItem.Click += (s, e) => { _trayIcon.Visible = false; _trayIcon.Dispose(); Close(); };
        }

        /// <summary>Клип контента по скруглению корневого бордера (после AllowsTransparency).</summary>
        private void ClipRootBorder()
        {
            RootBorder.Clip = new System.Windows.Media.RectangleGeometry(
                new Rect(0, 0, RootBorder.ActualWidth, RootBorder.ActualHeight), 10, 10);
        }

        private void CaptionMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void CaptionClose_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ApplyHotkey(false);
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
        }
    }
}
