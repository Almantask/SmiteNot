using System;
using System.Collections.Generic;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>Where a checked route must lead.</summary>
    public enum RouteTarget
    {
        /// <summary>The other army's deployment zone.</summary>
        Enemy = 0,

        /// <summary>The army's own retreat edge: the map edge nearest its zone.</summary>
        Retreat = 1,
    }

    /// <summary>One checked route: from a side's zone, for one movement class, to one target.</summary>
    public readonly struct RouteCheck
    {
        public RouteCheck(
            Side side,
            MovementClass movementClass,
            RouteTarget target,
            bool reached,
            long lengthMm,
            long capMm
        )
        {
            Side = side;
            MovementClass = movementClass;
            Target = target;
            Reached = reached;
            LengthMm = lengthMm;
            CapMm = capMm;
        }

        public Side Side { get; }

        public MovementClass MovementClass { get; }

        public RouteTarget Target { get; }

        /// <summary>False when no open route reaches the target at all (a full trap).</summary>
        public bool Reached { get; }

        /// <summary>The shortest open route's length; 0 when not reached.</summary>
        public long LengthMm { get; }

        /// <summary>The longest the route may be: the straight-line distance × the cap, plus a little for the grid.</summary>
        public long CapMm { get; }

        public bool Ok => Reached && LengthMm <= CapMm;

        /// <summary>Length against the cap in percent, for the "1.6x of 2x" readout (0 when not reached).</summary>
        public long PercentOfCap => Reached && CapMm > 0 ? LengthMm * 100 / CapMm : 0;
    }

    /// <summary>The result of a reachability check: every route, and each route's cells for the overlay.</summary>
    public sealed class ReachabilityResult
    {
        private readonly RouteCheck[] _checks;
        private readonly FixVec2[][] _routes;

        internal ReachabilityResult(RouteCheck[] checks, FixVec2[][] routes)
        {
            _checks = checks;
            _routes = routes;
        }

        /// <summary>In a fixed order: attacker then defender; foot, horse, wheeled; enemy then retreat.</summary>
        public IReadOnlyList<RouteCheck> Checks => _checks;

        public bool Ok
        {
            get
            {
                foreach (var check in _checks)
                {
                    if (!check.Ok)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>The first failing route in <see cref="Checks"/> order, the reason a placement is refused; null when all pass.</summary>
        public RouteCheck? FirstFailure
        {
            get
            {
                foreach (var check in _checks)
                {
                    if (!check.Ok)
                    {
                        return check;
                    }
                }
                return null;
            }
        }

        /// <summary>The shortest route's coarse cell centres, from the zone to the target (empty when not reached): the overlay.</summary>
        public IReadOnlyList<FixVec2> Route(int checkIndex) => _routes[checkIndex];
    }

    /// <summary>
    /// The no-full-trap check (brief, 2026-10-09; tech notes, section 3): placements may never close the last route
    /// from either army's zone to the other army or to its own retreat edge, for any land movement class, and that
    /// route may be at most a set multiple (2×) of the straight-line distance. Run on every placement drag, so it works
    /// on a coarse copy of the navigation grid (<see cref="RouteRules.CoarseCells"/> field cells a side).
    /// <para>
    /// Only hard blockers count: a coarse cell is closed for a class when fewer than half its field cells are
    /// passable (cliffs and, later, walls, boulders, deep water); costly ground stays open, because the cap counts
    /// distance, not travel time, so slowing an army stays legal. A clearance map (distance to the nearest closed cell
    /// or the map edge, in coarse cells) keeps wide units out of narrow gaps. The distance fill is breadth-first with
    /// integer steps (5 straight, 7 diagonally, no corner cutting), from every open zone cell at once.
    /// </para>
    /// <para>
    /// Planning-time validation: it reads the cost fields and the map, keeps its coarse grids up to date by rectangle
    /// (<see cref="Update"/>), and decides nothing in the battle itself. Same answer on any runner.
    /// </para>
    /// </summary>
    public sealed class Reachability
    {
        /// <summary>The land classes checked, in result order.</summary>
        public static readonly MovementClass[] Classes =
        {
            MovementClass.Foot,
            MovementClass.Horse,
            MovementClass.Wheeled,
        };

        private const int Straight = 5;
        private const int Diagonal = 7;

        private readonly CostFields _costs;
        private readonly Terrain _terrain;
        private readonly MapData _map;
        private readonly RouteRules _rules;
        private readonly int _k;
        private readonly int _columns;
        private readonly int _rows;
        private readonly long _cellMm;
        private readonly bool[][] _open; // per class, per coarse cell
        private readonly int[][] _clearance; // per class
        private readonly int[][] _distance; // per fill (side × class)
        private readonly bool[][] _usable; // per class: open with the class's clearance
        private readonly int[][][] _buckets; // per fill: Dial's ring of stacks, reused
        private readonly int[][] _bucketCount;
        private readonly int[] _delta; // coarse index offset per direction

        public Reachability(CostFields costs, MapData map, RouteRules rules)
        {
            _costs = costs ?? throw new ArgumentNullException(nameof(costs));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _terrain = costs.Terrain;
            _k = rules.CoarseCells;
            _columns = (_terrain.Columns + _k - 1) / _k;
            _rows = (_terrain.Rows + _k - 1) / _k;
            _cellMm = (long)_terrain.CellSizeMm * _k;
            _open = new bool[Classes.Length][];
            _clearance = new int[Classes.Length][];
            _usable = new bool[Classes.Length][];
            _distance = new int[2 * Classes.Length][];
            _buckets = new int[_distance.Length][][];
            _bucketCount = new int[_distance.Length][];
            for (int c = 0; c < Classes.Length; c++)
            {
                _open[c] = new bool[_columns * _rows];
                _clearance[c] = new int[_columns * _rows];
                _usable[c] = new bool[_columns * _rows];
            }
            for (int f = 0; f < _distance.Length; f++)
            {
                _distance[f] = new int[_columns * _rows];
                _buckets[f] = new int[Diagonal + 1][];
                _bucketCount[f] = new int[Diagonal + 1];
                for (int b = 0; b <= Diagonal; b++)
                {
                    _buckets[f][b] = new int[256];
                }
            }
            _delta = new int[8];
            for (int d = 0; d < 8; d++)
            {
                var (dx, dy) = FlowField.Offset(d);
                _delta[d] = dy * _columns + dx;
            }
            Update(_terrain.All);
        }

        public int Columns => _columns;

        public int Rows => _rows;

        /// <summary>Coarse cell edge in mm.</summary>
        public long CellMm => _cellMm;

        /// <summary>Refreshes the coarse cells over a rectangle of field cells whose costs were rebuilt.</summary>
        public void Update(CellRect fineCells)
        {
            if (fineCells.IsEmpty)
            {
                return;
            }
            int x0 = System.Math.Max(0, fineCells.MinX / _k);
            int y0 = System.Math.Max(0, fineCells.MinY / _k);
            int x1 = System.Math.Min(_columns - 1, fineCells.MaxX / _k);
            int y1 = System.Math.Min(_rows - 1, fineCells.MaxY / _k);
            for (int c = 0; c < Classes.Length; c++)
            {
                var costs = _costs.Of(Classes[c]);
                for (int cy = y0; cy <= y1; cy++)
                {
                    for (int cx = x0; cx <= x1; cx++)
                    {
                        int open = 0;
                        int total = 0;
                        for (
                            int fy = cy * _k;
                            fy < System.Math.Min(_terrain.Rows, cy * _k + _k);
                            fy++
                        )
                        {
                            for (
                                int fx = cx * _k;
                                fx < System.Math.Min(_terrain.Columns, cx * _k + _k);
                                fx++
                            )
                            {
                                total++;
                                if (costs[_terrain.Index(fx, fy)] != CostFields.Impassable)
                                {
                                    open++;
                                }
                            }
                        }
                        _open[c][cy * _columns + cx] = open * 2 >= total;
                    }
                }
            }
        }

        /// <summary>Runs every route check: clearance per class, then one fill per side and class, in parallel.</summary>
        public ReachabilityResult Check(IParallelRunner runner)
        {
            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }
            runner.For(Classes.Length, 1, new ClearanceKernel(this));
            runner.For(_distance.Length, 1, new FillKernel(this));

            var checks = new RouteCheck[_distance.Length * 2];
            var routes = new FixVec2[checks.Length][];
            for (int f = 0; f < _distance.Length; f++)
            {
                var side = f < Classes.Length ? Side.Attacker : Side.Defender;
                int c = f % Classes.Length;
                var own = Zone(side);
                var other = Zone(side == Side.Attacker ? Side.Defender : Side.Attacker);
                for (int t = 0; t < 2; t++)
                {
                    var target = (RouteTarget)t;
                    long straight;
                    int best = int.MaxValue;
                    int bestCell = -1;
                    if (target == RouteTarget.Enemy)
                    {
                        straight = Gap(own, other);
                        var cells = CoarseCellsOf(other);
                        for (int y = cells.MinY; y <= cells.MaxY; y++)
                        {
                            for (int x = cells.MinX; x <= cells.MaxX; x++)
                            {
                                Consider(f, y * _columns + x, ref best, ref bestCell);
                            }
                        }
                    }
                    else
                    {
                        // The edge band a class can stand in: a wide class keeps its clearance from the map edge
                        // too, so its band is that many cells deep.
                        int edge = RetreatEdge(own, out straight);
                        int band = System.Math.Min(
                            _rules.Clearance(Classes[c]),
                            edge < 2 ? _columns : _rows
                        );
                        for (int depth = 0; depth < band; depth++)
                        {
                            for (int i = 0; i < (edge < 2 ? _rows : _columns); i++)
                            {
                                int cell = edge switch
                                {
                                    0 => i * _columns + depth,
                                    1 => i * _columns + _columns - 1 - depth,
                                    2 => depth * _columns + i,
                                    _ => (_rows - 1 - depth) * _columns + i,
                                };
                                Consider(f, cell, ref best, ref bestCell);
                            }
                        }
                    }
                    bool reached = bestCell >= 0;
                    long length = reached ? best * _cellMm / Straight : 0;
                    // The coarse grid can't draw a route closer than a couple of cells, so the cap allows two more.
                    long cap = straight * _rules.CapPercent / 100 + 2 * _cellMm;
                    int index = f * 2 + t;
                    checks[index] = new RouteCheck(side, Classes[c], target, reached, length, cap);
                    routes[index] = reached ? Trace(f, c, bestCell) : Array.Empty<FixVec2>();
                }
            }
            return new ReachabilityResult(checks, routes);
        }

        private void Consider(int fill, int cell, ref int best, ref int bestCell)
        {
            int d = _distance[fill][cell];
            if (d < best || (d == best && d != int.MaxValue && cell < bestCell))
            {
                best = d;
                bestCell = d == int.MaxValue ? -1 : cell;
            }
        }

        private RectMm Zone(Side side)
        {
            foreach (var zone in _map.DeploymentZones)
            {
                if (zone.Side == side)
                {
                    return zone.Area;
                }
            }
            throw new InvalidOperationException(
                "The reachability check needs both deployment zones; map "
                    + _map.Id
                    + " has no "
                    + side
                    + " zone."
            );
        }

        private CellRect CoarseCellsOf(RectMm area)
        {
            var fine = _terrain.CellsOf(area);
            return new CellRect(fine.MinX / _k, fine.MinY / _k, fine.MaxX / _k, fine.MaxY / _k);
        }

        // Shortest distance between two rectangles, in mm (0 when they touch or overlap).
        private static long Gap(RectMm a, RectMm b)
        {
            long dx = System.Math.Max(0, System.Math.Max(b.MinX - a.MaxX, a.MinX - b.MaxX));
            long dy = System.Math.Max(0, System.Math.Max(b.MinY - a.MaxY, a.MinY - b.MaxY));
            return IntMath.Sqrt((ulong)(dx * dx + dy * dy));
        }

        /// <summary>0 west, 1 east, 2 south, 3 north: the map edge nearest the zone (ties in that order).</summary>
        private int RetreatEdge(RectMm zone, out long gapMm)
        {
            long[] gaps =
            {
                zone.MinX,
                _map.WidthMm - zone.MaxX,
                zone.MinY,
                _map.DepthMm - zone.MaxY,
            };
            int edge = 0;
            for (int e = 1; e < 4; e++)
            {
                if (gaps[e] < gaps[edge])
                {
                    edge = e;
                }
            }
            gapMm = gaps[edge];
            return edge;
        }

        private bool Usable(int c, int cell) => _usable[c][cell];

        // Back from the target along exact predecessors (fixed neighbour order), then reversed: zone to target.
        private FixVec2[] Trace(int fill, int c, int cell)
        {
            var path = new List<int> { cell };
            var dist = _distance[fill];
            while (dist[cell] > 0)
            {
                int cx = cell % _columns;
                int cy = cell / _columns;
                int next = -1;
                for (int d = 0; d < 8 && next < 0; d++)
                {
                    var (dx, dy) = FlowField.Offset(d);
                    int w = StepCost(c, cx, cy, dx, dy);
                    if (w > 0 && dist[(cy + dy) * _columns + cx + dx] == dist[cell] - w)
                    {
                        next = (cy + dy) * _columns + cx + dx;
                    }
                }
                cell = next;
                path.Add(cell);
            }
            var route = new FixVec2[path.Count];
            for (int i = 0; i < route.Length; i++)
            {
                int p = path[route.Length - 1 - i];
                route[i] = new FixVec2(
                    Fix.FromRatio((p % _columns) * _cellMm + _cellMm / 2, 1000),
                    Fix.FromRatio((p / _columns) * _cellMm + _cellMm / 2, 1000)
                );
            }
            return route;
        }

        // 5 straight, 7 diagonal; 0 when off the map, not usable, or a diagonal that cuts a corner.
        private int StepCost(int c, int x, int y, int dx, int dy)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= _columns || ny >= _rows || !Usable(c, ny * _columns + nx))
            {
                return 0;
            }
            if (dx != 0 && dy != 0)
            {
                return Usable(c, y * _columns + nx) && Usable(c, ny * _columns + x) ? Diagonal : 0;
            }
            return Straight;
        }

        private void BuildClearance(int c)
        {
            // Chessboard distance to the nearest closed cell, the outside of the map counting as closed, capped at
            // the largest clearance any class needs (all that is ever compared).
            int cap = _rules.MaxClearance + 1;
            var open = _open[c];
            var clear = _clearance[c];
            for (int i = 0; i < clear.Length; i++)
            {
                clear[i] = open[i] ? cap : 0;
            }
            for (int y = 0; y < _rows; y++)
            {
                for (int x = 0; x < _columns; x++)
                {
                    int i = y * _columns + x;
                    int v = clear[i];
                    v = System.Math.Min(v, At(clear, x - 1, y) + 1);
                    v = System.Math.Min(v, At(clear, x - 1, y - 1) + 1);
                    v = System.Math.Min(v, At(clear, x, y - 1) + 1);
                    v = System.Math.Min(v, At(clear, x + 1, y - 1) + 1);
                    clear[i] = v;
                }
            }
            for (int y = _rows - 1; y >= 0; y--)
            {
                for (int x = _columns - 1; x >= 0; x--)
                {
                    int i = y * _columns + x;
                    int v = clear[i];
                    v = System.Math.Min(v, At(clear, x + 1, y) + 1);
                    v = System.Math.Min(v, At(clear, x + 1, y + 1) + 1);
                    v = System.Math.Min(v, At(clear, x, y + 1) + 1);
                    v = System.Math.Min(v, At(clear, x - 1, y + 1) + 1);
                    clear[i] = v;
                }
            }
            int need = _rules.Clearance(Classes[c]);
            var usable = _usable[c];
            for (int i = 0; i < clear.Length; i++)
            {
                usable[i] = clear[i] >= need;
            }
        }

        private int At(int[] grid, int x, int y) =>
            x < 0 || y < 0 || x >= _columns || y >= _rows ? 0 : grid[y * _columns + x];

        private void Fill(int f)
        {
            var side = f < Classes.Length ? Side.Attacker : Side.Defender;
            int c = f % Classes.Length;
            var dist = _distance[f];
            var usable = _usable[c];
            for (int i = 0; i < dist.Length; i++)
            {
                dist[i] = int.MaxValue;
            }
            // Dial's algorithm on a ring of 8 buckets (steps cost 5 or 7); each bucket is a stack, so the order cells
            // are finished in is fixed (it doesn't change the distances, which are unique).
            var buckets = _buckets[f];
            var counts = _bucketCount[f];
            Array.Clear(counts, 0, counts.Length);
            int queued = 0;
            var zone = CoarseCellsOf(Zone(side));
            for (int y = zone.MinY; y <= zone.MaxY; y++)
            {
                for (int x = zone.MinX; x <= zone.MaxX; x++)
                {
                    int i = y * _columns + x;
                    if (usable[i])
                    {
                        dist[i] = 0;
                        Push(buckets, counts, 0, i);
                        queued++;
                    }
                }
            }
            // Stop once both targets are finished: the enemy zone and the retreat band (their nearest cells are
            // final then, and every cell on their routes is nearer still).
            var enemy = CoarseCellsOf(Zone(side == Side.Attacker ? Side.Defender : Side.Attacker));
            int retreatEdge = RetreatEdge(Zone(side), out _);
            int band = _rules.Clearance(Classes[c]);
            bool enemyDone = false;
            bool retreatDone = false;
            int last = _columns - 1;
            int top = _rows - 1;
            for (int current = 0; queued > 0 && !(enemyDone && retreatDone); current++)
            {
                int b = current % buckets.Length;
                while (counts[b] > 0)
                {
                    int cell = buckets[b][--counts[b]];
                    queued--;
                    if (dist[cell] != current)
                    {
                        continue; // reached more cheaply after it was queued
                    }
                    int cx = cell % _columns;
                    int cy = cell / _columns;
                    enemyDone |=
                        cx >= enemy.MinX
                        && cx <= enemy.MaxX
                        && cy >= enemy.MinY
                        && cy <= enemy.MaxY;
                    retreatDone |= retreatEdge switch
                    {
                        0 => cx < band,
                        1 => cx > last - band,
                        2 => cy < band,
                        _ => cy > top - band,
                    };
                    bool interior = cx > 0 && cy > 0 && cx < last && cy < top;
                    for (int d = 0; d < 8; d++)
                    {
                        int n;
                        int w;
                        if (interior)
                        {
                            n = cell + _delta[d];
                            if (!usable[n])
                            {
                                continue;
                            }
                            if ((d & 1) == 0)
                            {
                                w = Straight;
                            }
                            else
                            {
                                var (dx, dy) = FlowField.Offset(d);
                                if (!usable[cell + dx] || !usable[cell + dy * _columns])
                                {
                                    continue;
                                }
                                w = Diagonal;
                            }
                        }
                        else
                        {
                            var (dx, dy) = FlowField.Offset(d);
                            w = StepCost(c, cx, cy, dx, dy);
                            if (w == 0)
                            {
                                continue;
                            }
                            n = (cy + dy) * _columns + cx + dx;
                        }
                        int nd = current + w;
                        if (nd < dist[n])
                        {
                            dist[n] = nd;
                            Push(buckets, counts, nd % buckets.Length, n);
                            queued++;
                        }
                    }
                }
            }
        }

        private static void Push(int[][] buckets, int[] counts, int bucket, int cell)
        {
            if (counts[bucket] == buckets[bucket].Length)
            {
                Array.Resize(ref buckets[bucket], buckets[bucket].Length * 2);
            }
            buckets[bucket][counts[bucket]++] = cell;
        }

        private readonly struct ClearanceKernel : IChunkKernel
        {
            private readonly Reachability _r;

            public ClearanceKernel(Reachability r) => _r = r;

            public void Execute(int start, int end)
            {
                for (int c = start; c < end; c++)
                {
                    _r.BuildClearance(c);
                }
            }
        }

        private readonly struct FillKernel : IChunkKernel
        {
            private readonly Reachability _r;

            public FillKernel(Reachability r) => _r = r;

            public void Execute(int start, int end)
            {
                for (int f = start; f < end; f++)
                {
                    _r.Fill(f);
                }
            }
        }
    }
}
