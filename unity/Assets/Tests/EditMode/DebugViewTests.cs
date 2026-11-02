using System;
using System.IO;
using System.Threading;
using GodGame.Bridge;
using GodGame.Editor;
using GodGame.Rendering;
using GodGame.Sim;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;
using NUnit.Framework;
using UnityEngine;

namespace GodGame.Tests.EditMode
{
    public class DebugViewTests
    {
        private static Battle RepoBattle(string mapId, string planFile)
        {
            string repo = RepoLoader.EditorRepoRoot;
            var rules = RepoLoader.LoadRules(repo);
            var map = RepoLoader.LoadMap(repo, mapId);
            return new Battle(rules, map, RepoLoader.LoadPlan(repo, planFile, map, rules), 1);
        }

        [Test]
        public void BattleHost_Snapshots_MatchTheSimulationItRuns()
        {
            var battle = RepoBattle("m1-flat", "m1-ten-thousand.json");
            using var host = new BattleHost(battle);
            var reference = battle.Start(new SerialRunner());

            host.StepNow(25);
            reference.Step(25);

            host.Read(out var previous, out var current, out _);
            Assert.That(current.Tick, Is.EqualTo(25));
            Assert.That(previous.Tick, Is.EqualTo(24));
            for (int u = 0; u < reference.World.Units.HighWater; u += 97)
            {
                Assert.That(current.X[u], Is.EqualTo(reference.World.Position[u].X.Raw / (float)Fix.OneRaw));
                Assert.That(current.Z[u], Is.EqualTo(reference.World.Position[u].Y.Raw / (float)Fix.OneRaw));
                Assert.That(current.Regiment[u], Is.EqualTo(reference.World.UnitRegiment[u]));
            }
        }

        [Test]
        public void BattleHost_Worker_PublishesTicks_AndStops()
        {
            using var host = new BattleHost(RepoBattle("m1-flat", "m1-ten-thousand.json")) { Speed = 8 };

            host.Start();
            Thread.Sleep(500);
            host.Stop();

            host.Read(out _, out var current, out _);
            Assert.That(host.Failure, Is.Null);
            Assert.That(current.Tick, Is.GreaterThan(5));
            Assert.That(() => host.StepNow(1), Throws.Nothing, "the main thread may step once the worker stopped");
        }

        [Test]
        public void OverlayPainter_ShowsClosedCellsAndRisingCost_StraightFromTheFields()
        {
            var battle = RepoBattle("m2-bridge", "m2-bridge-ten-thousand.json");
            var navigation = new Navigation(battle.Map, battle.Rules, new SerialRunner());
            var pixels = new Color32[navigation.Terrain.CellCount];

            OverlayPainter.Paint(OverlayMode.CostFoot, navigation, null, pixels);

            var terrain = navigation.Terrain;
            int river = terrain.Index(393, 100); // x = 590 m: the trench's west wall (its flat floor is open, but walled in)
            int field = terrain.Index(100, 100);
            Assert.That(navigation.Costs.Cost(MovementClass.Foot, 393, 100), Is.EqualTo(CostFields.Impassable));
            Assert.That(pixels[river].a, Is.GreaterThan(200), "closed cells are drawn");
            Assert.That(pixels[field].a, Is.Zero, "flat open ground stays clear");
        }

        [Test]
        public void MapExport_RoundTripsThroughTheSimulationsLoader()
        {
            string repo = Path.Combine(Path.GetTempPath(), "godgame-map-export-" + Guid.NewGuid().ToString("N"));
            try
            {
                // A 2 × 2 heightmap: west low, east high.
                var heights = MapExport.Resample(new[] { 0f, 1f, 0f, 1f }, 2, 2, 20, 10, 0, 1_000);
                string json = MapExport.MapJson(
                    "export-test",
                    30_000,
                    15_000,
                    1_500,
                    new ZoneMm(0, 0, 5_000, 15_000),
                    new ZoneMm(25_000, 0, 30_000, 15_000),
                    "a test"
                );

                string dir = MapExport.Write(repo, json, MapExport.HeightBytes(heights));
                var map = RepoLoader.LoadMap(repo, "export-test");

                Assert.That(Directory.Exists(dir), Is.True);
                Assert.That((map.Columns, map.Rows), Is.EqualTo((20, 10)));
                Assert.That(map.HeightMm[0], Is.EqualTo(0));
                Assert.That(map.HeightMm[19], Is.EqualTo(1_000));
                Assert.That(map.ZoneOf(Side.Defender).Area.MinX, Is.EqualTo(25_000));
                Assert.Throws<GodGame.Sim.Rules.Json.DataFormatException>(() =>
                    MapExport.Validate(json.Replace("\"export-test\"", "\"Bad Id\""), MapExport.HeightBytes(heights))
                );
            }
            finally
            {
                if (Directory.Exists(repo))
                {
                    Directory.Delete(repo, true);
                }
            }
        }
    }
}
