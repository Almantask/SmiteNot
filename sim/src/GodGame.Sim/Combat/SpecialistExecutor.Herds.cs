using System;
using System.Collections.Generic;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    public sealed partial class SpecialistExecutor
    {
        // ---- Drovers (S2 14.8) ----

        public const int DriveAction = 8;

        /// <summary>Engineers clearing a battle-made blocker (a wreck, a felled tree; S2 13.2, 14.7).</summary>
        public const int ClearBlockerAction = 9;

        /// <summary>
        /// Drovers walk a herd (S2 14.8). Attackers take a herd with an animal near their army's foot path ahead, stand
        /// between it and the path, and walk it away (their fear, under the stampede line) until every animal is clear of the
        /// path, hold, then take the next job. Defenders take the nearest herd and walk it onto the attackers' route, holding
        /// it there until an enemy comes near. Fire or a strike near the drovers scatters the dogs for a while (no fear). A
        /// herd that breaks into a stampede ends the job: drovers never steer one (brief, 2026-10-09).
        /// </summary>
        private bool Drive(TickContext context, int r)
        {
            var world = context.World;
            var army = world.Army;
            var rules = _rules.Specialists.DriveHerd;
            var herdRules = _rules.Herds;
            int tick = context.Tick;
            bool attacker = world.RegimentSide[r] == GodGame.Sim.Side.Attacker;
            if (rules == null || herdRules == null)
            {
                army.Job[r] = SpecialistJob.Done;
                return false;
            }
            int jobs = attacker ? rules.AttackerJobs : rules.DefenderJobs;
            var herds = world.Herds;
            if (army.Job[r] == SpecialistJob.Idle)
            {
                if (army.Spent[r] >= jobs)
                {
                    army.Job[r] = SpecialistJob.Done;
                    return false;
                }
                int found = attacker ? HerdNearPath(world, r, rules) : NearestHerd(world, r, rules);
                if (found < 0)
                {
                    return false;
                }
                army.Job[r] = SpecialistJob.Active;
                army.JobKind[r] = DriveAction;
                army.JobRef[r] = found;
                army.WorkProgress[r] = 0;
                world.Causes?.Add(CauseKind.SpecialistStarted, tick, r, DriveAction, CauseLog.None);
            }
            int h = army.JobRef[r];
            int herd = herds.Regiment[h];
            var path = army.JobCells[r];
            if (
                herds.Mode[h] == HerdMode.Agitated
                || herds.Mode[h] == HerdMode.Stampede
                || path == null
                || _summary.Count[herd] == 0
            )
            {
                return EndDrive(world, r, tick);
            }
            ScatterDogs(world, r, rules, tick);
            var terrain = _navigation.Terrain;
            var centre = _summary.Centroid[herd];
            int nearest = NearestCell(terrain, path, centre, out long nearestMm);
            var onPath = terrain.CellCentre(nearest % terrain.Columns, nearest / terrain.Columns);
            var towardPath = onPath - centre;
            if (towardPath == FixVec2.Zero)
            {
                towardPath = FixVec2.FromAngle(world.RegimentFacing[r], Fix.One);
            }
            var standOff = Fix.FromRatio(rules.StandOffMm, 1000);
            if (attacker)
            {
                // Between the herd and the path: the fear walks it away from the road.
                army.Point[r] = Clamp(centre + towardPath.WithLength(standOff));
                if (AllClear(world, herd, path, rules.ClearOfPathMm))
                {
                    army.WorkProgress[r] += TickOrder.RegimentAiRate.Period;
                    if (army.WorkProgress[r] >= rules.HoldTicks)
                    {
                        return EndDrive(world, r, tick);
                    }
                }
                else
                {
                    army.WorkProgress[r] = 0;
                }
            }
            else
            {
                // Beyond the herd from the route: the fear walks it onto the road, where it is held until enemies come.
                bool there = nearestMm <= 5_000;
                army.Point[r] = Clamp(
                    centre - towardPath.WithLength(there ? standOff * 3 : standOff)
                );
                if (EnemyNear(world, herd, rules.ReleaseWithinMm))
                {
                    return EndDrive(world, r, tick);
                }
            }
            army.Order[r] = RegimentOrder.Work;
            army.Target[r] = -1;
            return true;
        }

        private bool EndDrive(World world, int r, int tick)
        {
            var army = world.Army;
            world.Causes?.Add(CauseKind.SpecialistEnded, tick, r, DriveAction, CauseLog.None);
            army.Spent[r]++;
            army.Job[r] = SpecialistJob.Idle;
            army.JobKind[r] = 0;
            army.JobRef[r] = -1;
            army.JobCells[r] = null;
            army.JobTargets[r] = null;
            army.WorkProgress[r] = 0;
            army.Order[r] = RegimentOrder.March;
            return false;
        }

        /// <summary>Fire or a fresh strike near the drovers scatters their dogs: no fear for the scatter time.</summary>
        private void ScatterDogs(World world, int r, DriveHerdRules rules, int tick)
        {
            var strikes = world.Strikes;
            var terrain = _navigation.Terrain;
            var at = _summary.Centroid[r];
            long within = rules.ScatterWithinMm;
            bool scare = false;
            for (int s = 0; s < strikes.StrikeCount && !scare; s++)
            {
                if (tick - strikes.StrikeTick(s) < TickOrder.RegimentAiRate.Period)
                {
                    var point = new FixVec2(
                        Fix.FromRatio(strikes.StrikeXMm(s), 1000),
                        Fix.FromRatio(strikes.StrikeYMm(s), 1000)
                    );
                    scare = Mm(point - at) <= within;
                }
            }
            for (int f = 0; f < strikes.FireCount && !scare; f++)
            {
                if (strikes.FireOut(f))
                {
                    continue;
                }
                foreach (int cell in world.Blockers.Cells(strikes.FireBlocker(f)))
                {
                    if (
                        Mm(terrain.CellCentre(cell % terrain.Columns, cell / terrain.Columns) - at)
                        <= within
                    )
                    {
                        scare = true;
                        break;
                    }
                }
            }
            if (scare)
            {
                world.Army.FallBackReady[r] = tick + rules.ScatterTicks;
            }
        }

        /// <summary>Attackers: the first herd (path order, then herd order) with an animal near the path ahead; it keeps the path.</summary>
        private int HerdNearPath(World world, int r, DriveHerdRules rules)
        {
            var path = PathAhead(world, r, rules.AheadMm);
            if (path.Count == 0)
            {
                return -1;
            }
            var terrain = _navigation.Terrain;
            long near = rules.NearPathMm;
            var herds = world.Herds;
            for (int k = 0; k < path.Count; k++)
            {
                var at = terrain.CellCentre(path[k] % terrain.Columns, path[k] / terrain.Columns);
                for (int h = 0; h < herds.Count; h++)
                {
                    if (!Drivable(world, r, h))
                    {
                        continue;
                    }
                    int herd = herds.Regiment[h];
                    for (int u = 0; u < world.Units.HighWater; u++)
                    {
                        if (
                            world.Units.IsAlive(u)
                            && world.UnitRegiment[u] == herd
                            && Mm(world.Position[u] - at) <= near
                        )
                        {
                            world.Army.JobCells[r] = path.ToArray();
                            world.Army.JobTargets[r] = new int[path.Count];
                            return h;
                        }
                    }
                }
            }
            return -1;
        }

        /// <summary>Defenders: the nearest herd within reach, and the attackers' predicted foot route to walk it onto.</summary>
        private int NearestHerd(World world, int r, DriveHerdRules rules)
        {
            var herds = world.Herds;
            int best = -1;
            long bestMm = rules.AheadMm;
            for (int h = 0; h < herds.Count; h++)
            {
                if (!Drivable(world, r, h))
                {
                    continue;
                }
                long d = Mm(_summary.Centroid[herds.Regiment[h]] - _summary.Centroid[r]);
                if (d <= bestMm)
                {
                    best = h;
                    bestMm = d;
                }
            }
            if (best < 0)
            {
                return -1;
            }
            var route = AttackerRoute(world);
            if (route.Count == 0)
            {
                return -1;
            }
            world.Army.JobCells[r] = route.ToArray();
            world.Army.JobTargets[r] = new int[route.Count];
            return best;
        }

        /// <summary>The attackers' foot route as navigation predicts it: from the attacker zone's centre down their march field.</summary>
        private List<int> AttackerRoute(World world)
        {
            var route = new List<int>();
            var field = _navigation.MarchField(MovementClass.Foot, new SerialRunner());
            var map = _navigation.Map;
            DeploymentZone? zone = null;
            foreach (var z in map.DeploymentZones)
            {
                if (z.Side == GodGame.Sim.Side.Attacker)
                {
                    zone = z;
                }
            }
            if (field == null || zone == null)
            {
                return route;
            }
            var terrain = _navigation.Terrain;
            var area = zone.Value.Area;
            int x = (int)(((long)area.MinX + area.MaxX) / 2 / terrain.CellSizeMm);
            int y = (int)(((long)area.MinY + area.MaxY) / 2 / terrain.CellSizeMm);
            var directions = field.FreshDirections();
            for (int k = 0; k < terrain.Columns + terrain.Rows; k++)
            {
                route.Add(terrain.Index(x, y));
                byte d = directions[terrain.Index(x, y)];
                if (d >= FlowField.AtGoal)
                {
                    break;
                }
                var (dx, dy) = FlowField.Offset(d);
                if (!terrain.InBounds(x + dx, y + dy))
                {
                    break;
                }
                x += dx;
                y += dy;
            }
            return route;
        }

        /// <summary>A herd that grazes (or has scattered) and that no other drovers are walking.</summary>
        private static bool Drivable(World world, int r, int h)
        {
            var herds = world.Herds;
            if (herds.Mode[h] == HerdMode.Agitated || herds.Mode[h] == HerdMode.Stampede)
            {
                return false;
            }
            var army = world.Army;
            for (int o = 0; o < world.Regiments.HighWater; o++)
            {
                if (
                    o != r
                    && world.Regiments.IsAlive(o)
                    && army.Job[o] == SpecialistJob.Active
                    && army.JobKind[o] == DriveAction
                    && army.JobRef[o] == h
                )
                {
                    return false;
                }
            }
            return world.RegimentAlive[herds.Regiment[h]] > 0;
        }

        /// <summary>Whether every animal of the herd regiment is at least <paramref name="clearMm"/> from every path cell.</summary>
        private bool AllClear(World world, int herd, int[] path, long clearMm)
        {
            var terrain = _navigation.Terrain;
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (!world.Units.IsAlive(u) || world.UnitRegiment[u] != herd)
                {
                    continue;
                }
                NearestCell(terrain, path, world.Position[u], out long d);
                if (d < clearMm)
                {
                    return false;
                }
            }
            return true;
        }

        private bool EnemyNear(World world, int herd, long withinMm)
        {
            for (int e = 0; e < world.Regiments.HighWater; e++)
            {
                if (
                    world.Regiments.IsAlive(e)
                    && world.RegimentSide[e] == GodGame.Sim.Side.Attacker
                    && _summary.Count[e] > 0
                    && _summary.Within(herd, e, withinMm)
                )
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The path cell nearest a point (the first of equals) and its distance in mm.</summary>
        private static int NearestCell(Terrain terrain, int[] path, FixVec2 p, out long mm)
        {
            int best = path[0];
            long bestSq = long.MaxValue;
            foreach (int c in path)
            {
                var at = terrain.CellCentre(c % terrain.Columns, c / terrain.Columns);
                long dx = at.X.Raw - p.X.Raw;
                long dy = at.Y.Raw - p.Y.Raw;
                // Clamped to the map's extent (30 km), the squares fit a long.
                long sq = dx * dx + dy * dy;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = c;
                }
            }
            mm = (long)IntMath.Sqrt((ulong)bestSq) * 1000 >> Fix.FractionalBits;
            return best;
        }

        private static long Mm(FixVec2 v) => v.Length.Raw * 1000 >> Fix.FractionalBits;

        // ---- Engineers: wrecks and felled trees (S2 13.2, 14.7) ----

        /// <summary>
        /// An uncleared wreck or felled tree with a cell near the path ahead (path order, then blocker order) whose clearing
        /// time the pool still holds and that no one else clears; where to stand goes to <see cref="ArmyState.WorkStep"/>.
        /// </summary>
        private bool FindBattleBlocker(World world, int r, ClearBlockerRules clearing, int pool)
        {
            var blockers = world.Blockers;
            if (blockers.Count == 0)
            {
                return false;
            }
            var terrain = _navigation.Terrain;
            int near = System.Math.Max(1, clearing.NearPathMm / terrain.CellSizeMm);
            var path = PathAhead(world, r, clearing.WithinMm);
            foreach (int p in path)
            {
                int px = p % terrain.Columns;
                int py = p / terrain.Columns;
                for (int b = 0; b < blockers.Count; b++)
                {
                    if (
                        blockers.IsCleared(b)
                        || blockers.Kind(b) == BlockerKind.Fire
                        || BlockerHeld(world, r, b)
                        || pool < BlockerTicks(blockers.Kind(b), engineers: true)
                    )
                    {
                        continue;
                    }
                    foreach (int cell in blockers.Cells(b))
                    {
                        if (
                            System.Math.Abs(cell % terrain.Columns - px) <= near
                            && System.Math.Abs(cell / terrain.Columns - py) <= near
                        )
                        {
                            world.Army.JobRef[r] = b;
                            world.Army.WorkStep[r] = p;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>The shortest clearing job there is, so Engineers with less left in the pool are done.</summary>
        private int MinClearTicks(ClearBlockerRules clearing)
        {
            int min = clearing.BoulderTicks;
            foreach (var kind in new[] { BlockerKind.Wreck, BlockerKind.FelledTree })
            {
                min = System.Math.Min(min, BlockerTicks(kind, engineers: true));
            }
            return min;
        }

        /// <summary>How long a blocker takes to clear: Engineers', or any foot regiment's (D14).</summary>
        private int BlockerTicks(BlockerKind kind, bool engineers)
        {
            var rules = _navigation.BlockerRulesFor(kind, 0);
            return rules == null ? int.MaxValue
                : engineers ? rules.EngineerClearTicks
                : rules.FootRegimentClearTicks;
        }

        private static bool BlockerHeld(World world, int r, int b)
        {
            var army = world.Army;
            for (int o = 0; o < world.Regiments.HighWater; o++)
            {
                if (
                    o != r
                    && world.Regiments.IsAlive(o)
                    && army.Job[o] == SpecialistJob.Active
                    && army.JobKind[o] == ClearBlockerAction
                    && army.JobRef[o] == b
                )
                {
                    return true;
                }
            }
            return world.Blockers.Clearer(b) >= 0 && world.Blockers.Clearer(b) != r;
        }

        /// <summary>One AI update of Engineers clearing a battle blocker; true when it is cleared.</summary>
        private bool ClearBattleBlocker(TickContext context, int r, ClearBlockerRules clearing)
        {
            var world = context.World;
            var army = world.Army;
            int b = army.JobRef[r];
            if (world.Blockers.IsCleared(b))
            {
                return true; // cleared by someone else first: nothing spent
            }
            int ticks = BlockerTicks(world.Blockers.Kind(b), engineers: true);
            army.WorkProgress[r] += TickOrder.RegimentAiRate.Period;
            if (army.WorkProgress[r] < ticks)
            {
                return false;
            }
            _navigation.ClearBlocker(context, b);
            army.Spent2[r] += ticks;
            world.Causes?.Add(
                CauseKind.BlockerCleared,
                context.Tick,
                r,
                b,
                world.Blockers.Event(b)
            );
            return true;
        }

        // ---- Any foot regiment clears a battle blocker that closes the last route (D14) ----

        /// <summary>
        /// D14 (settled 2026-11-07): a wreck or felled tree that closes an army's last route for some class (judged when
        /// it was made) is cleared by the nearest foot line regiment of either army within reach, in its foot-regiment
        /// time, unless Engineers are on it; otherwise the army goes round. Runs after the targeting rules, in blocker
        /// order, and takes over the clearer's order while it works (it still turns on an enemy that comes close).
        /// </summary>
        public void FootClearing(TickContext context)
        {
            var world = context.World;
            var blockers = world.Blockers;
            var army = world.Army;
            var terrain = _navigation.Terrain;
            for (int b = 0; b < blockers.Count; b++)
            {
                if (
                    blockers.IsCleared(b)
                    || !blockers.ClosesRoute(b)
                    || blockers.Kind(b) == BlockerKind.Fire
                )
                {
                    continue;
                }
                if (BlockerHeldByEngineers(world, b))
                {
                    continue;
                }
                int clearer = blockers.Clearer(b);
                var cells = blockers.Cells(b);
                var at = terrain.CellCentre(cells[0] % terrain.Columns, cells[0] / terrain.Columns);
                if (clearer >= 0 && !CanClear(world, clearer))
                {
                    blockers.SetClearer(b, -1, 0);
                    clearer = -1;
                }
                if (clearer < 0)
                {
                    long best = FootClearReachMm;
                    for (int r = 0; r < world.Regiments.HighWater; r++)
                    {
                        if (!CanClear(world, r) || IsClearing(world, r))
                        {
                            continue;
                        }
                        long d = Mm(_summary.Centroid[r] - at);
                        if (d < best)
                        {
                            best = d;
                            clearer = r;
                        }
                    }
                    if (clearer < 0)
                    {
                        continue;
                    }
                    blockers.SetClearer(b, clearer, 0);
                }
                if (army.Order[clearer] == RegimentOrder.Engage)
                {
                    continue; // fighting first
                }
                army.Order[clearer] = RegimentOrder.Work;
                army.Point[clearer] = Clamp(at);
                army.Target[clearer] = -1;
                if (Mm(_summary.Centroid[clearer] - at) > FootClearAtMm)
                {
                    continue;
                }
                int progress = blockers.Progress(b) + TickOrder.RegimentAiRate.Period;
                if (progress < BlockerTicks(blockers.Kind(b), engineers: false))
                {
                    blockers.SetClearer(b, clearer, progress);
                    continue;
                }
                _navigation.ClearBlocker(context, b);
                blockers.SetClearer(b, -1, progress);
                world.Causes?.Add(
                    CauseKind.BlockerCleared,
                    context.Tick,
                    clearer,
                    b,
                    blockers.Event(b)
                );
                army.Order[clearer] = RegimentOrder.March;
            }
        }

        /// <summary>A foot line regiment clears within this of a blocker (placeholder: S2 names no distance).</summary>
        public const int FootClearReachMm = 100_000;

        /// <summary>It works once its centroid is this close (placeholder).</summary>
        public const int FootClearAtMm = 12_000;

        private bool CanClear(World world, int r)
        {
            if (!world.Regiments.IsAlive(r) || world.RegimentAlive[r] == 0 || world.Army.Wiped[r])
            {
                return false;
            }
            var type = _rules.UnitTypes[world.RegimentUnitType[r]];
            return world.RegimentSide[r] != GodGame.Sim.Side.Neutral
                && type.MovementClass == MovementClass.Foot
                && type.Action == null
                && type.Engine == null
                && !type.IsAnimal
                && world.Army.Order[r] != RegimentOrder.Rout;
        }

        private static bool IsClearing(World world, int r)
        {
            var blockers = world.Blockers;
            for (int b = 0; b < blockers.Count; b++)
            {
                if (!blockers.IsCleared(b) && blockers.Clearer(b) == r)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool BlockerHeldByEngineers(World world, int b)
        {
            var army = world.Army;
            for (int o = 0; o < world.Regiments.HighWater; o++)
            {
                if (
                    world.Regiments.IsAlive(o)
                    && army.Job[o] == SpecialistJob.Active
                    && army.JobKind[o] == ClearBlockerAction
                    && army.JobRef[o] == b
                )
                {
                    return true;
                }
            }
            return false;
        }
    }
}
