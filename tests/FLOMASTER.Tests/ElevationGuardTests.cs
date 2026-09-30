using System;
using System.IO;
using FLOMASTER.Services;
using Xunit;

namespace FLOMASTER.Tests
{
    /// <summary>Гвард цикла самолечения elevated-запуска: маркер «только что пытались».</summary>
    public class ElevationGuardTests : IDisposable
    {
        private readonly string _dir;
        private readonly string _marker;

        public ElevationGuardTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "flm_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _marker = Path.Combine(_dir, "delev.marker");
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        [Fact]
        public void NoMarker_NotFresh()
        {
            Assert.False(ElevationGuard.IsMarkerFresh(_marker, DateTime.Now));
        }

        [Fact]
        public void JustWritten_Marker_IsFresh()
        {
            File.WriteAllText(_marker, DateTime.Now.ToString("s"));
            Assert.True(ElevationGuard.IsMarkerFresh(_marker, DateTime.Now));
        }

        [Fact]
        public void OldMarker_NotFresh()
        {
            File.WriteAllText(_marker, DateTime.Now.ToString("s"));
            File.SetLastWriteTime(_marker, DateTime.Now.AddSeconds(-120));
            Assert.False(ElevationGuard.IsMarkerFresh(_marker, DateTime.Now));
        }
    }
}
