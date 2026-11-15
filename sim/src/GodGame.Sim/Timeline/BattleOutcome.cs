using System;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>Result of evaluating a battle against the win/loss rules in spec S2 section 7.5.</summary>
    public enum BattleOutcomeKind
    {
        Ongoing = 0,
        DefenderVictory = 1,
        AttackerVictory = 2,
    }

    /// <summary>Which specific clause of S2 section 7.5 settled the battle.</summary>
    public enum BattleOutcomeReason
    {
        InProgress = 0,
        AttackerArmyRouted = 1,
        HeldUntilSunset = 2,
        AllDefendersWiped = 3,
        ObjectiveCaptured = 4,
    }

    /// <summary>Summary of who won, why, and the final regiment &amp; casualty counts at the evaluated horizon.</summary>
    public sealed class BattleOutcomeResult
    {
        public BattleOutcomeResult(
            BattleOutcomeKind kind,
            BattleOutcomeReason reason,
            int decidedTick,
            int evaluatedTick,
            int sunsetTick,
            int attackerStartingSoldiers,
            int attackerAliveSoldiers,
            int attackerNonRoutingAlive,
            int attackerLostOrRoutedPermille,
            int attackerRoutedRegiments,
            int attackerWipedRegiments,
            int attackerTotalRegiments,
            int defenderStartingSoldiers,
            int defenderAliveSoldiers,
            int defenderRoutedRegiments,
            int defenderWipedRegiments,
            int defenderLiveRegiments,
            int defenderTotalRegiments,
            int objectiveHoldTicks,
            int objectivePeakHoldTicks
        )
        {
            Kind = kind;
            Reason = reason;
            DecidedTick = decidedTick;
            EvaluatedTick = evaluatedTick;
            SunsetTick = sunsetTick;
            AttackerStartingSoldiers = attackerStartingSoldiers;
            AttackerAliveSoldiers = attackerAliveSoldiers;
            AttackerNonRoutingAlive = attackerNonRoutingAlive;
            AttackerLostOrRoutedPermille = attackerLostOrRoutedPermille;
            AttackerRoutedRegiments = attackerRoutedRegiments;
            AttackerWipedRegiments = attackerWipedRegiments;
            AttackerTotalRegiments = attackerTotalRegiments;
            DefenderStartingSoldiers = defenderStartingSoldiers;
            DefenderAliveSoldiers = defenderAliveSoldiers;
            DefenderRoutedRegiments = defenderRoutedRegiments;
            DefenderWipedRegiments = defenderWipedRegiments;
            DefenderLiveRegiments = defenderLiveRegiments;
            DefenderTotalRegiments = defenderTotalRegiments;
            ObjectiveHoldTicks = objectiveHoldTicks;
            ObjectivePeakHoldTicks = objectivePeakHoldTicks;
        }

        public BattleOutcomeKind Kind { get; }
        public BattleOutcomeReason Reason { get; }
        public int DecidedTick { get; }
        public int EvaluatedTick { get; }
        public int SunsetTick { get; }
        public int AttackerStartingSoldiers { get; }
        public int AttackerAliveSoldiers { get; }
        public int AttackerNonRoutingAlive { get; }
        public int AttackerLostOrRoutedPermille { get; }
        public int AttackerRoutedRegiments { get; }
        public int AttackerWipedRegiments { get; }
        public int AttackerTotalRegiments { get; }
        public int DefenderStartingSoldiers { get; }
        public int DefenderAliveSoldiers { get; }
        public int DefenderRoutedRegiments { get; }
        public int DefenderWipedRegiments { get; }
        public int DefenderLiveRegiments { get; }
        public int DefenderTotalRegiments { get; }
        public int ObjectiveHoldTicks { get; }
        public int ObjectivePeakHoldTicks { get; }
    }

    /// <summary>
    /// Evaluates battle outcome strictly from integer/fixed-point simulation state (<see cref="PlaybackTrack"/> and
    /// <see cref="LevelOutcomeSpec"/>), with zero floats so outcomes match identically across platforms.
    /// </summary>
    public static class BattleOutcomeEvaluator
    {
        public static BattleOutcomeResult Evaluate(
            PlaybackTrack track,
            LevelOutcomeSpec? outcomeSpec = null,
            int armyRoutPermilleOverride = -1,
            int? maxTick = null
        )
        {
            if (track == null)
            {
                throw new ArgumentNullException(nameof(track));
            }
            var spec = outcomeSpec ?? LevelOutcomeSpec.DefaultSurvival();
            int routThreshold =
                armyRoutPermilleOverride > 0 ? armyRoutPermilleOverride : spec.ArmyRoutPermille;
            int regFrames = track.RegimentFrameCount;
            int unitSamples = track.SampleCount;
            var sides = track.RegimentSides;
            var starts = track.RegimentStartingSizes;
            var unitRegs = track.UnitRegiments;
            int regCount = sides.Length;

            int attackerStart = 0;
            int defenderStart = 0;
            int attackerRegTotal = 0;
            int defenderRegTotal = 0;
            for (int r = 0; r < regCount; r++)
            {
                if (sides[r] == Side.Attacker)
                {
                    attackerStart += starts[r];
                    attackerRegTotal++;
                }
                else
                {
                    defenderStart += starts[r];
                    defenderRegTotal++;
                }
            }

            bool checkObjective =
                spec.Variant == LevelOutcomeVariant.Objective && spec.ObjectivePointCount >= 3;
            int holdTicks = 0;
            int peakHold = 0;
            int prevTick = 0;

            BattleOutcomeKind decided = BattleOutcomeKind.Ongoing;
            BattleOutcomeReason reason = BattleOutcomeReason.InProgress;
            int decidedTick = 0;

            RegimentFrame? lastFrame = null;
            int limitTick = maxTick ?? int.MaxValue;
            int sampleIdx = 0;

            for (int f = 0; f < regFrames; f++)
            {
                var frame = track.RegimentAt(f);
                if (frame.Tick > limitTick)
                {
                    break;
                }
                lastFrame = frame;

                if (checkObjective && unitSamples > 0)
                {
                    while (sampleIdx < unitSamples - 1 && track.Sample(sampleIdx).Tick < frame.Tick)
                    {
                        sampleIdx++;
                    }
                    var uSample = track.Sample(sampleIdx);
                    int dt = System.Math.Max(0, frame.Tick - prevTick);
                    int attackersInObjective = CountAttackersInPolygon(
                        uSample,
                        unitRegs,
                        sides,
                        spec.ObjectivePolygonMm
                    );
                    if (attackersInObjective >= spec.ObjectiveMinSoldiers)
                    {
                        holdTicks += dt;
                        if (holdTicks > peakHold)
                        {
                            peakHold = holdTicks;
                        }
                    }
                    else
                    {
                        holdTicks = 0;
                    }
                }
                prevTick = frame.Tick;

                SummarizeFrame(
                    frame,
                    sides,
                    out _,
                    out int atkNonRoutingAlive,
                    out _,
                    out _,
                    out _,
                    out _,
                    out int defLiveRegs
                );
                int lostPermille = ComputeLostPermille(attackerStart, atkNonRoutingAlive);

                if (defenderRegTotal > 0 && defLiveRegs == 0)
                {
                    decided = BattleOutcomeKind.AttackerVictory;
                    reason = BattleOutcomeReason.AllDefendersWiped;
                    decidedTick = frame.Tick;
                    break;
                }
                if (checkObjective && holdTicks >= spec.ObjectiveHoldTicks)
                {
                    decided = BattleOutcomeKind.AttackerVictory;
                    reason = BattleOutcomeReason.ObjectiveCaptured;
                    decidedTick = frame.Tick;
                    break;
                }
                if (attackerRegTotal > 0 && lostPermille >= routThreshold)
                {
                    decided = BattleOutcomeKind.DefenderVictory;
                    reason = BattleOutcomeReason.AttackerArmyRouted;
                    decidedTick = frame.Tick;
                    break;
                }
                if (frame.Tick >= spec.SunsetTick)
                {
                    decided =
                        defLiveRegs > 0 || defenderRegTotal == 0
                            ? BattleOutcomeKind.DefenderVictory
                            : BattleOutcomeKind.AttackerVictory;
                    reason =
                        decided == BattleOutcomeKind.DefenderVictory
                            ? BattleOutcomeReason.HeldUntilSunset
                            : BattleOutcomeReason.AllDefendersWiped;
                    decidedTick = spec.SunsetTick;
                    break;
                }
            }

            if (lastFrame == null)
            {
                return new BattleOutcomeResult(
                    BattleOutcomeKind.Ongoing,
                    BattleOutcomeReason.InProgress,
                    0,
                    0,
                    spec.SunsetTick,
                    attackerStart,
                    attackerStart,
                    attackerStart,
                    0,
                    0,
                    0,
                    attackerRegTotal,
                    defenderStart,
                    defenderStart,
                    0,
                    0,
                    defenderRegTotal,
                    defenderRegTotal,
                    holdTicks,
                    peakHold
                );
            }

            SummarizeFrame(
                lastFrame,
                sides,
                out int atkAlive,
                out int atkNonRouting,
                out int atkRouted,
                out int atkWiped,
                out int defAlive,
                out int defRouted,
                out int defLive
            );
            int defWiped = defenderRegTotal - defLive;
            int finalLostPermille = ComputeLostPermille(attackerStart, atkNonRouting);

            return new BattleOutcomeResult(
                decided,
                reason,
                decidedTick,
                lastFrame.Tick,
                spec.SunsetTick,
                attackerStart,
                atkAlive,
                atkNonRouting,
                finalLostPermille,
                atkRouted,
                atkWiped,
                attackerRegTotal,
                defenderStart,
                defAlive,
                defRouted,
                defWiped,
                defLive,
                defenderRegTotal,
                holdTicks,
                peakHold
            );
        }

        private static int ComputeLostPermille(int starting, int nonRoutingAlive)
        {
            if (starting <= 0)
            {
                return 0;
            }
            long lost = System.Math.Max(0, starting - nonRoutingAlive);
            return (int)(lost * 1000L / starting);
        }

        private static void SummarizeFrame(
            RegimentFrame frame,
            Side[] sides,
            out int attackerAlive,
            out int attackerNonRoutingAlive,
            out int attackerRouted,
            out int attackerWiped,
            out int defenderAlive,
            out int defenderRouted,
            out int defenderNotWiped
        )
        {
            attackerAlive = 0;
            attackerNonRoutingAlive = 0;
            attackerRouted = 0;
            attackerWiped = 0;
            defenderAlive = 0;
            defenderRouted = 0;
            defenderNotWiped = 0;

            int count = System.Math.Min(frame.Regiments.Length, sides.Length);
            for (int r = 0; r < count; r++)
            {
                var reg = frame.Regiments[r];
                if (sides[r] == Side.Attacker)
                {
                    attackerAlive += reg.AliveCount;
                    if (reg.Wiped)
                    {
                        attackerWiped++;
                    }
                    else if (reg.Routing)
                    {
                        attackerRouted++;
                    }
                    else
                    {
                        attackerNonRoutingAlive += reg.AliveCount;
                    }
                }
                else
                {
                    defenderAlive += reg.AliveCount;
                    if (!reg.Wiped)
                    {
                        defenderNotWiped++;
                    }
                    if (reg.Routing && !reg.Wiped)
                    {
                        defenderRouted++;
                    }
                }
            }
        }

        private static int CountAttackersInPolygon(
            UnitSample sample,
            int[] unitRegiments,
            Side[] regimentSides,
            int[] polygonMm
        )
        {
            int count = 0;
            int limit = System.Math.Min(sample.Count, unitRegiments.Length);
            for (int u = 0; u < limit; u++)
            {
                byte st = sample.State[u];
                if ((st & UnitSample.Alive) == 0)
                {
                    continue;
                }
                if ((st & ((byte)UnitFlags.Routing | (byte)UnitFlags.Swept)) != 0)
                {
                    continue;
                }
                int r = unitRegiments[u];
                if (r < 0 || r >= regimentSides.Length || regimentSides[r] != Side.Attacker)
                {
                    continue;
                }
                long px = sample.XCm[u] * 10L;
                long py = sample.YCm[u] * 10L;
                if (ContainsPointMm(polygonMm, px, py))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Exact 64-bit integer ray-crossing containment test (mm coordinates, no division rounding).</summary>
        public static bool ContainsPointMm(int[] polygonMm, long px, long py)
        {
            int n = polygonMm.Length / 2;
            if (n < 3)
            {
                return false;
            }
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                long ax = polygonMm[2 * j];
                long ay = polygonMm[2 * j + 1];
                long bx = polygonMm[2 * i];
                long by = polygonMm[2 * i + 1];
                if ((ay > py) != (by > py))
                {
                    long lhs = (px - ax) * (by - ay);
                    long rhs = (py - ay) * (bx - ax);
                    if (by > ay ? lhs < rhs : lhs > rhs)
                    {
                        inside = !inside;
                    }
                }
            }
            return inside;
        }
    }
}
