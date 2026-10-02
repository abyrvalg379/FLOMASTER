using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // ---- Цикл APP-сортировки: повторный клик по Apps докручивает семейства вправо ----

        [Fact]
        public void App_CycleOffset1_PutsPainterFirst()
        {
            // кейс юзера: все .spp наверху «в моменте» — один клик по активному Apps
            var blend = MakeFile("g1", "scene.blend");
            var ma = MakeFile("g2", "rig.ma");
            var spp = MakeFile("g3", "tex.spp");
            var files = new List<string> { blend, ma, spp };

            MainViewModel.SortProjectFiles(files, "app", 1);

            Assert.Same(spp, files[0]);
            Assert.Same(blend, files[1]);
            Assert.Same(ma, files[2]);
        }

        [Fact]
        public void App_Cycle_TwoFamilies_FlipsEveryClick()
        {
            // фикс «через раз»: вращаются только ПРИСУТСТВУЮЩИЕ семейства —
            // с blender+maya каждый клик меняет порядок, пустые места не съедают клики
            var blend = MakeFile("h1", "scene.blend");
            var ma = MakeFile("h2", "rig.ma");
            var files = new List<string> { blend, ma };

            MainViewModel.SortProjectFiles(files, "app", 1); // последнее присутствующее — наверх
            Assert.Same(ma, files[0]);

            MainViewModel.SortProjectFiles(files, "app", 2); // 2 % 2 = 0 — обратно к канону
            Assert.Same(blend, files[0]);
        }

        [Fact]
        public void App_Cycle_SkipsAbsentFamilies()
        {
            // maya/nuke файлов в папке нет: offset 1 ставит painter (последнее ПРИСУТСТВУЮЩЕЕ),
            // offset 2 — houdini, а не прокручивает пустые maya/nuke
            var blend = MakeFile("h7", "scene.blend");
            var hip = MakeFile("h8", "fx.hip");
            var spp = MakeFile("h9", "tex.spp");
            var files = new List<string> { blend, hip, spp };

            MainViewModel.SortProjectFiles(files, "app", 1);
            Assert.Same(spp, files[0]);
            Assert.Same(blend, files[1]);
            Assert.Same(hip, files[2]);

            MainViewModel.SortProjectFiles(files, "app", 2);
            Assert.Same(hip, files[0]);
        }

        [Fact]
        public void App_CycleOffset_EqualsCanonical_WhenModuloPresentCount()
        {
            var five = new List<string>
            {
                MakeFile("h3", "tex.spp"), MakeFile("h4", "comp.nk"), MakeFile("h5", "rig.ma"),
                MakeFile("h6", "fx.hip"), MakeFile("h10", "scene.blend")
            };
            MainViewModel.SortProjectFiles(five, "app", 0);
            var canonical = five.ToList();
            MainViewModel.SortProjectFiles(five, "app", 5);
            Assert.Equal(canonical, five);
        }

        [Fact]
        public void App_OtherFamily_AlwaysLast_RegardlessOfOffset()
        {
            var unknown = MakeFile("i1", "readme.txt");
            var spp = MakeFile("i2", "tex.spp");
            var files = new List<string> { unknown, spp };

            MainViewModel.SortProjectFiles(files, "app", 3); // painter наверх, «прочее» не участвует в цикле

            Assert.Same(spp, files[0]);
            Assert.Same(unknown, files[1]);
        }
    }
}
