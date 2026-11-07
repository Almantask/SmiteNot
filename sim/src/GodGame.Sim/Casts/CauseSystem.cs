using System;

namespace GodGame.Sim
{
    /// <summary>
    /// The bookkeeping step's share of the cause log (spec S1, section 12, D7): after every field step, water tags
    /// spread with the water. A tile whose net inflow from a neighbouring tile, over that step, is more than
    /// <see cref="InflowThresholdUm"/> of depth averaged over the tile takes the neighbour's tag if it is newer (a higher
    /// event index); with several candidates the newest wins (equal tags are the same cause, so no other tie-break). Every
    /// tile decides from the tags as they were before the step, so the tile order can't matter. Only tiles the water
    /// model has awake are considered; still water carries no new cause.
    /// </summary>
    public sealed class CauseSystem : ISimSystem
    {
        /// <summary>S1: 1,000 µm of depth, averaged over the receiving tile.</summary>
        public const int InflowThresholdUm = 1_000;

        private int[] _awake = new int[0];
        private int[] _before = new int[0];

        public TickStep Step => TickStep.Bookkeeping;

        public TickRate Rate => TickRate.EveryTick;

        public void Run(TickContext context)
        {
            var world = context.World;
            var log = world.Causes;
            var fields = world.Fields;
            if (
                log == null
                || fields == null
                || fields.Rules.IndexOf(WaterModel.Depth) < 0
                || !TickOrder.FieldsRate.IsDue(context.Tick)
            )
            {
                return;
            }
            var depth = fields.Get(WaterModel.Depth);
            var east = fields.Get(WaterModel.OutEast);
            var west = fields.Get(WaterModel.OutWest);
            var north = fields.Get(WaterModel.OutNorth);
            var south = fields.Get(WaterModel.OutSouth);
            if (_awake.Length < depth.TileCount)
            {
                _awake = new int[depth.TileCount];
            }
            if (_before.Length != log.TileTag.Length)
            {
                _before = new int[log.TileTag.Length];
            }
            Array.Copy(log.TileTag, _before, _before.Length);
            int awake = depth.AwakeTiles(_awake);
            // Outflows are µm/s of the source cell's depth; one field step moves outflow × period / ticks-per-second.
            long cells = (long)depth.TileCells * depth.TileCells;
            long threshold =
                InflowThresholdUm
                * cells
                * SimConstants.TicksPerSecond
                / TickOrder.FieldsRate.Period;
            for (int k = 0; k < awake; k++)
            {
                int tile = _awake[k];
                int tx = tile % depth.TileColumns;
                int ty = tile / depth.TileColumns;
                var r = depth.TileRect(tile);
                int best = _before[tile];
                // West, east, south, north neighbours, in that fixed order (lower tile indices first for ties).
                Consider(tx - 1, ty, r.MinY, r.MaxY, r.MinX - 1, r.MinX, east, west, true);
                Consider(tx + 1, ty, r.MinY, r.MaxY, r.MaxX + 1, r.MaxX, west, east, true);
                Consider(tx, ty - 1, r.MinX, r.MaxX, r.MinY - 1, r.MinY, north, south, false);
                Consider(tx, ty + 1, r.MinX, r.MaxX, r.MaxY + 1, r.MaxY, south, north, false);
                log.TileTag[tile] = best;

                // Net inflow across the shared edge: the neighbour's border cells sending in, less this tile's sending back.
                void Consider(
                    int nx,
                    int ny,
                    int from,
                    int to,
                    int outside,
                    int inside,
                    FieldGrid inward,
                    FieldGrid outward,
                    bool vertical
                )
                {
                    if (nx < 0 || ny < 0 || nx >= depth.TileColumns || ny >= depth.TileRows)
                    {
                        return;
                    }
                    int neighbour = ny * depth.TileColumns + nx;
                    int tag = _before[neighbour];
                    if (tag <= best)
                    {
                        return;
                    }
                    long inflow = 0;
                    for (int i = from; i <= to; i++)
                    {
                        inflow += vertical
                            ? (long)inward.Get(outside, i) - outward.Get(inside, i)
                            : (long)inward.Get(i, outside) - outward.Get(i, inside);
                    }
                    if (inflow > threshold)
                    {
                        best = tag;
                    }
                }
            }
        }
    }
}
