using System;
using System.Diagnostics;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// One cost field per movement class over the terrain's grid (tech notes, section 3): what crossing a cell costs a
    /// foot, horse, wheeled or water unit, relative to 100 for flat open ground, or <see cref="Impassable"/>. Flow
    /// fields read these. M2 costs come from slope alone; later milestones add mud, water depth, snow, debris and
    /// congestion through the same rebuild. The water class is impassable everywhere until water depth arrives (M3).
    /// <para>
    /// Derived state: built from the terrain and the ruleset, never carried across a change without a rebuild, so it
    /// needs no place in the state hash or in snapshots while heights never change during the battle (see docs/milestones/STATUS.md, M2 notes, for what changes when they do). Rebuilds are by rectangle (<see cref="Rebuild"/>), in parallel
    /// rows; each cell depends only on the terrain, so the result is the same on any runner.
    /// </para>
    /// </summary>
    public sealed class CostFields
    {
        public const ushort Impassable = ushort.MaxValue;

        /// <summary>Rows per parallel chunk.</summary>
        public const int RowsPerChunk = 16;

        private readonly Terrain _terrain;
        private readonly MovementClassRules[] _classes;
        private readonly ushort[][] _costs;

        /// <summary>Allocates the fields and builds them over the whole map.</summary>
        public CostFields(Terrain terrain, Ruleset rules, IParallelRunner runner)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }
            _classes = new MovementClassRules[rules.MovementClasses.Count];
            _costs = new ushort[_classes.Length][];
            for (int c = 0; c < _classes.Length; c++)
            {
                _classes[c] = rules.MovementClasses[c];
                _costs[c] = new ushort[terrain.CellCount];
            }
            RebuildAll(runner);
        }

        /// <summary>The terrain version the fields last caught up with; reads check it in Debug builds.</summary>
        public int BuiltFromVersion { get; private set; }

        public Terrain Terrain => _terrain;

        /// <summary>Row-major costs for one class.</summary>
        public ReadOnlySpan<ushort> Of(MovementClass movementClass)
        {
            AssertFresh();
            return _costs[(int)movementClass];
        }

        public ushort Cost(MovementClass movementClass, int x, int y)
        {
            AssertFresh();
            return _costs[(int)movementClass][_terrain.Index(x, y)];
        }

        [Conditional("DEBUG")]
        private void AssertFresh() =>
            Debug.Assert(
                BuiltFromVersion == _terrain.Version,
                "Cost fields read after a height edit without a rebuild (Terrain.DirtyAround, then Rebuild)."
            );

        /// <summary>The cost of one cell for one class from its slope; deterministic integer arithmetic.</summary>
        public static ushort CellCost(MovementClassRules rules, int slopePermille)
        {
            if (
                rules.MovementClass == MovementClass.Water
                || slopePermille > rules.MaxSlopePermille
            )
            {
                return Impassable;
            }
            // slope ≤ 100,000 ‰ and slopeCost ≤ 10,000 (loader limits), so the product fits a long; capped below Impassable.
            long cost = rules.BaseCost + (long)rules.SlopeCost * slopePermille / 1000;
            return (ushort)System.Math.Min(cost, Impassable - 1);
        }

        public void RebuildAll(IParallelRunner runner) => Rebuild(_terrain.All, runner);

        public void Rebuild(CellRect area, IParallelRunner runner)
        {
            if (area.IsEmpty)
            {
                return;
            }
            if (
                area.MinX < 0
                || area.MinY < 0
                || area.MaxX >= _terrain.Columns
                || area.MaxY >= _terrain.Rows
            )
            {
                throw new ArgumentOutOfRangeException(nameof(area));
            }
            runner.For(area.Height, RowsPerChunk, new RowKernel(_terrain, _classes, _costs, area));
            // The caller passes the area its edits dirtied (DirtyAround); after that the fields match the terrain.
            BuiltFromVersion = _terrain.Version;
        }

        private readonly struct RowKernel : IChunkKernel
        {
            private readonly Terrain _terrain;
            private readonly MovementClassRules[] _classes;
            private readonly ushort[][] _costs;
            private readonly CellRect _area;

            public RowKernel(
                Terrain terrain,
                MovementClassRules[] classes,
                ushort[][] costs,
                CellRect area
            )
            {
                _terrain = terrain;
                _classes = classes;
                _costs = costs;
                _area = area;
            }

            // Each chunk writes only its own rows of every class's array.
            public void Execute(int start, int end)
            {
                for (int row = start; row < end; row++)
                {
                    int y = _area.MinY + row;
                    for (int x = _area.MinX; x <= _area.MaxX; x++)
                    {
                        int slope = _terrain.SlopePermille(x, y);
                        int i = _terrain.Index(x, y);
                        for (int c = 0; c < _classes.Length; c++)
                        {
                            _costs[c][i] = CellCost(_classes[c], slope);
                        }
                    }
                }
            }
        }
    }
}
