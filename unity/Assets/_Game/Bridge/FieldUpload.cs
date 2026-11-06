using System;
using GodGame.Sim;
using UnityEngine;

namespace GodGame.Bridge
{
    /// <summary>
    /// The world's fields for shaders, one record of four integers per simulation cell, in the simulation's own units
    /// (plan, section 6: fields arrive on the GPU as integers in documented units):
    /// <list type="bullet">
    /// <item>x: water depth in µm;</item>
    /// <item>y: flow velocity in mm/s, east + 32768 in the low 16 bits and north + 32768 in the high 16;</item>
    /// <item>z: mud (0-65535) in the low 16 bits and wetness (0-65535) in the high 16;</item>
    /// <item>w: the navigation speed codes (one byte per class: foot, horse, wheeled; see <see cref="FieldSpeed"/>).</item>
    /// </list>
    /// <see cref="BattleHost"/> fills a <see cref="Snapshot"/> on its worker after each field step; the main thread
    /// copies the newest one into a structured GPU buffer (<c>StructuredBuffer&lt;uint4&gt;</c>, row-major, x fastest),
    /// which every field shader reads with <c>_FieldSize</c>. Presentation only.
    /// </summary>
    public sealed class FieldUpload : IDisposable
    {
        public static readonly int FieldsId = Shader.PropertyToID("_Fields");
        public static readonly int FieldSizeId = Shader.PropertyToID("_FieldSize");

        /// <summary>Uints per cell.</summary>
        public const int Stride = 4;

        /// <summary>One field step's worth of cells.</summary>
        public sealed class Snapshot
        {
            public Snapshot(int columns, int rows)
            {
                Columns = columns;
                Rows = rows;
                Cells = new uint[columns * rows * Stride];
            }

            public int Tick = -1;
            public readonly int Columns;
            public readonly int Rows;
            public readonly uint[] Cells;

            /// <summary>Copies the world's fields (worker thread, between ticks).</summary>
            public void Fill(World world, int tick, int cellSizeMm)
            {
                var fields = world.Fields;
                Tick = tick;
                if (fields == null)
                {
                    Array.Clear(Cells, 0, Cells.Length);
                    return;
                }
                var depth = Optional(fields, WaterModel.Depth);
                var mud = Optional(fields, GroundModel.Mud);
                var wetness = Optional(fields, GroundModel.Wetness);
                var codes = Optional(fields, FieldSpeed.FieldId);
                int cells = Columns * Rows;
                for (int i = 0; i < cells; i++)
                {
                    int d = depth?.Values[i] ?? 0;
                    uint flow = 32768u | 32768u << 16;
                    if (d >= WaterModel.VelocityMinDepthUm)
                    {
                        WaterModel.Velocity(
                            fields,
                            cellSizeMm,
                            i % Columns,
                            i / Columns,
                            out int east,
                            out int north
                        );
                        flow = (uint)(east + 32768) | (uint)(north + 32768) << 16;
                    }
                    int o = i * Stride;
                    Cells[o] = (uint)d;
                    Cells[o + 1] = flow;
                    Cells[o + 2] =
                        (uint)(mud?.Values[i] ?? 0) | (uint)(wetness?.Values[i] ?? 0) << 16;
                    Cells[o + 3] = (uint)(codes?.Values[i] ?? FieldSpeed.AllDry);
                }
            }

            private static FieldGrid Optional(FieldStore fields, string id) =>
                fields.Rules.IndexOf(id) >= 0 ? fields.Get(id) : null;
        }

        private readonly GraphicsBuffer _buffer;
        private int _uploadedTick = -1;

        public FieldUpload(int columns, int rows)
        {
            Columns = columns;
            Rows = rows;
            _buffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                columns * rows,
                Stride * sizeof(uint)
            );
            _buffer.SetData(new uint[columns * rows * Stride]);
        }

        public int Columns { get; }

        public int Rows { get; }

        public GraphicsBuffer Buffer => _buffer;

        /// <summary>The field step the GPU holds; -1 before the first upload.</summary>
        public int UploadedTick => _uploadedTick;

        /// <summary>Uploads the host's newest field snapshot if it is newer than the last one. Main thread.</summary>
        public bool Update(BattleHost host)
        {
            var snapshot = host.ReadFields();
            if (snapshot == null || snapshot.Tick == _uploadedTick)
            {
                return false;
            }
            _buffer.SetData(snapshot.Cells);
            _uploadedTick = snapshot.Tick;
            return true;
        }

        /// <summary>Points a material at the buffer.</summary>
        public void Bind(Material material)
        {
            material.SetBuffer(FieldsId, _buffer);
            material.SetVector(FieldSizeId, new Vector4(Columns, Rows, 0, 0));
        }

        public void Dispose() => _buffer.Release();
    }
}
