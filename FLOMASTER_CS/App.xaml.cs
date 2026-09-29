using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace FLOMASTER
{
    public partial class App : Application
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        private const int ATTACH_PARENT_PROCESS = -1;

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

            new MainWindow().Show();
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
