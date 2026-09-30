using System.Collections.Generic;
using FLOMASTER.Models;
using FLOMASTER.Services;
using Xunit;

namespace FLOMASTER.Tests
{
    public class DccScannerTests
    {
        [Theory]
        [InlineData("Nuke14.0.exe", true)]
        [InlineData("Nuke15.1v2.exe", true)]
        [InlineData("Nuke.exe", true)]
        [InlineData("NukeInit.exe", false)]
        [InlineData("NukeAssist.exe", false)]
        [InlineData("NukeRegister.exe", false)]
        [InlineData("NukeStudio.exe", false)]   // не версионированный главный
        [InlineData("blender.exe", false)]
        [InlineData(null, false)]
        public void IsNukeMainExe_MatchesOnlyMainExecutable(string fileName, bool expected)
        {
            Assert.Equal(expected, DccScanner.IsNukeMainExe(fileName));
        }

        [Theory]
        [InlineData("Blender 4.2.3", true)]
        [InlineData("blender", true)]
        [InlineData("Autodesk Maya 2025", true)]
        [InlineData("Sidefx Houdini 20.5.332", true)]
        [InlineData("NUKE 15.0v3", true)]
        [InlineData("DaVinci Resolve", true)]
        [InlineData("Adobe Substance 3D Painter 2024", true)]
        [InlineData("Unreal Editor 5.4", true)]
        [InlineData("Epic Games Launcher", false)]  // лаунчер — не DCC
        [InlineData("Google Chrome", false)]
        public void MatchesKnownApp_RecognizesKnownDccs(string displayName, bool expected)
        {
            Assert.Equal(expected, DccScanner.MatchesKnownApp(displayName));
        }

        [Theory]
        [InlineData("C:\\Program Files\\Side Effects Software\\Houdini 20.5.278\\bin\\Uninstall Houdini.exe", true)]
        [InlineData("C:\\Autodesk\\Installer.exe", true)]
        [InlineData("C:\\Autodesk\\Setup.exe", true)]
        [InlineData("C:\\app\\CrashReport.exe", true)]
        [InlineData("C:\\app\\crash_handler.exe", true)]
        [InlineData("C:\\app\\vcredist_x64.exe", true)]
        [InlineData("C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe", false)]
        [InlineData("C:\\Program Files\\Autodesk\\Maya2025\\bin\\maya.exe", false)]
        [InlineData("C:\\Program Files\\Side Effects Software\\Houdini 20.5.278\\bin\\houdini.exe", false)]
        [InlineData("C:\\Program Files\\Adobe\\Adobe Substance 3D Painter\\Adobe Substance 3D Painter.exe", false)]
        [InlineData("C:\\Program Files\\Blackmagic Design\\DaVinci Resolve\\Resolve.exe", false)]
        [InlineData("E:\\UE_5.4\\Engine\\Binaries\\Win64\\UnrealEditor.exe", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsJunkExe_FiltersInstallersAndUninstallers(string exePath, bool expected)
        {
            Assert.Equal(expected, DccScanner.IsJunkExe(exePath));
        }

        [Fact]
        public void FromEpicManifest_ParsesUnrealManifest()
        {
            var json = @"{""DisplayName"": ""UNREAL ENGINE"", ""InstallLocation"": ""E:\\UE_5.4"", ""LaunchExecutable"": ""Engine\\Binaries\\Win64\\UnrealEditor.exe"", ""AppID"": ""123""}";
            var preset = DccScanner.FromEpicManifest(json);
            Assert.NotNull(preset);
            Assert.Equal(@"E:\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe", preset.Exe);
        }

        [Fact]
        public void FromEpicManifest_IgnoresNonDcc()
        {
            var json = @"{""DisplayName"": ""Some Game"", ""InstallLocation"": ""E:\\Game"", ""LaunchExecutable"": ""Game.exe""}";
            Assert.Null(DccScanner.FromEpicManifest(json));
        }
    }
}
