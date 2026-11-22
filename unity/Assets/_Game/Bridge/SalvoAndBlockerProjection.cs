using System;
using System.Collections.Generic;
using GodGame.Sim;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Bridge
{
    /// <summary>One cell of a battle-made siege wreck ready for instanced rendering.</summary>
    public readonly struct WreckCellInstance
    {
        public WreckCellInstance(
            float worldX,
            float worldY,
            float worldZ,
            float yawRad,
            bool cleared,
            int blockerIndex,
            int eventIndex,
            int cellIndex
        )
        {
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
            YawRad = yawRad;
            Cleared = cleared;
            BlockerIndex = blockerIndex;
            EventIndex = eventIndex;
            CellIndex = cellIndex;
        }

        public float WorldX { get; }
        public float WorldY { get; }
        public float WorldZ { get; }
        public float YawRad { get; }
        public bool Cleared { get; }
        public int BlockerIndex { get; }
        public int EventIndex { get; }
        public int CellIndex { get; }
    }

    /// <summary>One siege stone impact recorded by <see cref="CauseKind.StoneLanded"/> and linked crush casualties.</summary>
    public readonly struct StoneLandingInstance
    {
        public StoneLandingInstance(
            int eventIndex,
            int tick,
            int firingRegiment,
            int cellIndex,
            float worldX,
            float worldY,
            float worldZ,
            int crushedCount,
            float ageSeconds
        )
        {
            EventIndex = eventIndex;
            Tick = tick;
            FiringRegiment = firingRegiment;
            CellIndex = cellIndex;
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
            CrushedCount = crushedCount;
            AgeSeconds = ageSeconds;
        }

        public int EventIndex { get; }
        public int Tick { get; }
        public int FiringRegiment { get; }
        public int CellIndex { get; }
        public float WorldX { get; }
        public float WorldY { get; }
        public float WorldZ { get; }
        public int CrushedCount { get; }
        public float AgeSeconds { get; }
    }

    /// <summary>One standing or falling map tree projected from <see cref="MapData.Trees"/> and <see cref="StrikeSnapshot"/>.</summary>
    public readonly struct TreePropInstance
    {
        public TreePropInstance(
            int treeIndex,
            float worldX,
            float worldY,
            float worldZ,
            float scale,
            float yawRad,
            float fallProgress,
            float heightM
        )
        {
            TreeIndex = treeIndex;
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
            Scale = scale;
            YawRad = yawRad;
            FallProgress = fallProgress;
            HeightM = heightM;
        }

        public int TreeIndex { get; }
        public float WorldX { get; }
        public float WorldY { get; }
        public float WorldZ { get; }
        public float Scale { get; }
        public float YawRad { get; }
        public float FallProgress { get; }
        public float HeightM { get; }
    }

    /// <summary>One felled tree trunk + crown projected from a <see cref="BlockerKind.FelledTree"/> blocker.</summary>
    public readonly struct FelledTreeInstance
    {
        public FelledTreeInstance(
            int blockerIndex,
            int eventIndex,
            int treeIndex,
            float worldX,
            float worldY,
            float worldZ,
            float scale,
            float yawRad,
            bool cleared
        )
        {
            BlockerIndex = blockerIndex;
            EventIndex = eventIndex;
            TreeIndex = treeIndex;
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
            Scale = scale;
            YawRad = yawRad;
            Cleared = cleared;
        }

        public int BlockerIndex { get; }
        public int EventIndex { get; }
        public int TreeIndex { get; }
        public float WorldX { get; }
        public float WorldY { get; }
        public float WorldZ { get; }
        public float Scale { get; }
        public float YawRad { get; }
        public bool Cleared { get; }
    }

    /// <summary>One cell of a burning fire patch projected from <see cref="BlockerKind.Fire"/> and <see cref="StrikeSnapshot"/>.</summary>
    public readonly struct FireCellInstance
    {
        public FireCellInstance(
            int blockerIndex,
            int cellIndex,
            int eventIndex,
            float worldX,
            float worldY,
            float worldZ,
            float scale,
            float phase,
            bool extinguished
        )
        {
            BlockerIndex = blockerIndex;
            CellIndex = cellIndex;
            EventIndex = eventIndex;
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
            Scale = scale;
            Phase = phase;
            Extinguished = extinguished;
        }

        public int BlockerIndex { get; }
        public int CellIndex { get; }
        public int EventIndex { get; }
        public float WorldX { get; }
        public float WorldY { get; }
        public float WorldZ { get; }
        public float Scale { get; }
        public float Phase { get; }
        public bool Extinguished { get; }
    }

    /// <summary>One lightning bolt impact projected from <see cref="StrikeSnapshot"/> or <see cref="CauseKind.LightningStruck"/>.</summary>
    public readonly struct LightningStrikeInstance
    {
        public LightningStrikeInstance(
            int eventIndex,
            int tick,
            float worldX,
            float worldY,
            float worldZ,
            float ageSeconds
        )
        {
            EventIndex = eventIndex;
            Tick = tick;
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
            AgeSeconds = ageSeconds;
        }

        public int EventIndex { get; }
        public int Tick { get; }
        public float WorldX { get; }
        public float WorldY { get; }
        public float WorldZ { get; }
        public float AgeSeconds { get; }
    }

    /// <summary>Live or scrubbed herd ring/chevron state projected from <see cref="HerdSnapshot"/> and unit positions.</summary>
    public readonly struct HerdMarkerInstance
    {
        public HerdMarkerInstance(
            int herdIndex,
            int regimentIndex,
            int drivenByRegiment,
            HerdMode mode,
            int fear,
            float headingRad,
            float yawRad,
            float worldX,
            float worldY,
            float worldZ,
            float radiusM
        )
        {
            HerdIndex = herdIndex;
            RegimentIndex = regimentIndex;
            DrivenByRegiment = drivenByRegiment;
            Mode = mode;
            Fear = fear;
            HeadingRad = headingRad;
            YawRad = yawRad;
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
            RadiusM = radiusM;
        }

        public int HerdIndex { get; }
        public int RegimentIndex { get; }
        public int DrivenByRegiment { get; }
        public HerdMode Mode { get; }
        public int Fear { get; }
        public float HeadingRad { get; }
        public float YawRad { get; }
        public float WorldX { get; }
        public float WorldY { get; }
        public float WorldZ { get; }
        public float RadiusM { get; }
    }

    /// <summary>One placed boulder prop projected from <see cref="MapData.Boulders"/>.</summary>
    public readonly struct BoulderPropInstance
    {
        public BoulderPropInstance(
            int boulderIndex,
            float worldX,
            float worldY,
            float worldZ,
            float scale,
            float yawRad
        )
        {
            BoulderIndex = boulderIndex;
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
            Scale = scale;
            YawRad = yawRad;
        }

        public int BoulderIndex { get; }
        public float WorldX { get; }
        public float WorldY { get; }
        public float WorldZ { get; }
        public float Scale { get; }
        public float YawRad { get; }
    }

    /// <summary>One 1.5 m hedgerow or drystone-wall segment projected along a <see cref="PieceKind.Line"/> polyline.</summary>
    public readonly struct LineBarrierInstance
    {
        public LineBarrierInstance(
            int pieceIndex,
            bool isWall,
            float worldX,
            float worldY,
            float worldZ,
            float scaleX,
            float scaleY,
            float yawRad
        )
        {
            PieceIndex = pieceIndex;
            IsWall = isWall;
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
            ScaleX = scaleX;
            ScaleY = scaleY;
            YawRad = yawRad;
        }

        public int PieceIndex { get; }
        public bool IsWall { get; }
        public float WorldX { get; }
        public float WorldY { get; }
        public float WorldZ { get; }
        public float ScaleX { get; }
        public float ScaleY { get; }
        public float YawRad { get; }
    }

    /// <summary>One rustic herd pen fence/gate section projected around a <see cref="MapData.Herds"/> grazing circle.</summary>
    public readonly struct HerdPenPropInstance
    {
        public HerdPenPropInstance(
            int herdIndex,
            float worldX,
            float worldY,
            float worldZ,
            float scale,
            float yawRad
        )
        {
            HerdIndex = herdIndex;
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
            Scale = scale;
            YawRad = yawRad;
        }

        public int HerdIndex { get; }
        public float WorldX { get; }
        public float WorldY { get; }
        public float WorldZ { get; }
        public float Scale { get; }
        public float YawRad { get; }
    }

    /// <summary>One active or winding-up Rain cast ready for falling rain curtain rendering.</summary>
    public readonly struct RainCastInstance
    {
        public RainCastInstance(
            int castIndex,
            float worldX,
            float worldY,
            float worldZ,
            float radiusM,
            float activeIntensity,
            float windUpProgress,
            float windXMps,
            float windZMps
        )
        {
            CastIndex = castIndex;
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
            RadiusM = radiusM;
            ActiveIntensity = activeIntensity;
            WindUpProgress = windUpProgress;
            WindXMps = windXMps;
            WindZMps = windZMps;
        }

        public int CastIndex { get; }
        public float WorldX { get; }
        public float WorldY { get; }
        public float WorldZ { get; }
        public float RadiusM { get; }
        public float ActiveIntensity { get; }
        public float WindUpProgress { get; }
        public float WindXMps { get; }
        public float WindZMps { get; }
    }

    /// <summary>One mid-air siege boulder projected along its ballistic arc before a <see cref="CauseKind.StoneLanded"/> tick.</summary>
    public readonly struct FlyingStoneInstance
    {
        public FlyingStoneInstance(
            int eventIndex,
            float worldX,
            float worldY,
            float worldZ,
            float spinRad,
            float progress,
            float scale
        )
        {
            EventIndex = eventIndex;
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
            SpinRad = spinRad;
            Progress = progress;
            Scale = scale;
        }

        public int EventIndex { get; }
        public float WorldX { get; }
        public float WorldY { get; }
        public float WorldZ { get; }
        public float SpinRad { get; }
        public float Progress { get; }
        public float Scale { get; }
    }

    /// <summary>One mid-air arrow in a ranged regiment's volley arc projected from <see cref="CauseKind.SoldierKilled"/> events.</summary>
    public readonly struct ArrowVolleyInstance
    {
        public ArrowVolleyInstance(
            float worldX,
            float worldY,
            float worldZ,
            float dirX,
            float dirY,
            float dirZ,
            float progress
        )
        {
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
            DirX = dirX;
            DirY = dirY;
            DirZ = dirZ;
            Progress = progress;
        }

        public float WorldX { get; }
        public float WorldY { get; }
        public float WorldZ { get; }
        public float DirX { get; }
        public float DirY { get; }
        public float DirZ { get; }
        public float Progress { get; }
    }

    /// <summary>One live regiment's heraldic banner/standard projected from unit positions, morale and wind.</summary>
    public readonly struct RegimentBannerInstance
    {
        public RegimentBannerInstance(
            int regimentIndex,
            Side side,
            int emblemKind,
            float worldX,
            float worldY,
            float worldZ,
            float morale01,
            float strength01,
            bool routing,
            float windYawRad
        )
        {
            RegimentIndex = regimentIndex;
            Side = side;
            EmblemKind = emblemKind;
            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
            Morale01 = morale01;
            Strength01 = strength01;
            Routing = routing;
            WindYawRad = windYawRad;
        }

        public int RegimentIndex { get; }
        public Side Side { get; }
        public int EmblemKind { get; }
        public float WorldX { get; }
        public float WorldY { get; }
        public float WorldZ { get; }
        public float Morale01 { get; }
        public float Strength01 { get; }
        public bool Routing { get; }
        public float WindYawRad { get; }
    }

    /// <summary>
    /// Pure bridge projections from <see cref="BlockerSnapshot"/> and <see cref="CauseEvent"/> logs into world-space
    /// draw records for both live (<see cref="BattleHost"/>) and scrubbable (<see cref="PreviewWorker"/>) views.
    /// </summary>
    public static class SalvoAndBlockerProjection
    {
        /// <summary>Collects active (and optionally cleared) wreck cells at <paramref name="atTick"/>.</summary>
        public static void CollectWrecks(
            BlockerSnapshot blockers,
            CauseEvent[] causes,
            MapData map,
            Terrain terrain,
            int atTick,
            bool filterByTime,
            bool includeCleared,
            List<WreckCellInstance> output
        )
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            output.Clear();
            if (blockers == null || blockers.Count == 0 || map == null)
            {
                return;
            }

            int totalCells = map.Columns * map.Rows;
            float cellM = map.CellSizeMm / 1000f;
            for (int b = 0; b < blockers.Count; b++)
            {
                if (blockers.Kind[b] != BlockerKind.Wreck)
                {
                    continue;
                }

                int evIdx = blockers.EventIndex[b];
                if (filterByTime && causes != null && (uint)evIdx < (uint)causes.Length)
                {
                    if (causes[evIdx].Tick > atTick)
                    {
                        continue;
                    }
                }

                bool cleared = filterByTime
                    ? IsClearedAtTick(causes, b, atTick)
                    : blockers.Cleared[b];
                if (cleared && !includeCleared)
                {
                    continue;
                }

                int[] cells = blockers.Cells[b];
                if (cells == null || cells.Length == 0)
                {
                    continue;
                }

                for (int k = 0; k < cells.Length; k++)
                {
                    int cell = cells[k];
                    if ((uint)cell >= (uint)totalCells)
                    {
                        continue;
                    }
                    CellWorldCenter(
                        map,
                        terrain,
                        cell,
                        cellM,
                        out float wx,
                        out float wy,
                        out float wz
                    );
                    // Spread beam orientation slightly per cell/blocker for a tumbled wreck silhouette.
                    float yaw = ((b * 37 + k * 19) & 15) * (MathF.PI * 2f / 16f);
                    output.Add(new WreckCellInstance(wx, wy, wz, yaw, cleared, b, evIdx, cell));
                }
            }
        }

        /// <summary>
        /// Collects <see cref="CauseKind.StoneLanded"/> impacts at or before <paramref name="atTick"/> within
        /// <paramref name="windowSeconds"/>, attaching linked <see cref="CauseKind.SoldierCrushed"/> counts.
        /// </summary>
        public static void CollectStoneLandings(
            CauseEvent[] causes,
            MapData map,
            Terrain terrain,
            int atTick,
            float windowSeconds,
            List<StoneLandingInstance> output,
            Dictionary<int, int> scratchCrushCounts = null
        )
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            output.Clear();
            if (causes == null || causes.Length == 0 || map == null)
            {
                return;
            }

            var crushCounts = scratchCrushCounts ?? new Dictionary<int, int>();
            crushCounts.Clear();
            for (int i = 0; i < causes.Length; i++)
            {
                CauseEvent e = causes[i];
                if (e.Tick > atTick)
                {
                    continue;
                }
                if (e.Kind == CauseKind.SoldierCrushed && e.Cause >= 0)
                {
                    crushCounts.TryGetValue(e.Cause, out int prev);
                    crushCounts[e.Cause] = prev + 1;
                }
            }

            int totalCells = map.Columns * map.Rows;
            float cellM = map.CellSizeMm / 1000f;
            float invTickRate = 1f / SimConstants.TicksPerSecond;
            bool infiniteWindow = float.IsPositiveInfinity(windowSeconds);

            for (int i = 0; i < causes.Length; i++)
            {
                CauseEvent e = causes[i];
                if (e.Kind != CauseKind.StoneLanded || e.Tick > atTick)
                {
                    continue;
                }
                float ageSeconds = (atTick - e.Tick) * invTickRate;
                if (!infiniteWindow && ageSeconds > windowSeconds)
                {
                    continue;
                }
                int cell = e.Detail;
                if ((uint)cell >= (uint)totalCells)
                {
                    continue;
                }
                CellWorldCenter(
                    map,
                    terrain,
                    cell,
                    cellM,
                    out float wx,
                    out float wy,
                    out float wz
                );
                crushCounts.TryGetValue(i, out int crushed);
                output.Add(
                    new StoneLandingInstance(
                        i,
                        e.Tick,
                        e.Subject,
                        cell,
                        wx,
                        wy,
                        wz,
                        crushed,
                        ageSeconds
                    )
                );
            }
        }

        /// <summary>Collects standing and currently falling map trees at <paramref name="atTick"/>.</summary>
        public static void CollectTrees(
            MapData map,
            Terrain terrain,
            StrikeSnapshot strikes,
            CauseEvent[] causes,
            int atTick,
            bool filterByTime,
            int fallTicks,
            List<TreePropInstance> output
        )
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            output.Clear();
            if (map == null || map.Trees == null || map.Trees.Count == 0)
            {
                return;
            }

            int totalCells = map.Columns * map.Rows;
            float cellM = map.CellSizeMm / 1000f;
            int fallDuration = Math.Max(1, fallTicks);

            for (int t = 0; t < map.Trees.Count; t++)
            {
                TreeSpec tree = map.Trees[t];
                if ((uint)tree.Cell >= (uint)totalCells)
                {
                    continue;
                }

                if (HasTreeFelledCauseAtTick(causes, t, atTick, filterByTime))
                {
                    continue;
                }

                int fallIdx = FindFallForTree(strikes, t);
                float fallProgress = 0f;
                float yawRad = (tree.Index * 2.3999632f) % ((float)Math.PI * 2f);

                if (fallIdx >= 0 && strikes.FallTick[fallIdx] <= atTick)
                {
                    if (!filterByTime && strikes.FallLanded[fallIdx])
                    {
                        continue;
                    }
                    int elapsed = atTick - strikes.FallTick[fallIdx];
                    if (filterByTime && strikes.FallLanded[fallIdx] && elapsed >= fallDuration)
                    {
                        continue;
                    }
                    fallProgress = Math.Min(1f, Math.Max(0f, elapsed / (float)fallDuration));
                    yawRad = HeadingToUnityYawRad(strikes.FallHeadingUnits[fallIdx]);
                }

                CellWorldCenter(
                    map,
                    terrain,
                    tree.Cell,
                    cellM,
                    out float wx,
                    out float wy,
                    out float wz
                );
                float heightM = Math.Max(1f, tree.HeightMm / 1000f);
                float scale = Math.Max(0.25f, tree.HeightMm / 14_000f);
                output.Add(
                    new TreePropInstance(t, wx, wy, wz, scale, yawRad, fallProgress, heightM)
                );
            }
        }

        /// <summary>Collects landed felled trees (<see cref="BlockerKind.FelledTree"/>) at <paramref name="atTick"/>.</summary>
        public static void CollectFelledTrees(
            BlockerSnapshot blockers,
            MapData map,
            Terrain terrain,
            StrikeSnapshot strikes,
            CauseEvent[] causes,
            int atTick,
            bool filterByTime,
            bool includeCleared,
            List<FelledTreeInstance> output
        )
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            output.Clear();
            if (blockers == null || map == null)
            {
                return;
            }

            int totalCells = map.Columns * map.Rows;
            float cellM = map.CellSizeMm / 1000f;

            for (int b = 0; b < blockers.Count; b++)
            {
                if (blockers.Kind[b] != BlockerKind.FelledTree)
                {
                    continue;
                }
                int ev = blockers.EventIndex[b];
                if (
                    filterByTime
                    && causes != null
                    && (uint)ev < (uint)causes.Length
                    && causes[ev].Tick > atTick
                )
                {
                    continue;
                }

                bool cleared = filterByTime
                    ? IsClearedAtTick(causes, b, atTick)
                    : blockers.Cleared[b];
                if (cleared && !includeCleared)
                {
                    continue;
                }

                int[] cells = blockers.Cells[b];
                byte[] profile = blockers.Profile[b];
                if (cells == null || cells.Length == 0)
                {
                    continue;
                }

                int treeIndex = -1;
                if (
                    causes != null
                    && (uint)ev < (uint)causes.Length
                    && causes[ev].Kind == CauseKind.TreeFelled
                )
                {
                    treeIndex = causes[ev].Subject;
                }

                float wx;
                float wy;
                float wz;
                float scale = 1f;
                float yawRad = (b * 1.11f) % ((float)Math.PI * 2f) - (float)Math.PI * 0.5f;

                if (
                    treeIndex >= 0
                    && map.Trees != null
                    && treeIndex < map.Trees.Count
                    && (uint)map.Trees[treeIndex].Cell < (uint)totalCells
                )
                {
                    TreeSpec tree = map.Trees[treeIndex];
                    CellWorldCenter(map, terrain, tree.Cell, cellM, out wx, out wy, out wz);
                    scale = Math.Max(0.25f, tree.HeightMm / 14_000f);
                    int fallIdx = FindFallForTree(strikes, treeIndex);
                    if (fallIdx >= 0)
                    {
                        yawRad = HeadingToUnityYawRad(strikes.FallHeadingUnits[fallIdx]);
                    }
                    else
                    {
                        TryDirectionFromTrunkToCrown(
                            map,
                            terrain,
                            cells,
                            profile,
                            totalCells,
                            cellM,
                            out _,
                            out _,
                            out _,
                            ref yawRad
                        );
                    }
                }
                else if (
                    TryDirectionFromTrunkToCrown(
                        map,
                        terrain,
                        cells,
                        profile,
                        totalCells,
                        cellM,
                        out wx,
                        out wy,
                        out wz,
                        ref yawRad
                    )
                )
                {
                    // Derived base position and yaw directly from trunk/crown blocker cells.
                }
                else
                {
                    continue;
                }

                output.Add(
                    new FelledTreeInstance(b, ev, treeIndex, wx, wy, wz, scale, yawRad, cleared)
                );
            }
        }

        /// <summary>Collects active fire patch cells (<see cref="BlockerKind.Fire"/>) at <paramref name="atTick"/>.</summary>
        public static void CollectFires(
            BlockerSnapshot blockers,
            MapData map,
            Terrain terrain,
            StrikeSnapshot strikes,
            CauseEvent[] causes,
            int atTick,
            bool filterByTime,
            bool includeExtinguished,
            List<FireCellInstance> output
        )
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            output.Clear();
            if (blockers == null || map == null)
            {
                return;
            }

            int totalCells = map.Columns * map.Rows;
            float cellM = map.CellSizeMm / 1000f;
            float scale = Math.Max(0.5f, cellM / 1.45f);

            for (int b = 0; b < blockers.Count; b++)
            {
                if (blockers.Kind[b] != BlockerKind.Fire)
                {
                    continue;
                }
                int ev = blockers.EventIndex[b];
                if (
                    filterByTime
                    && causes != null
                    && (uint)ev < (uint)causes.Length
                    && causes[ev].Tick > atTick
                )
                {
                    continue;
                }

                int fireIdx = FindFireForBlocker(strikes, b);
                bool extinguished = filterByTime
                    ? IsClearedAtTick(causes, b, atTick)
                    : blockers.Cleared[b];
                if (fireIdx >= 0)
                {
                    if (!filterByTime && strikes.FireOut[fireIdx])
                    {
                        extinguished = true;
                    }
                    else if (strikes.FireEnds[fireIdx] > 0 && atTick >= strikes.FireEnds[fireIdx])
                    {
                        extinguished = true;
                    }
                }
                if (extinguished && !includeExtinguished)
                {
                    continue;
                }

                int[] cells = blockers.Cells[b];
                if (cells == null)
                {
                    continue;
                }
                for (int i = 0; i < cells.Length; i++)
                {
                    int cell = cells[i];
                    if ((uint)cell >= (uint)totalCells)
                    {
                        continue;
                    }
                    CellWorldCenter(
                        map,
                        terrain,
                        cell,
                        cellM,
                        out float wx,
                        out float wy,
                        out float wz
                    );
                    float phase = (b * 0.618034f + cell * 0.271828f) % 1f;
                    output.Add(
                        new FireCellInstance(b, cell, ev, wx, wy, wz, scale, phase, extinguished)
                    );
                }
            }
        }

        /// <summary>Collects recent lightning strikes visible at <paramref name="atTick"/>.</summary>
        public static void CollectLightningStrikes(
            StrikeSnapshot strikes,
            CauseEvent[] causes,
            MapData map,
            Terrain terrain,
            int atTick,
            float windowSeconds,
            List<LightningStrikeInstance> output
        )
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            output.Clear();
            if (map == null)
            {
                return;
            }

            float invTickRate = 1f / SimConstants.TicksPerSecond;
            bool infiniteWindow = float.IsPositiveInfinity(windowSeconds);

            if (strikes != null && strikes.StrikeCount > 0)
            {
                for (int i = 0; i < strikes.StrikeCount; i++)
                {
                    int tick = strikes.StrikeTick[i];
                    if (tick > atTick)
                    {
                        continue;
                    }
                    float age = (atTick - tick) * invTickRate;
                    if (!infiniteWindow && age > windowSeconds)
                    {
                        continue;
                    }
                    int xMm = strikes.StrikeXMm[i];
                    int yMm = strikes.StrikeYMm[i];
                    float wx = xMm / 1000f;
                    float wz = yMm / 1000f;
                    float wy = WorldYAtMm(map, terrain, xMm, yMm);
                    output.Add(
                        new LightningStrikeInstance(strikes.StrikeEvent[i], tick, wx, wy, wz, age)
                    );
                }
                return;
            }

            if (causes == null)
            {
                return;
            }
            int totalCells = map.Columns * map.Rows;
            float cellM = map.CellSizeMm / 1000f;
            for (int i = 0; i < causes.Length; i++)
            {
                CauseEvent e = causes[i];
                if (e.Kind != CauseKind.LightningStruck || e.Tick > atTick)
                {
                    continue;
                }
                float age = (atTick - e.Tick) * invTickRate;
                if (!infiniteWindow && age > windowSeconds)
                {
                    continue;
                }
                int cell = e.Detail;
                if ((uint)cell >= (uint)totalCells)
                {
                    continue;
                }
                CellWorldCenter(
                    map,
                    terrain,
                    cell,
                    cellM,
                    out float wx,
                    out float wy,
                    out float wz
                );
                output.Add(new LightningStrikeInstance(i, e.Tick, wx, wy, wz, age));
            }
        }

        /// <summary>Collects live herd centroids, bounding rings, and heading indicators.</summary>
        public static void CollectHerdMarkers(
            HerdSnapshot herds,
            BattleHost.UnitSnapshot units,
            MapData map,
            Terrain terrain,
            CauseEvent[] causes,
            int atTick,
            bool filterByTime,
            List<HerdMarkerInstance> output
        )
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            output.Clear();
            if (herds == null || herds.Count == 0 || units == null || map == null)
            {
                return;
            }

            int n = herds.Count;
            var sumX = new float[n];
            var sumZ = new float[n];
            var minX = new float[n];
            var maxX = new float[n];
            var minZ = new float[n];
            var maxZ = new float[n];
            var alive = new int[n];
            for (int h = 0; h < n; h++)
            {
                minX[h] = float.MaxValue;
                maxX[h] = float.MinValue;
                minZ[h] = float.MaxValue;
                maxZ[h] = float.MinValue;
            }

            for (int u = 0; u < units.Count; u++)
            {
                if (!units.Alive[u])
                {
                    continue;
                }
                int reg = units.Regiment[u];
                for (int h = 0; h < n; h++)
                {
                    if (herds.Regiment[h] != reg)
                    {
                        continue;
                    }
                    float x = units.X[u];
                    float z = units.Z[u];
                    sumX[h] += x;
                    sumZ[h] += z;
                    if (x < minX[h])
                        minX[h] = x;
                    if (x > maxX[h])
                        maxX[h] = x;
                    if (z < minZ[h])
                        minZ[h] = z;
                    if (z > maxZ[h])
                        maxZ[h] = z;
                    alive[h]++;
                    break;
                }
            }

            EmitHerdMarkers(
                herds,
                map,
                terrain,
                causes,
                atTick,
                filterByTime,
                sumX,
                sumZ,
                minX,
                maxX,
                minZ,
                maxZ,
                alive,
                output
            );
        }

        /// <summary>Collects timeline-scrubbed herd centroids, bounding rings, and heading indicators.</summary>
        public static void CollectHerdMarkers(
            HerdSnapshot herds,
            int[] regimentOfUnit,
            UnitSample sample,
            MapData map,
            Terrain terrain,
            CauseEvent[] causes,
            int atTick,
            bool filterByTime,
            List<HerdMarkerInstance> output
        )
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            output.Clear();
            if (
                herds == null
                || herds.Count == 0
                || regimentOfUnit == null
                || sample == null
                || map == null
            )
            {
                return;
            }

            int n = herds.Count;
            var sumX = new float[n];
            var sumZ = new float[n];
            var minX = new float[n];
            var maxX = new float[n];
            var minZ = new float[n];
            var maxZ = new float[n];
            var alive = new int[n];
            for (int h = 0; h < n; h++)
            {
                minX[h] = float.MaxValue;
                maxX[h] = float.MinValue;
                minZ[h] = float.MaxValue;
                maxZ[h] = float.MinValue;
            }

            int unitCount = Math.Min(sample.Count, regimentOfUnit.Length);
            for (int u = 0; u < unitCount; u++)
            {
                if ((sample.State[u] & UnitSample.Alive) == 0)
                {
                    continue;
                }
                int reg = regimentOfUnit[u];
                for (int h = 0; h < n; h++)
                {
                    if (herds.Regiment[h] != reg)
                    {
                        continue;
                    }
                    float x = sample.XCm[u] / 100f;
                    float z = sample.YCm[u] / 100f;
                    sumX[h] += x;
                    sumZ[h] += z;
                    if (x < minX[h])
                        minX[h] = x;
                    if (x > maxX[h])
                        maxX[h] = x;
                    if (z < minZ[h])
                        minZ[h] = z;
                    if (z > maxZ[h])
                        maxZ[h] = z;
                    alive[h]++;
                    break;
                }
            }

            EmitHerdMarkers(
                herds,
                map,
                terrain,
                causes,
                atTick,
                filterByTime,
                sumX,
                sumZ,
                minX,
                maxX,
                minZ,
                maxZ,
                alive,
                output
            );
        }

        /// <summary>
        /// Converts a simulation angle unit (0 = +X, counter-clockwise toward +Z) into the <c>RotateY</c> yaw in radians
        /// used by <c>DebugMarkers.shader</c> where unrotated local +Z maps to the simulation heading.
        /// </summary>
        public static float HeadingToUnityYawRad(int headingUnits)
        {
            float thetaRad = HeadingToSimRad(headingUnits);
            return thetaRad - (float)Math.PI * 0.5f;
        }

        private static float HeadingToSimRad(int headingUnits)
        {
            int wrapped = headingUnits % Angle.UnitsPerTurn;
            if (wrapped < 0)
            {
                wrapped += Angle.UnitsPerTurn;
            }
            return wrapped * ((float)Math.PI * 2f / Angle.UnitsPerTurn);
        }

        private static void EmitHerdMarkers(
            HerdSnapshot herds,
            MapData map,
            Terrain terrain,
            CauseEvent[] causes,
            int atTick,
            bool filterByTime,
            float[] sumX,
            float[] sumZ,
            float[] minX,
            float[] maxX,
            float[] minZ,
            float[] maxZ,
            int[] alive,
            List<HerdMarkerInstance> output
        )
        {
            for (int h = 0; h < herds.Count; h++)
            {
                if (alive[h] == 0)
                {
                    continue;
                }
                float cx = sumX[h] / alive[h];
                float cz = sumZ[h] / alive[h];
                int cxMm = (int)Math.Round(cx * 1000f);
                int czMm = (int)Math.Round(cz * 1000f);
                float cy = WorldYAtMm(map, terrain, cxMm, czMm);

                float halfW = (maxX[h] - minX[h]) * 0.5f;
                float halfD = (maxZ[h] - minZ[h]) * 0.5f;
                float radius = Math.Max(3f, (float)Math.Sqrt(halfW * halfW + halfD * halfD) + 1.2f);

                HerdMode mode = ResolveHerdAtTick(
                    herds,
                    causes,
                    h,
                    atTick,
                    filterByTime,
                    out int headingUnits,
                    out int fear
                );

                output.Add(
                    new HerdMarkerInstance(
                        h,
                        herds.Regiment[h],
                        herds.DrivenBy[h],
                        mode,
                        fear,
                        HeadingToSimRad(headingUnits),
                        HeadingToUnityYawRad(headingUnits),
                        cx,
                        cy,
                        cz,
                        radius
                    )
                );
            }
        }

        /// <summary>Resolves herd mode, heading units, and fear at <paramref name="atTick"/> from cause events when scrubbing.</summary>
        public static HerdMode ResolveHerdAtTick(
            HerdSnapshot herds,
            CauseEvent[] causes,
            int herdIndex,
            int atTick,
            bool filterByTime,
            out int headingUnits,
            out int fear
        )
        {
            HerdMode mode = herds.Mode[herdIndex];
            fear = herds.Fear[herdIndex];
            headingUnits = herds.HeadingUnits[herdIndex];
            if (!filterByTime)
            {
                return mode;
            }
            mode = HerdMode.Grazing;
            fear = 0;
            if (causes != null)
            {
                for (int i = 0; i < causes.Length; i++)
                {
                    CauseEvent e = causes[i];
                    if (e.Tick > atTick || e.Subject != herdIndex)
                    {
                        continue;
                    }
                    if (e.Kind == CauseKind.HerdAgitated)
                    {
                        mode = HerdMode.Agitated;
                        fear = Math.Max(fear, 500);
                    }
                    else if (e.Kind == CauseKind.HerdStampeded)
                    {
                        mode = HerdMode.Stampede;
                        headingUnits = e.Detail;
                        fear = Math.Max(fear, 1000);
                    }
                    else if (e.Kind == CauseKind.HerdScattered)
                    {
                        mode = HerdMode.Scattered;
                    }
                }
            }
            return mode;
        }

        private static bool HasTreeFelledCauseAtTick(
            CauseEvent[] causes,
            int treeIndex,
            int atTick,
            bool filterByTime
        )
        {
            if (causes == null)
            {
                return false;
            }
            for (int i = 0; i < causes.Length; i++)
            {
                CauseEvent e = causes[i];
                if (e.Kind != CauseKind.TreeFelled || e.Subject != treeIndex)
                {
                    continue;
                }
                if (!filterByTime || e.Tick <= atTick)
                {
                    return true;
                }
            }
            return false;
        }

        private static int FindFallForTree(StrikeSnapshot strikes, int treeIndex)
        {
            if (strikes == null)
            {
                return -1;
            }
            for (int i = 0; i < strikes.FallCount; i++)
            {
                if (strikes.FallTree[i] == treeIndex)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int FindFireForBlocker(StrikeSnapshot strikes, int blockerIndex)
        {
            if (strikes == null)
            {
                return -1;
            }
            for (int i = 0; i < strikes.FireCount; i++)
            {
                if (strikes.FireBlocker[i] == blockerIndex)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool TryDirectionFromTrunkToCrown(
            MapData map,
            Terrain terrain,
            int[] cells,
            byte[] profile,
            int totalCells,
            float cellM,
            out float baseX,
            out float baseY,
            out float baseZ,
            ref float yawRad
        )
        {
            baseX = 0f;
            baseY = 0f;
            baseZ = 0f;
            float trunkSumX = 0f;
            float trunkSumZ = 0f;
            int trunkCount = 0;
            float crownSumX = 0f;
            float crownSumZ = 0f;
            int crownCount = 0;
            bool hasFirst = false;

            for (int i = 0; i < cells.Length; i++)
            {
                int cell = cells[i];
                if ((uint)cell >= (uint)totalCells)
                {
                    continue;
                }
                CellWorldCenter(
                    map,
                    terrain,
                    cell,
                    cellM,
                    out float wx,
                    out float wy,
                    out float wz
                );
                if (!hasFirst)
                {
                    baseX = wx;
                    baseY = wy;
                    baseZ = wz;
                    hasFirst = true;
                }
                bool isCrown = profile != null && i < profile.Length && profile[i] != 0;
                if (isCrown)
                {
                    crownSumX += wx;
                    crownSumZ += wz;
                    crownCount++;
                }
                else
                {
                    trunkSumX += wx;
                    trunkSumZ += wz;
                    trunkCount++;
                }
            }

            if (!hasFirst)
            {
                return false;
            }
            if (trunkCount > 0 && crownCount > 0)
            {
                float tx = trunkSumX / trunkCount;
                float tz = trunkSumZ / trunkCount;
                float cx = crownSumX / crownCount;
                float cz = crownSumZ / crownCount;
                float dx = cx - tx;
                float dz = cz - tz;
                if (dx * dx + dz * dz > 1e-4f)
                {
                    float theta = (float)Math.Atan2(dz, dx);
                    yawRad = theta - (float)Math.PI * 0.5f;
                }
            }
            return true;
        }

        private static float WorldYAtMm(MapData map, Terrain terrain, int xMm, int yMm)
        {
            if (terrain != null)
            {
                var pos = new FixVec2(Fix.FromRatio(xMm, 1000), Fix.FromRatio(yMm, 1000));
                return terrain.HeightMmAt(pos) / 1000f;
            }
            int col = Math.Min(map.Columns - 1, Math.Max(0, xMm / map.CellSizeMm));
            int row = Math.Min(map.Rows - 1, Math.Max(0, yMm / map.CellSizeMm));
            return map.HeightMm[row * map.Columns + col] / 1000f;
        }

        private static bool IsClearedAtTick(CauseEvent[] causes, int blockerIndex, int atTick)
        {
            if (causes == null)
            {
                return false;
            }
            for (int i = 0; i < causes.Length; i++)
            {
                CauseEvent e = causes[i];
                if (
                    e.Kind == CauseKind.BlockerCleared
                    && e.Detail == blockerIndex
                    && e.Tick <= atTick
                )
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Collects placed boulder props from <see cref="MapData.Boulders"/>, grounded on their pre-stamp terrain heights.</summary>
        public static void CollectBoulders(
            MapData map,
            Terrain terrain,
            List<BoulderPropInstance> output
        )
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            output.Clear();
            if (map == null || map.Boulders == null || map.Boulders.Count == 0)
            {
                return;
            }

            int totalCells = map.Columns * map.Rows;
            float cellM = map.CellSizeMm / 1000f;

            for (int b = 0; b < map.Boulders.Count; b++)
            {
                BoulderSpec spec = map.Boulders[b];
                if (spec == null || spec.Cells == null || spec.Cells.Length == 0)
                {
                    continue;
                }
                float sumX = 0f;
                float sumZ = 0f;
                int valid = 0;
                int maxGroundMm = int.MinValue;
                bool anyStillRaised = false;
                for (int i = 0; i < spec.Cells.Length; i++)
                {
                    int cell = spec.Cells[i];
                    if ((uint)cell >= (uint)totalCells)
                    {
                        continue;
                    }
                    int col = cell % map.Columns;
                    int row = cell / map.Columns;
                    sumX += (col + 0.5f) * cellM;
                    sumZ += (row + 0.5f) * cellM;
                    valid++;
                    int groundMm =
                        spec.GroundHeights != null && i < spec.GroundHeights.Length
                            ? spec.GroundHeights[i]
                            : map.HeightMm[cell] - 2000;
                    if (groundMm > maxGroundMm)
                    {
                        maxGroundMm = groundMm;
                    }
                    if (terrain == null || terrain.HeightMm(col, row) > groundMm + 300)
                    {
                        anyStillRaised = true;
                    }
                }
                if (valid == 0 || !anyStillRaised)
                {
                    continue;
                }
                float wx = sumX / valid;
                float wz = sumZ / valid;
                float wy = maxGroundMm / 1000f - 0.14f;
                float footprintSpanM = (float)Math.Sqrt(valid) * cellM;
                float scale = Math.Max(0.65f, footprintSpanM / 2.75f);
                float yaw = (b * 2.3999632f) % ((float)Math.PI * 2f);
                output.Add(new BoulderPropInstance(b, wx, wy, wz, scale, yaw));
            }
        }

        /// <summary>
        /// Collects hedgerow and drystone-wall prop segments along every <see cref="PieceKind.Line"/> piece in
        /// <paramref name="plan"/>.
        /// </summary>
        public static void CollectLineBarriers(
            BattlePlan plan,
            MapData map,
            Terrain terrain,
            List<LineBarrierInstance> output
        )
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            output.Clear();
            if (plan == null || plan.Terrain == null || map == null)
            {
                return;
            }

            const float NominalSpacingM = 1.42f;
            for (int pIdx = 0; pIdx < plan.Terrain.Count; pIdx++)
            {
                PlannedPiece piece = plan.Terrain[pIdx];
                if (piece == null || piece.Rules.Kind != PieceKind.Line || piece.PointCount < 2)
                {
                    continue;
                }
                bool isWall = string.Equals(
                    piece.Rules.Material,
                    "stone",
                    StringComparison.OrdinalIgnoreCase
                );
                int[] pts = piece.PointsMm;
                for (int seg = 0; seg < piece.PointCount - 1; seg++)
                {
                    float ax = pts[seg * 2] / 1000f;
                    float az = pts[seg * 2 + 1] / 1000f;
                    float bx = pts[(seg + 1) * 2] / 1000f;
                    float bz = pts[(seg + 1) * 2 + 1] / 1000f;
                    float dx = bx - ax;
                    float dz = bz - az;
                    float length = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (length < 0.25f)
                    {
                        continue;
                    }
                    int count = Math.Max(1, (int)Math.Ceiling(length / NominalSpacingM));
                    float stepLen = length / count;
                    // P_hedgerow_a & P_wall_drystone span 1.5 m along local X; RotateY aligns local X with (dx, dz).
                    float yaw = (float)Math.Atan2(dz, dx);
                    float scaleX = Math.Max(0.4f, (stepLen * 1.06f) / 1.50f);

                    for (int k = 0; k < count; k++)
                    {
                        float t = (k + 0.5f) / count;
                        float wx = ax + dx * t;
                        float wz = az + dz * t;
                        int xMm = Math.Max(0, Math.Min(map.WidthMm - 1, (int)(wx * 1000f)));
                        int yMm = Math.Max(0, Math.Min(map.DepthMm - 1, (int)(wz * 1000f)));
                        float wy = WorldYAtMm(map, terrain, xMm, yMm) - 0.05f;
                        int hash = ((pIdx * 97 + seg * 31 + k * 17) & 15);
                        float scaleY = isWall
                            ? 0.96f + (hash / 15f) * 0.08f
                            : 0.92f + (hash / 15f) * 0.18f;
                        output.Add(
                            new LineBarrierInstance(pIdx, isWall, wx, wy, wz, scaleX, scaleY, yaw)
                        );
                    }
                }
            }
        }

        /// <summary>Collects rustic split-rail herd pen sections around each <see cref="MapData.Herds"/> grazing ring.</summary>
        public static void CollectHerdPens(
            MapData map,
            Terrain terrain,
            List<HerdPenPropInstance> output
        )
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            output.Clear();
            if (map == null || map.Herds == null || map.Herds.Count == 0)
            {
                return;
            }

            float mapCenterX = map.WidthMm * 0.0005f;
            float mapCenterZ = map.DepthMm * 0.0005f;

            for (int h = 0; h < map.Herds.Count; h++)
            {
                HerdSpec herd = map.Herds[h];
                float cx = herd.XMm / 1000f;
                float cz = herd.YMm / 1000f;
                float radiusM = Math.Max(5f, herd.RadiusMm / 1000f);
                // Leave a wide pasture opening facing toward the centre of the battlefield so herds clearly run out.
                float openAngle = (float)Math.Atan2(mapCenterZ - cz, mapCenterX - cx);
                float circumference = 2f * (float)Math.PI * radiusM;
                int slots = Math.Max(8, Math.Min(32, (int)(circumference / 5.2f)));

                for (int s = 0; s < slots; s++)
                {
                    float a = (s / (float)slots) * (2f * (float)Math.PI);
                    float diff = (float)
                        Math.Atan2(Math.Sin(a - openAngle), Math.Cos(a - openAngle));
                    if (Math.Abs(diff) < 0.58f)
                    {
                        continue;
                    }
                    float wx = cx + (float)Math.Cos(a) * radiusM;
                    float wz = cz + (float)Math.Sin(a) * radiusM;
                    int xMm = (int)(wx * 1000f);
                    int yMm = (int)(wz * 1000f);
                    if (xMm < 0 || yMm < 0 || xMm >= map.WidthMm || yMm >= map.DepthMm)
                    {
                        continue;
                    }
                    float wy = WorldYAtMm(map, terrain, xMm, yMm) - 0.04f;
                    float yaw = -a + (float)Math.PI * 0.5f;
                    output.Add(new HerdPenPropInstance(h, wx, wy, wz, 1.35f, yaw));
                }
            }
        }

        /// <summary>
        /// Collects winding-up and active Rain casts at <paramref name="atTick"/>, excluding any cast that fizzled.
        /// </summary>
        public static void CollectActiveRainCasts(
            BattlePlan plan,
            CauseEvent[] causes,
            MapData map,
            Terrain terrain,
            int atTick,
            List<RainCastInstance> output
        )
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            output.Clear();
            if (plan == null || plan.Casts == null || plan.Casts.Count == 0 || map == null)
            {
                return;
            }

            float windX = map.WindXMmPerS / 1000f;
            float windZ = map.WindYMmPerS / 1000f;

            for (int c = 0; c < plan.Casts.Count; c++)
            {
                PlannedCast cast = plan.Casts[c];
                if (
                    cast == null
                    || cast.Power == null
                    || !string.Equals(cast.Power.Id, "rain", StringComparison.OrdinalIgnoreCase)
                )
                {
                    continue;
                }
                if (atTick < cast.Tick)
                {
                    continue;
                }
                if (IsCastFizzled(causes, c))
                {
                    continue;
                }

                int windUpEnd = cast.Tick + cast.Power.WindUpTicks;
                int rainEnd = windUpEnd + cast.Power.DurationTicks;
                if (atTick > rainEnd)
                {
                    continue;
                }

                float activeIntensity = 0f;
                float windUpProgress = 0f;
                if (atTick < windUpEnd)
                {
                    windUpProgress =
                        (atTick - cast.Tick) / (float)Math.Max(1, cast.Power.WindUpTicks);
                }
                else
                {
                    windUpProgress = 1f;
                    float rampIn = Math.Min(1f, (atTick - windUpEnd) / 30f);
                    float rampOut = Math.Min(1f, (rainEnd - atTick) / 40f);
                    activeIntensity = Math.Max(0.05f, Math.Min(rampIn, rampOut));
                }

                float wx = cast.XMm / 1000f;
                float wz = cast.YMm / 1000f;
                float wy = WorldYAtMm(map, terrain, cast.XMm, cast.YMm);
                float radiusM = cast.RadiusMm / 1000f;
                output.Add(
                    new RainCastInstance(
                        c,
                        wx,
                        wy,
                        wz,
                        radiusM,
                        activeIntensity,
                        windUpProgress,
                        windX,
                        windZ
                    )
                );
            }
        }

        /// <summary>
        /// Collects mid-air ballistic siege boulders approaching their <see cref="CauseKind.StoneLanded"/> impact
        /// within the next 2.2 seconds (44 ticks), giving scrubbable live projectile arcs.
        /// </summary>
        public static void CollectFlyingStones(
            CauseEvent[] causes,
            BattleHost.UnitSnapshot units,
            BattlePlan plan,
            MapData map,
            Terrain terrain,
            int atTick,
            List<FlyingStoneInstance> output
        )
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            output.Clear();
            if (causes == null || causes.Length == 0 || map == null)
            {
                return;
            }

            const int FlightTicks = 44; // 2.2 s ballistic arc
            int totalCells = map.Columns * map.Rows;
            float cellM = map.CellSizeMm / 1000f;
            float[] regX = null;
            float[] regY = null;
            float[] regZ = null;
            int[] regN = null;

            for (int i = 0; i < causes.Length; i++)
            {
                CauseEvent e = causes[i];
                if (e.Kind != CauseKind.StoneLanded)
                {
                    continue;
                }
                int remaining = e.Tick - atTick;
                if (remaining <= 0 || remaining > FlightTicks)
                {
                    continue;
                }
                int cell = e.Detail;
                if ((uint)cell >= (uint)totalCells)
                {
                    continue;
                }

                if (regX == null)
                {
                    ComputeRegimentCentroids(
                        units,
                        plan,
                        map,
                        terrain,
                        out regX,
                        out regY,
                        out regZ,
                        out regN
                    );
                }

                CellWorldCenter(
                    map,
                    terrain,
                    cell,
                    cellM,
                    out float tx,
                    out float ty,
                    out float tz
                );
                int r = e.Subject;
                float sx,
                    sy,
                    sz;
                if (regN != null && (uint)r < (uint)regN.Length && regN[r] > 0)
                {
                    sx = regX[r];
                    sy = regY[r] + 3.2f;
                    sz = regZ[r];
                }
                else
                {
                    sx = Math.Max(10f, tx - 90f);
                    sy = ty + 3.2f;
                    sz = tz;
                }

                float t = 1f - remaining / (float)FlightTicks;
                float dx = tx - sx;
                float dz = tz - sz;
                float horiz = (float)Math.Sqrt(dx * dx + dz * dz);
                float arcHeight = Math.Max(12f, Math.Min(46f, horiz * 0.25f));
                float wx = sx + dx * t;
                float wz = sz + dz * t;
                float wy = sy + (ty - sy) * t + 4f * arcHeight * t * (1f - t);
                float spin = t * 14f + (i & 7) * 0.8f;

                output.Add(new FlyingStoneInstance(i, wx, wy, wz, spin, t, 1.15f));
            }
        }

        /// <summary>
        /// Collects mid-air feathered arrow volleys fired by ranged regiments before <see cref="CauseKind.SoldierKilled"/>
        /// ticks, fanned into readable parabolic arcs for both live playback and scrubbing.
        /// </summary>
        public static void CollectArrowVolleys(
            BattlePlan plan,
            CauseEvent[] causes,
            BattleHost.UnitSnapshot units,
            MapData map,
            Terrain terrain,
            int atTick,
            List<ArrowVolleyInstance> output
        )
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            output.Clear();
            if (
                plan == null
                || plan.Army == null
                || causes == null
                || causes.Length == 0
                || map == null
            )
            {
                return;
            }

            const int FlightTicks = 26; // 1.3 s arrow flight
            const int ArrowsPerVolley = 10;
            int regCount = plan.Army.Count;
            float[] regX = null;
            float[] regY = null;
            float[] regZ = null;
            int[] regN = null;
            HashSet<long> seenVolleys = null;

            for (int i = 0; i < causes.Length; i++)
            {
                CauseEvent e = causes[i];
                if (e.Kind != CauseKind.SoldierKilled)
                {
                    continue;
                }
                int remaining = e.Tick - atTick;
                if (remaining < 0 || remaining > FlightTicks)
                {
                    continue;
                }
                int killer = e.Detail;
                int victim = e.Subject;
                if ((uint)killer >= (uint)regCount || (uint)victim >= (uint)regCount)
                {
                    continue;
                }
                if (
                    !string.Equals(
                        plan.Army[killer].UnitType,
                        "ranged",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    continue;
                }

                seenVolleys ??= new HashSet<long>();
                long key = ((long)killer << 32) | ((long)victim << 16) | (uint)(e.Tick / 20);
                if (!seenVolleys.Add(key))
                {
                    continue;
                }

                if (regX == null)
                {
                    ComputeRegimentCentroids(
                        units,
                        plan,
                        map,
                        terrain,
                        out regX,
                        out regY,
                        out regZ,
                        out regN
                    );
                }
                if (regN[killer] <= 0 || regN[victim] <= 0)
                {
                    continue;
                }

                float sx = regX[killer];
                float sy = regY[killer] + 1.85f;
                float sz = regZ[killer];
                float tx = regX[victim];
                float ty = regY[victim] + 1.1f;
                float tz = regZ[victim];

                float baseDx = tx - sx;
                float baseDz = tz - sz;
                float horiz = (float)Math.Sqrt(baseDx * baseDx + baseDz * baseDz);
                if (horiz < 12f)
                {
                    continue;
                }
                float perpX = -baseDz / horiz;
                float perpZ = baseDx / horiz;
                float baseT = 1f - remaining / (float)FlightTicks;
                float arcHeight = Math.Max(6f, Math.Min(32f, horiz * 0.20f));

                for (int a = 0; a < ArrowsPerVolley; a++)
                {
                    int h = ((killer * 73 + victim * 37 + (e.Tick / 20) * 19 + a * 53) & 0xFF);
                    float spreadLateral = (((h & 15) / 15f) - 0.5f) * 14f;
                    float spreadDepth = ((((h >> 4) & 15) / 15f) - 0.5f) * 8f;
                    float tJitter = ((((a * 31 + h) & 15) / 15f) - 0.5f) * 0.14f;
                    float t = baseT + tJitter;
                    if (t < 0.02f || t > 0.99f)
                    {
                        continue;
                    }

                    float ax = sx + perpX * spreadLateral;
                    float az = sz + perpZ * spreadLateral;
                    float bx =
                        tx + perpX * (spreadLateral * 0.85f) + (baseDx / horiz) * spreadDepth;
                    float bz =
                        tz + perpZ * (spreadLateral * 0.85f) + (baseDz / horiz) * spreadDepth;

                    float dx = bx - ax;
                    float dz = bz - az;
                    float dy = ty - sy;

                    float wx = ax + dx * t;
                    float wz = az + dz * t;
                    float wy = sy + dy * t + 4f * arcHeight * t * (1f - t);

                    // Analytical derivative of parabolic arc gives exact arrow pitch tangent.
                    float vx = dx;
                    float vz = dz;
                    float vy = dy + 4f * arcHeight * (1f - 2f * t);
                    float vLen = (float)Math.Sqrt(vx * vx + vy * vy + vz * vz);
                    if (vLen < 1e-4f)
                    {
                        continue;
                    }
                    float inv = 1f / vLen;
                    output.Add(
                        new ArrowVolleyInstance(wx, wy, wz, vx * inv, vy * inv, vz * inv, t)
                    );
                }
            }
        }

        private static void ComputeRegimentCentroids(
            BattleHost.UnitSnapshot units,
            BattlePlan plan,
            MapData map,
            Terrain terrain,
            out float[] regX,
            out float[] regY,
            out float[] regZ,
            out int[] regN
        )
        {
            int count = plan?.Army != null ? plan.Army.Count : 0;
            if (units != null)
            {
                for (int u = 0; u < units.Count; u++)
                {
                    if (units.Regiment[u] + 1 > count)
                    {
                        count = units.Regiment[u] + 1;
                    }
                }
            }
            regX = new float[count];
            regY = new float[count];
            regZ = new float[count];
            regN = new int[count];

            if (units != null)
            {
                for (int u = 0; u < units.Count; u++)
                {
                    if (!units.Alive[u])
                    {
                        continue;
                    }
                    int r = units.Regiment[u];
                    if ((uint)r >= (uint)count)
                    {
                        continue;
                    }
                    regX[r] += units.X[u];
                    regY[r] += units.Y[u];
                    regZ[r] += units.Z[u];
                    regN[r]++;
                }
                for (int r = 0; r < count; r++)
                {
                    if (regN[r] > 0)
                    {
                        regX[r] /= regN[r];
                        regY[r] /= regN[r];
                        regZ[r] /= regN[r];
                    }
                }
            }

            if (plan?.Army != null)
            {
                for (int r = 0; r < plan.Army.Count; r++)
                {
                    if (regN[r] == 0)
                    {
                        regX[r] = plan.Army[r].XMm / 1000f;
                        regZ[r] = plan.Army[r].YMm / 1000f;
                        regY[r] = WorldYAtMm(map, terrain, plan.Army[r].XMm, plan.Army[r].YMm);
                        regN[r] = 1;
                    }
                }
            }
        }

        /// <summary>
        /// Collects heraldic standards/banners for every live non-neutral regiment from <paramref name="units"/> and
        /// <paramref name="regiments"/>, oriented along the map's prevailing wind (or a gentle breeze when calm).
        /// </summary>
        public static void CollectRegimentBanners(
            BattleHost.UnitSnapshot units,
            RegimentSnapshot regiments,
            BattlePlan plan,
            MapData map,
            List<RegimentBannerInstance> output
        )
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            output.Clear();
            if (units == null || units.Count == 0 || plan == null || plan.Army == null)
            {
                return;
            }

            int regCount = plan.Army.Count;
            if (regCount == 0)
            {
                return;
            }

            var sumX = new float[regCount];
            var sumY = new float[regCount];
            var sumZ = new float[regCount];
            var alive = new int[regCount];

            for (int u = 0; u < units.Count; u++)
            {
                if (!units.Alive[u])
                {
                    continue;
                }
                int r = units.Regiment[u];
                if ((uint)r >= (uint)regCount)
                {
                    continue;
                }
                sumX[r] += units.X[u];
                sumY[r] += units.Y[u];
                sumZ[r] += units.Z[u];
                alive[r]++;
            }

            float windYaw = 0.25f;
            if (map != null && (map.WindXMmPerS != 0 || map.WindYMmPerS != 0))
            {
                windYaw = (float)Math.Atan2(-map.WindYMmPerS, map.WindXMmPerS);
            }

            for (int r = 0; r < regCount; r++)
            {
                if (alive[r] <= 0)
                {
                    continue;
                }
                PlannedRegiment planned = plan.Army[r];
                if (planned.Side == Side.Neutral)
                {
                    continue;
                }
                if (
                    regiments != null
                    && r < regiments.Count
                    && (regiments.Wiped[r] || !regiments.Alive[r])
                )
                {
                    continue;
                }

                float wx = sumX[r] / alive[r];
                float wy = sumY[r] / alive[r];
                float wz = sumZ[r] / alive[r];
                float morale01 =
                    regiments != null && r < regiments.Count
                        ? Math.Min(1f, Math.Max(0f, regiments.Morale[r] / 1000f))
                        : 1f;
                int startSize =
                    regiments != null && r < regiments.Count && regiments.StartingSize[r] > 0
                        ? regiments.StartingSize[r]
                        : Math.Max(1, planned.Soldiers);
                float strength01 = Math.Min(1f, Math.Max(0.1f, alive[r] / (float)startSize));
                bool routing =
                    regiments != null
                    && r < regiments.Count
                    && (regiments.Routing[r] || regiments.Order[r] == RegimentOrder.Rout);
                int emblem = EmblemKindOf(planned.UnitType);

                output.Add(
                    new RegimentBannerInstance(
                        r,
                        planned.Side,
                        emblem,
                        wx,
                        wy,
                        wz,
                        morale01,
                        strength01,
                        routing,
                        windYaw
                    )
                );
            }
        }

        private static int EmblemKindOf(string unitType)
        {
            if (string.IsNullOrEmpty(unitType))
            {
                return 0;
            }
            if (string.Equals(unitType, "infantry", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
            if (string.Equals(unitType, "cavalry", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
            if (string.Equals(unitType, "ranged", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }
            if (string.Equals(unitType, "siege", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }
            return 4; // specialists
        }

        private static bool IsCastFizzled(CauseEvent[] causes, int castIndex)
        {
            if (causes == null)
            {
                return false;
            }
            for (int i = 0; i < causes.Length; i++)
            {
                if (causes[i].Kind == CauseKind.CastFizzled && causes[i].Subject == castIndex)
                {
                    return true;
                }
            }
            return false;
        }

        private static void CellWorldCenter(
            MapData map,
            Terrain terrain,
            int cell,
            float cellM,
            out float worldX,
            out float worldY,
            out float worldZ
        )
        {
            int col = cell % map.Columns;
            int row = cell / map.Columns;
            worldX = (col + 0.5f) * cellM;
            worldZ = (row + 0.5f) * cellM;
            if (terrain != null)
            {
                long xMm = (long)col * map.CellSizeMm + map.CellSizeMm / 2L;
                long yMm = (long)row * map.CellSizeMm + map.CellSizeMm / 2L;
                var pos = new FixVec2(Fix.FromRatio(xMm, 1000), Fix.FromRatio(yMm, 1000));
                worldY = terrain.HeightMmAt(pos) / 1000f;
            }
            else
            {
                worldY = map.HeightMm[cell] / 1000f;
            }
        }
    }
}
