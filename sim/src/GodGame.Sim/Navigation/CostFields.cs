using System;
using System.Diagnostics;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// One cost field per movement class over the terrain's grid (tech notes, section 3): what crossing a cell costs a
    /// foot, horse, wheeled or water unit, relative to 100 for flat open ground, or <see cref="Impassable"/>. Flow
    /// fields read these. A cell's cost is its slope cost divided by the speed water and mud leave the class there
    /// (<see cref="FieldSpeed"/>), and water too deep to wade or fast enough to sweep the class is impassable; snow,
    /// debris and congestion join through the same rebuild later. The water class stays impassable everywhere.
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
        private int[]? _fieldCodes; // FieldSpeed codes per cell, or null for dry ground everywhere

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

        /// <summary>
        /// Counts rebuilds that touched at least one cell. A flow field repairs only across exactly one of them (it
        /// knows the dirty area of that one alone); after more, it rebuilds in full.
        /// </summary>
        public int RebuildCount { get; private set; }

        public Terrain Terrain => _terrain;

        /// <summary>Row-major costs for one class.</summary>
        public ReadOnlySpan<ushort> Of(MovementClass movementClass)
        {
            AssertFresh();
            return _costs[(int)movementClass];
        }

        /// <summary>Checks freshness once, for hot loops that then index the array directly.</summary>
        internal ushort[] FreshArray(MovementClass movementClass)
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

        /// <summary>
        /// The field speed codes later rebuilds read (the live <see cref="FieldSpeed.FieldId"/> values, or null for
        /// dry ground). Set before every rebuild: a restored world may hold another array.
        /// </summary>
        public void UseFieldCodes(int[]? codes)
        {
            if (codes != null && codes.Length != _terrain.CellCount)
            {
                throw new ArgumentException("One code per terrain cell.", nameof(codes));
            }
            _fieldCodes = codes;
        }

        /// <summary>A cell's cost with the field's speed code for the class (see <see cref="FieldSpeed"/>).</summary>
        public static ushort CellCost(MovementClassRules rules, int slopePermille, int code)
        {
            ushort dry = CellCost(rules, slopePermille);
            if (dry == Impassable || code == FieldSpeed.AllDryCode)
            {
                return dry;
            }
            int speed = FieldSpeed.Permille(code);
            if (speed == 0)
            {
                return Impassable;
            }
            long cost = (long)dry * 1000 / speed;
            return (ushort)System.Math.Min(cost, Impassable - 1);
        }

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
            // slope ≤ 100,000 ‰ and slopeCost ≤ 10,000 (loader limits), so the product fits a long; capped below
            // Impassable and at least 1, which flow-field repairs rely on (every step strictly costs something).
            long cost = rules.BaseCost + (long)rules.SlopeCost * slopePermille / 1000;
            return (ushort)System.Math.Max(1, System.Math.Min(cost, Impassable - 1));
        }

        public void RebuildAll(IParallelRunner runner) => Rebuild(_terrain.All, runner);

        public void Rebuild(CellRect area, IParallelRunner runner)
        {
            if (area.IsEmpty)
            {
                BuiltFromVersion = _terrain.Version;
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
            runner.For(
                area.Height,
                RowsPerChunk,
                new RowKernel(_terrain, _classes, _costs, _fieldCodes, area)
            );
            // The caller passes the area its edits dirtied (DirtyAround); after that the fields match the terrain.
            BuiltFromVersion = _terrain.Version;
            RebuildCount++;
        }

        private readonly struct RowKernel : IChunkKernel
        {
            private readonly Terrain _terrain;
            private readonly MovementClassRules[] _classes;
            private readonly ushort[][] _costs;
            private readonly int[]? _codes;
            private readonly CellRect _area;

            public RowKernel(
                Terrain terrain,
                MovementClassRules[] classes,
                ushort[][] costs,
                int[]? codes,
                CellRect area
            )
            {
                _terrain = terrain;
                _classes = classes;
                _costs = costs;
                _codes = codes;
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
                        int codes = _codes == null ? FieldSpeed.AllDry : _codes[i];
                        for (int c = 0; c < _classes.Length; c++)
                        {
                            _costs[c][i] = CellCost(
                                _classes[c],
                                slope,
                                FieldSpeed.Code(codes, _classes[c].MovementClass)
                            );
                        }
                    }
                }
            }
        }
    }
}
