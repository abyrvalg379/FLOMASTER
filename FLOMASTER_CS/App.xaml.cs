using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using FLOMASTER.Services;
using FLOMASTER.ViewModels;

namespace FLOMASTER
{
    public partial class App : Application
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        private const int ATTACH_PARENT_PROCESS = -1;

        private TrayService? _tray;
        private HotkeyService? _hotkey;
        private UiController? _ui;

        /// <summary>Истинный выход (Quit в трее): окна при Closing не прячутся, а закрываются.</summary>
        public static bool IsExiting { get; private set; }

        public void RequestExit()
        {
            IsExiting = true;
            Shutdown();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // CLI-режим: --launch "Профиль" [--project файл] / --list-profiles.
            // Без окна, трея и проверки обновлений; код возврата — для батников и ферм.
            if (e.Args.Length > 0)
            {
                AttachToParentConsole();
                var code = Services.CliRunner.Run(e.Args);
                Shutdown(code);
                return;
            }

            // Ручной DI без контейнеров; composition root теперь App — окна может не быть
            // (старт с дашборда по рульке StartupDashboard).
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // запуск с правами админа (обычно после автообновления) наследуется всеми DCC —
            // вылеты Painter, блок drag&drop; маркер в логе для мгновенного диагноза
            if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                Logger.Log("APP", "Running ELEVATED (admin): launched DCC apps inherit admin rights", "warn");

            var ocio = new OcioService();
            var viewModel = new MainViewModel(new ConfigManager(), ocio, new LaunchService(ocio));

            ThemeApplier.Apply(viewModel.SelectedTheme);
            viewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.SelectedTheme))
                    ThemeApplier.Apply(viewModel.SelectedTheme);
            };

            _ui = new UiController(viewModel);
            _tray = new TrayService(viewModel, () => _ui.ShowUi());
            _hotkey = new HotkeyService(viewModel, _ui.ToggleDashboard, _ui.ToggleWindow);

            _ui.ShowStartupUi();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            IsExiting = true;
            _hotkey?.Dispose();
            _tray?.Dispose();
            base.OnExit(e);
        }

        /// <summary>Вывод в консоль родительского процесса (winexe не имеет своей консоли).</summary>
        private void AttachToParentConsole()
        {
            try
            {
                if (!AttachConsole(ATTACH_PARENT_PROCESS)) return;
                Console.SetOut(new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
                Console.SetError(new System.IO.StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
            }
            catch { /* вывод не критичен — остаётся код возврата */ }
        }
    }
}
