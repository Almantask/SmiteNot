using System;
using System.Collections.Generic;
using GodGame.Bridge;
using GodGame.Rendering;
using GodGame.Sim;
using NUnit.Framework;
using UnityEngine;

namespace GodGame.Tests.EditMode
{
    public class WreckSalvoAndCrewTests
    {
        private static MapData FlatMap(int widthMm = 300_000, int depthMm = 180_000)
        {
            string json =
                "{\"version\":0,\"id\":\"test-siege-flat\",\"widthMm\":"
                + widthMm
                + ",\"depthMm\":"
                + depthMm
                + ",\"cellSizeMm\":1500,\"deployment\":["
                + "{\"side\":\"attacker\",\"minXMm\":0,\"minYMm\":0,\"maxXMm\":60000,\"maxYMm\":"
                + depthMm
                + "},"
                + "{\"side\":\"defender\",\"minXMm\":"
                + (widthMm - 60_000)
                + ",\"minYMm\":0,\"maxXMm\":"
                + widthMm
                + ",\"maxYMm\":"
                + depthMm
                + "}]}";
            return MapData.Load(json);
        }

        [Test]
        public void SalvoAndBlockerProjection_FiltersWrecksByScrubTimeAndClearEvents()
        {
            var map = FlatMap();
            int cellA = 10 * map.Columns + 20;
            int cellB = 10 * map.Columns + 21;

            var blockers = new BlockerSnapshot(4) { Tick = 400, Count = 2 };
            blockers.Kind[0] = BlockerKind.Wreck;
            blockers.EventIndex[0] = 0;
            blockers.Cleared[0] = true; // cleared later at tick 300
            blockers.Cells[0] = new[] { cellA, cellB };
            blockers.Profile[0] = new byte[] { 0, 0 };

            // FelledTree blocker should not be emitted as an engine wreck.
            blockers.Kind[1] = BlockerKind.FelledTree;
            blockers.EventIndex[1] = 1;
            blockers.Cleared[1] = false;
            blockers.Cells[1] = new[] { cellA + map.Columns };
            blockers.Profile[1] = new byte[] { 0 };

            var causes = new[]
            {
                new CauseEvent(
                    CauseKind.EngineWrecked,
                    tick: 100,
                    subject: 0,
                    detail: 0,
                    cause: -1,
                    cause2: -1
                ),
                new CauseEvent(
                    CauseKind.TreeFelled,
                    tick: 120,
                    subject: 0,
                    detail: 1,
                    cause: -1,
                    cause2: -1
                ),
                new CauseEvent(
                    CauseKind.BlockerCleared,
                    tick: 300,
                    subject: 1,
                    detail: 0,
                    cause: -1,
                    cause2: -1
                ),
            };

            var output = new List<WreckCellInstance>();

            // Before wreck event tick (50 < 100): not yet active in scrub view.
            SalvoAndBlockerProjection.CollectWrecks(
                blockers,
                causes,
                map,
                null,
                atTick: 50,
                filterByTime: true,
                includeCleared: false,
                output
            );
            Assert.That(output.Count, Is.EqualTo(0));

            // Between creation (100) and clear (300): both wreck cells visible at world coordinates.
            SalvoAndBlockerProjection.CollectWrecks(
                blockers,
                causes,
                map,
                null,
                atTick: 200,
                filterByTime: true,
                includeCleared: false,
                output
            );
            Assert.That(output.Count, Is.EqualTo(2));
            Assert.That(output[0].Cleared, Is.False);
            Assert.That(output[0].WorldX, Is.EqualTo(20.5f * 1.5f).Within(0.01f));
            Assert.That(output[0].WorldZ, Is.EqualTo(10.5f * 1.5f).Within(0.01f));

            // After clear tick (350 >= 300): hidden when includeCleared=false, flagged when includeCleared=true.
            SalvoAndBlockerProjection.CollectWrecks(
                blockers,
                causes,
                map,
                null,
                atTick: 350,
                filterByTime: true,
                includeCleared: false,
                output
            );
            Assert.That(output.Count, Is.EqualTo(0));

            SalvoAndBlockerProjection.CollectWrecks(
                blockers,
                causes,
                map,
                null,
                atTick: 350,
                filterByTime: true,
                includeCleared: true,
                output
            );
            Assert.That(output.Count, Is.EqualTo(2));
            Assert.That(output[0].Cleared, Is.True);
        }

        [Test]
        public void SalvoAndBlockerProjection_LinksStoneLandedAndSoldierCrushedCounts()
        {
            var map = FlatMap();
            int cell1 = 20 * map.Columns + 30;
            int cell2 = 25 * map.Columns + 40;

            var causes = new[]
            {
                new CauseEvent(
                    CauseKind.StoneLanded,
                    tick: 40,
                    subject: 0,
                    detail: cell1,
                    cause: -1,
                    cause2: -1
                ),
                new CauseEvent(
                    CauseKind.SoldierCrushed,
                    tick: 40,
                    subject: 1,
                    detail: 12,
                    cause: 0,
                    cause2: -1
                ),
                new CauseEvent(
                    CauseKind.SoldierCrushed,
                    tick: 40,
                    subject: 1,
                    detail: 13,
                    cause: 0,
                    cause2: -1
                ),
                new CauseEvent(
                    CauseKind.StoneLanded,
                    tick: 260,
                    subject: 0,
                    detail: cell2,
                    cause: -1,
                    cause2: -1
                ),
            };

            var output = new List<StoneLandingInstance>();
            SalvoAndBlockerProjection.CollectStoneLandings(
                causes,
                map,
                null,
                atTick: 300,
                windowSeconds: 10f,
                output
            );

            // Event 0 was 260 ticks = 13 s ago (outside 10 s window); event 3 was 2 s ago (inside).
            Assert.That(output.Count, Is.EqualTo(1));
            Assert.That(output[0].EventIndex, Is.EqualTo(3));
            Assert.That(output[0].WorldX, Is.EqualTo(40.5f * 1.5f).Within(0.01f));
            Assert.That(output[0].WorldZ, Is.EqualTo(25.5f * 1.5f).Within(0.01f));
            Assert.That(output[0].CrushedCount, Is.EqualTo(0));

            // Full history shows both landings with 2 crushed soldiers attributed to event 0.
            SalvoAndBlockerProjection.CollectStoneLandings(
                causes,
                map,
                null,
                atTick: 300,
                windowSeconds: float.PositiveInfinity,
                output
            );
            Assert.That(output.Count, Is.EqualTo(2));
            Assert.That(output[0].CrushedCount, Is.EqualTo(2));
            Assert.That(output[1].CrushedCount, Is.EqualTo(0));
        }

        [Test]
        public void BattleHostAndPreviewWorker_ExposeSiegeBlockersAndStoneLandingsFromSim()
        {
            var rules = RepoLoader.LoadRules(RepoLoader.EditorRepoRoot);
            var map = FlatMap(widthMm: 220_000, depthMm: 120_000);
            string planJson =
                "{\"version\":0,\"map\":\""
                + map.Id
                + "\",\"army\":["
                + "{\"id\":\"att-cav-a\",\"side\":\"attacker\",\"unitType\":\"cavalry\",\"soldiers\":120,\"xMm\":50000,\"yMm\":55000,\"facingCentiDegrees\":0},"
                + "{\"id\":\"att-cav-b\",\"side\":\"attacker\",\"unitType\":\"cavalry\",\"soldiers\":120,\"xMm\":50000,\"yMm\":65000,\"facingCentiDegrees\":0},"
                + "{\"id\":\"def-siege\",\"side\":\"defender\",\"unitType\":\"siege\",\"soldiers\":1,\"xMm\":165000,\"yMm\":60000,\"facingCentiDegrees\":18000}"
                + "],\"terrain\":[],\"casts\":[]}";
            var plan = BattlePlan.Load(planJson, map, rules);
            var battle = new Battle(rules, map, plan, 1UL);

            using var worker = new PreviewWorker(
                battle,
                new SerialRunner(),
                new KeyframeStore(32L << 20),
                endTick: 180 * SimConstants.TicksPerSecond
            );
            worker.RunUntilIdle();

            var blockers = new BlockerSnapshot();
            worker.ReadBlockers(blockers);
            var causes = worker.ReadCauses();

            var salvos = new List<StoneLandingInstance>();
            SalvoAndBlockerProjection.CollectStoneLandings(
                causes,
                map,
                null,
                worker.EndTick,
                float.PositiveInfinity,
                salvos
            );
            Assert.That(salvos.Count, Is.GreaterThan(0), "Expected siege engines to fire stones.");

            var wrecks = new List<WreckCellInstance>();
            SalvoAndBlockerProjection.CollectWrecks(
                blockers,
                causes,
                map,
                null,
                worker.EndTick,
                filterByTime: true,
                includeCleared: true,
                wrecks
            );
            Assert.That(
                blockers.Count,
                Is.GreaterThan(0),
                "Expected overrun siege engines to create wreck blockers."
            );
            Assert.That(wrecks.Count, Is.GreaterThan(0));

            using var host = new BattleHost(battle);
            var initialBlockers = new BlockerSnapshot();
            host.ReadBlockers(initialBlockers);
            Assert.That(initialBlockers.Count, Is.EqualTo(0));
        }

        [Test]
        public void UnitCapsuleRenderer_ConfiguresCrewAsDistinctCapsules()
        {
            var rules = RepoLoader.LoadRules(RepoLoader.EditorRepoRoot);
            var map = FlatMap();
            Shader unitShader = Shader.Find("GodGame/DebugUnits");
            Assert.That(unitShader, Is.Not.Null);

            int crewType = -1;
            int infantryType = -1;
            for (int t = 0; t < rules.UnitTypes.Count; t++)
            {
                if (rules.UnitTypes[t].Id == "crew")
                {
                    crewType = t;
                }
                else if (rules.UnitTypes[t].Id == "infantry")
                {
                    infantryType = t;
                }
            }
            Assert.That(crewType, Is.GreaterThanOrEqualTo(0));
            Assert.That(infantryType, Is.GreaterThanOrEqualTo(0));

            using var renderer = new UnitCapsuleRenderer(
                unitShader,
                64,
                rules,
                new[] { Side.Attacker, Side.Defender },
                map,
                tint: null,
                typeMeshes: new Mesh[rules.UnitTypes.Count]
            );

            Assert.That(renderer.UsesCrewCapsuleStyle(crewType), Is.True);
            Assert.That(renderer.UsesCrewCapsuleStyle(infantryType), Is.False);
            Vector4 crewShape = renderer.GetTypeShape(crewType);
            Vector4 infShape = renderer.GetTypeShape(infantryType);
            Assert.That(crewShape.z, Is.EqualTo(1f));
            Assert.That(infShape.z, Is.EqualTo(0f));
            Assert.That(crewShape.x, Is.GreaterThan(infShape.x));
            Assert.That(crewShape.y, Is.LessThan(infShape.y));
        }

        [Test]
        public void WorldMarkerRenderer_DrawsWrecksAndSalvosWithoutErrors()
        {
            var map = FlatMap();
            Shader markerShader = Shader.Find("GodGame/DebugMarkers");
            Assert.That(markerShader, Is.Not.Null);

            using var markers = new WorldMarkerRenderer(map, markerShader);
            var wrecks = new List<WreckCellInstance>
            {
                new WreckCellInstance(15f, 0f, 15f, 0.5f, false, 0, 0, 10),
            };
            var salvos = new List<StoneLandingInstance>
            {
                new StoneLandingInstance(1, 20, 0, 12, 30f, 0f, 20f, 3, 0.5f),
            };

            markers.Draw(wrecks, salvos);
            Assert.That(markers.DrawnWrecksLastFrame, Is.EqualTo(1));
            Assert.That(markers.DrawnSalvosLastFrame, Is.EqualTo(1));
        }

        [Test]
        public void CauseBreakdown_RecognizesItem7Causes()
        {
            var rules = RepoLoader.LoadRules(RepoLoader.EditorRepoRoot);
            var map = FlatMap();
            string planJson =
                "{\"version\":0,\"map\":\""
                + map.Id
                + "\",\"army\":["
                + "{\"id\":\"att\",\"side\":\"attacker\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":10000,\"yMm\":90000,\"facingCentiDegrees\":0},"
                + "{\"id\":\"def\",\"side\":\"defender\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":270000,\"yMm\":90000,\"facingCentiDegrees\":18000}"
                + "],\"terrain\":[],\"casts\":[]}";
            var plan = BattlePlan.Load(planJson, map, rules);
            var events = new[]
            {
                new CauseEvent(
                    CauseKind.SoldierTrampled,
                    tick: 80,
                    subject: 0,
                    detail: 1,
                    cause: -1,
                    cause2: -1
                ),
                new CauseEvent(
                    CauseKind.SoldierBurned,
                    tick: 90,
                    subject: 1,
                    detail: 2,
                    cause: -1,
                    cause2: -1
                ),
            };
            var breakdown = CauseBreakdown.Analyze(events, plan);
            Assert.That(breakdown.AttackerLosses, Is.EqualTo(1));
            Assert.That(breakdown.DefenderLosses, Is.EqualTo(1));
            Assert.That(breakdown.UnknownKindEvents, Is.EqualTo(0));
            Assert.That(breakdown.DeathCauses.Count, Is.EqualTo(2));
        }

        [Test]
        public void GreyBoxMaps_M5StampedeDroversCharge_LoadCleanly()
        {
            foreach (string mapId in new[] { "m5-stampede", "m5-drovers", "m5-charge" })
            {
                var map = RepoLoader.LoadMap(RepoLoader.EditorRepoRoot, mapId);
                Assert.That(map.Columns, Is.GreaterThan(0), mapId);
                Assert.That(map.Rows, Is.GreaterThan(0), mapId);
                Assert.That(map.HeightMm.Length, Is.EqualTo(map.Columns * map.Rows), mapId);
                Assert.That(map.DeploymentZones.Count, Is.EqualTo(2), mapId);
            }

            var stampede = RepoLoader.LoadMap(RepoLoader.EditorRepoRoot, "m5-stampede");
            Assert.That(stampede.SoilAreas.Count, Is.EqualTo(1));
            Assert.That(stampede.SoilAreas[0].Soil, Is.EqualTo("road"));

            var drovers = RepoLoader.LoadMap(RepoLoader.EditorRepoRoot, "m5-drovers");
            Assert.That(drovers.SoilAreas.Count, Is.EqualTo(1));
            Assert.That(drovers.SoilAreas[0].Soil, Is.EqualTo("road"));
        }

        [Test]
        public void ExportedArtAssets_CrewCattleAndProps_ImportCleanly()
        {
            foreach (string unitAsset in new[] { "U_crew_a", "U_cattle_a", "U_drovers_a" })
            {
                string lod0 = $"Assets/_Game/Art/Generated/{unitAsset}/{unitAsset}_LOD0.fbx";
                Mesh mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(lod0);
                Assert.That(mesh, Is.Not.Null, "Missing unit mesh: " + lod0);
                Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount), unitAsset + " UVs");
                Assert.That(
                    mesh.colors32.Length,
                    Is.EqualTo(mesh.vertexCount),
                    unitAsset + " TeamMask"
                );
                Assert.That(mesh.bounds.max.z, Is.LessThan(0.15f), unitAsset + " feet near Z=0");
                Assert.That(mesh.bounds.min.z, Is.LessThan(-1.4f), unitAsset + " upright height");

                bool hasMasked = false;
                bool hasUnmasked = false;
                foreach (Color32 c in mesh.colors32)
                {
                    if (c.r > 200)
                        hasMasked = true;
                    else if (c.r < 50)
                        hasUnmasked = true;
                }
                Assert.That(hasMasked, Is.True, unitAsset + " TeamMask==1");
                Assert.That(hasUnmasked, Is.True, unitAsset + " TeamMask==0");
            }

            foreach (string cleanId in new[] { "crew", "cattle", "drovers" })
            {
                UnitVatData vat = GodGame.Editor.VatBaker.BakeOrLoadUnit(cleanId, forceBake: true);
                Assert.That(vat.ClipCount, Is.EqualTo(6), cleanId + " clips");
                Assert.That(vat.FrameCount, Is.EqualTo(180), cleanId + " total frames");
                float maxY = float.MinValue;
                for (int v = 0; v < vat.VertexCount; v++)
                {
                    maxY = Mathf.Max(maxY, vat.Positions[v].y);
                }
                Assert.That(maxY, Is.GreaterThan(1.4f), cleanId + " VAT maxY");
            }

            foreach (
                string propAsset in new[]
                {
                    "P_tree_grove",
                    "P_trunk_felled",
                    "P_wreck_siege",
                    "P_fire_small",
                    "P_boulder_a",
                    "P_hedgerow_a",
                    "P_wall_drystone",
                    "P_bridge_wood",
                    "P_bridge_foot",
                    "P_pen_herd",
                }
            )
            {
                for (int lod = 0; lod < 3; lod++)
                {
                    string fbxPath =
                        $"Assets/_Game/Art/Generated/{propAsset}/{propAsset}_LOD{lod}.fbx";
                    Mesh mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(fbxPath);
                    Assert.That(mesh, Is.Not.Null, "Missing prop mesh: " + fbxPath);
                    Assert.That(mesh.vertexCount, Is.GreaterThan(0), fbxPath);
                    Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount), fbxPath + " UVs");
                }
            }
        }

        [Test]
        public void Item7Snapshots_AndProjection_CoverTreesFallsFelledFiresStrikesAndHerds()
        {
            var rules = RepoLoader.LoadRules(RepoLoader.EditorRepoRoot);
            var map = FlatMap(widthMm: 400_000, depthMm: 240_000);
            string planJson =
                "{\"version\":0,\"map\":\""
                + map.Id
                + "\",\"army\":["
                + "{\"id\":\"att\",\"side\":\"attacker\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":15000,\"yMm\":120000,\"facingCentiDegrees\":0},"
                + "{\"id\":\"def\",\"side\":\"defender\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":385000,\"yMm\":120000,\"facingCentiDegrees\":18000}"
                + "],\"terrain\":["
                + "{\"id\":\"g0\",\"piece\":\"grove\",\"xMm\":200000,\"yMm\":120000,\"radiusMm\":18000},"
                + "{\"id\":\"h0\",\"piece\":\"herd\",\"xMm\":180000,\"yMm\":180000,\"radiusMm\":18000,\"animals\":30}"
                + "],\"casts\":[]}";
            var plan = BattlePlan.Load(planJson, map, rules);
            var battle = new Battle(rules, map, plan, seed: 42);
            Assert.That(battle.PlayedMap.Trees.Count, Is.GreaterThan(0));
            Assert.That(battle.PlayedMap.Herds.Count, Is.EqualTo(1));

            using (var host = new BattleHost(battle))
            {
                var hostStrikes = new StrikeSnapshot();
                var hostHerds = new HerdSnapshot();
                host.ReadStrikes(hostStrikes);
                host.ReadHerds(hostHerds);
                Assert.That(hostHerds.Count, Is.EqualTo(1));
                Assert.That(hostHerds.Mode[0], Is.EqualTo(HerdMode.Grazing));
            }

            using var worker = new PreviewWorker(
                new Battle(rules, map, plan, seed: 42),
                new SerialRunner(),
                new KeyframeStore(long.MaxValue),
                endTick: 40
            );
            worker.RunUntilIdle();
            var strikesSnap = new StrikeSnapshot();
            var herdsSnap = new HerdSnapshot();
            worker.ReadStrikes(strikesSnap);
            worker.ReadHerds(herdsSnap);
            Assert.That(herdsSnap.Count, Is.EqualTo(1));
            Assert.That(herdsSnap.Mode[0], Is.EqualTo(HerdMode.Grazing));

            var reader = new TrackReader(new Battle(rules, map, plan, seed: 42));
            Assert.That(reader.Read(worker.Track, 0f, out _, out var unitSnap, out _), Is.True);
            var herdMarkers = new List<HerdMarkerInstance>();
            SalvoAndBlockerProjection.CollectHerdMarkers(
                herdsSnap,
                unitSnap,
                battle.PlayedMap,
                reader.Terrain,
                Array.Empty<CauseEvent>(),
                atTick: 0,
                filterByTime: true,
                herdMarkers
            );
            Assert.That(herdMarkers.Count, Is.EqualTo(1));
            Assert.That(herdMarkers[0].WorldX, Is.InRange(150f, 210f));
            Assert.That(herdMarkers[0].WorldZ, Is.InRange(150f, 210f));
            Assert.That(herdMarkers[0].RadiusM, Is.GreaterThan(3f));

            // Exercise strike -> falling tree -> landed felled trunk + fire lifecycle over scrubbed ticks.
            int treeCell0 = battle.PlayedMap.Trees[0].Cell;
            int fireCell = battle.PlayedMap.Columns * 40 + 60;
            var syntheticStrikes = new StrikeSnapshot
            {
                Tick = 300,
                StrikeCount = 1,
                FallCount = 1,
                FireCount = 1,
            };
            syntheticStrikes.StrikeXMm[0] = 200_000;
            syntheticStrikes.StrikeYMm[0] = 120_000;
            syntheticStrikes.StrikeTick[0] = 100;
            syntheticStrikes.StrikeEvent[0] = 0;

            syntheticStrikes.FallTree[0] = 0;
            syntheticStrikes.FallHeadingUnits[0] = 0; // +X heading
            syntheticStrikes.FallTick[0] = 100;
            syntheticStrikes.FallCause[0] = 0;
            syntheticStrikes.FallLanded[0] = true;

            syntheticStrikes.FireBlocker[0] = 1;
            syntheticStrikes.FireEnds[0] = 240;
            syntheticStrikes.FireEvent[0] = 2;
            syntheticStrikes.FireOut[0] = true;

            var blockers = new BlockerSnapshot(4) { Tick = 300, Count = 2 };
            blockers.Kind[0] = BlockerKind.FelledTree;
            blockers.EventIndex[0] = 1;
            blockers.Cells[0] = new[] { treeCell0, treeCell0 + 1 };
            blockers.Profile[0] = new byte[] { 0, 1 };

            blockers.Kind[1] = BlockerKind.Fire;
            blockers.EventIndex[1] = 2;
            blockers.Cells[1] = new[] { fireCell };
            blockers.Profile[1] = new byte[] { 0 };

            var causes = new[]
            {
                new CauseEvent(
                    CauseKind.LightningStruck,
                    tick: 100,
                    subject: 0,
                    detail: treeCell0,
                    -1,
                    -1
                ),
                new CauseEvent(
                    CauseKind.TreeFelled,
                    tick: 140,
                    subject: 0,
                    detail: 0,
                    cause: 0,
                    cause2: -1
                ),
                new CauseEvent(
                    CauseKind.FireStarted,
                    tick: 100,
                    subject: 0,
                    detail: 1,
                    cause: 0,
                    cause2: -1
                ),
                new CauseEvent(
                    CauseKind.HerdAgitated,
                    tick: 110,
                    subject: 0,
                    detail: 0,
                    cause: 0,
                    cause2: -1
                ),
                new CauseEvent(
                    CauseKind.HerdStampeded,
                    tick: 130,
                    subject: 0,
                    detail: GodGame.Sim.Math.Angle.QuarterTurnUnits,
                    cause: 3,
                    cause2: -1
                ),
            };

            var trees = new List<TreePropInstance>();
            var felled = new List<FelledTreeInstance>();
            var fires = new List<FireCellInstance>();
            var strikes = new List<LightningStrikeInstance>();

            // Mid-fall tick (120): tree 0 is falling (progress 0.5), fire active, strike flash visible.
            SalvoAndBlockerProjection.CollectTrees(
                battle.PlayedMap,
                reader.Terrain,
                syntheticStrikes,
                causes,
                atTick: 120,
                filterByTime: true,
                fallTicks: 40,
                trees
            );
            Assert.That(trees.Count, Is.EqualTo(battle.PlayedMap.Trees.Count));
            Assert.That(trees[0].FallProgress, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(trees[0].YawRad, Is.EqualTo(-Mathf.PI * 0.5f).Within(0.01f));

            SalvoAndBlockerProjection.CollectFelledTrees(
                blockers,
                battle.PlayedMap,
                reader.Terrain,
                syntheticStrikes,
                causes,
                atTick: 120,
                filterByTime: true,
                includeCleared: false,
                felled
            );
            Assert.That(felled.Count, Is.EqualTo(0), "Not landed yet at tick 120");

            SalvoAndBlockerProjection.CollectFires(
                blockers,
                battle.PlayedMap,
                reader.Terrain,
                syntheticStrikes,
                causes,
                atTick: 120,
                filterByTime: true,
                includeExtinguished: false,
                fires
            );
            Assert.That(fires.Count, Is.EqualTo(1));

            SalvoAndBlockerProjection.CollectLightningStrikes(
                syntheticStrikes,
                causes,
                battle.PlayedMap,
                reader.Terrain,
                atTick: 110,
                windowSeconds: 1.5f,
                strikes
            );
            Assert.That(strikes.Count, Is.EqualTo(1));
            Assert.That(strikes[0].AgeSeconds, Is.EqualTo(0.5f).Within(0.01f));

            // After landing (tick 140): tree 0 leaves upright trees and moves to felled trunks;
            // after fire end (tick 260): fire disappears unless includeExtinguished=true.
            SalvoAndBlockerProjection.CollectTrees(
                battle.PlayedMap,
                reader.Terrain,
                syntheticStrikes,
                causes,
                atTick: 140,
                filterByTime: true,
                fallTicks: 40,
                trees
            );
            Assert.That(trees.Count, Is.EqualTo(battle.PlayedMap.Trees.Count - 1));

            SalvoAndBlockerProjection.CollectFelledTrees(
                blockers,
                battle.PlayedMap,
                reader.Terrain,
                syntheticStrikes,
                causes,
                atTick: 140,
                filterByTime: true,
                includeCleared: false,
                felled
            );
            Assert.That(felled.Count, Is.EqualTo(1));
            Assert.That(felled[0].YawRad, Is.EqualTo(-Mathf.PI * 0.5f).Within(0.01f));

            SalvoAndBlockerProjection.CollectFires(
                blockers,
                battle.PlayedMap,
                reader.Terrain,
                syntheticStrikes,
                causes,
                atTick: 260,
                filterByTime: true,
                includeExtinguished: false,
                fires
            );
            Assert.That(fires.Count, Is.EqualTo(0));

            Assert.That(
                SalvoAndBlockerProjection.ResolveHerdAtTick(
                    herdsSnap,
                    causes,
                    0,
                    atTick: 120,
                    filterByTime: true,
                    out _,
                    out _
                ),
                Is.EqualTo(HerdMode.Agitated)
            );
            Assert.That(
                SalvoAndBlockerProjection.ResolveHerdAtTick(
                    herdsSnap,
                    causes,
                    0,
                    atTick: 135,
                    filterByTime: true,
                    out int stampedeHeading,
                    out _
                ),
                Is.EqualTo(HerdMode.Stampede)
            );
            Assert.That(stampedeHeading, Is.EqualTo(GodGame.Sim.Math.Angle.QuarterTurnUnits));
        }

        [Test]
        public void WorldMarkerRenderer_DrawsAllAuthoredPropsAndProceduralMarkers()
        {
            var map = FlatMap();
            Shader markerShader = Shader.Find("GodGame/DebugMarkers");
            Assert.That(markerShader, Is.Not.Null);

            var meshes = new WorldMarkerMeshes(
                UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/_Game/Art/Generated/P_wreck_siege/P_wreck_siege_LOD0.fbx"
                ),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/_Game/Art/Generated/P_tree_grove/P_tree_grove_LOD0.fbx"
                ),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/_Game/Art/Generated/P_trunk_felled/P_trunk_felled_LOD0.fbx"
                ),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/_Game/Art/Generated/P_fire_small/P_fire_small_LOD0.fbx"
                )
            );

            using var markers = new WorldMarkerRenderer(map, markerShader, meshes);
            Assert.That(markers.UsesAuthoredWreckMesh, Is.True);
            Assert.That(markers.UsesAuthoredTreeMesh, Is.True);
            Assert.That(markers.UsesAuthoredFelledTrunkMesh, Is.True);
            Assert.That(markers.UsesAuthoredFireMesh, Is.True);

            markers.Draw(
                new[] { new WreckCellInstance(15f, 0f, 15f, 0.5f, false, 0, 0, 10) },
                new[] { new StoneLandingInstance(1, 20, 0, 12, 30f, 0f, 20f, 2, 0.4f) },
                new[] { new TreePropInstance(0, 60f, 0f, 40f, 1f, 0.2f, 0.35f, 14f) },
                new[]
                {
                    new FelledTreeInstance(1, 2, 1, 75f, 0f, 42f, 1f, -Mathf.PI * 0.5f, false),
                },
                new[] { new FireCellInstance(2, 55, 3, 82f, 0f, 44f, 1f, 0.5f, false) },
                new[] { new LightningStrikeInstance(4, 25, 60f, 0f, 40f, 0.25f) },
                new[]
                {
                    new HerdMarkerInstance(
                        0,
                        2,
                        -1,
                        HerdMode.Stampede,
                        1000,
                        0f,
                        -Mathf.PI * 0.5f,
                        90f,
                        0f,
                        60f,
                        11f
                    ),
                },
                timeSeconds: 1.25f
            );

            Assert.That(markers.DrawnWrecksLastFrame, Is.EqualTo(1));
            Assert.That(markers.DrawnSalvosLastFrame, Is.EqualTo(1));
            Assert.That(markers.DrawnTreesLastFrame, Is.EqualTo(1));
            Assert.That(markers.DrawnFelledTreesLastFrame, Is.EqualTo(1));
            Assert.That(markers.DrawnFiresLastFrame, Is.EqualTo(1));
            Assert.That(markers.DrawnStrikesLastFrame, Is.EqualTo(1));
            Assert.That(markers.DrawnHerdsLastFrame, Is.EqualTo(1));
        }

        [Test]
        public void UnitCapsuleRenderer_TintsHerdsByMoodAndUsesAuthoredCrewMesh()
        {
            var rules = RepoLoader.LoadRules(RepoLoader.EditorRepoRoot);
            var map = FlatMap();
            Shader unitShader = Shader.Find("GodGame/DebugUnits");
            Assert.That(unitShader, Is.Not.Null);

            var typeMeshes = new Mesh[rules.UnitTypes.Count];
            int crewType = -1;
            for (int t = 0; t < rules.UnitTypes.Count; t++)
            {
                string id = rules.UnitTypes[t].Id;
                if (id == "crew")
                {
                    crewType = t;
                    typeMeshes[t] = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                        "Assets/_Game/Art/Generated/U_crew_a/U_crew_a_LOD0.fbx"
                    );
                }
                else if (id == "cattle")
                {
                    typeMeshes[t] = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                        "Assets/_Game/Art/Generated/U_cattle_a/U_cattle_a_LOD0.fbx"
                    );
                }
            }
            Assert.That(crewType, Is.GreaterThanOrEqualTo(0));
            Assert.That(GodGame.Editor.UnitArt.AwaitsModel("crew"), Is.False);
            Assert.That(GodGame.Editor.UnitArt.AwaitsModel("cattle"), Is.False);
            Assert.That(GodGame.Editor.UnitArt.AwaitsModel("drovers"), Is.False);

            using var renderer = new UnitCapsuleRenderer(
                unitShader,
                64,
                rules,
                new[] { Side.Attacker, Side.Neutral },
                map,
                tint: null,
                typeMeshes: typeMeshes
            );
            Assert.That(renderer.UsesCrewCapsuleStyle(crewType), Is.False);

            var reg = new RegimentSnapshot(2);
            reg.Count = 2;
            reg.Alive[0] = true;
            reg.Alive[1] = true;
            reg.Morale[0] = 1000;
            reg.Morale[1] = 1000;

            var herds = new HerdSnapshot { Count = 1 };
            herds.Regiment[0] = 1;
            herds.DrivenBy[0] = -1;
            herds.Mode[0] = HerdMode.Grazing;
            renderer.UpdateMoralePalette(reg, cameraDistance: 90f, boostStrategic: false, herds);
            Color grazingCol = renderer.GetDisplayColor(1);

            herds.Mode[0] = HerdMode.Stampede;
            renderer.UpdateMoralePalette(reg, cameraDistance: 90f, boostStrategic: false, herds);
            Color stampedeCol = renderer.GetDisplayColor(1);

            Assert.That(
                Vector4.Distance((Vector4)(Color)grazingCol, (Vector4)(Color)stampedeCol),
                Is.GreaterThan(0.25f)
            );
        }

        [Test]
        public void PlanEditor_RoundTripsGroveHerdTreeRowAndLightningCast()
        {
            var rules = RepoLoader.LoadRules(RepoLoader.EditorRepoRoot);
            var map = FlatMap(widthMm: 450_000, depthMm: 240_000);
            string planJson =
                "{\"version\":0,\"map\":\""
                + map.Id
                + "\",\"army\":["
                + "{\"id\":\"att\",\"side\":\"attacker\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":15000,\"yMm\":120000,\"facingCentiDegrees\":0},"
                + "{\"id\":\"def\",\"side\":\"defender\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":435000,\"yMm\":120000,\"facingCentiDegrees\":18000}"
                + "],\"terrain\":["
                + "{\"id\":\"g0\",\"piece\":\"grove\",\"xMm\":200000,\"yMm\":120000,\"radiusMm\":18000},"
                + "{\"id\":\"tr0\",\"piece\":\"tree-row\",\"points\":[[180000,60000],[210000,90000]]},"
                + "{\"id\":\"h0\",\"piece\":\"herd\",\"xMm\":220000,\"yMm\":170000,\"radiusMm\":18000,\"animals\":24}"
                + "],\"casts\":["
                + "{\"id\":\"l0\",\"power\":\"lightning\",\"tick\":120,\"xMm\":200000,\"yMm\":120000,\"radiusMm\":30000}"
                + "]}";
            var loaded = BattlePlan.Load(planJson, map, rules);
            var editor = new PlanEditor(loaded, map, rules);
            Assert.That(editor.Commit(), Is.True, editor.Error ?? "Expected valid round-trip");
            Assert.That(editor.Plan.Terrain.Count, Is.EqualTo(3));
            Assert.That(editor.Plan.Casts.Count, Is.EqualTo(1));
            Assert.That(editor.Plan.Casts[0].Power.Id, Is.EqualTo("lightning"));
            Assert.That(editor.Plan.Casts[0].RadiusMm, Is.EqualTo(30_000));
        }

        [Test]
        public void DesignGateB_EnablesLightningInHand()
        {
            string repo = RepoLoader.EditorRepoRoot;
            var rules = RepoLoader.LoadRules(repo);
            var header = RepoLoader.LoadLevel(
                repo,
                "chapter-0/b1-river-crossing.json",
                null,
                rules
            );
            var map = RepoLoader.LoadMap(repo, header.MapId);
            var level = RepoLoader.LoadLevel(repo, "chapter-0/b1-river-crossing.json", map, rules);

            bool hasLightningInHand = false;
            for (int i = 0; i < level.Hand.Count; i++)
            {
                if (level.Hand[i].PowerId == "lightning" && level.Hand[i].Charges == 1)
                {
                    hasLightningInHand = true;
                }
            }
            bool hasLightningPending = false;
            bool hasWindShiftPending = false;
            for (int i = 0; i < level.PendingPowers.Count; i++)
            {
                if (level.PendingPowers[i].PowerId == "lightning")
                {
                    hasLightningPending = true;
                }
                if (level.PendingPowers[i].PowerId == "wind-shift")
                {
                    hasWindShiftPending = true;
                }
            }

            Assert.That(
                hasLightningInHand,
                Is.True,
                "Lightning should be in active hand for Gate B"
            );
            Assert.That(hasLightningPending, Is.False, "Lightning should no longer be pending");
            Assert.That(
                hasWindShiftPending,
                Is.True,
                "wind-shift stays pending for a later milestone"
            );
        }

        [Test]
        public void TerrainPropAndRainProjection_CollectsBouldersBarriersHerdPensAndRainCasts()
        {
            var rules = RepoLoader.LoadRules(RepoLoader.EditorRepoRoot);
            var map = FlatMap(widthMm: 450_000, depthMm: 240_000);
            string planJson =
                "{\"version\":0,\"map\":\""
                + map.Id
                + "\",\"army\":["
                + "{\"id\":\"att\",\"side\":\"attacker\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":15000,\"yMm\":120000,\"facingCentiDegrees\":0},"
                + "{\"id\":\"def\",\"side\":\"defender\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":435000,\"yMm\":120000,\"facingCentiDegrees\":18000}"
                + "],\"terrain\":["
                + "{\"id\":\"b0\",\"piece\":\"boulder\",\"xMm\":210000,\"yMm\":120000},"
                + "{\"id\":\"h0\",\"piece\":\"hedgerow\",\"points\":[[190000,70000],[205000,70000]]},"
                + "{\"id\":\"w0\",\"piece\":\"drystone-wall\",\"points\":[[190000,150000],[205000,150000]]},"
                + "{\"id\":\"herd0\",\"piece\":\"herd\",\"xMm\":240000,\"yMm\":175000,\"radiusMm\":18000,\"animals\":30}"
                + "],\"casts\":["
                + "{\"id\":\"r0\",\"power\":\"rain\",\"tick\":100,\"xMm\":220000,\"yMm\":120000,\"radiusMm\":50000},"
                + "{\"id\":\"r1\",\"power\":\"rain\",\"tick\":120,\"xMm\":260000,\"yMm\":120000,\"radiusMm\":50000}"
                + "]}";
            var plan = BattlePlan.Load(planJson, map, rules);
            var battle = new Battle(rules, map, plan, seed: 7);
            var terrain = new GodGame.Sim.Terrain(battle.PlayedMap);

            var boulders = new List<BoulderPropInstance>();
            SalvoAndBlockerProjection.CollectBoulders(battle.PlayedMap, terrain, boulders);
            Assert.That(boulders.Count, Is.EqualTo(1));
            Assert.That(boulders[0].WorldX, Is.InRange(208f, 214f));
            Assert.That(
                boulders[0].WorldY,
                Is.LessThan(0.5f),
                "Boulder grounded on pre-stamp terrain"
            );

            var barriers = new List<LineBarrierInstance>();
            SalvoAndBlockerProjection.CollectLineBarriers(
                plan,
                battle.PlayedMap,
                terrain,
                barriers
            );
            int hedges = 0;
            int walls = 0;
            foreach (var seg in barriers)
            {
                if (seg.IsWall)
                    walls++;
                else
                    hedges++;
            }
            Assert.That(hedges, Is.GreaterThanOrEqualTo(10));
            Assert.That(walls, Is.GreaterThanOrEqualTo(10));

            var pens = new List<HerdPenPropInstance>();
            SalvoAndBlockerProjection.CollectHerdPens(battle.PlayedMap, terrain, pens);
            Assert.That(pens.Count, Is.GreaterThanOrEqualTo(6));

            // Cast 1 fizzled; Cast 0 winds up at tick 100..299 and rains at tick 300..1499.
            var causes = new[]
            {
                new CauseEvent(CauseKind.CastFizzled, tick: 120, subject: 1, detail: 1, -1, -1),
            };
            var rains = new List<RainCastInstance>();
            SalvoAndBlockerProjection.CollectActiveRainCasts(
                plan,
                causes,
                battle.PlayedMap,
                terrain,
                atTick: 200,
                rains
            );
            Assert.That(rains.Count, Is.EqualTo(1));
            Assert.That(rains[0].CastIndex, Is.EqualTo(0));
            Assert.That(rains[0].ActiveIntensity, Is.EqualTo(0f));
            Assert.That(rains[0].WindUpProgress, Is.GreaterThan(0.2f));

            SalvoAndBlockerProjection.CollectActiveRainCasts(
                plan,
                causes,
                battle.PlayedMap,
                terrain,
                atTick: 500,
                rains
            );
            Assert.That(rains.Count, Is.EqualTo(1));
            Assert.That(rains[0].ActiveIntensity, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void WorldMarkerAndDeckRenderers_WireAllNewAuthoredPropsAndRainCurtain()
        {
            var map = FlatMap();
            Shader markerShader = Shader.Find("GodGame/DebugMarkers");
            Shader rainShader = Shader.Find("GodGame/RainCurtain");
            Shader deckShader = Shader.Find("GodGame/DebugDeck");
            Assert.That(markerShader, Is.Not.Null);
            Assert.That(rainShader, Is.Not.Null);
            Assert.That(deckShader, Is.Not.Null);

            var meshes = new WorldMarkerMeshes(
                UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/_Game/Art/Generated/P_wreck_siege/P_wreck_siege_LOD0.fbx"
                ),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/_Game/Art/Generated/P_tree_grove/P_tree_grove_LOD0.fbx"
                ),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/_Game/Art/Generated/P_trunk_felled/P_trunk_felled_LOD0.fbx"
                ),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/_Game/Art/Generated/P_fire_small/P_fire_small_LOD0.fbx"
                ),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/_Game/Art/Generated/P_boulder_a/P_boulder_a_LOD0.fbx"
                ),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/_Game/Art/Generated/P_hedgerow_a/P_hedgerow_a_LOD0.fbx"
                ),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/_Game/Art/Generated/P_wall_drystone/P_wall_drystone_LOD0.fbx"
                ),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/_Game/Art/Generated/P_pen_herd/P_pen_herd_LOD0.fbx"
                )
            );
            using var markers = new WorldMarkerRenderer(map, markerShader, meshes, rainShader);
            Assert.That(markers.UsesAuthoredBoulderMesh, Is.True);
            Assert.That(markers.UsesAuthoredHedgerowMesh, Is.True);
            Assert.That(markers.UsesAuthoredWallDrystoneMesh, Is.True);
            Assert.That(markers.UsesAuthoredHerdPenMesh, Is.True);

            markers.DrawTerrainProps(
                new[] { new BoulderPropInstance(0, 120f, 0f, 90f, 1f, 0.4f) },
                new[]
                {
                    new LineBarrierInstance(0, false, 100f, 0f, 70f, 1f, 1f, 0f),
                    new LineBarrierInstance(1, true, 100f, 0f, 110f, 1f, 1f, 0f),
                },
                new[] { new HerdPenPropInstance(0, 150f, 0f, 130f, 1.3f, 0.2f) },
                timeSeconds: 2.0f
            );
            Assert.That(markers.DrawnBouldersLastFrame, Is.EqualTo(1));
            Assert.That(markers.DrawnHedgerowsLastFrame, Is.EqualTo(1));
            Assert.That(markers.DrawnWallsLastFrame, Is.EqualTo(1));
            Assert.That(markers.DrawnHerdPensLastFrame, Is.EqualTo(1));

            markers.DrawRain(
                new[] { new RainCastInstance(0, 140f, 0f, 90f, 50f, 1f, 1f, 2f, -1f) },
                timeSeconds: 2.0f
            );
            Assert.That(markers.DrawnRainLastFrame, Is.EqualTo(1));

            Mesh woodBridge = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                "Assets/_Game/Art/Generated/P_bridge_wood/P_bridge_wood_LOD0.fbx"
            );
            Mesh footBridge = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                "Assets/_Game/Art/Generated/P_bridge_foot/P_bridge_foot_LOD0.fbx"
            );
            using var decks = new DeckRenderer(map, deckShader, woodBridge, footBridge);
            Assert.That(decks.UsesAuthoredWoodBridgeMesh, Is.True);
            Assert.That(decks.UsesAuthoredFootbridgeMesh, Is.True);

            var snap = new StructureSnapshot(4) { Tick = 10, Count = 2 };
            snap.KindIds[0] = "wooden-bridge";
            snap.DeckHeightM[0] = 2f;
            snap.StrengthPermille[0] = 700;
            snap.LoadPermille[0] = 860;
            snap.Stage[0] = StrainStage.Crack;
            snap.Cells[0] = new[] { 20 * map.Columns + 30, 20 * map.Columns + 31 };

            snap.KindIds[1] = "footbridge";
            snap.DeckHeightM[1] = 1.5f;
            snap.StrengthPermille[1] = 1000;
            snap.LoadPermille[1] = 400;
            snap.Stage[1] = StrainStage.Quiet;
            snap.Cells[1] = new[] { 25 * map.Columns + 30, 25 * map.Columns + 31 };

            decks.Draw(snap, map, showStrainOverlay: false);
            Assert.That(decks.DrawnBridgeSegmentsLastFrame, Is.EqualTo(2));
            Assert.That(decks.DrawnCellsLastFrame, Is.EqualTo(0));
        }

        [Test]
        public void GroundMesh_BuildTerrainMask_EncodesRoadsWoodlandZonesAndBoulders()
        {
            var stampedeMap = RepoLoader.LoadMap(RepoLoader.EditorRepoRoot, "m5-stampede");
            Texture2D mask = GroundMesh.BuildTerrainMask(stampedeMap);
            try
            {
                Assert.That(mask, Is.Not.Null);
                Assert.That(mask.width, Is.EqualTo(stampedeMap.Columns));
                Assert.That(mask.height, Is.EqualTo(stampedeMap.Rows));
                Color32[] pixels = mask.GetPixels32();
                bool hasRoad = false;
                bool hasAttackerZone = false;
                bool hasDefenderZone = false;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].r > 200)
                        hasRoad = true;
                    if (pixels[i].b == 128)
                        hasAttackerZone = true;
                    if (pixels[i].b == 255)
                        hasDefenderZone = true;
                }
                Assert.That(hasRoad, Is.True, "Expected road soil encoded in R channel");
                Assert.That(hasAttackerZone, Is.True, "Expected attacker zone border in B channel");
                Assert.That(hasDefenderZone, Is.True, "Expected defender zone border in B channel");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mask);
            }
        }
    }
}
