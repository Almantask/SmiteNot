using System;
using System.Collections.Generic;
using GodGame.Sim;
using GodGame.Sim.Math;

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
