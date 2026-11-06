using System;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// The hazards step (spec S1, section 4; brief, 2026-10-09 and 2026-10-11): what the water does to the soldiers in
    /// it, from the current depth and flow of the cell each one stands in.
    /// <list type="bullet">
    /// <item>A soldier in water that sweeps its class (<see cref="WaterResponse.Sweeps"/>) loses its footing: it is
    /// <see cref="UnitFlags.Swept"/> and its drowning timer starts. Soldiers never walk into such water (movement closes
    /// it), so this happens when water rises around them, or when the crowd pushed them over after teetering.</item>
    /// <item>A swept soldier who drifts or paddles into water below the regain-footing thresholds is out: stunned for
    /// <see cref="HazardRules.BankExitStunTicks"/>, then back on its feet.</item>
    /// <item>A swept soldier whose timer runs out drowns and leaves the battle.</item>
    /// </list>
    /// Serial in slot order: it is one cell lookup per soldier, and despawns must run in a fixed order. Movement moves
    /// swept soldiers; this step only changes their state.
    /// </summary>
    public sealed class HazardSystem : ISimSystem
    {
        private readonly Navigation _navigation;
        private readonly Ruleset _rules;
        private int[] _drowned = new int[64];

        public HazardSystem(Navigation navigation)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _rules = navigation.Rules;
        }

        public TickStep Step => TickStep.Hazards;

        public TickRate Rate => TickRate.EveryTick;

        public void Run(TickContext context)
        {
            var world = context.World;
            var fields = world.Fields;
            var water = _rules.Fields.Water;
            var depth = fields != null && water != null ? fields.Get(WaterModel.Depth) : null;
            var hazards = _rules.Hazards;
            var terrain = _navigation.Terrain;
            int drowned = 0;
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (!world.Units.IsAlive(u))
                {
                    continue;
                }
                var flags = world.Flags[u];
                long depthMm = 0;
                long flowMmPerS = 0;
                if (depth != null)
                {
                    terrain.CellOf(world.Position[u], out int x, out int y);
                    long um = depth.Get(x, y);
                    if (um >= water!.DryDepthUm)
                    {
                        depthMm = um / 1000;
                        WaterModel.Velocity(
                            fields!,
                            terrain.CellSizeMm,
                            x,
                            y,
                            out int east,
                            out int north
                        );
                        flowMmPerS = IntMath.Sqrt((ulong)((long)east * east + (long)north * north));
                    }
                }
                var response = _rules
                    .Movement(_rules.UnitTypes[world.UnitType[u]].MovementClass)
                    .Water;

                if ((flags & UnitFlags.Swept) != 0)
                {
                    if (response.RegainsFooting(depthMm, flowMmPerS))
                    {
                        flags &= ~UnitFlags.Swept;
                        world.HazardTimer[u] = hazards.BankExitStunTicks;
                        if (hazards.BankExitStunTicks > 0)
                        {
                            flags |= UnitFlags.Stunned;
                        }
                        world.Counters.ReachedBank++;
                    }
                    else if (--world.HazardTimer[u] <= 0)
                    {
                        if (drowned == _drowned.Length)
                        {
                            Array.Resize(ref _drowned, drowned * 2);
                        }
                        _drowned[drowned++] = u;
                    }
                    world.Flags[u] = flags;
                    continue;
                }

                if (depthMm > 0 && response.Sweeps(depthMm, flowMmPerS))
                {
                    if ((flags & UnitFlags.Teetering) != 0 && world.HazardTimer[u] == 0)
                    {
                        world.Counters.PushedOver++; // movement pushed him over this tick
                    }
                    flags = (flags & ~(UnitFlags.Stunned | UnitFlags.Teetering)) | UnitFlags.Swept;
                    world.HazardTimer[u] = response.DrowningTicks;
                    world.Counters.Swept++;
                }
                else if ((flags & UnitFlags.Stunned) != 0)
                {
                    if (--world.HazardTimer[u] <= 0)
                    {
                        flags &= ~UnitFlags.Stunned;
                        world.HazardTimer[u] = 0;
                    }
                }
                else if ((flags & UnitFlags.Teetering) != 0 && world.HazardTimer[u] == 0)
                {
                    // Pushed over into water that had stopped sweeping by the time it landed: back on its feet.
                    flags &= ~UnitFlags.Teetering;
                }
                world.Flags[u] = flags;
            }
            for (int k = 0; k < drowned; k++)
            {
                world.DespawnUnit(world.Units.IdOf(_drowned[k]));
                world.Counters.Drowned++;
            }
        }
    }
}
