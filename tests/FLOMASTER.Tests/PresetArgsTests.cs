using FLOMASTER.Models;
using Xunit;

namespace FLOMASTER.Tests
{
    public class PresetArgsTests
    {
        [Fact]
        public void CombineArgs_BakedOnly()
        {
            var preset = new Preset { Args = "--nukex" };
            Assert.Equal("--nukex", preset.CombineArgs(""));
            Assert.Equal("--nukex", preset.CombineArgs(null));
        }

        [Fact]
        public void CombineArgs_UserOnly()
        {
            var preset = new Preset();
            Assert.Equal("-t", preset.CombineArgs("-t"));
            Assert.Equal("", preset.CombineArgs(null));
        }

        [Fact]
        public void CombineArgs_BakedFirst()
        {
            var preset = new Preset { Args = "--nukex" };
            Assert.Equal("--nukex -x \"script.nk\"", preset.CombineArgs(" -x \"script.nk\" "));
        }

        [Fact]
        public void CombineArgs_TrimsBothSides()
        {
            var preset = new Preset { Args = " --nukex " };
            Assert.Equal("--nukex -t", preset.CombineArgs("  -t  "));
        }
    }
}
