using System;
using System.IO;
using System.Windows;
using FLOMASTER.ViewModels;
using WinForms = System.Windows.Forms;

namespace FLOMASTER.Services
{
    /// <summary>
    /// Трей на уровне приложения: живёт независимо от того, какое окно показано
    /// (маленькое или дашборд). Show — поднять основной UI, Quit — единственный выход.
    /// Меню: пресеты-лаунч, профили, Show, Quit; перестраивается на изменении коллекций.
    /// </summary>
    public class TrayService : IDisposable
    {
        private readonly MainViewModel _vm;
        private readonly Action _showUi;
        private readonly WinForms.NotifyIcon _icon;

        public TrayService(MainViewModel vm, Action showUi)
        {
            _vm = vm;
            _showUi = showUi;

            _icon = new WinForms.NotifyIcon();
            try
            {
                var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flomaster.ico");
                _icon.Icon = File.Exists(icoPath)
                    ? new System.Drawing.Icon(icoPath)
                    : System.Drawing.SystemIcons.Application;
            }
            catch { _icon.Icon = System.Drawing.SystemIcons.Application; }

            _icon.Text = "FLOMASTER";
            _icon.Visible = true;
            _icon.ContextMenuStrip = new WinForms.ContextMenuStrip();
            RebuildMenu();
            _vm.Presets.CollectionChanged += (_, _) => RebuildMenu();
            _vm.Profiles.CollectionChanged += (_, _) => RebuildMenu();
            _icon.DoubleClick += (_, _) => _showUi();
        }

        private void RebuildMenu()
        {
            var menu = _icon.ContextMenuStrip;
            if (menu == null) return;
            menu.Items.Clear();

            foreach (var preset in _vm.Presets)
            {
                var item = menu.Items.Add(preset.Name);
                item.Click += (_, _) =>
                {
                    if (!File.Exists(preset.Exe))
                    {
                        Logger.Log("Tray", $"Preset exe not found: {preset.Exe}", "warn");
                        _vm.StatusText = $"{preset.Name}: exe not found";
                        return;
                    }
                    _vm.SelectedPreset = preset;
                    _vm.LaunchCommand.Execute(null);
                };
            }

            // Профили: применение состояния + запуск тем же путём, что и Launch
            if (_vm.Profiles.Count > 0)
            {
                menu.Items.Add(new WinForms.ToolStripSeparator());
                var header = menu.Items.Add("PROFILES");
                header.Enabled = false;

                foreach (var profile in _vm.Profiles)
                {
                    var item = menu.Items.Add(profile.Name);
                    item.Click += (_, _) =>
                    {
                        _vm.ApplyProfile(profile);
                        var exe = _vm.SelectedPreset?.Exe;
                        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                        {
                            Logger.Log("Tray", $"Profile '{profile.Name}': exe not found: {exe}", "warn");
                            return;
                        }
                        _vm.LaunchCommand.Execute(null);
                    };
                }
            }

            menu.Items.Add(new WinForms.ToolStripSeparator());
            var showItem = menu.Items.Add("Show FLOMASTER");
            showItem.Click += (_, _) => _showUi();
            var quitItem = menu.Items.Add("Quit");
            quitItem.Click += (_, _) => ((App)Application.Current).RequestExit();
        }

        public void Dispose()
        {
            try { _icon.Visible = false; _icon.Dispose(); } catch { }
        }
    }
}
