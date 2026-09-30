using System;
using System.Linq;
using System.Windows;
using FLOMASTER.Services;
using FLOMASTER.ViewModels;
using WinForms = System.Windows.Forms;

namespace FLOMASTER
{
    /// <summary>
    /// Единственный владелец окон приложения: маленькое окно и дашборд создаются/показываются
    /// здесь — стартовый UI (StartupDashboard), Show из трея, тогглы хоткея.
    /// </summary>
    public class UiController
    {
        private readonly MainViewModel _vm;
        private MainWindow? _window;
        private OverlayWindow? _overlay;

        public UiController(MainViewModel vm)
        {
            _vm = vm;
            _vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.OverlayTopmost) && _overlay != null)
                    _overlay.Topmost = _vm.OverlayTopmost;
            };
        }

        /// <summary>Первое окно при старте: по рульке StartupDashboard.</summary>
        public void ShowStartupUi()
        {
            if (_vm.StartupDashboard) ShowOverlay();
            else ShowWindow();
        }

        /// <summary>Show из трея: предпочтение — стартовый режим.</summary>
        public void ShowUi()
        {
            if (_vm.StartupDashboard) { ShowOverlay(); return; }
            ShowWindow();
        }

        // ---- Дашборд ----

        /// <summary>Открыт и активен — закрыть; свёрнут — поднять; иначе — открыть/активировать.</summary>
        public void ToggleDashboard()
        {
            if (_overlay != null)
            {
                if (_overlay.WindowState == WindowState.Minimized)
                {
                    _overlay.WindowState = WindowState.Normal;
                    _overlay.Activate();
                    return;
                }
                if (_overlay.IsActive)
                {
                    _overlay.Close();
                    return;
                }
                _overlay.Activate();
                return;
            }
            ShowOverlay();
            Logger.Log("Hotkey", "Overlay opened", "info");
        }

        private void ShowOverlay()
        {
            if (_overlay != null)
            {
                _overlay.Activate();
                return;
            }

            // приоритет монитора: конфиг (переживает рестарт) -> монитор маленького окна, если есть -> primary
            WinForms.Screen? preferred = null;
            var saved = _vm.OverlayScreenDeviceName;
            if (!string.IsNullOrEmpty(saved))
                preferred = WinForms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == saved);

            var overlay = new OverlayWindow(_vm, _window, preferred);
            overlay.Topmost = _vm.OverlayTopmost;
            overlay.Closing += (_, _) =>
            {
                try
                {
                    _vm.OverlayScreenDeviceName = WinForms.Screen.FromHandle(
                        new System.Windows.Interop.WindowInteropHelper(overlay).Handle).DeviceName;
                }
                catch { }
            };
            overlay.Closed += (_, _) => _overlay = null;
            _overlay = overlay;
            overlay.Show();
            overlay.Activate();
        }

        // ---- Маленькое окно ----

        /// <summary>Видно и активно — спрятать; иначе — показать и поднять.</summary>
        public void ToggleWindow()
        {
            var w = EnsureWindow();
            if (w.IsVisible && w.IsActive)
            {
                w.Hide();
                return;
            }
            ShowWindow();
        }

        public void ShowWindow()
        {
            var w = EnsureWindow();
            w.Show();
            w.WindowState = WindowState.Normal;
            w.Activate();
            // всплытие поверх DCC: Win32-topmost на миг, WPF-биндинг Topmost не трогаем
            var handle = new System.Windows.Interop.WindowInteropHelper(w).Handle;
            SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpShowwindow);
            SetWindowPos(handle, HwndNotTopmost, 0, 0, 0, 0, SwpNomove | SwpNosize);
        }

        private MainWindow EnsureWindow()
        {
            if (_window != null) return _window;
            _window = new MainWindow(_vm);
            _window.Closed += (_, _) => _window = null;
            return _window;
        }

        // ---- Win32: разовое всплытие маленького окна ----
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private static readonly IntPtr HwndNotTopmost = new IntPtr(-2);
        private const uint SwpNomove = 0x2, SwpNosize = 0x1, SwpShowwindow = 0x40;
    }
}
