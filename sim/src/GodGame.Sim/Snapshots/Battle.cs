using System;
using System.Collections.Generic;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// Everything that decides a battle, and the one way to start it: the ruleset, the map, the plan and the seed. The
    /// CLI, the determinism harness, the goldens and Unity all build simulations through here, so they all run the same
    /// systems on the same starting world.
    /// </summary>
    public sealed class Battle
    {
        public Battle(Ruleset rules, MapData map, BattlePlan plan, ulong seed)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Seed = seed;
            if (!string.Equals(plan.MapId, map.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException("The plan was made for another map.", nameof(plan));
            }
        }

        public Ruleset Rules { get; }

        public MapData Map { get; }

        public BattlePlan Plan { get; }

        public ulong Seed { get; }

        /// <summary>The rules, map and plan this battle runs on, as one hash (the seed is kept apart, in Simulation.Seed).</summary>
        public ulong Id
        {
            get
            {
                var hash = new GodGame.Sim.Math.SimHash();
                hash.AppendUInt64(Rules.Hash);
                hash.AppendUInt64(Map.Hash);
                hash.AppendUInt64(Plan.Hash);
                return hash.Digest();
            }
        }

        /// <summary>
        /// The tick so far: the plan's casts (range rule, wind-up, Rain's water), the fields step (water, wetness and
        /// mud), navigation (regiment goals and flow fields),
        /// movement and hazards (swept, drowning, bank exit); every other step is still empty. The navigation's cost fields are built with
        /// <paramref name="runner"/>, which never changes the result.
        /// </summary>
        public ISimSystem[] CreateSystems(IParallelRunner runner)
        {
            var navigation = new Navigation(Map, Rules, runner);
            var systems = TickOrder.CreateDefault();
            systems[(int)TickStep.PlanEvents] = new CastSystem(Plan, navigation);
            // The fields at 5 Hz, in the interaction order (plan, section 3.4): water, then wetness and mud.
            var models = new List<IFieldModel>();
            if (Rules.Fields.Water != null)
            {
                models.Add(
                    new WaterModel(
                        navigation.Terrain,
                        Map,
                        Rules.Fields.Water,
                        TickOrder.FieldsRate
                    )
                );
            }
            if (Rules.Fields.Ground != null)
            {
                models.Add(new GroundModel(navigation.Terrain, Map, Rules, TickOrder.FieldsRate));
            }
            systems[(int)TickStep.Fields] = new FieldSystem(models);
            systems[(int)TickStep.Navigation] = new NavigationSystem(navigation);
            systems[(int)TickStep.Movement] = new MovementSystem(navigation);
            systems[(int)TickStep.Hazards] = new HazardSystem(navigation);
            return systems;
        }

        /// <summary>A fresh simulation at tick 0. Each call builds new systems: they belong to one simulation.</summary>
        public Simulation Start(IParallelRunner runner) =>
            new Simulation(
                BattleSetup.Deploy(Map, Plan, Rules),
                Seed,
                runner,
                CreateSystems(runner),
                Id
            );

        /// <summary>
        /// Runs from tick 0 for <paramref name="seconds"/> and returns the state hash after every second, the sequence
        /// goldens and the cross-runtime checks compare.
        /// </summary>
        public ulong[] PerSecondHashes(int seconds, IParallelRunner runner) =>
            Start(runner)
                .RunAndHash(seconds * SimConstants.TicksPerSecond, SimConstants.TicksPerSecond);

        public Replay ToReplay(int seconds, IReadOnlyList<ulong>? perSecondHashes = null) =>
            new Replay(
                SimVersion.Current,
                Rules.Hash,
                Map.Id,
                Map.Hash,
                Seed,
                Plan.CanonicalJson,
                seconds,
                perSecondHashes ?? Array.Empty<ulong>()
            );
    }
}
