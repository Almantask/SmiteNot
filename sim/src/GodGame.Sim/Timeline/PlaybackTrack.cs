using System;
using System.Threading;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>Where every soldier was at one tick, for drawing (never for resuming: that is what keyframes are for).</summary>
    public sealed class UnitSample
    {
        internal UnitSample(int tick, int count)
        {
            Tick = tick;
            XCm = new int[count];
            YCm = new int[count];
            State = new byte[count];
        }

        public int Tick { get; }

        /// <summary>Unit slots recorded (the pool's high-water mark).</summary>
        public int Count => XCm.Length;

        /// <summary>Position in centimetres on the ground plane.</summary>
        public int[] XCm { get; }

        public int[] YCm { get; }

        /// <summary><see cref="Alive"/> plus the low <see cref="UnitFlags"/> (launched, stunned, teetering, swept).</summary>
        public byte[] State { get; }

        public const byte Alive = 0x80;
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
        private int _sampleCount;
        private int _frameCount;
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

        public UnitSample Sample(int index) => _samples[index / BlockSize]![index % BlockSize];

        public FieldFrame Frame(int index) => _frames[index / BlockSize]![index % BlockSize];

        /// <summary>The newest sample at or before <paramref name="tick"/>, or null before the first.</summary>
        public UnitSample? SampleAtOrBefore(int tick)
        {
            int count = SampleCount;
            int lo = 0;
            int hi = count - 1;
            int found = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (Sample(mid).Tick <= tick)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return found < 0 ? null : Sample(found);
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
            var sample = new UnitSample(tick, world.Units.HighWater);
            for (int u = 0; u < sample.Count; u++)
            {
                if (!world.Units.IsAlive(u))
                {
                    continue;
                }
                sample.XCm[u] = (int)(world.Position[u].X.Raw * 100 >> Fix.FractionalBits);
                sample.YCm[u] = (int)(world.Position[u].Y.Raw * 100 >> Fix.FractionalBits);
                sample.State[u] = (byte)(UnitSample.Alive | ((uint)world.Flags[u] & 0x0F));
            }
            Append(_samples, ref _sampleCount, sample);
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
            for (int i = 0; i < SampleCount && Sample(i).Tick <= tick; i++)
            {
                Append(fork._samples, ref fork._sampleCount, Sample(i));
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
