using System;
using GodGame.Sim;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Bridge
{
    /// <summary>
    /// Turns a <see cref="PlaybackTrack"/> into what the renderers draw at any scrub time: the two samples around it as
    /// <see cref="BattleHost.UnitSnapshot"/>s (metres, ground height from the played map) and the blend between them.
    /// Which regiment and unit type a slot holds never changes in a battle without spawns, so they come from the
    /// deployed world once. Main thread only; it reuses its snapshot buffers.
    /// </summary>
    public sealed class TrackReader
    {
        private readonly MapData _map;
        private readonly Ruleset _rules;
        private readonly Terrain _terrain;
        private readonly int[] _regiment;
        private readonly int[] _unitType;
        private BattleHost.UnitSnapshot _a;
        private BattleHost.UnitSnapshot _b;
        private UnitSample _sampleA;
        private UnitSample _sampleB;
        private PlaybackTrack _lastTrack;

        public TrackReader(Battle battle)
        {
            if (battle == null)
            {
                throw new ArgumentNullException(nameof(battle));
            }
            _map = battle.PlayedMap;
            _rules = battle.Rules;
            _terrain = new Terrain(battle.PlayedMap);
            var world = BattleSetup.Deploy(battle.PlayedMap, battle.Plan, battle.Rules);
            int capacity = world.Units.Capacity;
            _regiment = new int[capacity];
            _unitType = new int[capacity];
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                _regiment[u] = world.UnitRegiment[u];
                _unitType[u] = world.UnitType[u];
            }
            Capacity = capacity;
            RegimentSides = new Side[world.Regiments.Capacity];
            for (int r = 0; r < RegimentSides.Length; r++)
            {
                RegimentSides[r] = world.RegimentSide[r];
            }
            _a = new BattleHost.UnitSnapshot(capacity);
            _b = new BattleHost.UnitSnapshot(capacity);
        }

        public int Capacity { get; }

        public MapData Map => _map;

        public Terrain Terrain => _terrain;

        public Side[] RegimentSides { get; }

        /// <summary>
        /// The samples around <paramref name="seconds"/> of battle time and how far between them it lies, or false when
        /// the track holds nothing yet. Positions beyond the filled future hold at its last sample.
        /// </summary>
        public bool Read(
            PlaybackTrack track,
            float seconds,
            out BattleHost.UnitSnapshot previous,
            out BattleHost.UnitSnapshot current,
            out float alpha
        )
        {
            previous = _a;
            current = _b;
            alpha = 0;
            if (track == null || track.SampleCount == 0)
            {
                return false;
            }
            if (!ReferenceEquals(track, _lastTrack))
            {
                _sampleA = null;
                _sampleB = null;
                _lastTrack = track;
            }
            float ticks = Math.Max(0, seconds * SimConstants.TicksPerSecond);
            var before = track.SampleAtOrBefore((int)ticks) ?? track.Sample(0);
            var after = track.SampleAtOrBefore(before.Tick + PlaybackTrack.SampleTicks) ?? before;
            if (after.Tick == before.Tick)
            {
                after = before;
            }
            if (!ReferenceEquals(before, _sampleA))
            {
                Fill(before, _a, track);
                _sampleA = before;
            }
            if (!ReferenceEquals(after, _sampleB))
            {
                Fill(after, _b, track);
                _sampleB = after;
            }
            alpha =
                after.Tick == before.Tick
                    ? 0
                    : Math.Min(1f, (ticks - before.Tick) / (after.Tick - before.Tick));
            return true;
        }

        /// <summary>Forgets the cached samples (after the track was replaced by an edit).</summary>
        public void Invalidate()
        {
            _sampleA = null;
            _sampleB = null;
        }

        private void Fill(
            UnitSample sample,
            BattleHost.UnitSnapshot into,
            PlaybackTrack track = null
        )
        {
            int count = Math.Min(sample.Count, Capacity);
            into.Tick = sample.Tick;
            into.Count = count;
            StructureSample structSample =
                track != null ? track.StructureAtOrBefore(sample.Tick) : null;
            for (int u = 0; u < count; u++)
            {
                bool alive = (sample.State[u] & UnitSample.Alive) != 0;
                into.Alive[u] = alive;
                byte flags = (byte)(sample.State[u] & 0x7F);
                into.Flags[u] = flags;
                int xCm = sample.XCm[u];
                int yCm = sample.YCm[u];
                var p = new GodGame.Sim.Math.FixVec2(
                    GodGame.Sim.Math.Fix.FromRatio(xCm, 100),
                    GodGame.Sim.Math.Fix.FromRatio(yCm, 100)
                );
                float groundY = _terrain.HeightMmAt(p) / 1000f;
                bool launched = (flags & (byte)UnitFlags.Launched) != 0;
                LaunchRecord launch =
                    alive && track != null && track.LaunchCount > 0
                        ? FindActiveLaunch(track, u, sample.Tick)
                        : null;
                if (launch != null)
                {
                    float dt =
                        (sample.Tick - launch.LaunchTick) / (float)SimConstants.TicksPerSecond;
                    float lx = launch.StartXCm / 100f + (launch.VelXMmPerS / 1000f) * dt;
                    float lz = launch.StartYCm / 100f + (launch.VelYMmPerS / 1000f) * dt;
                    var lp = new GodGame.Sim.Math.FixVec2(
                        GodGame.Sim.Math.Fix.FromRatio((int)Math.Round(lx * 100f), 100),
                        GodGame.Sim.Math.Fix.FromRatio((int)Math.Round(lz * 100f), 100)
                    );
                    float localGround = _terrain.HeightMmAt(lp) / 1000f;
                    float arcY =
                        launch.StartHeightMm / 1000f
                        + (launch.VerticalMmPerS / 1000f) * dt
                        - 0.5f * (launch.GravityMmPerS2 / 1000f) * dt * dt;
                    into.X[u] = lx;
                    into.Z[u] = lz;
                    into.Y[u] = Math.Max(localGround, arcY);
                    into.SpinRad[u] = (launch.SpinCdPerS / 100f) * (float)(Math.PI / 180.0) * dt;
                }
                else if (alive && launched)
                {
                    into.X[u] = xCm / 100f;
                    into.Z[u] = yCm / 100f;
                    into.Y[u] = groundY + sample.HeightCm[u] / 100f;
                    into.SpinRad[u] = 0f;
                }
                else
                {
                    into.X[u] = xCm / 100f;
                    into.Z[u] = yCm / 100f;
                    float y = groundY;
                    if (alive)
                    {
                        float deckY = DeckHeightAtCm(track, structSample, sample.Tick, xCm, yCm);
                        if (deckY > y)
                        {
                            y = deckY;
                        }
                    }
                    into.Y[u] = y;
                    into.SpinRad[u] = 0f;
                }
                into.Regiment[u] = _regiment[u];
                into.UnitType[u] = _unitType[u];
            }
        }

        /// <summary>
        /// Evaluates a single unit's exact ballistic trajectory and pitch spin at continuous fractional tick
        /// <paramref name="exactTick"/> when an active <see cref="LaunchRecord"/> covers it; returns false otherwise.
        /// </summary>
        public bool HasAnyActiveLaunchAt(PlaybackTrack track, float exactTick)
        {
            if (track == null || track.LaunchCount == 0 || exactTick < 0f)
            {
                return false;
            }
            int floorTick = (int)Math.Floor(exactTick);
            int firstAfter = UpperBoundLaunchTick(track, floorTick);
            const int MaxFlightTicks = 400;
            for (int i = firstAfter - 1; i >= 0; i--)
            {
                var rec = track.Launch(i);
                if (exactTick - rec.LaunchTick > MaxFlightTicks)
                {
                    break;
                }
                int landTick = FindLandingTick(track, rec.LaunchEvent);
                if (landTick < 0 || exactTick <= landTick)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Evaluates a single unit's exact ballistic trajectory and pitch spin at continuous fractional tick
        /// <paramref name="exactTick"/> when an active <see cref="LaunchRecord"/> covers it; returns false otherwise.
        /// </summary>
        public bool TryEvaluateActiveLaunch(
            PlaybackTrack track,
            int unit,
            float exactTick,
            out float x,
            out float y,
            out float z,
            out float spinRad
        )
        {
            x = 0f;
            y = 0f;
            z = 0f;
            spinRad = 0f;
            if (track == null || track.LaunchCount == 0 || exactTick < 0f)
            {
                return false;
            }
            int floorTick = (int)Math.Floor(exactTick);
            int firstAfter = UpperBoundLaunchTick(track, floorTick);
            const int MaxFlightTicks = 400;
            for (int i = firstAfter - 1; i >= 0; i--)
            {
                var rec = track.Launch(i);
                if (exactTick - rec.LaunchTick > MaxFlightTicks)
                {
                    break;
                }
                if (rec.Unit != unit)
                {
                    continue;
                }
                int landTick = FindLandingTick(track, rec.LaunchEvent);
                if (landTick >= 0 && exactTick > landTick)
                {
                    return false;
                }
                float dt = Math.Max(0f, (exactTick - rec.LaunchTick) / SimConstants.TicksPerSecond);
                float lx = rec.StartXCm / 100f + (rec.VelXMmPerS / 1000f) * dt;
                float lz = rec.StartYCm / 100f + (rec.VelYMmPerS / 1000f) * dt;
                var lp = new GodGame.Sim.Math.FixVec2(
                    GodGame.Sim.Math.Fix.FromRatio((int)Math.Round(lx * 100f), 100),
                    GodGame.Sim.Math.Fix.FromRatio((int)Math.Round(lz * 100f), 100)
                );
                float localGround = _terrain.HeightMmAt(lp) / 1000f;
                float arcY =
                    rec.StartHeightMm / 1000f
                    + (rec.VerticalMmPerS / 1000f) * dt
                    - 0.5f * (rec.GravityMmPerS2 / 1000f) * dt * dt;
                x = lx;
                z = lz;
                y = Math.Max(localGround, arcY);
                spinRad = (rec.SpinCdPerS / 100f) * (float)(Math.PI / 180.0) * dt;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Refines airborne positions and spin angles at continuous fractional tick (<paramref name="seconds"/> * 20)
        /// from <see cref="LaunchRecord"/>s, giving smooth sub-sample parabolas and pitch tumble.
        /// </summary>
        public void ApplyContinuousLaunches(
            PlaybackTrack track,
            float seconds,
            int count,
            float[] x,
            float[] y,
            float[] z,
            float[] spinRad,
            bool[] alive = null
        )
        {
            if (track == null || track.LaunchCount == 0 || count <= 0)
            {
                return;
            }
            float exactTick = Math.Max(0f, seconds * SimConstants.TicksPerSecond);
            int floorTick = (int)Math.Floor(exactTick);
            int firstAfter = UpperBoundLaunchTick(track, floorTick);
            const int MaxFlightTicks = 400; // 20 s generous cap for active parabolic flight scan.
            // Scan latest-first so the newest launch for any unit wins without allocating per-frame maps.
            for (int i = firstAfter - 1; i >= 0; i--)
            {
                var rec = track.Launch(i);
                if (exactTick - rec.LaunchTick > MaxFlightTicks)
                {
                    break;
                }
                int u = rec.Unit;
                if ((uint)u >= (uint)count)
                {
                    continue;
                }
                if (alive != null && !alive[u])
                {
                    continue;
                }
                int landTick = FindLandingTick(track, rec.LaunchEvent);
                if (landTick >= 0 && exactTick > landTick)
                {
                    continue;
                }
                float dt = Math.Max(0f, (exactTick - rec.LaunchTick) / SimConstants.TicksPerSecond);
                float lx = rec.StartXCm / 100f + (rec.VelXMmPerS / 1000f) * dt;
                float lz = rec.StartYCm / 100f + (rec.VelYMmPerS / 1000f) * dt;
                var lp = new GodGame.Sim.Math.FixVec2(
                    GodGame.Sim.Math.Fix.FromRatio((int)Math.Round(lx * 100f), 100),
                    GodGame.Sim.Math.Fix.FromRatio((int)Math.Round(lz * 100f), 100)
                );
                float localGround = _terrain.HeightMmAt(lp) / 1000f;
                float arcY =
                    rec.StartHeightMm / 1000f
                    + (rec.VerticalMmPerS / 1000f) * dt
                    - 0.5f * (rec.GravityMmPerS2 / 1000f) * dt * dt;
                x[u] = lx;
                z[u] = lz;
                y[u] = Math.Max(localGround, arcY);
                spinRad[u] = (rec.SpinCdPerS / 100f) * (float)(Math.PI / 180.0) * dt;
            }
        }

        /// <summary>Populates <paramref name="into"/> with deck/ice strain state at <paramref name="seconds"/>.</summary>
        public bool ReadStructures(PlaybackTrack track, float seconds, StructureSnapshot into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }
            if (track == null || track.StructureSampleCount == 0)
            {
                into.Count = 0;
                return false;
            }
            int tick = Math.Max(0, (int)Math.Round(seconds * SimConstants.TicksPerSecond));
            var sample = track.StructureAtOrBefore(tick);
            if (sample == null || sample.Segments.Length == 0)
            {
                into.Count = 0;
                return false;
            }
            int count = sample.Segments.Length;
            into.EnsureCapacity(count);
            into.Tick = sample.Tick;
            into.Count = count;
            int staticCount = _map.Decks != null ? _map.Decks.Segments.Count : 0;
            int dynCount = track.DynamicSegmentCount;
            for (int s = 0; s < count; s++)
            {
                var st = sample.Segments[s];
                into.LoadPermille[s] = st.LoadPermille;
                into.StrengthPermille[s] = st.StrengthPermille;
                into.Stage[s] = st.Stage;
                into.Broken[s] = st.Broken;
                if (s < staticCount)
                {
                    var seg = _map.Decks.Segments[s];
                    into.DeckHeightM[s] = seg.DeckHeightMm / 1000f;
                    into.KindIndex[s] = seg.Kind != null ? seg.Kind.Index : -1;
                    into.KindIds[s] = seg.Kind != null ? seg.Kind.Id : string.Empty;
                    into.Cells[s] = seg.Cells;
                }
                else
                {
                    int dIdx = s - staticCount;
                    if (dIdx < dynCount)
                    {
                        var dyn = track.DynamicSegment(dIdx);
                        into.DeckHeightM[s] = dyn.DeckHeightMm / 1000f;
                        into.KindIndex[s] = dyn.KindIndex;
                        into.KindIds[s] =
                            dyn.KindIndex >= 0 && dyn.KindIndex < _rules.Structures.Kinds.Count
                                ? _rules.Structures.Kinds[dyn.KindIndex].Id
                                : string.Empty;
                        into.Cells[s] = dyn.Cells;
                    }
                    else
                    {
                        into.DeckHeightM[s] = 0f;
                        into.KindIndex[s] = -1;
                        into.KindIds[s] = string.Empty;
                        into.Cells[s] = Array.Empty<int>();
                    }
                }
            }
            return true;
        }

        /// <summary>Populates <paramref name="into"/> with regiment morale and routing state at <paramref name="seconds"/>.</summary>
        public bool ReadRegiments(PlaybackTrack track, float seconds, RegimentSnapshot into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }
            if (track == null || track.RegimentFrameCount == 0)
            {
                into.Count = 0;
                return false;
            }
            int tick = Math.Max(0, (int)Math.Round(seconds * SimConstants.TicksPerSecond));
            var frame = track.RegimentsAtOrBefore(tick);
            if (frame == null || frame.Regiments.Length == 0)
            {
                into.Count = 0;
                return false;
            }
            int count = frame.Regiments.Length;
            into.EnsureCapacity(count);
            into.Tick = frame.Tick;
            into.Count = count;
            var starts = track.RegimentStartingSizes;
            var sides = track.RegimentSides;
            for (int r = 0; r < count; r++)
            {
                var reg = frame.Regiments[r];
                into.Morale[r] = reg.Morale;
                into.Order[r] = reg.Order;
                into.Alive[r] = reg.Alive;
                into.Wiped[r] = reg.Wiped;
                into.Routing[r] = reg.Routing;
                into.AliveCount[r] = reg.AliveCount;
                into.StartingSize[r] = r < starts.Length ? starts[r] : 0;
                into.Side[r] = r < sides.Length ? sides[r] : Side.Attacker;
            }
            return true;
        }

        private float DeckHeightAtCm(
            PlaybackTrack track,
            StructureSample structSample,
            int tick,
            int xCm,
            int yCm
        )
        {
            if (xCm < 0 || yCm < 0 || _map == null)
            {
                return float.MinValue;
            }
            int col = (int)((long)xCm * 10L / _map.CellSizeMm);
            int row = (int)((long)yCm * 10L / _map.CellSizeMm);
            if ((uint)col >= (uint)_map.Columns || (uint)row >= (uint)_map.Rows)
            {
                return float.MinValue;
            }
            int cell = row * _map.Columns + col;
            int staticSeg = _map.Decks != null ? _map.Decks.SegmentOfCell[cell] : -1;
            if (staticSeg >= 0)
            {
                bool broken =
                    structSample != null
                    && staticSeg < structSample.Segments.Length
                    && structSample.Segments[staticSeg].Broken;
                if (!broken)
                {
                    return _map.Decks.Segments[staticSeg].DeckHeightMm / 1000f;
                }
            }
            if (track != null && track.DynamicSegmentCount > 0)
            {
                int staticCount = _map.Decks != null ? _map.Decks.Segments.Count : 0;
                for (int d = 0; d < track.DynamicSegmentCount; d++)
                {
                    var dyn = track.DynamicSegment(d);
                    if (dyn.AddedTick > tick)
                    {
                        continue;
                    }
                    int segIdx = staticCount + d;
                    if (
                        structSample != null
                        && segIdx < structSample.Segments.Length
                        && structSample.Segments[segIdx].Broken
                    )
                    {
                        continue;
                    }
                    for (int k = 0; k < dyn.Cells.Length; k++)
                    {
                        if (dyn.Cells[k] == cell)
                        {
                            return dyn.DeckHeightMm / 1000f;
                        }
                    }
                }
            }
            return float.MinValue;
        }

        private static LaunchRecord FindActiveLaunch(PlaybackTrack track, int unit, int tick)
        {
            int firstAfter = UpperBoundLaunchTick(track, tick);
            for (int i = firstAfter - 1; i >= 0; i--)
            {
                var rec = track.Launch(i);
                if (tick - rec.LaunchTick > 400)
                {
                    break;
                }
                if (rec.Unit != unit)
                {
                    continue;
                }
                int landTick = FindLandingTick(track, rec.LaunchEvent);
                if (landTick < 0 || tick <= landTick)
                {
                    return rec;
                }
                return null;
            }
            return null;
        }

        private static int UpperBoundLaunchTick(PlaybackTrack track, int tick)
        {
            int lo = 0;
            int hi = track.LaunchCount;
            while (lo < hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                if (track.Launch(mid).LaunchTick <= tick)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }
            return lo;
        }

        private static int FindLandingTick(PlaybackTrack track, int launchEvent)
        {
            for (int i = track.LaunchLandingCount - 1; i >= 0; i--)
            {
                var l = track.LaunchLanding(i);
                if (l.LaunchEvent == launchEvent)
                {
                    return l.LandTick;
                }
            }
            return -1;
        }
    }
}
