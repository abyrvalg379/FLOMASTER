using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using FLOMASTER.Models;
using FLOMASTER.Services;
using Xunit;

namespace FLOMASTER.Tests
{
    /// <summary>Герметичные тесты OCIO-логики: temp-файлы, без сети и UI.</summary>
    public class OcioServiceTests : IDisposable
    {
        private readonly string _dir;
        private readonly OcioService _svc;

        public OcioServiceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "flm_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _svc = new OcioService();
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        private string WriteOcio(string content)
        {
            var path = Path.Combine(_dir, "config.ocio");
            File.WriteAllText(path, content);
            return path;
        }

        private const string FullConfig =
            "ocio_profile_version: 2\n" +
            "roles:\n" +
            "  reference: raw\n" +
            "  scene_linear: acescg\n" +
            "  data: raw\n" +
            "  default_byte: raw\n" +
            "  texture_paint: raw\n" +
            "\n" +
            "colorspace:\n" +
            "  name: raw\n" +
            "  family: Utility/Aliases\n" +
            "\n" +
            "colorspace:\n" +
            "  name: acescg\n" +
            "  family: Utility/Aliases\n";

        [Fact]
        public void Parse_ParsesRolesAndFamilies()
        {
            var path = WriteOcio(FullConfig);
            var (roles, colorspaces) = _svc.Parse(path);

            Assert.Contains(roles, r => r.Key == "scene_linear" && r.Value == "acescg");
            Assert.Contains(roles, r => r.Key == "default_byte" && r.Value == "raw");
            Assert.Equal("Utility/Aliases", colorspaces["raw"]);
            Assert.Equal("Utility/Aliases", colorspaces["acescg"]);
        }

        [Fact]
        public void Validate_CleanConfig_NoWarnings()
        {
            var path = WriteOcio(FullConfig);
            var report = _svc.Validate(path);
            Assert.Empty(report.Warnings);
        }

        [Fact]
        public void Validate_MissingRequiredRoles_Warns()
        {
            var path = WriteOcio("roles:\n  data: raw\n\ncolorspace:\n  name: raw\n  family: Utility\n");
            var report = _svc.Validate(path);
            Assert.Contains(report.Warnings, w => w.Contains("missing required role 'reference'"));
            Assert.Contains(report.Warnings, w => w.Contains("missing required role 'scene_linear'"));
            Assert.Contains(report.Warnings, w => w.Contains("no default_byte"));
        }

        [Fact]
        public void Validate_UnknownColorspaceTarget_Warns()
        {
            var path = WriteOcio("roles:\n  scene_linear: acescg\n\ncolorspace:\n  name: raw\n");
            var report = _svc.Validate(path);
            Assert.Contains(report.Warnings, w => w.Contains("role scene_linear -> colorspace 'acescg' not found"));
        }

        [Fact]
        public void Validate_NoRolesSection_WarnsNotOcio()
        {
            var path = WriteOcio("str: hello\n");
            var report = _svc.Validate(path);
            Assert.Contains(report.Warnings, w => w.Contains("no roles section"));
        }

        [Fact]
        public void ApplyOcio_SetsEnvVar_ForNonUnreal()
        {
            var psi = new ProcessStartInfo { FileName = @"C:\apps\blender.exe" };
            _svc.ApplyOcio(psi, new OcioConfig { Name = "ACES", Path = @"E:\configs\config.ocio" }, @"C:\apps\blender.exe");
            Assert.Equal(@"E:\configs\config.ocio", psi.EnvironmentVariables["OCIO"]);
        }

        [Fact]
        public void ApplyOcio_UE_PrependsArg()
        {
            var psi = new ProcessStartInfo { FileName = @"E:\Unreal\UnrealEditor.exe", Arguments = "-log" };
            _svc.ApplyOcio(psi, new OcioConfig { Name = "ACES", Path = @"E:\configs\config.ocio" }, @"E:\Unreal\UnrealEditor.exe");
            Assert.StartsWith("-ocio=\"E:\\configs\\config.ocio\"", psi.Arguments);
            Assert.Contains("-log", psi.Arguments);
        }

        [Fact]
        public void ApplyOcio_NoConfig_DoesNothing()
        {
            var psi = new ProcessStartInfo { FileName = @"C:\apps\blender.exe" };
            _svc.ApplyOcio(psi, null, @"C:\apps\blender.exe");
            Assert.False(psi.EnvironmentVariables.ContainsKey("OCIO")); // StringDictionary кидает на отсутствии ключа
        }

        [Fact]
        public void ApplyOcio_NoOcioMode_RemovesInheritedEnvVar()
        {
            var psi = new ProcessStartInfo { FileName = @"C:\apps\blender.exe" };
            psi.EnvironmentVariables["OCIO"] = @"E:\inherited\config.ocio"; // сам FLOMASTER запущен под OCIO
            _svc.ApplyOcio(psi, new OcioConfig { Name = ConfigManager.NoOcioName, IsNoOcio = true }, @"C:\apps\blender.exe");
            Assert.False(psi.EnvironmentVariables.ContainsKey("OCIO"));
        }

        [Fact]
        public void ApplyOcio_NoOcioMode_UE_AddsNoArgAndNoEnv()
        {
            var psi = new ProcessStartInfo { FileName = @"E:\Unreal\UnrealEditor.exe", Arguments = "-log" };
            _svc.ApplyOcio(psi, new OcioConfig { Name = ConfigManager.NoOcioName, IsNoOcio = true }, @"E:\Unreal\UnrealEditor.exe");
            Assert.DoesNotContain("-ocio", psi.Arguments);
            Assert.Contains("-log", psi.Arguments);
            Assert.False(psi.EnvironmentVariables.ContainsKey("OCIO"));
        }

        [Fact]
        public void ApplyOcio_NoOcioMode_IsNotBrokenConfig_WarnPathUntouched()
        {
            // у псевдо-записи Path пустой, но сломанным конфигом она не считается:
            // ветка empty-path WARN не срабатывает, env var просто снимается (см. тест выше)
            var psi = new ProcessStartInfo { FileName = @"C:\apps\blender.exe" };
            var broken = new OcioConfig { Name = "ACES 1.2", Path = "" }; // сломанный: без флага
            _svc.ApplyOcio(psi, broken, @"C:\apps\blender.exe");
            Assert.False(psi.EnvironmentVariables.ContainsKey("OCIO")); // оба пути запускают без OCIO...
            Assert.False(broken.IsNoOcio); // ...но флаг псевдо у сломанной записи не появился
        }

        [Fact]
        public void AddOcioConfig_RejectsReservedNoOcioName()
        {
            var pathA = Path.Combine(_dir, "a.ocio"); File.WriteAllText(pathA, FullConfig);
            var pathB = Path.Combine(_dir, "b.ocio"); File.WriteAllText(pathB, FullConfig);
            var config = new Config();
            config.OcioConfigs.Add(new() { Name = "ACES 1.2", Path = pathA });

            Assert.False(_svc.AddOcioConfig(config, ConfigManager.NoOcioName, pathB));
            Assert.DoesNotContain(config.OcioConfigs, o => o.Name == ConfigManager.NoOcioName);
        }

        [Fact]
        public void BuildVariant_RewritesRole_CanonicalUntouched_EolKept()
        {
            var baseCrlf = FullConfig.Replace("\n", "\r\n");
            var path = WriteOcio(baseCrlf);
            var before = File.ReadAllBytes(path);

            var variant = _svc.BuildVariant(path,
                new Dictionary<string, string> { { "scene_linear", "raw" } }, "TB3");

            Assert.NotNull(variant);
            // канон не изменился байт-в-байт
            Assert.Equal(before, File.ReadAllBytes(path));
            // вариант содержит новую роль и CRLF
            var text = File.ReadAllText(variant);
            Assert.Contains("scene_linear: raw", text);
            Assert.Contains("\r\n", text);
            // меняется ровно одна строка
            var baseLines = File.ReadAllLines(path);
            var varLines = File.ReadAllLines(variant);
            Assert.Equal(baseLines.Length, varLines.Length);
            Assert.Equal(1, baseLines.Zip(varLines, (a, b) => a != b).Count(x => x));
        }

        [Fact]
        public void BuildVariant_SameAsBase_ReturnsNull()
        {
            var path = WriteOcio(FullConfig);
            var variant = _svc.BuildVariant(path,
                new Dictionary<string, string> { { "scene_linear", "acescg" } }, "TB3");
            Assert.Null(variant);
        }

        [Fact]
        public void BuildVariant_UnknownColorspace_Dropped()
        {
            var path = WriteOcio(FullConfig);
            var variant = _svc.BuildVariant(path,
                new Dictionary<string, string> { { "scene_linear", "not_a_colorspace" } }, "TB3");
            Assert.Null(variant); // единственное переопределение отброшено -> нечего применять
        }

        [Fact]
        public void BuildVariant_AddsMissingRole()
        {
            var path = WriteOcio("roles:\n  scene_linear: raw\n\ncolorspace:\n  name: raw\n  family: Utility\n");
            var variant = _svc.BuildVariant(path,
                new Dictionary<string, string> { { "default_byte", "raw" } }, "TB3");
            Assert.NotNull(variant);
            Assert.Contains("default_byte: raw", File.ReadAllText(variant));
        }
    }
}
