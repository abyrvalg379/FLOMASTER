using System;
using System.Collections.Generic;
using System.IO;
using FLOMASTER.Models;
using FLOMASTER.Services;
using Xunit;

namespace FLOMASTER.Tests
{
    /// <summary>Герметичные тесты папки-синка v2: temp-папки, без сети и UI.</summary>
    public class SyncServiceTests : IDisposable
    {
        private readonly string _dir;

        public SyncServiceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "flm_sync_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        private static SettingsExport MakeExport(string machine, string profileName = "TB3") => new()
        {
            App = "FLOMASTER",
            MachineName = machine,
            ExportedAt = "2026-10-02T00:00:00",
            Profiles = new List<Profile> { new() { Name = profileName, PresetName = "Blender", OcioName = "ACES 1.2", Args = "" } },
            ProjectRoots = new List<string> { @"E:\Project\Work" }
        };

        // ---- Имена файлов и hash ----

        [Theory]
        [InlineData("WORKHORSE", "FLOMASTER_WORKHORSE.flomaster")]
        [InlineData("work pc v2", "FLOMASTER_work-pc-v2.flomaster")]   // пробелы/точки — не для имени файла
        [InlineData("ПК-ДОМ", "FLOMASTER_pc.flomaster")]               // не-ASCII полностью схлопнулся — fallback "pc"
        [InlineData("", "FLOMASTER_pc.flomaster")]
        public void FileNameFor_SanitizesMachineName(string machine, string expected)
        {
            Assert.Equal(expected, SyncService.FileNameFor(machine));
        }

        [Fact]
        public void Hash8_Deterministic_DiffersOnContentChange()
        {
            var a = SyncService.Hash8("payload v1");
            Assert.Equal(8, a.Length);
            Assert.Equal(a, SyncService.Hash8("payload v1"));
            Assert.NotEqual(a, SyncService.Hash8("payload v2"));
        }

        // ---- Payload → Parse: свой формат читается, чужой — нет ----

        [Fact]
        public void Parse_RoundtripsPayload_WithMachineName()
        {
            var json = SyncService.BuildPayload(MakeExport("WORKHORSE"));
            var parsed = SyncService.Parse(json);

            Assert.NotNull(parsed);
            Assert.Equal("WORKHORSE", parsed!.MachineName);
            Assert.Single(parsed.Profiles);
            Assert.Equal("TB3", parsed.Profiles[0].Name);
        }

        [Fact]
        public void Parse_RejectsForeignJson()
        {
            Assert.Null(SyncService.Parse("not json"));
            Assert.Null(SyncService.Parse("{\"app\":\"OtherTool\"}"));
        }

        // ---- Push: tmp + move, без мусора в папке ----

        [Fact]
        public void Push_WritesOwnFile_AndLeavesNoTmp()
        {
            var own = SyncService.OwnFileName();
            SyncService.Push(_dir, SyncService.BuildPayload(MakeExport(Environment.MachineName)));

            Assert.True(File.Exists(Path.Combine(_dir, own)));
            Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
        }

        // ---- Scan: свой мимо, seen мимо, новое/изменившееся — в кандидаты ----

        [Fact]
        public void Scan_FiltersOwnSeenAndForeign_IncludesNewAndChanged()
        {
            var own = SyncService.OwnFileName();
            var otherA = SyncService.FileNameFor("OTHER-A");
            var otherB = SyncService.FileNameFor("OTHER-B");

            File.WriteAllText(Path.Combine(_dir, own), SyncService.BuildPayload(MakeExport(Environment.MachineName)));
            var jsonA = SyncService.BuildPayload(MakeExport("OTHER-A"));
            File.WriteAllText(Path.Combine(_dir, otherA), jsonA);
            var jsonB = SyncService.BuildPayload(MakeExport("OTHER-B"));
            File.WriteAllText(Path.Combine(_dir, otherB), jsonB);
            File.WriteAllText(Path.Combine(_dir, "garbage.flomaster"), "{ not flo }");

            var seen = new Dictionary<string, string> { [otherA] = SyncService.Hash8(jsonA) };

            var pending = SyncService.Scan(_dir, own, seen);

            // OTHER-A уже обработан (hash совпал), свой и мусор — мимо, OTHER-B новый
            var candidate = Assert.Single(pending);
            Assert.Equal(otherB, candidate.FileName);
            Assert.Equal("OTHER-B", candidate.Label);
        }

        [Fact]
        public void Scan_ChangedFile_Reoffered_NewHash()
        {
            var other = SyncService.FileNameFor("OTHER");
            var jsonV1 = SyncService.BuildPayload(MakeExport("OTHER"));
            File.WriteAllText(Path.Combine(_dir, other), jsonV1);

            var seen = new Dictionary<string, string> { [other] = SyncService.Hash8(jsonV1) };
            Assert.Empty(SyncService.Scan(_dir, SyncService.OwnFileName(), seen));

            // вторая машина обновила состояние — hash изменился, файл предлагают снова
            var jsonV2 = SyncService.BuildPayload(MakeExport("OTHER", "NewProfile"));
            File.WriteAllText(Path.Combine(_dir, other), jsonV2);

            var pending = SyncService.Scan(_dir, SyncService.OwnFileName(), seen);
            Assert.Single(pending);
            Assert.NotEqual(SyncService.Hash8(jsonV1), pending[0].Hash);
        }

        [Fact]
        public void Scan_MissingFolder_ReturnsEmpty_NoThrow()
        {
            Assert.Empty(SyncService.Scan(Path.Combine(_dir, "nope"), "x.flomaster", new Dictionary<string, string>()));
        }
    }
}
