using System;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// Every world field of a battle, in the rules file's order (the order they are hashed and snapshotted), on the
    /// map's cell grid. Part of the world's state: <see cref="World.Fields"/>.
    /// </summary>
    public sealed class FieldStore
    {
        private readonly FieldGrid[] _grids;

        public FieldStore(FieldRules rules, int columns, int rows)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Columns = columns;
            Rows = rows;
            _grids = new FieldGrid[rules.Fields.Count];
            for (int i = 0; i < _grids.Length; i++)
            {
                _grids[i] = new FieldGrid(
                    rules.Fields[i],
                    columns,
                    rows,
                    rules.TileCells,
                    rules.SleepAfterSteps
                );
            }
        }

        public FieldRules Rules { get; }

        public int Columns { get; }

        public int Rows { get; }

        public int Count => _grids.Length;

        public FieldGrid this[int index] => _grids[index];

        /// <summary>The field with this id; throws when the rules don't define it.</summary>
        public FieldGrid Get(string id)
        {
            int index = Rules.IndexOf(id);
            if (index < 0)
            {
                throw new ArgumentException(
                    "The rules define no field \"" + id + "\".",
                    nameof(id)
                );
            }
            return _grids[index];
        }

        public void AppendTo(SimHash hash)
        {
            hash.AppendInt32(_grids.Length);
            for (int i = 0; i < _grids.Length; i++)
            {
                _grids[i].AppendTo(hash);
            }
        }

        internal void WriteTo(ByteWriter writer)
        {
            writer.WriteInt32(_grids.Length);
            for (int i = 0; i < _grids.Length; i++)
            {
                _grids[i].WriteTo(writer);
            }
        }

        internal void ReadFrom(ByteReader reader)
        {
            if (reader.ReadInt32() != _grids.Length)
            {
                throw new FormatException("The snapshot has another set of fields.");
            }
            for (int i = 0; i < _grids.Length; i++)
            {
                _grids[i].ReadFrom(reader);
            }
        }
    }
}
