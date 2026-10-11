using System;
using GodGame.Sim;

namespace GodGame.Bridge
{
    /// <summary>
    /// Owns the simulation for the presentation layer and turns frame time into whole ticks.
    /// Unity may read anything from the simulation and may write only the plan; this class is the
    /// boundary. M0: the placeholder battle, stepped on the main thread. M4 moves stepping to a
    /// preview worker so the main thread never waits on the simulation (plan, section 3.6).
    /// </summary>
    public sealed class SimHost
    {
        public const double SecondsPerTick = 1.0 / SimConstants.TicksPerSecond;

        /// <summary>Catch-up cap per call, so a long frame (a breakpoint, a hitch) can't stall the editor.</summary>
        public const int MaxTicksPerAdvance = SimConstants.TicksPerSecond;

        private readonly PlaceholderBattle _battle;
        private readonly int[] _previousXMm;
        private readonly int[] _previousYMm;
        private double _accumulatedSeconds;

        public SimHost(PlaceholderSetup setup)
        {
            _battle = new PlaceholderBattle(setup ?? throw new ArgumentNullException(nameof(setup)));
            _previousXMm = _battle.PositionXMm.ToArray();
            _previousYMm = _battle.PositionYMm.ToArray();
        }

        public PlaceholderBattle Battle => _battle;

        public int Tick => _battle.Tick;

        /// <summary>How far real time is between the last tick and the next, 0 to 1, for smooth drawing.</summary>
        public double Alpha => _accumulatedSeconds / SecondsPerTick;

        /// <summary>Adds real time and runs every whole tick it covers. Returns the ticks run.</summary>
        public int Advance(double deltaSeconds)
        {
            if (deltaSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            _accumulatedSeconds += deltaSeconds;
            int ticks = 0;
            while (_accumulatedSeconds >= SecondsPerTick && ticks < MaxTicksPerAdvance)
            {
                _battle.PositionXMm.CopyTo(_previousXMm);
                _battle.PositionYMm.CopyTo(_previousYMm);
                _battle.Step();
                _accumulatedSeconds -= SecondsPerTick;
                ticks++;
            }
            if (ticks == MaxTicksPerAdvance && _accumulatedSeconds >= SecondsPerTick)
            {
                _accumulatedSeconds = 0; // drop the backlog rather than spiral
            }
            return ticks;
        }

        /// <summary>A unit's position in metres on the ground plane, blended between the last two ticks.</summary>
        public void GetPositionMetres(int unit, out double x, out double z)
        {
            double alpha = Math.Min(Alpha, 1.0);
            double fromX = _previousXMm[unit];
            double fromY = _previousYMm[unit];
            x = (fromX + (_battle.PositionXMm[unit] - fromX) * alpha) / 1000.0;
            z = (fromY + (_battle.PositionYMm[unit] - fromY) * alpha) / 1000.0;
        }
    }
}
