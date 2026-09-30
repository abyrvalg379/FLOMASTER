using System;
using FLOMASTER.Services;
using Xunit;

namespace FLOMASTER.Tests
{
    /// <summary>Чистое сравнение версий автообновления: тег vs локальная сборка.</summary>
    public class UpdateServiceTests
    {
        private static readonly Version Local241 = new Version(2, 4, 1, 0);

        [Theory]
        [InlineData("v2.4.1", false)]      // та же версия
        [InlineData("2.4.1", false)]       // без v-префикса
        [InlineData("v2.3.9", false)]      // старее
        [InlineData("v2.4", false)]        // 2.4.0 < 2.4.1
        [InlineData("v2.4.2", true)]
        [InlineData("v2.5", true)]         // 2.5.0 > 2.4.1
        [InlineData("v3.0.0", true)]
        [InlineData("v2.4.1.5", false)]    // ревизия сборки не участвует: 2.4.1 == 2.4.1
        [InlineData("garbage", false)]     // неразборчивый тег
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsUpdateAvailable_ComparesMajorMinorBuild(string tag, bool expected)
        {
            Assert.Equal(expected, UpdateService.IsUpdateAvailable(tag, Local241));
        }

        // ---- SelectUpdateAsset: ZIP (exe + ocio) важнее голого exe; мусорные вложения не берём ----

        private const string Base = "\"browser_download_url\":\"https://example.com/abyrvalg379/FLOMASTER/releases/download/v2.5.7/";

        [Fact]
        public void SelectUpdateAsset_PrefersVersionedZip()
        {
            var json = "{" + Base + "FLOMASTER.exe\"}," + Base + "FLOMASTER_v2.5.7.zip\"}";
            var (url, isZip) = UpdateService.SelectUpdateAsset(json);
            Assert.True(isZip);
            Assert.Contains("FLOMASTER_v2.5.7.zip", url);
        }

        [Fact]
        public void SelectUpdateAsset_FallsBackToExe_WhenNoZip()
        {
            var json = "{" + Base + "FLOMASTER.exe\"}";
            var (url, isZip) = UpdateService.SelectUpdateAsset(json);
            Assert.False(isZip);
            Assert.Contains("FLOMASTER.exe", url);
        }

        [Fact]
        public void SelectUpdateAsset_IgnoresWindowsAutobundle()
        {
            var json = "{" + Base + "FLOMASTER_Windows.zip\"}";
            var (url, isZip) = UpdateService.SelectUpdateAsset(json);
            Assert.False(isZip);
            Assert.Null(url);
        }

        [Fact]
        public void SelectUpdateAsset_IgnoresSourceArchive()
        {
            var json = "{\"browser_download_url\":\"https://example.com/abyrvalg379/FLOMASTER/archive/refs/tags/v2.5.7.zip\"}";
            var (url, isZip) = UpdateService.SelectUpdateAsset(json);
            Assert.False(isZip);
            Assert.Null(url);
        }

        [Fact]
        public void SelectUpdateAsset_ReturnsNull_OnEmptyAssets()
        {
            var (url, isZip) = UpdateService.SelectUpdateAsset("{\"assets\":[]}");
            Assert.Null(url);
            Assert.False(isZip);
        }
    }
}
