using System;
using System.Collections.Generic;

namespace GodGame.Sim
{
    /// <summary>
    /// One field's behaviour (water, wetness and mud, temperature, …). The field system calls models in the fixed
    /// interaction order (plan, section 3.4); each steps the tiles it is given, reading <see cref="FieldGrid.Values"/>
    /// and writing the cells it changes into the grid's next buffer (<see cref="FieldGrid.Next"/>, which starts as a
    /// copy of the tiles' values); the commit finds which tiles changed. Kernels go through the parallel runner, one
    /// chunk per range of tiles, each writing only its own tiles. A model keeps no state between steps: everything
    /// lasting lives in the grids.
    /// </summary>
    public interface IFieldModel
    {
        /// <summary>The field it steps (the grid whose next buffer it fills and which is committed after it).</summary>
        string FieldId { get; }

        /// <summary>
        /// Other fields it writes for the same tiles (water's four outflows): prepared and committed with the main one.
        /// Which tiles step follows the main field's activity alone.
        /// </summary>
        IReadOnlyList<string> AlsoWrites { get; }

        /// <summary>How often it steps.</summary>
        TickRate Rate { get; }

        /// <summary>
        /// True: each awake tile steps on its own phase within the period (tile index modulo the period), which spreads
        /// the work over the ticks; for fields whose tiles exchange nothing within a step. False: every awake tile
        /// steps together when the rate is due (for flows between tiles, so both sides see the same moment).
        /// </summary>
        bool Staggered { get; }

        void Step(FieldStepContext context, FieldGrid grid, ReadOnlySpan<int> tiles);
    }

    /// <summary>What a field model gets when it steps.</summary>
    public sealed class FieldStepContext
    {
        internal FieldStepContext(TickContext tick, FieldStore fields)
        {
            Tick = tick;
            Fields = fields;
        }

        public TickContext Tick { get; }

        public FieldStore Fields { get; }

        public IParallelRunner Runner => Tick.Runner;
    }

    /// <summary>
    /// The fields step (plan, section 3.4, step 2): runs every model in the given order, each on the awake tiles of
    /// its field that are due this tick, then commits them. Runs every tick; each model's rate decides when it steps.
    /// </summary>
    public sealed class FieldSystem : ISimSystem
    {
        private readonly IFieldModel[] _models;

        // Scratch, overwritten every run before it is read.
        private int[] _awake = new int[0];
        private int[] _due = new int[0];

        public FieldSystem(IReadOnlyList<IFieldModel> models)
        {
            if (models == null)
            {
                throw new ArgumentNullException(nameof(models));
            }
            _models = new IFieldModel[models.Count];
            for (int i = 0; i < _models.Length; i++)
            {
                _models[i] = models[i] ?? throw new ArgumentNullException(nameof(models));
            }
        }

        public TickStep Step => TickStep.Fields;

        public TickRate Rate => TickRate.EveryTick;

        public IReadOnlyList<IFieldModel> Models => _models;

        public void Run(TickContext context)
        {
            var fields = context.World.Fields;
            if (fields == null || _models.Length == 0)
            {
                return;
            }
            var step = new FieldStepContext(context, fields);
            foreach (var model in _models)
            {
                var grid = fields.Get(model.FieldId);
                if (_awake.Length < grid.TileCount)
                {
                    _awake = new int[grid.TileCount];
                    _due = new int[grid.TileCount];
                }
                int awake = grid.AwakeTiles(_awake);
                int due = 0;
                var rate = model.Rate;
                if (model.Staggered)
                {
                    for (int k = 0; k < awake; k++)
                    {
                        if (rate.IsDueFor(context.Tick, _awake[k]))
                        {
                            _due[due++] = _awake[k];
                        }
                    }
                }
                else if (rate.IsDue(context.Tick))
                {
                    Array.Copy(_awake, _due, awake);
                    due = awake;
                }
                if (due == 0)
                {
                    continue;
                }
                var tiles = new ReadOnlySpan<int>(_due, 0, due);
                grid.PrepareStep(tiles);
                foreach (string id in model.AlsoWrites)
                {
                    fields.Get(id).PrepareStep(tiles);
                }
                model.Step(step, grid, tiles);
                grid.Commit(tiles);
                foreach (string id in model.AlsoWrites)
                {
                    fields.Get(id).Commit(tiles);
                }
            }
        }
    }
}
