using System;
using System.Collections.Generic;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Rules
{
    /// <summary>How a unit type moves; at most four at launch (data rules): foot, horse, wheeled and one water class.</summary>
    public enum MovementClass
    {
        Foot = 0,
        Horse = 1,
        Wheeled = 2,
        Water = 3,
    }

    /// <summary>A speed in ‰ as a piecewise-linear function of an integer (water depth in mm, mud on 0-65535).</summary>
    public sealed class SpeedCurve
    {
        private readonly int[] _x;
        private readonly int[] _permille;

        public SpeedCurve(int[] x, int[] permille)
        {
            _x = x;
            _permille = permille;
        }

        /// <summary>The speed at <paramref name="value"/>: linear between points, flat beyond the ends.</summary>
        public int At(long value)
        {
            if (value <= _x[0])
            {
                return _permille[0];
            }
            for (int i = 1; i < _x.Length; i++)
            {
                if (value <= _x[i])
                {
                    long dx = _x[i] - _x[i - 1];
                    return (int)(
                        _permille[i - 1]
                        + (_permille[i] - _permille[i - 1]) * (value - _x[i - 1]) / dx
                    );
                }
            }
            return _permille[_permille.Length - 1];
        }
    }

    /// <summary>
    /// How a movement class meets water (spec S1, section 3): wading speed by depth, the deepest water its own steering
    /// enters, and when water sweeps it away (deep enough, or deep and fast enough).
    /// </summary>
    public sealed class WaterResponse
    {
        public WaterResponse(
            SpeedCurve speed,
            int maxEnterDepthMm,
            int sweptDepthMm,
            int sweptDepthSpeed,
            int sweptMinDepthMm
        )
        {
            Speed = speed;
            MaxEnterDepthMm = maxEnterDepthMm;
            SweptDepthMm = sweptDepthMm;
            SweptDepthSpeed = sweptDepthSpeed;
            SweptMinDepthMm = sweptMinDepthMm;
        }

        /// <summary>Wading speed in ‰ by depth in mm.</summary>
        public SpeedCurve Speed { get; }

        /// <summary>Deeper water is closed to the class's own steering.</summary>
        public int MaxEnterDepthMm { get; }

        /// <summary>At this depth the class is swept away whatever the current.</summary>
        public int SweptDepthMm { get; }

        /// <summary>Depth × flow speed (mm × mm/s / 1000, so 600 is 0.6 m²/s) at which the class loses its footing.</summary>
        public int SweptDepthSpeed { get; }

        /// <summary>Shallower water never sweeps, however fast.</summary>
        public int SweptMinDepthMm { get; }

        /// <summary>A swept soldier drifts at this share of the water's velocity, in ‰.</summary>
        public int CarriedPermille { get; internal set; } = 1000;

        /// <summary>How fast a swept soldier paddles toward the nearest footing, in mm/s.</summary>
        public int PaddleMmPerS { get; internal set; }

        /// <summary>Ticks a swept soldier lasts before drowning.</summary>
        public int DrowningTicks { get; internal set; } = 1;

        /// <summary>A swept soldier regains footing below this share (‰) of every sweep threshold.</summary>
        public int RegainPermille { get; internal set; } = 1000;

        /// <summary>Whether a swept soldier finds footing here: below <see cref="RegainPermille"/> of every threshold.</summary>
        public bool RegainsFooting(long depthMm, long flowMmPerS) =>
            depthMm * 1000 < (long)SweptDepthMm * RegainPermille
            && (
                depthMm * 1000 < (long)SweptMinDepthMm * RegainPermille
                || depthMm * flowMmPerS < (long)SweptDepthSpeed * RegainPermille
            );

        /// <summary>Whether water this deep and this fast sweeps the class away.</summary>
        public bool Sweeps(long depthMm, long flowMmPerS) =>
            depthMm >= SweptDepthMm
            || (depthMm >= SweptMinDepthMm && depthMm * flowMmPerS / 1000 >= SweptDepthSpeed);
    }

    /// <summary>
    /// How one movement class meets the ground, from <c>data/rules/movement-classes.json</c>. A cell's cost is
    /// <see cref="BaseCost"/> plus <see cref="SlopeCost"/> × slope / 1000 (slope in ‰, rise over run); steeper than
    /// <see cref="MaxSlopePermille"/> is impassable. Costs are relative: 100 means "a flat cell for this class".
    /// </summary>
    public sealed class MovementClassRules
    {
        public MovementClassRules(
            MovementClass movementClass,
            int baseCost,
            int slopeCost,
            int maxSlopePermille,
            WaterResponse water,
            SpeedCurve mudSpeed
        )
        {
            Water = water;
            MudSpeed = mudSpeed;
            MovementClass = movementClass;
            BaseCost = baseCost;
            SlopeCost = slopeCost;
            MaxSlopePermille = maxSlopePermille;
        }

        public MovementClass MovementClass { get; }

        public int BaseCost { get; }

        /// <summary>Extra cost per 1000 ‰ of slope (a 45° climb).</summary>
        public int SlopeCost { get; }

        public int MaxSlopePermille { get; }

        /// <summary>How the class meets water (spec S1, section 3).</summary>
        public WaterResponse Water { get; }

        /// <summary>Speed in ‰ by mud (0-65535; spec S1, section 6).</summary>
        public SpeedCurve MudSpeed { get; }
    }

    /// <summary>One unit type from <c>data/rules/unit-types.json</c>. Units are documented in data/schema/unit-types.schema.json.</summary>
    public sealed class UnitTypeRules
    {
        public UnitTypeRules(
            int index,
            string id,
            MovementClass movementClass,
            int health,
            int morale,
            int speedMmPerSecond,
            int radiusMm,
            FormationRules formation
        )
        {
            Index = index;
            Id = id;
            MovementClass = movementClass;
            Health = health;
            Morale = morale;
            SpeedMmPerSecond = speedMmPerSecond;
            RadiusMm = radiusMm;
            Formation = formation;
        }

        /// <summary>Position in the ruleset, which is what <c>World.UnitType</c> stores.</summary>
        public int Index { get; }

        /// <summary>Stable id used by plans, maps and replays.</summary>
        public string Id { get; }

        public MovementClass MovementClass { get; }

        /// <summary>Starting hit points.</summary>
        public int Health { get; }

        /// <summary>Starting morale.</summary>
        public int Morale { get; }

        /// <summary>Marching speed on flat open ground, in mm per second.</summary>
        public int SpeedMmPerSecond { get; }

        /// <summary>The body's radius on the ground plane, in mm: how close another soldier may stand.</summary>
        public int RadiusMm { get; }

        public FormationRules Formation { get; }

        /// <summary>Running speed (a rout), in mm per second.</summary>
        public int RunSpeedMmPerSecond { get; internal set; }

        /// <summary>Every morale loss is multiplied by this, in ‰ (archers are shakier than line infantry).</summary>
        public int MoraleShockPermille { get; internal set; } = 1000;

        /// <summary>The swing, or null for a type that doesn't fight hand to hand.</summary>
        public MeleeRules? Melee { get; internal set; }

        /// <summary>Melee damage taken, in ‰ (shields).</summary>
        public int MeleeTakenPermille { get; internal set; } = 1000;

        /// <summary>Arrow damage taken, in ‰.</summary>
        public int ArrowTakenPermille { get; internal set; } = 1000;

        /// <summary>What it shoots, or null.</summary>
        public ProjectileRules? Projectile { get; internal set; }

        /// <summary>Mass in kg (spec S2 2.1): what it weighs on a bridge or ice.</summary>
        public int MassKg { get; internal set; }

        /// <summary>Whether the attacker may buy it (a seat lock, spec S2 14.2: Fascine Carriers attacker only).</summary>
        public bool AttackerMay { get; internal set; } = true;

        /// <summary>Whether the defender may buy it (Spademen defender only).</summary>
        public bool DefenderMay { get; internal set; } = true;

        /// <summary>The specialist action it carries out (<c>specialists.json</c>), or null for a line or engine type.</summary>
        public string? Action { get; internal set; }
    }

    /// <summary>The guaranteed-route check, from <c>data/rules/routes.json</c>.</summary>
    public sealed class RouteRules
    {
        private readonly int[] _clearance;

        public RouteRules(int capPercent, int coarseCells, int foot, int horse, int wheeled)
        {
            CapPercent = capPercent;
            CoarseCells = coarseCells;
            _clearance = new[] { foot, horse, wheeled, 1 };
            MaxClearance = System.Math.Max(foot, System.Math.Max(horse, wheeled));
        }

        /// <summary>The longest route allowed, in percent of the straight-line distance (200: the brief's 2×).</summary>
        public int CapPercent { get; }

        /// <summary>Field cells per side of a reachability cell.</summary>
        public int CoarseCells { get; }

        /// <summary>The largest clearance any class needs.</summary>
        public int MaxClearance { get; }

        /// <summary>How far, in reachability cells, a class must stay from a closed one (1: any open cell).</summary>
        public int Clearance(MovementClass movementClass) => _clearance[(int)movementClass];
    }

    /// <summary>World hazards from <c>data/rules/hazards.json</c>.</summary>
    public sealed class HazardRules
    {
        public HazardRules(
            int lethalDropMm,
            int teeterTicks,
            int teeterPressureMm,
            int bankExitStunTicks
        )
        {
            LethalDropMm = lethalDropMm;
            TeeterTicks = teeterTicks;
            TeeterPressureMm = teeterPressureMm;
            BankExitStunTicks = bankExitStunTicks;
        }

        /// <summary>A cell with a 4-neighbour at least this much lower is a lethal cliff top.</summary>
        public int LethalDropMm { get; }

        /// <summary>How long a soldier teeters at a lethal edge before the crowd pushes him over (S1: 1 s).</summary>
        public int TeeterTicks { get; }

        /// <summary>The crowd pressure (overlap with neighbours, mm) that pushes a soldier at a lethal edge over it.</summary>
        public int TeeterPressureMm { get; }

        /// <summary>How long a swept soldier lies stunned after reaching footing (S1: 2 s).</summary>
        public int BankExitStunTicks { get; }
    }

    /// <summary>How a unit type's regiments form up, and how big they may be (tech notes, section 4: about 50 to 200).</summary>
    public sealed class FormationRules
    {
        public FormationRules(
            int files,
            int fileSpacingMm,
            int rankSpacingMm,
            int minSoldiers,
            int maxSoldiers
        )
        {
            Files = files;
            FileSpacingMm = fileSpacingMm;
            RankSpacingMm = rankSpacingMm;
            MinSoldiers = minSoldiers;
            MaxSoldiers = maxSoldiers;
        }

        /// <summary>Soldiers side by side in a rank (fewer when the regiment is smaller).</summary>
        public int Files { get; }

        /// <summary>Between neighbours in a rank, centre to centre.</summary>
        public int FileSpacingMm { get; }

        /// <summary>Between one rank and the next, centre to centre.</summary>
        public int RankSpacingMm { get; }

        /// <summary>Regiment size limits a plan must respect, officer included.</summary>
        public int MinSoldiers { get; }

        public int MaxSoldiers { get; }
    }

    /// <summary>
    /// The immutable rules a battle runs on, with a hash over their canonical form (plan, section 3.8): replays
    /// record it and PvP checks it at lock-in, so two machines can't fight with different rules.
    /// </summary>
    public sealed class Ruleset
    {
        private readonly Dictionary<string, UnitTypeRules> _unitTypesById;

        internal Ruleset(
            IReadOnlyList<UnitTypeRules> unitTypes,
            IReadOnlyList<MovementClassRules> movementClasses,
            HazardRules hazards,
            RouteRules routes,
            FieldRules fields,
            ulong hash,
            bool hasPlaceholders
        )
        {
            MovementClasses = movementClasses;
            Hazards = hazards;
            Routes = routes;
            Fields = fields;
            UnitTypes = unitTypes;
            Hash = hash;
            HasPlaceholders = hasPlaceholders;
            _unitTypesById = new Dictionary<string, UnitTypeRules>(StringComparer.Ordinal);
            foreach (var type in unitTypes)
            {
                _unitTypesById.Add(type.Id, type);
            }
        }

        /// <summary>In file order; <see cref="UnitTypeRules.Index"/> is the position here.</summary>
        public IReadOnlyList<UnitTypeRules> UnitTypes { get; }

        /// <summary>Indexed by <see cref="Rules.MovementClass"/>: all four classes, in enum order.</summary>
        public IReadOnlyList<MovementClassRules> MovementClasses { get; }

        public HazardRules Hazards { get; }

        public RouteRules Routes { get; }

        public FieldRules Fields { get; }

        /// <summary>The god powers, in file order; <see cref="PowerRules.Index"/> is the position here.</summary>
        public IReadOnlyList<PowerRules> Powers { get; internal set; } = Array.Empty<PowerRules>();

        /// <summary>Melee, volleys and the regiment targeting default (<c>combat.json</c>, <c>projectiles.json</c>).</summary>
        public CombatRules Combat { get; internal set; } = null!;

        /// <summary>Decks, load, strain and collapse (<c>load.json</c>).</summary>
        public StructureRules Structures { get; internal set; } = new StructureRules();

        /// <summary>The specialist executor's actions (<c>specialists.json</c>).</summary>
        public SpecialistRules Specialists { get; internal set; } = new SpecialistRules();

        /// <summary>Morale, routing and wiping (<c>morale.json</c>).</summary>
        public MoraleRules Morale { get; internal set; } = new MoraleRules();

        /// <summary>Terrain pieces (<c>terrain.json</c>).</summary>
        public TerrainRules Terrain { get; internal set; } =
            new TerrainRules(0, Array.Empty<PieceRules>());

        /// <summary>The latest tick a cast may be queued for (the battle cap; plan section 10: 15 minutes).</summary>
        public int LatestCastTick { get; internal set; }

        /// <summary>The power with this id, or null.</summary>
        public PowerRules? Power(string id)
        {
            foreach (var power in Powers)
            {
                if (string.Equals(power.Id, id, StringComparison.Ordinal))
                {
                    return power;
                }
            }
            return null;
        }

        /// <summary>Ticks between refreshes of the field-driven navigation costs (S1: at most 1 Hz).</summary>
        public int FieldCostRefreshTicks { get; internal set; } = 20;

        /// <summary>A class's stored speed code changes only when its speed moves by this much of full speed (S1).</summary>
        public int FieldCostHysteresisPermille { get; internal set; } = 100;

        public MovementClassRules Movement(MovementClass movementClass) =>
            MovementClasses[(int)movementClass];

        public ulong Hash { get; }

        /// <summary>True while any file is marked <c>"status": "placeholder"</c>: numbers no spec has set yet.</summary>
        public bool HasPlaceholders { get; }

        public UnitTypeRules UnitType(string id) =>
            _unitTypesById.TryGetValue(id, out var type)
                ? type
                : throw new DataFormatException("Unknown unit type \"" + id + "\".");
    }
}
