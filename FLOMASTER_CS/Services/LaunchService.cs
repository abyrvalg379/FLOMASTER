using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FLOMASTER.Models;

namespace FLOMASTER.Services
{
    public interface ILaunchService
    {
        /// <summary>
        /// Запускает exe с аргументами и OCIO (вариант конфига собирается при переопределениях ролей).
        /// Возвращает null при успехе, иначе сообщение об ошибке ("App not found" — особый случай).
        /// </summary>
        string? Launch(string exePath, string presetName, OcioConfig ocio, Dictionary<string, string> roleOverrides, string args);
    }

    public class LaunchService : ILaunchService
    {
        private readonly IOcioService _ocio;

        public LaunchService(IOcioService ocio)
        {
            _ocio = ocio;
        }

        public string? Launch(string exePath, string presetName, OcioConfig ocio, Dictionary<string, string> roleOverrides, string args)
        {
            if (!File.Exists(exePath)) return "App not found";
            try
            {
                var psi = new ProcessStartInfo { FileName = exePath, UseShellExecute = false };
                if (!string.IsNullOrWhiteSpace(args)) psi.Arguments = args;
                // не наследуем CWD FLOMASTER (после автообновления из elevated-PS это System32)
                var dir = Path.GetDirectoryName(exePath);
                if (!string.IsNullOrEmpty(dir)) psi.WorkingDirectory = dir;

                // переопределения ролей -> вариант конфига в %APPDATA% (канон не трогается)
                string? variantPath = null;
                if (ocio != null && roleOverrides is { Count: > 0 })
                    variantPath = _ocio.BuildVariant(ocio.Path, roleOverrides, presetName);
                _ocio.ApplyOcio(psi, ocio, exePath, variantPath);

                Process.Start(psi);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
