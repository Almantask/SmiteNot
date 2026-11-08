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
            if (map.IsPlayed)
            {
                throw new ArgumentException(
                    "A played map already carries a plan's pieces; pass the map as loaded.",
                    nameof(map)
                );
            }
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Seed = seed;
            if (!string.Equals(plan.MapId, map.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException("The plan was made for another map.", nameof(plan));
            }
            PlayedMap = TerrainPieces.Apply(map, plan.Terrain, rules);
        }

        public Ruleset Rules { get; }

        public MapData Map { get; }

        /// <summary>
        /// The map as the battle plays it: <see cref="Map"/> with the plan's terrain pieces stamped in before tick 0
        /// (the same object when there are none). Navigation, the fields and presentation all read this one.
        /// </summary>
        public MapData PlayedMap { get; }

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
        /// mud), regiment orders (S2's targeting default), navigation (regiment goals and flow fields),
        /// movement, combat (melee and volleys), hazards (swept, drowning, bank exit), morale (routs, rallies, wiped,
        /// fleeing) and bookkeeping (water's cause tags); structures and ballistics are still empty. The navigation's cost fields are built with
        /// <paramref name="runner"/>, which never changes the result.
        /// </summary>
        public ISimSystem[] CreateSystems(IParallelRunner runner)
        {
            var navigation = new Navigation(PlayedMap, Rules, runner);
            var systems = TickOrder.CreateDefault();
            systems[(int)TickStep.PlanEvents] = new CastSystem(Plan, navigation);
            // The fields at 5 Hz, in the interaction order (plan, section 3.4): water, then wetness and mud.
            var models = new List<IFieldModel>();
            if (Rules.Fields.Water != null)
            {
                models.Add(
                    new WaterModel(
                        navigation.Terrain,
                        PlayedMap,
                        Rules.Fields.Water,
                        TickOrder.FieldsRate
                    )
                );
            }
            if (Rules.Fields.Ground != null)
            {
                models.Add(
                    new GroundModel(navigation.Terrain, PlayedMap, Rules, TickOrder.FieldsRate)
                );
            }
            systems[(int)TickStep.Fields] = new FieldSystem(models);
            systems[(int)TickStep.StructuresAndLoad] = new LoadSystem(Rules, navigation);
            systems[(int)TickStep.RegimentAi] = new RegimentOrderSystem(Rules, navigation, Plan);
            systems[(int)TickStep.Navigation] = new NavigationSystem(navigation);
            systems[(int)TickStep.Movement] = new MovementSystem(navigation);
            systems[(int)TickStep.Combat] = new CombatSystem(Rules, PlayedMap, navigation.Terrain);
            systems[(int)TickStep.Hazards] = new HazardSystem(navigation);
            systems[(int)TickStep.Morale] = new MoraleSystem(Rules, navigation);
            systems[(int)TickStep.Bookkeeping] = new CauseSystem();
            return systems;
        }

        /// <summary>A fresh simulation at tick 0. Each call builds new systems: they belong to one simulation.</summary>
        public Simulation Start(IParallelRunner runner) =>
            new Simulation(
                BattleSetup.Deploy(PlayedMap, Plan, Rules),
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
