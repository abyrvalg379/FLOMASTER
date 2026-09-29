using System.Collections.Generic;
using FLOMASTER.Models;
using FLOMASTER.ViewModels;
using Xunit;

namespace FLOMASTER.Tests
{
    /// <summary>Маршрутизация файлов проектов на пресеты по расширению.</summary>
    public class ProjectRoutingTests
    {
        private static readonly List<Preset> Presets = new()
        {
            new Preset { Name = "Blender 5.2", Exe = @"C:\apps\blender-4.2\blender.exe" },
            new Preset { Name = "K-Cycles 2026", Exe = @"C:\apps\K-Cycles_2026\blender.exe" },
            new Preset { Name = "Substance 3D Painter", Exe = @"C:\Program Files\Adobe\Adobe Substance 3D Painter\Adobe Substance 3D Painter.exe" },
            new Preset { Name = "Houdini 20.5", Exe = @"C:\Program Files\Side Effects Software\Houdini 20.5.332\bin\houdini.exe" },
        };

        [Theory]
        [InlineData(@"E:\work\tex.spp", "Substance 3D Painter")]
        [InlineData(@"E:\work\scene.blend", "Blender 5.2")]
        [InlineData(@"E:\work\shot.hip", "Houdini 20.5")]
        [InlineData(@"E:\work\shot.hipl", "Houdini 20.5")]
        [InlineData(@"E:\work\comp.nk", null)]  // Nuke-пресета нет — маршрута нет
        [InlineData(@"E:\work\model.max", null)] // неизвестное расширение
        public void FindPresetForExtension_RoutesByFamily(string file, string expectedName)
        {
            var found = MainViewModel.FindPresetForExtension(Presets, file);
            if (expectedName == null) Assert.Null(found);
            else Assert.Equal(expectedName, found.Name);
        }

        [Fact]
        public void FindPresetForExtension_KCyclesCountsAsBlender()
        {
            // K-Cycles: exe содержит blender.exe — валидный кандидат для .blend
            var only = new List<Preset> { Presets[1] };
            Assert.NotNull(MainViewModel.FindPresetForExtension(only, @"E:\x\b.blend"));
        }
    }
}
