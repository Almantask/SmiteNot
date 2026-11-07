using System;
using GodGame.Sim;

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
        private readonly Terrain _terrain;
        private readonly int[] _regiment;
        private readonly int[] _unitType;
        private BattleHost.UnitSnapshot _a;
        private BattleHost.UnitSnapshot _b;
        private UnitSample _sampleA;
        private UnitSample _sampleB;

        public TrackReader(Battle battle)
        {
            if (battle == null)
            {
                throw new ArgumentNullException(nameof(battle));
            }
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
            float ticks = Math.Max(0, seconds * SimConstants.TicksPerSecond);
            var before = track.SampleAtOrBefore((int)ticks) ?? track.Sample(0);
            var after = track.SampleAtOrBefore(before.Tick + PlaybackTrack.SampleTicks) ?? before;
            if (after.Tick == before.Tick)
            {
                after = before;
            }
            if (!ReferenceEquals(before, _sampleA))
            {
                Fill(before, _a);
                _sampleA = before;
            }
            if (!ReferenceEquals(after, _sampleB))
            {
                Fill(after, _b);
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

        private void Fill(UnitSample sample, BattleHost.UnitSnapshot into)
        {
            int count = Math.Min(sample.Count, Capacity);
            into.Tick = sample.Tick;
            into.Count = count;
            for (int u = 0; u < count; u++)
            {
                bool alive = (sample.State[u] & UnitSample.Alive) != 0;
                into.Alive[u] = alive;
                if (!alive)
                {
                    continue;
                }
                var p = new GodGame.Sim.Math.FixVec2(
                    GodGame.Sim.Math.Fix.FromRatio(sample.XCm[u], 100),
                    GodGame.Sim.Math.Fix.FromRatio(sample.YCm[u], 100)
                );
                into.X[u] = sample.XCm[u] / 100f;
                into.Z[u] = sample.YCm[u] / 100f;
                into.Y[u] = _terrain.HeightMmAt(p) / 1000f;
                into.Regiment[u] = _regiment[u];
                into.UnitType[u] = _unitType[u];
            }
        }
    }
}
