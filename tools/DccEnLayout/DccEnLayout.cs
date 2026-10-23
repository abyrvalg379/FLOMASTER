// DccEnLayout — авто-английская раскладка для DCC-приложений (Houdini, Maya, mplay).
//
// Проблема: Houdini/Maya (Qt) привязывают хоткеи к символам, а не к физическим
// клавишам — в русской раскладке латинские биндинги не срабатывают.
//
// Решение: фоновая утилита. При переходе фокуса на окно отслеживаемого DCC
// раскладка автоматически переключается на английскую. В остальных программах
// всё как обычно; внутри DCC русский при нужде включается Alt+Shift и держится,
// пока не уйдёшь из окна и не вернёшься.
//
// Механика (все три пункта выстраданы экспериментально 23.10.2026):
//  1) WinEvent-хуки (foreground + object focus) И опрос foreground каждые 400 мс
//     по переходам — доставка WinEvent-событий в Windows иногда теряет переходы;
//  2) WM_INPUTLANGCHANGEREQUEST шлём в верхнеуровневое окно — дочерним Qt-окнам
//     сообщение не действует;
//  3) шлём НЕ сразу, а через 250 мс после перехода, с «окном стабильности»
//     ~1.5 с: во время активации Windows восстанавливает запомненную для окна
//     раскладку и затирает сообщение, отправленное в момент события.
//
// Сборка: build.ps1 (штатный csc .NET Framework — ноль зависимостей).
// Аргументы командной строки (опционально): имена процессов без .exe,
// переопределяющие список по умолчанию (задел под настройку из FLOMASTER).
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace DccEnLayout
{
    static class Program
    {
        const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        const uint EVENT_OBJECT_FOCUS      = 0x8005;
        const uint WINEVENT_OUTOFCONTEXT   = 0;
        const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;
        const int  MaxAttempts = 10;   // максимум постов на один переход
        const int  EnStreakStop = 3;   // сколько подряд EN-проверок считаем «стабильно EN»

        static string[] _watched = { "houdini", "houdinifx", "hindie", "houdinicore", "mplay", "maya" };
        static IntPtr _hklEn = IntPtr.Zero;
        static string _logPath;
        static DateTime _lastLog = DateTime.MinValue;
        static System.Windows.Forms.Timer _timer;
        static System.Windows.Forms.Timer _poll;
        static IntPtr _lastFg = IntPtr.Zero;
        static int _attempts;
        static int _enStreak;

        delegate void WinEventProc(IntPtr hHook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

        [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] static extern int GetKeyboardLayoutList(int nBuff, [Out] IntPtr[] list);
        [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint tid);

        static WinEventProc _proc; // удерживаем делегат от GC

        static void Log(string s) { Log(s, false); }

        static void Log(string s, bool force)
        {
            try
            {
                var now = DateTime.Now;
                if (!force && (now - _lastLog).TotalMilliseconds < 5) return; // анти-спам при всплесках событий
                _lastLog = now;
                // не даём логу бессрочно расти
                if (File.Exists(_logPath) && new FileInfo(_logPath).Length > 524288) File.WriteAllText(_logPath, "");
                File.AppendAllText(_logPath, now.ToString("HH:mm:ss.fff ") + s + "\r\n");
            }
            catch { }
        }

        [STAThread]
        static void Main(string[] args)
        {
            // аргументы = список процессов (без .exe), переопределяющий дефолт
            if (args != null && args.Length > 0)
            {
                var names = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                    names[i] = (args[i] ?? "").Trim().ToLowerInvariant().Replace(".exe", "");
                _watched = names;
            }

            _logPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
                "dcc_en_layout.log");
            _hklEn = FindEnglishLayout();
            if (_hklEn == IntPtr.Zero) { Log("no english layout found, exiting", true); return; }

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 250;
            _timer.Tick += OnTick;

            _proc = OnWinEvent;
            var h1 = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT);
            var h2 = SetWinEventHook(EVENT_OBJECT_FOCUS, EVENT_OBJECT_FOCUS, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT);

            // страховка: доставка WinEvent иногда теряет переходы — опрашиваем foreground по переходам
            _poll = new System.Windows.Forms.Timer();
            _poll.Interval = 400;
            _poll.Tick += OnPoll;
            _poll.Start();

            Log("start: hklEn=0x" + _hklEn.ToString("X") + " hookFg=" + h1 + " hookFocus=" + h2
                + " watched=" + string.Join(",", _watched));
            System.Windows.Forms.Application.Run();
            Log("message loop ended");
        }

        static IntPtr FindEnglishLayout()
        {
            int n = GetKeyboardLayoutList(0, null);
            var list = new IntPtr[n];
            GetKeyboardLayoutList(n, list);
            IntPtr anyEn = IntPtr.Zero, usEn = IntPtr.Zero;
            foreach (var hkl in list)
            {
                ushort lang = (ushort)((long)hkl & 0xFFFF);
                if ((lang & 0x3FF) == 0x09) // LANG_ENGLISH
                {
                    anyEn = hkl;
                    if (lang == 0x0409) usEn = hkl;
                }
            }
            return usEn != IntPtr.Zero ? usEn : anyEn;
        }

        static bool IsWatchedProcess(uint pid, out string name)
        {
            name = "";
            if (pid == 0) return false;
            try { using (var p = Process.GetProcessById((int)pid)) name = p.ProcessName.ToLowerInvariant(); }
            catch { return false; }
            return Array.IndexOf(_watched, name) >= 0;
        }

        static void ArmTimer(string why)
        {
            _attempts = 0;
            _enStreak = 0;
            _timer.Stop();
            _timer.Start();
            Log("arm timer (" + why + ")");
        }

        static void OnPoll(object sender, EventArgs e)
        {
            var fg = GetForegroundWindow();
            if (fg == _lastFg) return;
            _lastFg = fg;
            if (fg == IntPtr.Zero) return;
            uint pid;
            GetWindowThreadProcessId(fg, out pid);
            string name;
            if (IsWatchedProcess(pid, out name)) ArmTimer("poll:" + name);
        }

        static void OnWinEvent(IntPtr hHook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            // caret (-8) и прочие не-оконные объекты пропускаем, чтобы не мешать набору кириллицы
            if (idObject != 0 && idObject != -4) return;
            try
            {
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                string name;
                if (!IsWatchedProcess(pid, out name)) return;

                var fg = GetForegroundWindow();
                uint fgpid;
                GetWindowThreadProcessId(fg, out fgpid);
                if (fgpid != pid) return; // событие DCC, но фокус у другого приложения

                ArmTimer("evt " + evt.ToString("X") + " " + name);
            }
            catch (Exception ex) { Log("err: " + ex.Message, true); }
        }

        static void OnTick(object sender, EventArgs e)
        {
            try
            {
                var fg = GetForegroundWindow();
                uint pid;
                uint tid = GetWindowThreadProcessId(fg, out pid);
                string name = "?";
                try { using (var p = Process.GetProcessById((int)pid)) name = p.ProcessName.ToLowerInvariant(); }
                catch { }
                if (Array.IndexOf(_watched, name) < 0) { _timer.Stop(); Log("tick: fg=" + name + " not watched, stop", true); return; }

                var cur = GetKeyboardLayout(tid);
                if ((((long)cur) & 0x3FF) == 0x09) // английская
                {
                    // не останавливаемся на первом EN: Windows может ещё подтянуть
                    // запомненную для окна раскладку — ждём стабильности
                    _enStreak++;
                    if (_enStreak >= EnStreakStop)
                    {
                        _timer.Stop();
                        Log("tick: EN stable (attempts=" + _attempts + ")", true);
                    }
                    return;
                }
                if (_attempts >= MaxAttempts) { _timer.Stop(); Log("tick: gave up, layout=0x" + (((long)cur) & 0xFFFF).ToString("X"), true); return; }

                _enStreak = 0;
                _attempts++;
                bool ok = PostMessage(fg, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, _hklEn);
                Log("tick: post #" + _attempts + " ok=" + ok + " cur=0x" + (((long)cur) & 0xFFFF).ToString("X"), true);
            }
            catch (Exception ex) { Log("tick err: " + ex.Message, true); }
        }
    }
}
