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
    }
}
