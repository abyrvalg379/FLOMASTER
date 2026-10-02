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

        // ---- ExtractReleaseInfo + CleanupNotes: заметки релиза для баннера «What's new» ----

        [Fact]
        public void ExtractReleaseInfo_ParsesBodyAndHtmlUrl()
        {
            // экранированные \n и кавычки — как в реальном ответе GitHub API
            var json = "{\"html_url\":\"https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.5.9\"," +
                       "\"tag_name\":\"v2.5.9\"," +
                       "\"body\":\"## What's New\\n- ZIP updater\\n- **NO OCIO** mode with \\\"quotes\\\"\"}";
            var (notes, html) = UpdateService.ExtractReleaseInfo(json);
            Assert.Equal("https://github.com/abyrvalg379/FLOMASTER/releases/tag/v2.5.9", html);
            Assert.Contains("ZIP updater", notes);
            Assert.Contains("\"quotes\"", notes);
            Assert.Contains("\n", notes);
        }

        [Fact]
        public void ExtractReleaseInfo_MalformedJson_ReturnsNulls()
        {
            var (notes, html) = UpdateService.ExtractReleaseInfo("not json at all");
            Assert.Null(notes);
            Assert.Null(html);
        }

        [Fact]
        public void ExtractReleaseInfo_MissingBody_ReturnsNullNotes()
        {
            var (notes, html) = UpdateService.ExtractReleaseInfo("{\"html_url\":\"https://github.com/x\"}");
            Assert.Null(notes);
            Assert.Equal("https://github.com/x", html);
        }

        [Fact]
        public void CleanupNotes_StripsMarkdownHeadersAndBold()
        {
            var cleaned = UpdateService.CleanupNotes("## What's New\r\n\r\n- **NO OCIO** chip\r\n- `code` stays\r\n\r\n\r\n\r\n- tail");
            Assert.DoesNotContain("#", cleaned);
            Assert.DoesNotContain("**", cleaned);
            Assert.DoesNotContain("`", cleaned);
            Assert.Contains("What's New", cleaned);       // заголовок стал строкой, не пропал
            Assert.Contains("NO OCIO chip", cleaned);     // жирный снят, текст цел
            Assert.DoesNotContain("\n\n\n", cleaned);     // пустые строки схлопнуты
        }

        [Fact]
        public void CleanupNotes_CapsLongText_AndEmptySafe()
        {
            var longNotes = new string('x', 10000);
            var cleaned = UpdateService.CleanupNotes(longNotes);
            Assert.True(cleaned.Length < 4200);
            Assert.EndsWith("…", cleaned);

            Assert.Equal("", UpdateService.CleanupNotes(null));
            Assert.Equal("", UpdateService.CleanupNotes("   \n  "));
        }
    }
}
