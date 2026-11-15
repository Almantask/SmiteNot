using System;
using System.Threading;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>Where every soldier was at one tick, for drawing (never for resuming: that is what keyframes are for).</summary>
    internal interface ITickedEntry
    {
        int Tick { get; }
    }

    /// <summary>A 5 Hz snapshot of all living units' positions, state flags and ballistic heights (presentation only).</summary>
    public sealed class UnitSample : ITickedEntry
    {
        internal UnitSample(int tick, int count)
        {
            Tick = tick;
            XCm = new int[count];
            YCm = new int[count];
            State = new byte[count];
            HeightCm = new short[count];
        }

        public int Tick { get; }

        /// <summary>Unit slots recorded (the pool's high-water mark).</summary>
        public int Count => XCm.Length;

        /// <summary>Position in centimetres on the ground plane.</summary>
        public int[] XCm { get; }

        public int[] YCm { get; }

        /// <summary><see cref="Alive"/> plus the low <see cref="UnitFlags"/> (launched, stunned, teetering, swept, burning, routing, garrisoned).</summary>
        public byte[] State { get; }

        /// <summary>
        /// A launched soldier's height above the map datum, in centimetres (spec S2 9.1); 0 for soldiers on the ground,
        /// who stand at the terrain's height.
        /// </summary>
        public short[] HeightCm { get; }

        public const byte Alive = 0x80;
    }

    /// <summary>One soldier launch recorded for scrubbing and replays (S2 9.4: presentation only, never hashed).</summary>
    public sealed class LaunchRecord
    {
        public LaunchRecord(
            int launchTick,
            int unit,
            int sourceEvent,
            int launchEvent,
            int startXCm,
            int startYCm,
            int startHeightMm,
            int velXMmPerS,
            int velYMmPerS,
            int verticalMmPerS,
            int spinCdPerS,
            int gravityMmPerS2
        )
        {
            LaunchTick = launchTick;
            Unit = unit;
            SourceEvent = sourceEvent;
            LaunchEvent = launchEvent;
            StartXCm = startXCm;
            StartYCm = startYCm;
            StartHeightMm = startHeightMm;
            VelXMmPerS = velXMmPerS;
            VelYMmPerS = velYMmPerS;
            VerticalMmPerS = verticalMmPerS;
            SpinCdPerS = spinCdPerS;
            GravityMmPerS2 = gravityMmPerS2;
        }

        public int LaunchTick { get; }
        public int Unit { get; }
        public int SourceEvent { get; }
        public int LaunchEvent { get; }
        public int StartXCm { get; }
        public int StartYCm { get; }
        public int StartHeightMm { get; }
        public int VelXMmPerS { get; }
        public int VelYMmPerS { get; }
        public int VerticalMmPerS { get; }
        public int SpinCdPerS { get; }
        public int GravityMmPerS2 { get; }
    }

    /// <summary>The tick a launched soldier came back down, matched by <see cref="LaunchRecord.LaunchEvent"/>.</summary>
    public sealed class LaunchLandingRecord
    {
        public LaunchLandingRecord(int landTick, int launchEvent)
        {
            LandTick = landTick;
            LaunchEvent = launchEvent;
        }

        public int LandTick { get; }
        public int LaunchEvent { get; }
    }

    /// <summary>One deck or ice segment's load, strength, strain stage and failure state at a sample tick.</summary>
    public readonly struct SegmentStateSample
    {
        public SegmentStateSample(
            ushort loadPermille,
            ushort strengthPermille,
            StrainStage stage,
            bool broken
        )
        {
            LoadPermille = loadPermille;
            StrengthPermille = strengthPermille;
            Flags = (byte)(((int)stage & 0x03) | (broken ? 0x04 : 0));
        }

        public ushort LoadPermille { get; }
        public ushort StrengthPermille { get; }
        public byte Flags { get; }
        public StrainStage Stage => (StrainStage)(Flags & 0x03);
        public bool Broken => (Flags & 0x04) != 0;
    }

    /// <summary>Every deck segment's strain state at one 5 Hz sample tick (presentation only).</summary>
    public sealed class StructureSample : ITickedEntry
    {
        public StructureSample(int tick, SegmentStateSample[] segments)
        {
            Tick = tick;
            Segments = segments;
        }

        public int Tick { get; }
        public SegmentStateSample[] Segments { get; }
    }

    /// <summary>Metadata for a bridge/trestle/pontoon segment built during the battle (presentation only).</summary>
    public sealed class DynamicSegmentInfo
    {
        public DynamicSegmentInfo(
            int addedTick,
            int index,
            int kindIndex,
            int deckHeightMm,
            int[] cells
        )
        {
            AddedTick = addedTick;
            Index = index;
            KindIndex = kindIndex;
            DeckHeightMm = deckHeightMm;
            Cells = cells;
        }

        public int AddedTick { get; }
        public int Index { get; }
        public int KindIndex { get; }
        public int DeckHeightMm { get; }
        public int[] Cells { get; }
    }

    /// <summary>One regiment's morale, order and alive strength at a 5 Hz sample tick (presentation only).</summary>
    public readonly struct RegimentStateSample
    {
        public RegimentStateSample(
            ushort morale,
            RegimentOrder order,
            bool alive,
            bool wiped,
            int aliveCount
        )
        {
            Morale = morale;
            Order = order;
            Flags = (byte)(
                (alive ? 1 : 0) | (wiped ? 2 : 0) | (order == RegimentOrder.Rout ? 4 : 0)
            );
            AliveCount = aliveCount;
        }

        public ushort Morale { get; }
        public RegimentOrder Order { get; }
        public byte Flags { get; }
        public int AliveCount { get; }
        public bool Alive => (Flags & 1) != 0;
        public bool Wiped => (Flags & 2) != 0;
        public bool Routing => (Flags & 4) != 0;
    }

    /// <summary>Every regiment's morale and order at one 5 Hz sample tick (presentation only).</summary>
    public sealed class RegimentFrame : ITickedEntry
    {
        public RegimentFrame(int tick, RegimentStateSample[] regiments)
        {
            Tick = tick;
            Regiments = regiments;
        }

        public int Tick { get; }
        public RegimentStateSample[] Regiments { get; }
    }

    /// <summary>
    /// The fields at one second, for drawing: every cell's four integers in the GPU layout (water µm; flow mm/s, east and
    /// north + 32768 in 16 bits each; mud | wetness &lt;&lt; 16; the navigation speed codes), for every tile in a full
    /// frame and only the tiles that changed since the previous frame otherwise.
    /// </summary>
    public sealed class FieldFrame
    {
        internal FieldFrame(int tick, bool full, int[] tiles, uint[][] data)
        {
            Tick = tick;
            Full = full;
            Tiles = tiles;
            Data = data;
        }

        public int Tick { get; }

        public bool Full { get; }

        /// <summary>The water grid's tiles this frame holds.</summary>
        public int[] Tiles { get; }

        /// <summary>Per tile, its cells row by row (x fastest), four uints each.</summary>
        public uint[][] Data { get; }
    }

    /// <summary>
    /// The playback track (plan, section 3.7; tech notes, section 6: smooth scrubbing): unit samples 5 times a second and
    /// field frames once a second, recorded while the timeline fills the future. Scrubbing draws from it, interpolating
    /// between samples, so moving the cursor never re-simulates. Presentation data only: not hashed, not in snapshots.
    /// <para>
    /// Threads: one writer (the preview worker) appends; readers on other threads see everything below
    /// <see cref="SampleCount"/> and <see cref="FrameCount"/>, published after the data is complete. Samples and frames
    /// never change once added, and storage grows in fixed blocks that are never moved, so reading needs no lock. An edit
    /// doesn't truncate a track in place: <see cref="Fork"/> starts a new one sharing everything up to the resume tick.
    /// </para>
    /// </summary>
    public sealed class PlaybackTrack
    {
        /// <summary>5 samples a second.</summary>
        public const int SampleTicks = 4;

        /// <summary>A field frame every second, a full one every 10 s.</summary>
        public const int FrameTicks = SimConstants.TicksPerSecond;

        public const int FullFrameEvery = 10;

        public const int Stride = 4;

        private const int BlockSize = 1024;

        private readonly UnitSample[][] _samples = new UnitSample[4096][];
        private readonly FieldFrame[][] _frames = new FieldFrame[4096][];
        private readonly StructureSample[][] _structures = new StructureSample[4096][];
        private readonly DynamicSegmentInfo[][] _dynamicSegments = new DynamicSegmentInfo[256][];
        private readonly RegimentFrame[][] _regiments = new RegimentFrame[4096][];
        private readonly LaunchRecord[][] _launches = new LaunchRecord[4096][];
        private readonly LaunchLandingRecord[][] _landings = new LaunchLandingRecord[4096][];
        private int _sampleCount;
        private int _frameCount;
        private int _structureCount;
        private int _dynamicSegmentCount;
        private int _regimentFrameCount;
        private int _launchCount;
        private int _landingCount;
        private Side[] _regimentSides = Array.Empty<Side>();
        private int[] _regimentStartingSizes = Array.Empty<int>();
        private int[] _unitRegiments = Array.Empty<int>();
        private uint[] _last; // the fields as of the newest frame, for deltas (writer only)

        public PlaybackTrack(int columns, int rows, int tileCells)
        {
            Columns = columns;
            Rows = rows;
            TileCells = tileCells;
            TileColumns = (columns + tileCells - 1) / tileCells;
            TileRows = (rows + tileCells - 1) / tileCells;
            _last = new uint[columns * rows * Stride];
        }

        public int Columns { get; }

        public int Rows { get; }

        public int TileCells { get; }

        public int TileColumns { get; }

        public int TileRows { get; }

        public int SampleCount => Volatile.Read(ref _sampleCount);

        public int FrameCount => Volatile.Read(ref _frameCount);

        public int StructureSampleCount => Volatile.Read(ref _structureCount);

        public int DynamicSegmentCount => Volatile.Read(ref _dynamicSegmentCount);

        public int RegimentFrameCount => Volatile.Read(ref _regimentFrameCount);

        public int LaunchCount => Volatile.Read(ref _launchCount);

        public int LaunchLandingCount => Volatile.Read(ref _landingCount);

        public int RegimentCount => Volatile.Read(ref _regimentSides).Length;

        public Side[] RegimentSides => Volatile.Read(ref _regimentSides);

        public int[] RegimentStartingSizes => Volatile.Read(ref _regimentStartingSizes);

        public int[] UnitRegiments => Volatile.Read(ref _unitRegiments);

        public UnitSample Sample(int index) => _samples[index / BlockSize]![index % BlockSize];

        public FieldFrame Frame(int index) => _frames[index / BlockSize]![index % BlockSize];

        public StructureSample Structure(int index) =>
            _structures[index / BlockSize]![index % BlockSize];

        public DynamicSegmentInfo DynamicSegment(int index) =>
            _dynamicSegments[index / BlockSize]![index % BlockSize];

        public RegimentFrame RegimentAt(int index) =>
            _regiments[index / BlockSize]![index % BlockSize];

        public LaunchRecord Launch(int index) => _launches[index / BlockSize]![index % BlockSize];

        public LaunchLandingRecord LaunchLanding(int index) =>
            _landings[index / BlockSize]![index % BlockSize];

        /// <summary>The newest sample at or before <paramref name="tick"/>, or null before the first.</summary>
        public UnitSample? SampleAtOrBefore(int tick) =>
            FindAtOrBefore(_samples, SampleCount, tick);

        /// <summary>The newest structure sample at or before <paramref name="tick"/>, or null when the battle has none.</summary>
        public StructureSample? StructureAtOrBefore(int tick) =>
            FindAtOrBefore(_structures, StructureSampleCount, tick);

        /// <summary>The newest regiment morale/order frame at or before <paramref name="tick"/>, or null before the first.</summary>
        public RegimentFrame? RegimentsAtOrBefore(int tick) =>
            FindAtOrBefore(_regiments, RegimentFrameCount, tick);

        private static T? FindAtOrBefore<T>(T[][] blocks, int count, int tick)
            where T : class, ITickedEntry
        {
            int lo = 0;
            int hi = count - 1;
            int found = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                var entry = blocks[mid / BlockSize]![mid % BlockSize];
                if (entry.Tick <= tick)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return found < 0 ? null : blocks[found / BlockSize]![found % BlockSize];
        }

        /// <summary>
        /// Fills <paramref name="cells"/> (four uints per cell) with the fields as of the newest frame at or before
        /// <paramref name="tick"/>: its last full frame, then the deltas after it. Returns that frame's tick, or -1.
        /// </summary>
        public int FieldsAt(int tick, uint[] cells)
        {
            int count = FrameCount;
            int last = -1;
            for (int i = count - 1; i >= 0; i--)
            {
                if (Frame(i).Tick <= tick)
                {
                    last = i;
                    break;
                }
            }
            if (last < 0)
            {
                return -1;
            }
            int full = last;
            while (!Frame(full).Full)
            {
                full--;
            }
            for (int i = full; i <= last; i++)
            {
                Apply(Frame(i), cells);
            }
            return Frame(last).Tick;
        }

        private void Apply(FieldFrame frame, uint[] cells)
        {
            for (int k = 0; k < frame.Tiles.Length; k++)
            {
                int tile = frame.Tiles[k];
                int x0 = tile % TileColumns * TileCells;
                int y0 = tile / TileColumns * TileCells;
                int x1 = System.Math.Min(Columns, x0 + TileCells);
                int y1 = System.Math.Min(Rows, y0 + TileCells);
                var data = frame.Data[k];
                int o = 0;
                for (int y = y0; y < y1; y++)
                {
                    int width = (x1 - x0) * Stride;
                    Array.Copy(data, o, cells, (y * Columns + x0) * Stride, width);
                    o += width;
                }
            }
        }

        /// <summary>Records the world's units (writer only).</summary>
        public void RecordUnits(int tick, World world)
        {
            if (world.Units.HighWater > _unitRegiments.Length)
            {
                var regs = new int[world.Units.HighWater];
                for (int i = 0; i < regs.Length; i++)
                {
                    regs[i] = world.UnitRegiment[i];
                }
                Volatile.Write(ref _unitRegiments, regs);
            }
            var sample = new UnitSample(tick, world.Units.HighWater);
            for (int u = 0; u < sample.Count; u++)
            {
                if (!world.Units.IsAlive(u))
                {
                    continue;
                }
                sample.XCm[u] = (int)(world.Position[u].X.Raw * 100 >> Fix.FractionalBits);
                sample.YCm[u] = (int)(world.Position[u].Y.Raw * 100 >> Fix.FractionalBits);
                sample.State[u] = (byte)(UnitSample.Alive | ((uint)world.Flags[u] & 0x7F));
                if ((world.Flags[u] & UnitFlags.Launched) != 0)
                {
                    // Centimetres fit a short for any height within ±327 m of the datum.
                    sample.HeightCm[u] = (short)
                        System.Math.Max(
                            short.MinValue,
                            System.Math.Min(short.MaxValue, world.Ballistic.HeightMm[u] / 10)
                        );
                }
            }
            Append(_samples, ref _sampleCount, sample);
        }

        /// <summary>Records every deck/ice segment's strain state at <paramref name="tick"/> (writer only).</summary>
        public void RecordStructures(int tick, World world)
        {
            var structures = world.Structures;
            if (structures == null || structures.Count == 0)
            {
                return;
            }
            for (int s = structures.StaticCount + DynamicSegmentCount; s < structures.Count; s++)
            {
                var cells = structures.BattleCells[s];
                if (cells == null)
                {
                    continue;
                }
                Append(
                    _dynamicSegments,
                    ref _dynamicSegmentCount,
                    new DynamicSegmentInfo(
                        tick,
                        s,
                        structures.BattleKind[s],
                        structures.BattleDeckHeight[s],
                        (int[])cells.Clone()
                    )
                );
            }
            var states = new SegmentStateSample[structures.Count];
            for (int s = 0; s < states.Length; s++)
            {
                int load = System.Math.Max(
                    0,
                    System.Math.Min(ushort.MaxValue, structures.LoadPermille[s])
                );
                int strength = System.Math.Max(
                    0,
                    System.Math.Min(ushort.MaxValue, structures.Strength[s])
                );
                states[s] = new SegmentStateSample(
                    (ushort)load,
                    (ushort)strength,
                    structures.Stage[s],
                    structures.Broken[s]
                );
            }
            Append(_structures, ref _structureCount, new StructureSample(tick, states));
        }

        /// <summary>Records every regiment's morale, order and live count at <paramref name="tick"/> (writer only).</summary>
        public void RecordRegiments(int tick, World world)
        {
            int count = world.Regiments.HighWater;
            if (_regimentSides.Length == 0 && count > 0)
            {
                var sides = new Side[count];
                var starts = new int[count];
                for (int r = 0; r < count; r++)
                {
                    sides[r] = world.RegimentSide[r];
                    starts[r] = world.RegimentStartingSize[r];
                }
                Volatile.Write(ref _regimentStartingSizes, starts);
                Volatile.Write(ref _regimentSides, sides);
            }
            if (count == 0)
            {
                return;
            }
            var regs = new RegimentStateSample[count];
            for (int r = 0; r < count; r++)
            {
                int morale = System.Math.Max(
                    0,
                    System.Math.Min(ushort.MaxValue, world.RegimentMorale[r])
                );
                regs[r] = new RegimentStateSample(
                    (ushort)morale,
                    world.Army.Order[r],
                    world.Regiments.IsAlive(r),
                    world.Army.Wiped[r],
                    world.RegimentAlive[r]
                );
            }
            Append(_regiments, ref _regimentFrameCount, new RegimentFrame(tick, regs));
        }

        /// <summary>Appends an immutable launch record (writer only; chronological by <see cref="LaunchRecord.LaunchTick"/>).</summary>
        public void RecordLaunch(LaunchRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }
            Append(_launches, ref _launchCount, record);
        }

        /// <summary>Appends an immutable landing timestamp paired with a launch event (writer only).</summary>
        public void RecordLaunchLanding(LaunchLandingRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }
            Append(_landings, ref _landingCount, record);
        }

        /// <summary>Records the world's fields: every tile when <paramref name="full"/>, else the ones that changed.</summary>
        public void RecordFields(int tick, World world, int cellSizeMm, bool full)
        {
            var fields = world.Fields;
            if (fields == null)
            {
                return;
            }
            var now = new uint[_last.Length];
            Fill(fields, cellSizeMm, now);
            int tileCount = TileColumns * TileRows;
            var tiles = new int[tileCount];
            var data = new uint[tileCount][];
            int n = 0;
            for (int t = 0; t < tileCount; t++)
            {
                if (full || TileDiffers(t, now))
                {
                    tiles[n] = t;
                    data[n] = CopyTile(t, now);
                    n++;
                }
            }
            Array.Resize(ref tiles, n);
            Array.Resize(ref data, n);
            _last = now;
            Append(_frames, ref _frameCount, new FieldFrame(tick, full, tiles, data));
        }

        /// <summary>A new track holding this one's samples and frames up to <paramref name="tick"/> (an edit's resume point).</summary>
        public PlaybackTrack Fork(int tick)
        {
            var fork = new PlaybackTrack(Columns, Rows, TileCells);
            Volatile.Write(ref fork._regimentStartingSizes, _regimentStartingSizes);
            Volatile.Write(ref fork._regimentSides, _regimentSides);
            Volatile.Write(ref fork._unitRegiments, _unitRegiments);
            for (int i = 0; i < SampleCount && Sample(i).Tick <= tick; i++)
            {
                Append(fork._samples, ref fork._sampleCount, Sample(i));
            }
            for (int i = 0; i < StructureSampleCount && Structure(i).Tick <= tick; i++)
            {
                Append(fork._structures, ref fork._structureCount, Structure(i));
            }
            for (int i = 0; i < DynamicSegmentCount && DynamicSegment(i).AddedTick <= tick; i++)
            {
                Append(fork._dynamicSegments, ref fork._dynamicSegmentCount, DynamicSegment(i));
            }
            for (int i = 0; i < RegimentFrameCount && RegimentAt(i).Tick <= tick; i++)
            {
                Append(fork._regiments, ref fork._regimentFrameCount, RegimentAt(i));
            }
            for (int i = 0; i < LaunchCount && Launch(i).LaunchTick <= tick; i++)
            {
                Append(fork._launches, ref fork._launchCount, Launch(i));
            }
            for (int i = 0; i < LaunchLandingCount && LaunchLanding(i).LandTick <= tick; i++)
            {
                Append(fork._landings, ref fork._landingCount, LaunchLanding(i));
            }
            for (int i = 0; i < FrameCount && Frame(i).Tick <= tick; i++)
            {
                Append(fork._frames, ref fork._frameCount, Frame(i));
            }
            if (fork._frameCount > 0)
            {
                fork.FieldsAt(tick, fork._last);
            }
            return fork;
        }

        private bool TileDiffers(int tile, uint[] now)
        {
            int x0 = tile % TileColumns * TileCells;
            int y0 = tile / TileColumns * TileCells;
            int x1 = System.Math.Min(Columns, x0 + TileCells);
            int y1 = System.Math.Min(Rows, y0 + TileCells);
            for (int y = y0; y < y1; y++)
            {
                int from = (y * Columns + x0) * Stride;
                int to = (y * Columns + x1) * Stride;
                for (int i = from; i < to; i++)
                {
                    if (now[i] != _last[i])
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private uint[] CopyTile(int tile, uint[] now)
        {
            int x0 = tile % TileColumns * TileCells;
            int y0 = tile / TileColumns * TileCells;
            int x1 = System.Math.Min(Columns, x0 + TileCells);
            int y1 = System.Math.Min(Rows, y0 + TileCells);
            int width = (x1 - x0) * Stride;
            var data = new uint[width * (y1 - y0)];
            for (int y = y0; y < y1; y++)
            {
                Array.Copy(now, (y * Columns + x0) * Stride, data, (y - y0) * width, width);
            }
            return data;
        }

        /// <summary>The fields in the GPU layout (see <see cref="FieldFrame"/>).</summary>
        public static void Fill(FieldStore fields, int cellSizeMm, uint[] cells)
        {
            var depth = Optional(fields, WaterModel.Depth);
            var mud = Optional(fields, GroundModel.Mud);
            var wetness = Optional(fields, GroundModel.Wetness);
            var codes = Optional(fields, FieldSpeed.FieldId);
            int count = fields.Columns * fields.Rows;
            for (int i = 0; i < count; i++)
            {
                int d = depth?.Values[i] ?? 0;
                uint flow = 32768u | 32768u << 16;
                if (d >= WaterModel.VelocityMinDepthUm)
                {
                    WaterModel.Velocity(
                        fields,
                        cellSizeMm,
                        i % fields.Columns,
                        i / fields.Columns,
                        out int east,
                        out int north
                    );
                    flow = (uint)(east + 32768) | (uint)(north + 32768) << 16;
                }
                int o = i * Stride;
                cells[o] = (uint)d;
                cells[o + 1] = flow;
                cells[o + 2] = (uint)(mud?.Values[i] ?? 0) | (uint)(wetness?.Values[i] ?? 0) << 16;
                cells[o + 3] = (uint)(codes?.Values[i] ?? FieldSpeed.AllDry);
            }
        }

        private static FieldGrid? Optional(FieldStore fields, string id) =>
            fields.Rules.IndexOf(id) >= 0 ? fields.Get(id) : null;

        private static void Append<T>(T[][] blocks, ref int count, T item)
            where T : class
        {
            int index = count;
            int block = index / BlockSize;
            if (block >= blocks.Length)
            {
                throw new InvalidOperationException("The playback track is full.");
            }
            blocks[block] ??= new T[BlockSize];
            blocks[block][index % BlockSize] = item;
            Volatile.Write(ref count, index + 1); // published only once the item is in place
        }
    }
}
