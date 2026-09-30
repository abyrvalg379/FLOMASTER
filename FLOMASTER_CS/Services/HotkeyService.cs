using System;
using System.Windows.Interop;
using FLOMASTER.ViewModels;

namespace FLOMASTER.Services
{
    /// <summary>
    /// Глобальный хоткей Ctrl+Alt+F на уровне приложения: message-only окно вместо
    /// hwnd главного окна — работает и когда главное окно не создано (старт с дашборда).
    /// HotkeyOpensDashboard — тоггл дашборда, иначе тоггл маленького окна.
    /// </summary>
    public class HotkeyService : IDisposable
    {
        private const int HOTKEY_ID = 0xF10;
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000, VK_F = 0x46;
        private static readonly IntPtr HwndMessage = new IntPtr(-3);

        private readonly MainViewModel _vm;
        private readonly Action _toggleDashboard;
        private readonly Action _toggleWindow;
        private HwndSource? _source;
        private bool _registered;

        public HotkeyService(MainViewModel vm, Action toggleDashboard, Action toggleWindow)
        {
            _vm = vm;
            _toggleDashboard = toggleDashboard;
            _toggleWindow = toggleWindow;

            var parameters = new HwndSourceParameters("FLOMASTER.hotkey")
            {
                PositionX = 0,
                PositionY = 0,
                Width = 0,
                Height = 0,
                WindowStyle = 0,
                ParentWindow = HwndMessage,
                HwndSourceHook = Hook
            };
            _source = new HwndSource(parameters);

            Apply(_vm.HotkeyEnabled);
            _vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.HotkeyEnabled)) Apply(_vm.HotkeyEnabled);
            };
        }

        private void Apply(bool enabled)
        {
            if (_source == null) return;
            if (_registered)
            {
                UnregisterHotKey(_source.Handle, HOTKEY_ID);
                _registered = false;
            }
            if (enabled)
            {
                // MOD_NOREPEAT: автоповтор клавиатуры не дёргает тоггл
                _registered = RegisterHotKey(_source.Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_F);
                Logger.Log("Hotkey",
                    _registered ? "Registered Ctrl+Alt+F" : "RegisterHotKey failed (combination busy?)",
                    _registered ? "info" : "warn");
            }
        }

        private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                if (_vm.HotkeyOpensDashboard) _toggleDashboard();
                else _toggleWindow();
                handled = true;
            }
            return IntPtr.Zero;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public void Dispose()
        {
            if (_registered && _source != null) UnregisterHotKey(_source.Handle, HOTKEY_ID);
            _registered = false;
            _source?.Dispose();
            _source = null;
        }
    }
}
