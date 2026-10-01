using System;
using System.Collections.Generic;
using System.IO;
using FLOMASTER.Services;
using FLOMASTER.ViewModels;
using Xunit;

namespace FLOMASTER.Tests
{
    /// <summary>Сортировка списка проектов: имя (не полный путь), дата (свежие сверху), софт (семейства DCC).</summary>
    public class ProjectSortTests : IDisposable
    {
        private readonly string _dir;

        public ProjectSortTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "flm_sort_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        private string MakeFile(string sub, string name, DateTime? mtime = null)
        {
            var d = Path.Combine(_dir, sub);
            Directory.CreateDirectory(d);
            var f = Path.Combine(d, name);
            File.WriteAllText(f, "x");
            if (mtime.HasValue) File.SetLastWriteTimeUtc(f, mtime.Value);
            return f;
        }

        [Fact]
        public void Name_SortsByFileName_NotByFullPath()
        {
            // старый сортировщик по полному пути ставил \a\z выше \b\y — «идут чёрт знает как»
            var files = new List<string> { MakeFile("a", "zScrew.blend"), MakeFile("b", "yArrow.ma") };

            MainViewModel.SortProjectFiles(files, "name");

            Assert.EndsWith("yArrow.ma", files[0], StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Date_NewestFirst()
        {
            var old1 = MakeFile("s1", "old.blend", mtime: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var new1 = MakeFile("s2", "newer.hip", mtime: new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc));
            var files = new List<string> { old1, new1 };

            MainViewModel.SortProjectFiles(files, "date");

            Assert.Same(new1, files[0]);
        }

        [Fact]
        public void Date_TieBreaksByName()
        {
            var b = MakeFile("t1", "beta.ma", mtime: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
            var a = MakeFile("t2", "alpha.nk", mtime: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
            var files = new List<string> { b, a };

            MainViewModel.SortProjectFiles(files, "date");

            Assert.Same(a, files[0]); // одна дата — alpha выше beta
        }

        [Fact]
        public void App_GroupsByFamily_InCanonicalOrder()
        {
            var nk = MakeFile("f1", "comp.nk");
            var blend = MakeFile("f2", "scene.blend");
            var spp = MakeFile("f3", "tex.spp");
            var ma = MakeFile("f4", "rig.ma");
            var unknown = MakeFile("f5", "readme.txt");
            var files = new List<string> { unknown, spp, nk, ma, blend };

            MainViewModel.SortProjectFiles(files, "app");

            Assert.Same(blend, files[0]);  // blender
            Assert.Same(ma, files[1]);     // maya
            Assert.Same(nk, files[2]);     // nuke
            Assert.Same(spp, files[3]);    // painter
            Assert.Same(unknown, files[4]); // не-проектные расширения в конец
        }

        [Fact]
        public void UnknownMode_FallsBackToName()
        {
            var files = new List<string> { MakeFile("a", "z.ma"), MakeFile("b", "y.nk") };

            MainViewModel.SortProjectFiles(files, "garbage");

            Assert.EndsWith("y.nk", files[0], StringComparison.OrdinalIgnoreCase);
        }
    }
}
