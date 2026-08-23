using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FLOMASTER.Models;
using FLOMASTER.Services;
using WinForms = System.Windows.Forms;

namespace FLOMASTER
{
    public partial class MainWindow : Window
    {
        private Config _config;
        private readonly string _scriptDir;
        private WinForms.NotifyIcon _trayIcon;
        private int _baseHeight = 500;
        private System.Windows.Threading.DispatcherTimer _animTimer;
        private double _targetHeight;
        private bool _animating;

        public MainWindow()
        {
            InitializeComponent();
            _scriptDir = AppDomain.CurrentDomain.BaseDirectory;
            _config = ConfigManager.Load();

            // Auto-scan on first run
            if (_config.Presets.Count == 0)
            {
                var scanned = DccScanner.Scan(_config.ScanPaths);
                foreach (var app in scanned) { /* OCIO assigned at launch from combo */ }
                _config.Presets = scanned;
                ConfigManager.Save(_config);
            }

            InitializeUI();
            SetupTray();
            RegisterContextMenu();
        }

        private void InitializeUI()
        {
            // Set window icon
            var icoPath = Path.Combine(_scriptDir, "flomaster.ico");
            if (File.Exists(icoPath))
            {
                try
                {
                    var uri = new Uri(icoPath, UriKind.Absolute);
                    Icon = BitmapFrame.Create(uri);
                }
                catch { }
            }

            // Populate theme combo
            foreach (var key in ThemeManager.ThemeOrder)
            {
                ThemeCombo.Items.Add(new { Name = ThemeManager.Themes[key].Name, Key = key });
            }
            var currentIdx = ThemeManager.ThemeOrder.IndexOf(_config.Theme);
            ThemeCombo.SelectedIndex = currentIdx >= 0 ? currentIdx : 0;

            ThemeCombo.SelectionChanged += (s, e) =>
            {
                var newTheme = ThemeManager.ThemeOrder[ThemeCombo.SelectedIndex];
                if (newTheme != _config.Theme)
                {
                    _config.Theme = newTheme;
                    ConfigManager.Save(_config);
                    ApplyTheme(newTheme);
                }
            };

            // Populate app combo
            RefreshApps();
            AppCombo.SelectionChanged += (s, e) => { AppCombo.IsDropDownOpen = false; };

            // Populate OCIO combo
            RefreshOcio();
            OcioCombo.SelectionChanged += (s, e) => { OcioCombo.IsDropDownOpen = false; };

            // Apply theme
            ApplyTheme(_config.Theme);

            // Wire up events
            WireEvents();
        }

        private void ApplyTheme(string themeKey)
        {
            var t = ThemeManager.GetTheme(themeKey);
            Resources["BgBrush"] = ThemeManager.Brush(t.Bg);
            Resources["PanelBrush"] = ThemeManager.Brush(t.Panel);
            Resources["AccentBrush"] = ThemeManager.Brush(t.Accent);
            Resources["TextBrush"] = ThemeManager.Brush(t.Text);
            Resources["DimBrush"] = ThemeManager.Brush(t.Dim);
            Resources["BorderBrush"] = ThemeManager.Brush(t.Border);

            // Semi-transparent accent for hover/selection
            var accentColor = (Color)ColorConverter.ConvertFromString(t.Accent);
            Resources["AccentHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x55, accentColor.R, accentColor.G, accentColor.B));
            Resources["AccentPressBrush"] = new SolidColorBrush(Color.FromArgb(0x77, accentColor.R, accentColor.G, accentColor.B));

            // Update system color overrides
            Resources[SystemColors.WindowBrushKey] = ThemeManager.Brush(t.Panel);
            Resources[SystemColors.ControlBrushKey] = ThemeManager.Brush(t.Panel);
            Resources[SystemColors.HighlightBrushKey] = ThemeManager.Brush(t.Accent);
            Resources[SystemColors.ControlTextBrushKey] = ThemeManager.Brush(t.Text);
        }

        private void RefreshApps()
        {
            AppCombo.Items.Clear();
            foreach (var p in _config.Presets)
            {
                AppCombo.Items.Add(p);
            }
            if (AppCombo.Items.Count > 0) AppCombo.SelectedIndex = 0;
        }

        private void RefreshOcio()
        {
            OcioCombo.Items.Clear();
            foreach (var o in _config.OcioConfigs)
            {
                OcioCombo.Items.Add(o);
            }
            var defaultIdx = _config.OcioConfigs.FindIndex(o => o.Name == _config.DefaultOcio);
            if (defaultIdx >= 0) OcioCombo.SelectedIndex = defaultIdx;
            else if (OcioCombo.Items.Count > 0) OcioCombo.SelectedIndex = 0;
        }

        private void UpdateWindowHeight()
        {
            double h = _baseHeight;
            if (RecentPanel.Visibility == Visibility.Visible) h += 200;
            if (ArgsMenuPanel.Visibility == Visibility.Visible) h += 200;
            if (SettingsPanel.Visibility == Visibility.Visible) h += 200;

            if (AnimationCheck.IsChecked == true)
            {
                AnimateToHeight(h);
            }
            else
            {
                Height = h;
            }
        }

        private void AnimateToHeight(double target)
        {
            _targetHeight = target;
            if (_animating) return;

            _animating = true;
            _animTimer = new System.Windows.Threading.DispatcherTimer();
            _animTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps
            _animTimer.Tick += (s, e) =>
            {
                double current = Height;
                double diff = _targetHeight - current;

                if (Math.Abs(diff) < 1)
                {
                    Height = _targetHeight;
                    _animTimer.Stop();
                    _animating = false;
                    return;
                }

                // Ease-out animation
                Height += diff * 0.2;
            };
            _animTimer.Start();
        }

        private string GetAccentWithAlpha(byte alpha)
        {
            var t = ThemeManager.GetTheme(_config.Theme);
            var c = (Color)ColorConverter.ConvertFromString(t.Accent);
            return $"#{alpha:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        private OcioConfig GetActiveOcio()
        {
            return OcioService.GetActiveOcio(_config, OcioCombo.SelectedItem);
        }

        private void LaunchProcess(string exe, string arguments = "")
        {
            try
            {
                var ocio = GetActiveOcio();

                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = false
                };

                if (!string.IsNullOrEmpty(arguments))
                    psi.Arguments = arguments;

                OcioService.ApplyOcio(psi, ocio, exe);

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Launch error: {ex.Message}";
            }
        }

        private void AddRecentFile(string filePath)
        {
            if (!_config.RecentFiles.Contains(filePath))
            {
                _config.RecentFiles.Insert(0, filePath);
                if (_config.RecentFiles.Count > 20)
                    _config.RecentFiles.RemoveAt(_config.RecentFiles.Count - 1);
                ConfigManager.Save(_config);
            }
        }

        private void WireEvents()
        {
            // Launch
            LaunchBtn.Click += (s, e) =>
            {
                if (AppCombo.SelectedItem is not Preset preset) { StatusText.Text = "No app selected"; return; }
                if (!File.Exists(preset.Exe)) { StatusText.Text = "App not found"; return; }

                var ocio = GetActiveOcio();
                var args = ArgsBox.Text?.Trim() ?? "";

                if (!string.IsNullOrEmpty(args))
                {
                    LaunchProcess(preset.Exe, args);
                    Logger.Log(preset.Name, preset.Exe, ocio?.Name ?? "", args);
                    StatusText.Text = $"Launched: {preset.Name} + {args}";
                }
                else
                {
                    LaunchProcess(preset.Exe);
                    Logger.Log(preset.Name, preset.Exe, ocio?.Name ?? "", "");
                    StatusText.Text = $"Launched: {preset.Name}";
                }

                // Add to recent files if a file was opened
                if (!string.IsNullOrEmpty(args) && args.Contains("\"") && !args.StartsWith("-"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(args, @"""([^""]+)""");
                    if (match.Success && File.Exists(match.Groups[1].Value))
                    {
                        AddRecentFile(match.Groups[1].Value);
                    }
                }
            };

            // Add
            AddBtn.Click += (s, e) =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select executable",
                    Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*"
                };
                if (dialog.ShowDialog() != true) return;

                var exePath = dialog.FileName;
                var defaultName = Path.GetFileNameWithoutExtension(exePath);
                var name = Microsoft.VisualBasic.Interaction.InputBox("Preset name:", "FLOMASTER", defaultName);
                if (string.IsNullOrWhiteSpace(name)) return;

                _config.Presets.Add(new() { Name = name, Exe = exePath });
                ConfigManager.Save(_config);
                RefreshApps();
                StatusText.Text = $"Added: {name}";
            };

            // OCIO management
            AddOcioBtn.Click += (s, e) =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select OCIO config file",
                    Filter = "OCIO config (*.ocio)|*.ocio|All files (*.*)|*.*"
                };
                if (dialog.ShowDialog() != true) return;

                var ocioPath = dialog.FileName;
                var defaultName = Path.GetFileNameWithoutExtension(Path.GetDirectoryName(ocioPath)) + " " + Path.GetFileNameWithoutExtension(ocioPath);
                var name = Microsoft.VisualBasic.Interaction.InputBox("OCIO config name:", "FLOMASTER", defaultName);
                if (string.IsNullOrWhiteSpace(name)) return;

                if (OcioService.AddOcioConfig(_config, name, ocioPath))
                {
                    ConfigManager.Save(_config);
                    RefreshOcio();
                    StatusText.Text = $"Added OCIO: {name}";
                }
                else
                {
                    MessageBox.Show("This OCIO config already exists or is invalid.", "FLOMASTER", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };

            RemoveOcioBtn.Click += (s, e) =>
            {
                if (OcioCombo.SelectedItem is not OcioConfig selected) return;

                var result = MessageBox.Show($"Remove \"{selected.Name}\"?", "FLOMASTER", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                if (OcioService.RemoveOcioConfig(_config, selected))
                {
                    ConfigManager.Save(_config);
                    RefreshOcio();
                    StatusText.Text = $"Removed: {selected.Name}";
                }
                else
                {
                    MessageBox.Show("Cannot remove the last OCIO config.", "FLOMASTER", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            // Clear args
            ClearArgsBtn.Click += (s, e) => ArgsBox.Text = "";

            // Add scan path
            AddScanPathBtn.Click += (s, e) =>
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select folder containing DCC applications"
                };
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var path = dialog.SelectedPath;
                    if (!_config.ScanPaths.Contains(path))
                    {
                        _config.ScanPaths.Add(path);
                        ConfigManager.Save(_config);
                        StatusText.Text = $"Scan path added: {Path.GetFileName(path)}";
                    }
                }
            };

            // Rescan
            RescanBtn2.Click += (s, e) =>
            {
                _config.Presets.Clear();
                var scanned = DccScanner.Scan(_config.ScanPaths);
                foreach (var app in scanned) { /* OCIO assigned at launch from combo */ }
                _config.Presets = scanned;
                ConfigManager.Save(_config);
                RefreshApps();
                StatusText.Text = $"Found {scanned.Count} apps";
            };

            // Log viewer
            LogBtn.Click += (s, e) =>
            {
                var entries = Logger.GetLastEntries(50);
                if (entries.Count == 0)
                {
                    StatusText.Text = "No log entries yet";
                    return;
                }

                var logContent = string.Join(Environment.NewLine, entries);
                var logWindow = UiHelper.CreateLogWindow(logContent, this);
                logWindow.ShowDialog();
            };

            // Shortcut for selected app
            ShortcutBtn.Click += (s, e) =>
            {
                if (AppCombo.SelectedItem is not Preset preset) { StatusText.Text = "No app selected"; return; }
                if (!File.Exists(preset.Exe)) { StatusText.Text = "App not found"; return; }

                try
                {
                    var ocio = GetActiveOcio();
                    var ocioPath = ocio?.Path;
                    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    var lnkDir = Path.Combine(desktop, "FLOMASTER");
                    Directory.CreateDirectory(lnkDir);

                    dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                    if (shell == null) { StatusText.Text = "Shell COM unavailable"; return; }
                    dynamic shortcut = shell.CreateShortcut(Path.Combine(lnkDir, $"{preset.Name}.lnk"));
                    shortcut.TargetPath = preset.Exe;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(preset.Exe);
                    shortcut.IconLocation = $"{preset.Exe},0";

                    if (!string.IsNullOrEmpty(ocioPath) && File.Exists(ocioPath))
                    {
                        var batPath = Path.Combine(lnkDir, $"{preset.Name}.bat");
                        File.WriteAllText(batPath, $"@echo off\r\nset \"OCIO={ocioPath}\"\r\nstart \"\" \"{preset.Exe}\"");
                        shortcut.TargetPath = batPath;
                    }

                    shortcut.Save();
                    StatusText.Text = $"Shortcut: {preset.Name}";
                }
                catch (Exception ex) { StatusText.Text = $"Shortcut error: {ex.Message}"; }
            };

            // Desktop shortcut for FLOMASTER itself
            DesktopBtn.Click += (s, e) =>
            {
                try
                {
                    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FLOMASTER.exe");
                    var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flomaster.ico");

                    dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                    if (shell == null) { StatusText.Text = "Shell COM unavailable"; return; }
                    dynamic shortcut = shell.CreateShortcut(Path.Combine(desktop, "FLOMASTER.lnk"));
                    shortcut.TargetPath = exePath;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                    shortcut.IconLocation = File.Exists(icoPath) ? $"{icoPath},0" : $"{exePath},0";
                    shortcut.Description = "FLOMASTER - OCIO Launcher";
                    shortcut.Save();

                    StatusText.Text = "Desktop shortcut created";
                }
                catch (Exception ex) { StatusText.Text = $"Shortcut error: {ex.Message}"; }
            };

            // Recent toggle
            RecentToggle.Checked += (s, e) =>
            {
                RecentPanel.Visibility = Visibility.Visible;
                RefreshRecentFiles();
                UpdateWindowHeight();
            };
            RecentToggle.Unchecked += (s, e) =>
            {
                RecentPanel.Visibility = Visibility.Collapsed;
                UpdateWindowHeight();
            };

            // Quick commands toggle
            ArgsMenuToggle.Checked += (s, e) =>
            {
                ArgsMenuPanel.Visibility = Visibility.Visible;
                RefreshQuickCommands();
                UpdateWindowHeight();
            };
            ArgsMenuToggle.Unchecked += (s, e) =>
            {
                ArgsMenuPanel.Visibility = Visibility.Collapsed;
                UpdateWindowHeight();
            };

            // Settings toggle
            SettingsToggle.Checked += (s, e) =>
            {
                SettingsPanel.Visibility = Visibility.Visible;
                AutoStartCheck.IsChecked = IsAutoStartEnabled();
                TopMostCheck.IsChecked = _config.TopMostEnabled;
                AnimationCheck.IsChecked = _config.AnimationEnabled;

                // Populate default OCIO combo
                DefaultOcioCombo.Items.Clear();
                foreach (var ocio in _config.OcioConfigs)
                    DefaultOcioCombo.Items.Add(ocio);
                var defIdx = _config.OcioConfigs.FindIndex(o => o.Name == _config.DefaultOcio);
                if (defIdx >= 0) DefaultOcioCombo.SelectedIndex = defIdx;
                UpdateWindowHeight();
            };
            SettingsToggle.Unchecked += (s, e) =>
            {
                SettingsPanel.Visibility = Visibility.Collapsed;
                UpdateWindowHeight();
            };

            // Auto-start
            AutoStartCheck.Click += (s, e) =>
            {
                SetAutoStart(AutoStartCheck.IsChecked == true);
                StatusText.Text = AutoStartCheck.IsChecked == true ? "Auto-start enabled" : "Auto-start disabled";
            };

            // Always on top
            TopMostCheck.Click += (s, e) =>
            {
                Topmost = TopMostCheck.IsChecked == true;
                _config.TopMostEnabled = Topmost;
                ConfigManager.Save(_config);
                StatusText.Text = TopMostCheck.IsChecked == true ? "Always on top" : "Normal mode";
            };

            // Animation toggle
            AnimationCheck.Click += (s, e) =>
            {
                _config.AnimationEnabled = AnimationCheck.IsChecked == true;
                ConfigManager.Save(_config);
            };

            // Default OCIO change
            DefaultOcioCombo.SelectionChanged += (s, e) =>
            {
                if (DefaultOcioCombo.SelectedItem is OcioConfig selected)
                {
                    _config.DefaultOcio = selected.Name;
                    ConfigManager.Save(_config);
                    StatusText.Text = $"Default OCIO: {selected.Name}";
                }
            };

            // Drag & Drop
            DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                    e.Effects = System.Windows.DragDropEffects.Copy;
                else
                    e.Effects = System.Windows.DragDropEffects.None;
            };

            Drop += (s, e) =>
            {
                if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files) return;

                foreach (var file in files)
                {
                    if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        var defaultName = Path.GetFileNameWithoutExtension(file);
                        var name = Microsoft.VisualBasic.Interaction.InputBox("Preset name:", "FLOMASTER", defaultName);
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        _config.Presets.Add(new() { Name = name, Exe = file });
                        ConfigManager.Save(_config);
                        RefreshApps();
                        StatusText.Text = $"Added: {name}";
                    }
                    else if (file.EndsWith(".blend", StringComparison.OrdinalIgnoreCase) ||
                             file.EndsWith(".spp", StringComparison.OrdinalIgnoreCase) ||
                             file.EndsWith(".ma", StringComparison.OrdinalIgnoreCase) ||
                             file.EndsWith(".hip", StringComparison.OrdinalIgnoreCase) ||
                             file.EndsWith(".nk", StringComparison.OrdinalIgnoreCase))
                    {
                        if (AppCombo.SelectedItem is not Preset preset) continue;
                        if (!File.Exists(preset.Exe)) continue;

                        GetActiveOcio();
                        LaunchProcess(preset.Exe, $"\"{file}\"");
                        Logger.Log(preset.Name, preset.Exe, "", $"open: {file}");
                        StatusText.Text = $"Opened: {Path.GetFileName(file)}";
                    }
                }
            };

            // Window state changed (minimize to tray)
            StateChanged += (s, e) =>
            {
                if (WindowState == WindowState.Minimized) Hide();
            };

            Closing += (s, e) =>
            {
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
            };
        }

        private void RefreshRecentFiles()
        {
            RecentItems.Children.Clear();

            var allRecent = _config.RecentFiles.Where(f => File.Exists(f)).Distinct().Take(20).ToList();

            if (allRecent.Count == 0)
            {
                RecentItems.Children.Add(new TextBlock
                {
                    Text = "No recent files",
                    Foreground = ThemeManager.Brush("#999999"),
                    FontSize = 11,
                    Margin = new Thickness(4, 2, 4, 2)
                });
                return;
            }

            foreach (var f in allRecent)
            {
                var ext = Path.GetExtension(f).ToLower();
                var icon = ext switch
                {
                    ".blend" => "[B]",
                    ".spp" => "[S]",
                    ".ma" or ".mb" => "[M]",
                    ".hip" or ".hipnc" => "[H]",
                    ".nk" or ".nknc" => "[N]",
                    _ => "[F]"
                };

                var btn = new Button
                {
                    Content = $"{icon} {Path.GetFileName(f)}",
                    Tag = f,
                    ToolTip = f,
                    Background = Brushes.Transparent,
                    Foreground = ThemeManager.Brush("#E0E0E0"),
                    BorderThickness = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    FontSize = 11,
                    Padding = new Thickness(4, 3, 4, 3),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                // Custom template with proper hover colors
                var template = new ControlTemplate(typeof(Button));
                var border = new FrameworkElementFactory(typeof(Border));
                border.Name = "Bd";
                border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
                border.SetValue(Border.PaddingProperty, new Thickness(4, 3, 4, 3));
                var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
                border.AppendChild(presenter);
                template.VisualTree = border;

                var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, ThemeManager.Brush(GetAccentWithAlpha(0x55)), "Bd"));
                hoverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
                template.Triggers.Add(hoverTrigger);

                var pressTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
                pressTrigger.Setters.Add(new Setter(Border.BackgroundProperty, ThemeManager.Brush(GetAccentWithAlpha(0x77)), "Bd"));
                template.Triggers.Add(pressTrigger);

                btn.Template = template;

                btn.Click += (s, e) =>
                {
                    if (!File.Exists(f)) { StatusText.Text = "File not found"; return; }
                    if (AppCombo.SelectedItem is not Preset preset) return;
                    if (!File.Exists(preset.Exe)) return;

                    GetActiveOcio();
                    LaunchProcess(preset.Exe, $"\"{f}\"");
                    Logger.Log(preset.Name, preset.Exe, "", $"recent: {f}");
                    StatusText.Text = $"Opened: {Path.GetFileName(f)}";
                };

                RecentItems.Children.Add(btn);
            }
        }

        private void RefreshQuickCommands()
        {
            ArgsMenuItems.Children.Clear();

            var appName = (AppCombo.SelectedItem as Preset)?.Name ?? "";
            var commands = GetCommandsForApp(appName);

            if (commands.Count == 0)
            {
                ArgsMenuItems.Children.Add(new TextBlock
                {
                    Text = "No commands for this app",
                    Foreground = ThemeManager.Brush("#999999"),
                    FontSize = 11,
                    Margin = new Thickness(4, 2, 4, 2)
                });
                return;
            }

            foreach (var (cmd, desc) in commands)
            {
                var btn = new Button
                {
                    Content = desc,
                    Tag = cmd,
                    ToolTip = cmd,
                    Background = Brushes.Transparent,
                    Foreground = ThemeManager.Brush("#E0E0E0"),
                    BorderThickness = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    FontSize = 11,
                    Padding = new Thickness(4, 3, 4, 3),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                // Custom template with proper hover colors
                var template = new ControlTemplate(typeof(Button));
                var border = new FrameworkElementFactory(typeof(Border));
                border.Name = "Bd";
                border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
                border.SetValue(Border.PaddingProperty, new Thickness(4, 3, 4, 3));
                var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
                border.AppendChild(presenter);
                template.VisualTree = border;

                var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, ThemeManager.Brush(GetAccentWithAlpha(0x55)), "Bd"));
                hoverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
                template.Triggers.Add(hoverTrigger);

                var pressTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
                pressTrigger.Setters.Add(new Setter(Border.BackgroundProperty, ThemeManager.Brush(GetAccentWithAlpha(0x77)), "Bd"));
                template.Triggers.Add(pressTrigger);

                btn.Template = template;

                btn.Click += (s, e) =>
                {
                    var current = ArgsBox.Text?.Trim() ?? "";
                    ArgsBox.Text = string.IsNullOrEmpty(current) ? cmd : $"{current} {cmd}";
                };

                ArgsMenuItems.Children.Add(btn);
            }
        }

        private List<(string cmd, string desc)> GetCommandsForApp(string appName)
        {
            var lower = appName.ToLower();

            if (lower.Contains("blender") || lower.Contains("k-cycles"))
            {
                return new()
                {
                    ("--factory-startup", "Clean start (no settings/addons)"),
                    ("--debug-gpu", "GPU diagnostics"),
                    ("--debug-cycles", "Cycles render debug"),
                    ("-b \"{file}\" -o \"{out}\" -f 1", "Background render (no GUI)"),
                    ("--python-expr \"import bpy; bpy.context.scene.render.resolution_percentage = 25\"", "Quick preview (25%)")
                };
            }

            if (lower.Contains("maya"))
            {
                return new()
                {
                    ("-batch", "Batch mode (no GUI)"),
                    ("-command \"cmds.polyCube()\"", "Execute MEL command"),
                    ("-proj \"{path}\"", "Set project path")
                };
            }

            if (lower.Contains("houdini"))
            {
                return new()
                {
                    ("-batch", "Batch mode (no GUI)"),
                    ("-foreground", "Don't fork process")
                };
            }

            if (lower.Contains("nuke"))
            {
                return new()
                {
                    ("-t", "Terminal mode (no GUI)"),
                    ("-F 1-100", "Frame range")
                };
            }

            if (lower.Contains("unreal"))
            {
                return new()
                {
                    ("", "OCIO passed automatically"),
                    ("-game", "Standalone game mode"),
                    ("-log", "Show log window"),
                    ("-renderoffscreen", "Render without GUI"),
                    ("-nullrhi", "No rendering (headless)")
                };
            }

            return new();
        }

        private void SetupTray()
        {
            _trayIcon = new WinForms.NotifyIcon();
            try
            {
                var icoPath = Path.Combine(_scriptDir, "flomaster.ico");
                if (File.Exists(icoPath))
                    _trayIcon.Icon = new System.Drawing.Icon(icoPath);
                else
                    _trayIcon.Icon = System.Drawing.SystemIcons.Application;
            }
            catch { _trayIcon.Icon = System.Drawing.SystemIcons.Application; }

            _trayIcon.Text = "FLOMASTER";
            _trayIcon.Visible = true;

            var menu = new WinForms.ContextMenuStrip();
            foreach (var p in _config.Presets)
            {
                var item = menu.Items.Add(p.Name);
                item.Click += (s, e) =>
                {
                    if (File.Exists(p.Exe))
                    {
                        var ocio = OcioService.GetDefaultOcio(_config);
                        LaunchProcess(p.Exe);
                        Logger.Log(p.Name, p.Exe, ocio?.Name ?? "", "tray");
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

        private void RegisterContextMenu()
        {
            var extensions = new[] { ".blend", ".spp", ".ma", ".mb", ".hip", ".hipnc", ".nk", ".nknc" };
            var launcherPath = $"powershell -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{Path.Combine(_scriptDir, "launcher.ps1")}\"";

            foreach (var ext in extensions)
            {
                var key = $@"HKCU\Software\Classes\SystemFileAssociations{ext}\shell\FLOMASTER";
                try
                {
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "reg",
                        Arguments = $"query \"{key}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "reg",
                            Arguments = $"add \"{key}\" /ve /d \"Open in FLOMASTER\" /f",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        })?.WaitForExit();

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "reg",
                            Arguments = $"add \"{key}\\command\" /ve /d \"{launcherPath} -open \\\"%1\\\"\" /f",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        })?.WaitForExit();
                    }
                }
                catch { }
            }
        }

        private bool IsAutoStartEnabled()
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "reg",
                    Arguments = "query \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v FLOMASTER",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                });
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch { return false; }
        }

        private void SetAutoStart(bool enabled)
        {
            try
            {
                var exePath = Path.Combine(_scriptDir, "FLOMASTER.exe");
                if (enabled && File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "reg",
                        Arguments = $"add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v FLOMASTER /t REG_SZ /d \"\\\"{exePath}\\\"\" /f",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })?.WaitForExit();
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "reg",
                        Arguments = "delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v FLOMASTER /f",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })?.WaitForExit();
                }
            }
            catch { }
        }
    }
}
