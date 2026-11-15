using System;
using System.Collections.Generic;
using GodGame.Bridge;
using GodGame.Rendering;
using GodGame.Sim;
using GodGame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace GodGame.Tests.EditMode
{
    public class PlanningOverlaysAndResultsTests
    {
        private static MapData FlatMap(int widthMm = 300_000, int depthMm = 240_000)
        {
            string json =
                "{\"version\":0,\"id\":\"test-flat\",\"widthMm\":"
                + widthMm
                + ",\"depthMm\":"
                + depthMm
                + ",\"cellSizeMm\":1500,\"deployment\":["
                + "{\"side\":\"attacker\",\"minXMm\":0,\"minYMm\":0,\"maxXMm\":30000,\"maxYMm\":"
                + depthMm
                + "},"
                + "{\"side\":\"defender\",\"minXMm\":"
                + (widthMm - 30_000)
                + ",\"minYMm\":0,\"maxXMm\":"
                + widthMm
                + ",\"maxYMm\":"
                + depthMm
                + "}]}";
            return MapData.Load(json);
        }

        [Test]
        public void StrainOverlay_PaintsCreakCrackSagBrokenAndRotDistinctly()
        {
            var structures = new StructureSnapshot(8) { Count = 5, Tick = 40 };
            structures.Stage[0] = StrainStage.Quiet;
            structures.LoadPermille[0] = 0;
            structures.StrengthPermille[0] = 500;
            structures.Broken[0] = false;
            structures.Cells[0] = new[] { 1 };

            structures.Stage[1] = StrainStage.Creak;
            structures.LoadPermille[1] = 720;
            structures.StrengthPermille[1] = 1000;
            structures.Broken[1] = false;
            structures.Cells[1] = new[] { 2 };

            structures.Stage[2] = StrainStage.Crack;
            structures.LoadPermille[2] = 880;
            structures.StrengthPermille[2] = 1000;
            structures.Broken[2] = false;
            structures.Cells[2] = new[] { 3 };

            structures.Stage[3] = StrainStage.Sag;
            structures.LoadPermille[3] = 970;
            structures.StrengthPermille[3] = 1000;
            structures.Broken[3] = false;
            structures.Cells[3] = new[] { 4 };

            structures.Stage[4] = StrainStage.Sag;
            structures.LoadPermille[4] = 0;
            structures.StrengthPermille[4] = 1000;
            structures.Broken[4] = true;
            structures.Cells[4] = new[] { 5 };

            var pixels = new Color32[16];
            OverlayPainter.PaintStrain(structures, pixels);

            Assert.That(pixels[0].a, Is.EqualTo(0), "Untouched ground stays clear");
            Assert.That(pixels[1].a, Is.GreaterThan(0), "Quiet rotted span is tinted");
            Assert.That(pixels[2].r, Is.GreaterThan(200));
            Assert.That(pixels[2].g, Is.GreaterThan(170), "Creak is warm yellow");
            Assert.That(pixels[3].r, Is.GreaterThan(200));
            Assert.That(pixels[3].g, Is.InRange(100, 160), "Crack is orange");
            Assert.That(pixels[4].r, Is.GreaterThan(200));
            Assert.That(pixels[4].g, Is.LessThan(60), "Sag is red");
            Assert.That(pixels[5].r, Is.LessThan(80), "Broken span paints collapsed dark tint");
            Assert.That(pixels[5].a, Is.GreaterThan(180));

            var testMap = FlatMap(30_000, 30_000);
            using (var deckRenderer = new DeckRenderer(testMap, null))
            {
                deckRenderer.Draw(structures, testMap, true);
                Assert.That(deckRenderer.DrawnCellsLastFrame, Is.GreaterThan(0));
            }
        }

        [Test]
        public void LandingSpreadOverlay_ShowsAmberWhereStunned_AndRedOverridesWhereLethal()
        {
            var map = FlatMap(30_000, 30_000); // 20 x 20 cells of 1500 mm
            var log = new CauseLog(map.Columns * map.Rows, 4, 8);
            int sourceEv = log.Add(CauseKind.SegmentFailed, 10, 0, 1, CauseLog.None);
            int launchEv = log.Add(CauseKind.SoldierLaunched, 10, 0, 42, sourceEv);
            int overlapCell = 5 * map.Columns + 5;
            int stunCell = 9 * map.Columns + 9;
            log.Add(
                CauseKind.SoldierLanded,
                25,
                0,
                Landing.Detail(overlapCell, LandingOutcome.Ground),
                launchEv
            );
            log.Add(
                CauseKind.SoldierLanded,
                26,
                0,
                Landing.Detail(overlapCell, LandingOutcome.Killed),
                launchEv
            );
            log.Add(
                CauseKind.SoldierLanded,
                27,
                0,
                Landing.Detail(stunCell, LandingOutcome.Ground),
                launchEv
            );

            var spreads = LandingSpread.FromLog(log, map.Columns, map.CellSizeMm, 3000);
            Assert.That(spreads.Count, Is.EqualTo(1));

            var pixels = new Color32[map.Columns * map.Rows];
            OverlayPainter.PaintLandingSpread(spreads, map, 3000, pixels);

            Color32 overlapPixel = pixels[overlapCell];
            Color32 stunPixel = pixels[stunCell];
            Assert.That(overlapPixel.a, Is.GreaterThan(180));
            Assert.That(overlapPixel.r, Is.GreaterThan(200));
            Assert.That(
                overlapPixel.g,
                Is.LessThan(60),
                "Red lethal landing must override amber stun"
            );

            Assert.That(stunPixel.a, Is.GreaterThan(150));
            Assert.That(stunPixel.r, Is.GreaterThan(200));
            Assert.That(stunPixel.g, Is.GreaterThan(140), "Stun-only cell stays amber");
            Assert.That(pixels[0].a, Is.EqualTo(0), "Dry non-landing cell stays transparent");
        }

        [Test]
        public void MoraleTintsAtStrategicZoom_DifferentiateSteadyShakenRoutingWiped_WhilePreservingTeams()
        {
            string repo = RepoLoader.EditorRepoRoot;
            var rules = RepoLoader.LoadRules(repo);
            var map = FlatMap();
            var sides = new[]
            {
                Side.Attacker, // 0: steady
                Side.Attacker, // 1: shaken
                Side.Attacker, // 2: routing
                Side.Defender, // 3: steady
                Side.Defender, // 4: routing
                Side.Defender, // 5: wiped
            };
            Shader shader = Shader.Find("GodGame/DebugUnits");
            Assert.That(shader, Is.Not.Null, "GodGame/DebugUnits shader required");

            using var renderer = new UnitCapsuleRenderer(
                shader,
                64,
                rules,
                sides,
                map,
                null,
                null,
                null
            );
            var reg = new RegimentSnapshot(6) { Count = 6, Tick = 100 };
            for (int r = 0; r < 6; r++)
            {
                reg.Side[r] = sides[r];
                reg.Alive[r] = r != 5;
                reg.Wiped[r] = r == 5;
                reg.Routing[r] = r == 2 || r == 4;
                reg.Order[r] = (r == 2 || r == 4) ? RegimentOrder.Rout : RegimentOrder.March;
                reg.Morale[r] = (ushort)(r == 1 ? 250 : (r == 2 || r == 4 ? 0 : 1000));
            }

            renderer.UpdateMoralePalette(reg, cameraDistance: 20f);
            Color closeAttacker = renderer.GetDisplayColor(1);
            Color closeRoutingAtk = renderer.GetDisplayColor(2);

            renderer.UpdateMoralePalette(reg, cameraDistance: 500f);
            Color stratSteadyAtk = renderer.GetDisplayColor(0);
            Color stratShakenAtk = renderer.GetDisplayColor(1);
            Color stratRoutingAtk = renderer.GetDisplayColor(2);
            Color stratSteadyDef = renderer.GetDisplayColor(3);
            Color stratRoutingDef = renderer.GetDisplayColor(4);
            Color stratWipedDef = renderer.GetDisplayColor(5);

            Assert.That(
                stratShakenAtk,
                Is.Not.EqualTo(closeAttacker),
                "Shaken morale tints at strategic zoom"
            );
            Assert.That(
                stratRoutingAtk,
                Is.Not.EqualTo(closeRoutingAtk),
                "Routing tints at strategic zoom"
            );
            Assert.That(stratRoutingAtk, Is.Not.EqualTo(stratSteadyAtk));
            Assert.That(stratRoutingDef, Is.Not.EqualTo(stratSteadyDef));
            Assert.That(
                stratRoutingAtk.r,
                Is.GreaterThan(stratRoutingAtk.b),
                "Attacker stays warm orange hue"
            );
            Assert.That(
                stratRoutingDef.b,
                Is.GreaterThan(stratRoutingDef.r),
                "Defender stays cool blue hue"
            );
            Assert.That(
                Mathf.Max(stratWipedDef.r, Mathf.Max(stratWipedDef.g, stratWipedDef.b)),
                Is.LessThan(0.55f),
                "Wiped regiment is darkened"
            );
        }

        [Test]
        public void ResultsScreen_NamesCauses_WalksTraces_AndHandlesUnknownKindsGracefully()
        {
            var root = new VisualElement();
            var screen = new ResultsScreen(root);
            Assert.That(screen.IsVisible, Is.False);

            var map = FlatMap();
            string repo = RepoLoader.EditorRepoRoot;
            var rules = RepoLoader.LoadRules(repo);
            string planJson =
                "{\"version\":0,\"map\":\"test-flat\",\"army\":["
                + "{\"id\":\"atk0\",\"side\":\"attacker\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":15000,\"yMm\":120000,\"facingCentiDegrees\":0},"
                + "{\"id\":\"def0\",\"side\":\"defender\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":285000,\"yMm\":120000,\"facingCentiDegrees\":18000}"
                + "],\"terrain\":[],\"casts\":[{\"id\":\"storm1\",\"power\":\"rain\",\"tick\":20,\"xMm\":150000,\"yMm\":120000,\"radiusMm\":40000}]}";
            var plan = BattlePlan.Load(planJson, map, rules);

            var events = new[]
            {
                new CauseEvent(CauseKind.CastFired, 20, 0, 0, CauseLog.None, CauseLog.None),
                new CauseEvent(CauseKind.SoldierDrowned, 120, 0, 10, 0, CauseLog.None),
                new CauseEvent((CauseKind)99, 140, 0, 777, 0, CauseLog.None),
            };
            var outcome = new BattleOutcomeResult(
                kind: BattleOutcomeKind.DefenderVictory,
                reason: BattleOutcomeReason.AttackerArmyRouted,
                decidedTick: 160,
                evaluatedTick: 160,
                sunsetTick: 3600,
                attackerStartingSoldiers: 50,
                attackerAliveSoldiers: 10,
                attackerNonRoutingAlive: 10,
                attackerLostOrRoutedPermille: 800,
                attackerRoutedRegiments: 1,
                attackerWipedRegiments: 0,
                attackerTotalRegiments: 1,
                defenderStartingSoldiers: 50,
                defenderAliveSoldiers: 50,
                defenderRoutedRegiments: 0,
                defenderWipedRegiments: 0,
                defenderLiveRegiments: 1,
                defenderTotalRegiments: 1,
                objectiveHoldTicks: 0,
                objectivePeakHoldTicks: 0
            );

            screen.Show();
            Assert.That(screen.IsVisible, Is.True);
            Assert.DoesNotThrow(() => screen.SetData(outcome, events, plan));
            Assert.That(screen.HeadlineText, Does.Contain("DEFENDER VICTORY"));
            Assert.That(screen.StatsText, Does.Contain("Attackers: 10/50 alive"));
            Assert.That(screen.CurrentBreakdown.UnknownKindEvents, Is.EqualTo(1));

            int unknownIdx = -1;
            for (int i = 0; i < screen.CurrentBreakdown.AllCauses.Count; i++)
            {
                if (!screen.CurrentBreakdown.AllCauses[i].IsKnown)
                {
                    unknownIdx = i;
                    break;
                }
            }
            Assert.That(
                unknownIdx,
                Is.GreaterThanOrEqualTo(0),
                "Unknown cause #99 must appear cleanly"
            );
            Assert.DoesNotThrow(() => screen.SelectCauseGroup(unknownIdx));
            Assert.That(screen.TraceText, Does.Contain("#99"));
            Assert.That(screen.TraceText, Does.Contain("storm1"));

            screen.Toggle();
            Assert.That(screen.IsVisible, Is.False);
        }

        [Test]
        public void LaunchArcs_EvaluateExactParabolicMotionAndSpin_WithHeightSampleFallback()
        {
            string repo = RepoLoader.EditorRepoRoot;
            var rules = RepoLoader.LoadRules(repo);
            var map = FlatMap(300_000, 240_000);
            string planJson =
                "{\"version\":0,\"map\":\"test-flat\",\"army\":["
                + "{\"id\":\"atk0\",\"side\":\"attacker\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":15000,\"yMm\":120000,\"facingCentiDegrees\":0},"
                + "{\"id\":\"def0\",\"side\":\"defender\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":285000,\"yMm\":120000,\"facingCentiDegrees\":18000}"
                + "],\"terrain\":[],\"casts\":[]}";
            var plan = BattlePlan.Load(planJson, map, rules);
            var battle = new Battle(rules, map, plan, 1);

            var reader = new TrackReader(battle);
            var track = new PlaybackTrack(map.Columns, map.Rows, 16);
            track.RecordLaunch(
                new LaunchRecord(
                    launchTick: 10,
                    unit: 0,
                    sourceEvent: 0,
                    launchEvent: 1,
                    startXCm: 2000,
                    startYCm: 3000,
                    startHeightMm: 1000,
                    velXMmPerS: 10_000,
                    velYMmPerS: 0,
                    verticalMmPerS: 8_000,
                    spinCdPerS: 36_000,
                    gravityMmPerS2: 10_000
                )
            );
            track.RecordLaunchLanding(new LaunchLandingRecord(landTick: 30, launchEvent: 1));

            bool activeHalfTick = reader.TryEvaluateActiveLaunch(
                track,
                unit: 0,
                exactTick: 20f, // dt = 0.5 s -> x = 20 + 5 = 25 m, y = 1 + 4 - 0.5*10*0.25 = 3.75 m
                out float lx,
                out float ly,
                out float lz,
                out float spinRad
            );
            Assert.That(activeHalfTick, Is.True);
            Assert.That(lx, Is.EqualTo(25f).Within(0.05f));
            Assert.That(ly, Is.EqualTo(3.75f).Within(0.05f));
            Assert.That(lz, Is.EqualTo(30f).Within(0.05f));
            Assert.That(
                spinRad,
                Is.EqualTo(Mathf.PI).Within(0.05f),
                "360 deg/s for 0.5 s = pi rad"
            );

            bool afterLanding = reader.TryEvaluateActiveLaunch(
                track,
                unit: 0,
                exactTick: 31f,
                out _,
                out _,
                out _,
                out _
            );
            Assert.That(
                afterLanding,
                Is.False,
                "After landing tick, fallback uses UnitSample.HeightCm"
            );
        }

        [Test]
        public void DesignGateB_LevelMapAndPlan_LoadAndRunThroughPreviewAndOutcomeEvaluator()
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
            var plan = RepoLoader.LoadPlanForLevel(repo, level, map, rules);

            Assert.That(map.WindXMmPerS, Is.Not.EqualTo(0));
            Assert.That(map.StoneBridges.Count, Is.EqualTo(1));
            Assert.That(map.FrozenPonds.Count, Is.EqualTo(1));
            Assert.That(map.InitialFields.Count, Is.GreaterThan(0), "Settled water.bin present");
            Assert.That(level.Hand.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(level.PendingPowers.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(level.Outcome.Variant, Is.EqualTo(LevelOutcomeVariant.Objective));

            var battle = new Battle(rules, map, plan, (ulong)level.Seed);
            using var worker = new PreviewWorker(
                battle,
                new SerialRunner(),
                new KeyframeStore(long.MaxValue),
                endTick: 400
            );
            worker.SetOutcomeSpec(level.Outcome);
            worker.RunUntilIdle();

            var progress = worker.ReadProgress();
            Assert.That(progress.Complete, Is.True);
            var structures = new StructureSnapshot();
            var reader = new TrackReader(battle);
            reader.ReadStructures(worker.Track, 5f, structures);
            Assert.That(
                structures.Count,
                Is.GreaterThan(0),
                "Stone, wood, and ice decks appear in snapshot"
            );

            var outcome = worker.ReadOutcome();
            Assert.That(outcome, Is.Not.Null);
            Assert.That(
                outcome.AttackerStartingSoldiers,
                Is.GreaterThan(outcome.DefenderStartingSoldiers)
            );
            Assert.That(worker.ReadLandingSpreads(), Is.Not.Null);
        }
    }
}
