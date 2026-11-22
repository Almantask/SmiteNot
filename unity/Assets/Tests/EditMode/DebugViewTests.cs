using System;
using System.IO;
using System.Linq;
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
                Assert.That(
                    current.X[u],
                    Is.EqualTo(reference.World.Position[u].X.Raw / (float)Fix.OneRaw)
                );
                Assert.That(
                    current.Z[u],
                    Is.EqualTo(reference.World.Position[u].Y.Raw / (float)Fix.OneRaw)
                );
                Assert.That(current.Regiment[u], Is.EqualTo(reference.World.UnitRegiment[u]));
            }
        }

        [Test]
        public void BattleHost_Worker_PublishesTicks_AndStops()
        {
            using var host = new BattleHost(RepoBattle("m1-flat", "m1-ten-thousand.json"))
            {
                Speed = 8,
            };

            host.Start();
            // Wait for the worker rather than a fixed time: a busy machine (a parallel test run) can be slow to start it.
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 10_000)
            {
                host.Read(out _, out var newest, out _);
                if (newest.Tick > 5)
                {
                    break;
                }
                Thread.Sleep(20);
            }
            host.Stop();

            host.Read(out _, out var current, out _);
            Assert.That(host.Failure, Is.Null);
            Assert.That(current.Tick, Is.GreaterThan(5));
            Assert.That(
                () => host.StepNow(1),
                Throws.Nothing,
                "the main thread may step once the worker stopped"
            );
        }

        [Test]
        public void FieldSnapshot_CarriesTheSimulationsCells_InItsOwnUnits()
        {
            var battle = RepoBattle("m3-river", "m3-river-two-thousand.json");
            using var host = new BattleHost(battle);
            var reference = battle.Start(new SerialRunner());

            host.StepNow(60 * SimConstants.TicksPerSecond); // the river runs past the ford by then
            reference.Step(60 * SimConstants.TicksPerSecond);

            var snapshot = host.ReadFields();
            var fields = reference.World.Fields;
            var depth = fields.Get(WaterModel.Depth);
            int columns = battle.Map.Columns;
            Assert.That(
                snapshot.Tick,
                Is.EqualTo(60 * SimConstants.TicksPerSecond - 3),
                "the last field step"
            );
            int wet = 0;
            for (int i = 0; i < columns * battle.Map.Rows; i += 7)
            {
                int o = i * FieldUpload.Stride;
                Assert.That(snapshot.Cells[o], Is.EqualTo((uint)depth.Values[i]), "depth in µm");
                Assert.That(
                    snapshot.Cells[o + 2],
                    Is.EqualTo(
                        (uint)fields.Get(GroundModel.Mud).Values[i]
                            | (uint)fields.Get(GroundModel.Wetness).Values[i] << 16
                    )
                );
                Assert.That(
                    snapshot.Cells[o + 3],
                    Is.EqualTo((uint)fields.Get(FieldSpeed.FieldId).Values[i])
                );
                if (depth.Values[i] >= WaterModel.VelocityMinDepthUm)
                {
                    wet++;
                    WaterModel.Velocity(
                        fields,
                        battle.Map.CellSizeMm,
                        i % columns,
                        i / columns,
                        out int east,
                        out int north
                    );
                    Assert.That((int)(snapshot.Cells[o + 1] & 0xFFFF) - 32768, Is.EqualTo(east));
                    Assert.That((int)(snapshot.Cells[o + 1] >> 16) - 32768, Is.EqualTo(north));
                }
            }
            Assert.That(wet, Is.GreaterThan(100), "the river is running");
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
            Assert.That(
                navigation.Costs.Cost(MovementClass.Foot, 393, 100),
                Is.EqualTo(CostFields.Impassable)
            );
            Assert.That(pixels[river].a, Is.GreaterThan(200), "closed cells are drawn");
            Assert.That(pixels[field].a, Is.Zero, "flat open ground stays clear");
        }

        [Test]
        public void MapExport_RoundTripsThroughTheSimulationsLoader()
        {
            string repo = Path.Combine(
                Path.GetTempPath(),
                "godgame-map-export-" + Guid.NewGuid().ToString("N")
            );
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
                    MapExport.Validate(
                        json.Replace("\"export-test\"", "\"Bad Id\""),
                        MapExport.HeightBytes(heights)
                    )
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

        [Test]
        public void UnitMeshes_AllAuthoredUnitTypes_ImportWithPaletteUvsAndTeamMask()
        {
            var rules = RepoLoader.LoadRules(RepoLoader.EditorRepoRoot);
            Assert.That(rules.UnitTypes.Count(t => !UnitArt.AwaitsModel(t.Id)), Is.EqualTo(12));

            foreach (var unitType in rules.UnitTypes.Where(t => !UnitArt.AwaitsModel(t.Id)))
            {
                string cleanId = unitType.Id.Replace("-", string.Empty);
                string assetPath =
                    $"Assets/_Game/Art/Generated/U_{cleanId}_a/U_{cleanId}_a_LOD0.fbx";
                Mesh mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                Assert.That(mesh, Is.Not.Null, "Missing imported unit mesh at " + assetPath);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0), unitType.Id);
                Assert.That(
                    mesh.uv.Length,
                    Is.EqualTo(mesh.vertexCount),
                    unitType.Id + " missing UVs"
                );
                Assert.That(
                    mesh.colors32.Length,
                    Is.EqualTo(mesh.vertexCount),
                    unitType.Id + " missing vertex colors (TeamMask)"
                );

                bool hasMasked = false;
                bool hasUnmasked = false;
                foreach (Color32 c in mesh.colors32)
                {
                    if (c.r > 200)
                    {
                        hasMasked = true;
                    }
                    else if (c.r < 50)
                    {
                        hasUnmasked = true;
                    }
                }
                Assert.That(hasMasked, Is.True, unitType.Id + " has no TeamMask == 1 vertices");
                Assert.That(hasUnmasked, Is.True, unitType.Id + " has no TeamMask == 0 vertices");

                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    assetPath
                );
                Renderer rend = prefab != null ? prefab.GetComponentInChildren<Renderer>() : null;
                Assert.That(rend, Is.Not.Null, unitType.Id + " missing prefab renderer");
                Matrix4x4 relMat =
                    prefab.transform.worldToLocalMatrix * rend.transform.localToWorldMatrix;
                Assert.That(relMat.m00, Is.EqualTo(1f).Within(0.01f), unitType.Id + " relMat m00");
                Assert.That(relMat.m12, Is.EqualTo(-1f).Within(0.01f), unitType.Id + " relMat m12");
                Assert.That(relMat.m21, Is.EqualTo(1f).Within(0.01f), unitType.Id + " relMat m21");
                Assert.That(mesh.bounds.max.z, Is.LessThan(0.15f), unitType.Id + " feet near Z=0");
                Assert.That(
                    mesh.bounds.min.z,
                    Is.LessThan(-1.4f),
                    unitType.Id + " height along -Z"
                );

                if (unitType.Id == "cavalry")
                {
                    Vector3 nose = Vector3.zero;
                    foreach (Vector3 v in mesh.vertices)
                    {
                        float h = -v.z;
                        if (Mathf.Abs(v.x) < 0.18f && h >= 1.30f && h <= 1.62f)
                        {
                            if (Mathf.Abs(v.y) > Mathf.Abs(nose.y))
                            {
                                nose = v;
                            }
                        }
                    }
                    Assert.That(
                        nose.y,
                        Is.LessThan(-0.85f),
                        "Expected cavalry snout along -Y in raw imported FBX mesh"
                    );
                }
            }
        }

        [Test]
        public void UnitVats_AllAuthoredUnitTypes_BakeWithMovingVerticesAcrossFrames()
        {
            var rules = RepoLoader.LoadRules(RepoLoader.EditorRepoRoot);
            Assert.That(rules.UnitTypes.Count(t => !UnitArt.AwaitsModel(t.Id)), Is.EqualTo(12));

            foreach (var unitType in rules.UnitTypes.Where(t => !UnitArt.AwaitsModel(t.Id)))
            {
                string cleanId = unitType.Id.Replace("-", string.Empty);
                string meshPath =
                    $"Assets/_Game/Art/Generated/U_{cleanId}_a/U_{cleanId}_a_LOD0.fbx";
                Mesh mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                Assert.That(mesh, Is.Not.Null, unitType.Id);

                UnitVatData vat = VatBaker.BakeOrLoadUnit(cleanId, forceBake: false);
                Assert.That(vat, Is.Not.Null, unitType.Id);
                Assert.That(vat.ClipCount, Is.EqualTo(6), unitType.Id + " clip count");
                Assert.That(
                    vat.FramesPerClip,
                    Is.EqualTo(VatBaker.DefaultFramesPerClip),
                    unitType.Id + " frames per clip"
                );
                Assert.That(
                    vat.FrameCount,
                    Is.EqualTo(6 * VatBaker.DefaultFramesPerClip),
                    unitType.Id + " total frames"
                );
                Assert.That(vat.VertexCount, Is.EqualTo(mesh.vertexCount), unitType.Id);
                Assert.That(
                    vat.Positions.Length,
                    Is.EqualTo(vat.FrameCount * vat.VertexCount),
                    unitType.Id
                );
                Assert.That(
                    vat.Normals.Length,
                    Is.EqualTo(vat.FrameCount * vat.VertexCount),
                    unitType.Id
                );

                float minY = float.MaxValue;
                float maxY = float.MinValue;
                float walkDisp = 0f;
                int f7Offset = 7 * vat.VertexCount;
                for (int v = 0; v < vat.VertexCount; v++)
                {
                    Vector3 p0 = vat.Positions[v];
                    minY = Mathf.Min(minY, p0.y);
                    maxY = Mathf.Max(maxY, p0.y);
                    float d = Vector3.Distance(p0, (Vector3)vat.Positions[f7Offset + v]);
                    if (d > walkDisp)
                    {
                        walkDisp = d;
                    }
                }

                Assert.That(minY, Is.InRange(-0.25f, 0.25f), unitType.Id + " feet near Y=0");
                Assert.That(maxY, Is.GreaterThan(1.4f), unitType.Id + " upright height +Y");
                Assert.That(
                    walkDisp,
                    Is.GreaterThan(0.12f),
                    unitType.Id + " walk clip frame 0..7 vertex displacement"
                );

                for (int c = 1; c < vat.ClipCount; c++)
                {
                    int clipBase = c * vat.FramesPerClip * vat.VertexCount;
                    float clipMotion = 0f;
                    float diffFromWalk = 0f;
                    for (int f = 0; f < vat.FramesPerClip; f++)
                    {
                        int fOff = (c * vat.FramesPerClip + f) * vat.VertexCount;
                        int wOff = f * vat.VertexCount;
                        for (int v = 0; v < vat.VertexCount; v++)
                        {
                            Vector3 pCbt = vat.Positions[fOff + v];
                            if (f > 0)
                            {
                                float dm = Vector3.Distance(
                                    (Vector3)vat.Positions[clipBase + v],
                                    pCbt
                                );
                                if (dm > clipMotion)
                                {
                                    clipMotion = dm;
                                }
                            }
                            float dw = Vector3.Distance((Vector3)vat.Positions[wOff + v], pCbt);
                            if (dw > diffFromWalk)
                            {
                                diffFromWalk = dw;
                            }
                        }
                    }

                    Assert.That(
                        clipMotion,
                        Is.GreaterThan(0.30f),
                        $"{unitType.Id} combat clip {c} internal displacement"
                    );
                    Assert.That(
                        diffFromWalk,
                        Is.GreaterThan(0.25f),
                        $"{unitType.Id} combat clip {c} distinct from locomotion"
                    );
                }

                for (int c = 4; c < vat.ClipCount; c++)
                {
                    int f0Off = c * vat.FramesPerClip * vat.VertexCount;
                    int fEndOff = (c * vat.FramesPerClip + vat.FramesPerClip - 1) * vat.VertexCount;
                    float maxY0 = float.MinValue;
                    float maxYEnd = float.MinValue;
                    float deathMotion = 0f;
                    for (int v = 0; v < vat.VertexCount; v++)
                    {
                        Vector3 p0 = vat.Positions[f0Off + v];
                        Vector3 pEnd = vat.Positions[fEndOff + v];
                        if (p0.y > maxY0)
                        {
                            maxY0 = p0.y;
                        }
                        if (pEnd.y > maxYEnd)
                        {
                            maxYEnd = pEnd.y;
                        }
                        float d = Vector3.Distance(p0, pEnd);
                        if (d > deathMotion)
                        {
                            deathMotion = d;
                        }
                    }

                    Assert.That(
                        deathMotion,
                        Is.GreaterThan(0.45f),
                        $"{unitType.Id} death clip {c} frame 0..29 displacement"
                    );
                    Assert.That(
                        maxYEnd,
                        Is.LessThan(maxY0 - 0.35f),
                        $"{unitType.Id} death clip {c} settles closer to ground"
                    );
                }

                for (int c1 = 1; c1 < vat.ClipCount; c1++)
                {
                    for (int c2 = c1 + 1; c2 < vat.ClipCount; c2++)
                    {
                        float pairDiff = 0f;
                        for (int f = 0; f < vat.FramesPerClip; f++)
                        {
                            int off1 = (c1 * vat.FramesPerClip + f) * vat.VertexCount;
                            int off2 = (c2 * vat.FramesPerClip + f) * vat.VertexCount;
                            for (int v = 0; v < vat.VertexCount; v++)
                            {
                                float d = Vector3.Distance(
                                    (Vector3)vat.Positions[off1 + v],
                                    (Vector3)vat.Positions[off2 + v]
                                );
                                if (d > pairDiff)
                                {
                                    pairDiff = d;
                                }
                            }
                        }

                        Assert.That(
                            pairDiff,
                            Is.GreaterThan(0.25f),
                            $"{unitType.Id} combat clip {c1} vs {c2} distinct poses"
                        );
                    }
                }
            }
        }
    }
}
