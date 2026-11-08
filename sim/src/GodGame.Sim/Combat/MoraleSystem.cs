using System;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// The morale step (spec S2, section 7). Every tick, routing soldiers who reach the map's edge run off it (fled:
    /// they count as lost). At 4 Hz, every regiment's morale moves on casualty events rather than per-death checks:
    /// its own losses (a swept soldier counts as half), a friendly regiment's losses nearby, a friend wiped nearby;
    /// once a second, friends routing nearby (contagion), enemies routing nearby, and recovery after a quiet spell up to
    /// a ceiling its losses lower. Every loss is scaled by the type's shock. Below the rout line a regiment routs for its
    /// retreat edge; it rallies once after a safe spell, and a second rout is final. A regiment whose soldiers on the
    /// map fall below about 10% of its starting size is wiped: the remnant routs for good and no longer counts.
    /// <para>
    /// Determinism: one summary per update, every regiment's change computed from the state the update started with,
    /// then applied and its transitions taken in slot order; despawns run in slot order.
    /// </para>
    /// </summary>
    public sealed class MoraleSystem : ISimSystem
    {
        private readonly Ruleset _rules;
        private readonly Navigation _navigation;
        private readonly RegimentSummary _summary = new RegimentSummary();
        private int[] _lost = new int[0];
        private bool[] _newlyWiped = new bool[0];
        private int[] _morale = new int[0];
        private bool[] _flagsChanged = new bool[0];

        public MoraleSystem(Ruleset rules, Navigation navigation)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        }

        public TickStep Step => TickStep.Morale;

        public TickRate Rate => TickRate.EveryTick;

        public void Run(TickContext context)
        {
            Flee(context);
            if (TickOrder.RegimentAiRate.IsDue(context.Tick))
            {
                Update(context);
            }
        }

        private void Flee(TickContext context)
        {
            var world = context.World;
            var terrain = _navigation.Terrain;
            bool anyRouting = false;
            for (int r = 0; r < world.Regiments.HighWater && !anyRouting; r++)
            {
                anyRouting =
                    world.Regiments.IsAlive(r) && world.Army.Order[r] == RegimentOrder.Rout;
            }
            if (!anyRouting)
            {
                return; // only routing soldiers flee, and they carry their regiment's order
            }
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (
                    !world.Units.IsAlive(u)
                    || (world.Flags[u] & UnitFlags.Routing) == 0
                    || (world.Flags[u] & (UnitFlags.Swept | UnitFlags.Stunned | UnitFlags.Launched))
                        != 0
                )
                {
                    continue;
                }
                terrain.CellOf(world.Position[u], out int x, out int y);
                if (x > 0 && y > 0 && x < terrain.Columns - 1 && y < terrain.Rows - 1)
                {
                    continue;
                }
                int r = world.UnitRegiment[u];
                world.Causes?.Add(CauseKind.SoldierFled, context.Tick, r, u, CauseLog.None);
                world.DespawnUnit(world.Units.IdOf(u));
                world.Counters.Fled++;
            }
        }

        private void Update(TickContext context)
        {
            var world = context.World;
            var army = world.Army;
            var m = _rules.Morale;
            int tick = context.Tick;
            int n = world.Regiments.Capacity;
            if (_lost.Length != n)
            {
                _lost = new int[n];
                _newlyWiped = new bool[n];
                _morale = new int[n];
                _flagsChanged = new bool[n];
            }
            _summary.Gather(world);
            int high = world.Regiments.HighWater;

            // Losses since the last update, in half soldiers, and who falls below the wiped line.
            for (int r = 0; r < high; r++)
            {
                _lost[r] = 0;
                _newlyWiped[r] = false;
                _flagsChanged[r] = false;
                int start = world.RegimentStartingSize[r];
                if (!world.Regiments.IsAlive(r) || start == 0)
                {
                    continue;
                }
                int halves = 2 * (start - world.RegimentAlive[r]) + _summary.Swept[r];
                if (halves > army.LossesSeen[r])
                {
                    _lost[r] = halves - army.LossesSeen[r];
                    army.LastLoss[r] = tick;
                }
                army.LossesSeen[r] = halves;
                _newlyWiped[r] =
                    !army.Wiped[r]
                    && (long)world.RegimentAlive[r] * 1000 < (long)start * m.WipedBelowPermille;
            }

            // Every regiment's new morale from the update's starting state.
            bool perSecond = tick % SimConstants.TicksPerSecond == 0;
            for (int r = 0; r < high; r++)
            {
                _morale[r] = world.RegimentMorale[r];
                int start = world.RegimentStartingSize[r];
                if (!world.Regiments.IsAlive(r) || start == 0 || army.Wiped[r])
                {
                    continue;
                }
                long loss = (long)_lost[r] * m.OwnLossFactor / (2L * start);
                long gain = 0;
                for (int f = 0; f < high; f++)
                {
                    if (
                        f == r
                        || !world.Regiments.IsAlive(f)
                        || _summary.Count[f] == 0 && _lost[f] == 0
                    )
                    {
                        continue;
                    }
                    bool friend = world.RegimentSide[f] == world.RegimentSide[r];
                    bool matters =
                        (friend && (_lost[f] > 0 || _newlyWiped[f]))
                        || (perSecond && army.Order[f] == RegimentOrder.Rout && !army.Wiped[f]);
                    if (!matters)
                    {
                        continue; // nothing this regiment did can move r's morale: skip the distance
                    }
                    long gap =
                        _summary.Count[r] > 0 && _summary.Count[f] > 0
                            ? _summary.Gap(r, f)
                            : long.MaxValue;
                    if (friend && _lost[f] > 0 && gap <= m.FarMm)
                    {
                        long c = System.Math.Min(
                            m.NearbyLossCap,
                            (long)_lost[f] * m.NearbyLossFactor / (2L * start)
                        );
                        loss += gap <= m.NearMm ? c : c / 2;
                    }
                    if (gap > m.NearMm)
                    {
                        continue;
                    }
                    if (friend && _newlyWiped[f])
                    {
                        loss += m.FriendWipedOnce;
                    }
                    if (perSecond && army.Order[f] == RegimentOrder.Rout && !army.Wiped[f])
                    {
                        if (friend)
                        {
                            loss += m.FriendRoutingPerS;
                        }
                        else
                        {
                            gain += m.EnemyRoutingPerS;
                        }
                    }
                }
                var shock = _rules.UnitTypes[world.RegimentUnitType[r]].MoraleShockPermille;
                long morale = world.RegimentMorale[r] - loss * shock / 1000 + gain;
                if (perSecond && tick - army.LastLoss[r] >= m.RecoveryQuietTicks)
                {
                    // Recovery never lifts morale past 1,000 less the losses so far (‰ of the starting size).
                    long ceiling = m.Start - (long)(start - world.RegimentAlive[r]) * 1000 / start;
                    if (morale < ceiling)
                    {
                        morale = System.Math.Min(ceiling, morale + m.RecoveryPerS);
                    }
                }
                _morale[r] = (int)System.Math.Max(0, System.Math.Min(m.Start, morale));
            }

            // Apply, then take transitions in slot order.
            var log = world.Causes;
            var lethal = _navigation.Lethal;
            int awayFromLethal = System.Math.Min(
                m.RallyAwayFromLethalMm,
                LethalDistance.MaxDistanceMm
            );
            for (int r = 0; r < high; r++)
            {
                if (!world.Regiments.IsAlive(r) || world.RegimentStartingSize[r] == 0)
                {
                    continue;
                }
                world.RegimentMorale[r] = _morale[r];
                if (_newlyWiped[r])
                {
                    army.Wiped[r] = true;
                    army.RalliesLeft[r] = 0;
                    army.SafeTicks[r] = 0;
                    log?.Add(CauseKind.RegimentWiped, tick, r, 0, CauseLog.None);
                    StartRout(world, r);
                    continue;
                }
                if (army.Wiped[r])
                {
                    continue;
                }
                if (army.Order[r] != RegimentOrder.Rout)
                {
                    if (_morale[r] < m.RoutBelow)
                    {
                        log?.Add(
                            CauseKind.RegimentRouted,
                            tick,
                            r,
                            army.RalliesLeft[r] > 0 ? 1 : 2,
                            CauseLog.None
                        );
                        army.SafeTicks[r] = 0;
                        StartRout(world, r);
                    }
                    continue;
                }
                if (army.RalliesLeft[r] == 0 || _summary.Count[r] == 0)
                {
                    continue; // a final rout runs off the map
                }
                bool safe = true;
                for (int e = 0; e < high && safe; e++)
                {
                    if (
                        world.Regiments.IsAlive(e)
                        && world.RegimentSide[e] != world.RegimentSide[r]
                        && _summary.Count[e] > 0
                        && !army.Wiped[e]
                        && _summary.Gap(r, e) <= m.RallyNoEnemyWithinMm
                    )
                    {
                        safe = false;
                    }
                }
                if (safe)
                {
                    // The lethal field is capped at 50 m, so "at least 60 m" reads as "at the cap".
                    _navigation.Terrain.CellOf(_summary.Centroid[r], out int cx, out int cy);
                    safe = lethal.DistanceMm(cx, cy) >= awayFromLethal;
                }
                army.SafeTicks[r] = safe ? army.SafeTicks[r] + TickOrder.RegimentAiRate.Period : 0;
                if (army.SafeTicks[r] >= m.RallySafeTicks)
                {
                    army.RalliesLeft[r]--;
                    army.SafeTicks[r] = 0;
                    army.Order[r] = RegimentOrder.March;
                    army.Target[r] = -1;
                    world.RegimentMorale[r] = m.RallyMorale;
                    _flagsChanged[r] = true;
                    log?.Add(CauseKind.RegimentRallied, tick, r, 0, CauseLog.None);
                }
            }

            // Soldiers take their regiment's routing flag.
            bool any = false;
            for (int r = 0; r < high; r++)
            {
                any |= _flagsChanged[r];
            }
            if (!any)
            {
                return;
            }
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (!world.Units.IsAlive(u) || !_flagsChanged[world.UnitRegiment[u]])
                {
                    continue;
                }
                world.Flags[u] =
                    army.Order[world.UnitRegiment[u]] == RegimentOrder.Rout
                        ? world.Flags[u] | UnitFlags.Routing
                        : world.Flags[u] & ~UnitFlags.Routing;
            }
        }

        private void StartRout(World world, int r)
        {
            var army = world.Army;
            army.Order[r] = RegimentOrder.Rout;
            army.Target[r] = -1;
            _flagsChanged[r] = true;
        }
    }
}
