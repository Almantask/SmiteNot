using System;
using System.Collections.Generic;

namespace GodGame.Sim.Rules
{
    /// <summary>A unit type's swing (spec S2, section 5).</summary>
    public sealed class MeleeRules
    {
        public MeleeRules(int damage, int hitPermille, int intervalTicks, int reachMm)
        {
            Damage = damage;
            HitPermille = hitPermille;
            IntervalTicks = intervalTicks;
            ReachMm = reachMm;
        }

        public int Damage { get; }

        public int HitPermille { get; }

        /// <summary>Ticks between swings; each soldier's phase is seeded.</summary>
        public int IntervalTicks { get; }

        /// <summary>How far beyond body contact the swing reaches.</summary>
        public int ReachMm { get; }
    }

    /// <summary>One row of the projectile table (S2, section 6.1): range, flight, wind and height, hits and reload.</summary>
    public sealed class ProjectileRules
    {
        public ProjectileRules(
            string id,
            int rangeMm,
            int flightTicksAtRange,
            int windPermillePerMPerS,
            int windClampMinPermille,
            int windClampMaxPermille,
            int heightGainPerMm,
            int heightGainCapMm,
            int hitNearPermille,
            int hitFarPermille,
            int damage,
            int reloadTicks,
            int ammo
        )
        {
            Id = id;
            RangeMm = rangeMm;
            FlightTicksAtRange = flightTicksAtRange;
            WindPermillePerMPerS = windPermillePerMPerS;
            WindClampMinPermille = windClampMinPermille;
            WindClampMaxPermille = windClampMaxPermille;
            HeightGainPerMm = heightGainPerMm;
            HeightGainCapMm = heightGainCapMm;
            HitNearPermille = hitNearPermille;
            HitFarPermille = hitFarPermille;
            Damage = damage;
            ReloadTicks = reloadTicks;
            Ammo = ammo;
        }

        public string Id { get; }

        /// <summary>Range on flat ground in calm air.</summary>
        public int RangeMm { get; }

        /// <summary>Flight time at full range; shorter shots fly for proportionally less.</summary>
        public int FlightTicksAtRange { get; }

        /// <summary>‰ of range gained per m/s of tailwind along the shot (lost per m/s of headwind).</summary>
        public int WindPermillePerMPerS { get; }

        public int WindClampMinPermille { get; }

        public int WindClampMaxPermille { get; }

        /// <summary>mm of range per mm the shooter stands above the target.</summary>
        public int HeightGainPerMm { get; }

        public int HeightGainCapMm { get; }

        /// <summary>Hit chance at up to the close range share of full range, falling linearly to <see cref="HitFarPermille"/>.</summary>
        public int HitNearPermille { get; }

        public int HitFarPermille { get; }

        public int Damage { get; }

        public int ReloadTicks { get; }

        /// <summary>The nearest a target may be (a siege stone's 60 m); 0 for arrows.</summary>
        public int MinRangeMm { get; internal set; }

        /// <summary>A stone (spec S2 6.4): it crushes and launches where it lands instead of hitting soldiers.</summary>
        public bool IsStone { get; internal set; }

        /// <summary>A stone's seeded scatter radius at <see cref="MinRangeMm"/>, growing linearly to <see cref="ScatterFarMm"/> at full range.</summary>
        public int ScatterNearMm { get; internal set; }

        public int ScatterFarMm { get; internal set; }

        /// <summary>Soldiers within this of a stone's landing point die (combat; crushed).</summary>
        public int CrushRadiusMm { get; internal set; }

        /// <summary>Soldiers beyond the crush radius and within this are launched away from the landing point.</summary>
        public int LaunchRadiusMm { get; internal set; }

        /// <summary>Volleys a regiment carries.</summary>
        public int Ammo { get; }
    }

    /// <summary>
    /// Combat in M5 (spec S2, sections 5 and 6, and the regiment targeting working default until the regiment AI, M7):
    /// <c>data/rules/combat.json</c> and <c>projectiles.json</c>.
    /// </summary>
    public sealed class CombatRules
    {
        public CombatRules(
            int engagedSpeedPermille,
            int impactRadiusMm,
            int closeRangePermille,
            int attackerEngageMm,
            int defenderEngageMm,
            int rangedFallbackTriggerMm,
            int rangedFallbackMm,
            int rangedFallbackCooldownTicks,
            IReadOnlyList<ProjectileRules> projectiles
        )
        {
            EngagedSpeedPermille = engagedSpeedPermille;
            ImpactRadiusMm = impactRadiusMm;
            CloseRangePermille = closeRangePermille;
            AttackerEngageMm = attackerEngageMm;
            DefenderEngageMm = defenderEngageMm;
            RangedFallbackTriggerMm = rangedFallbackTriggerMm;
            RangedFallbackMm = rangedFallbackMm;
            RangedFallbackCooldownTicks = rangedFallbackCooldownTicks;
            Projectiles = projectiles ?? throw new ArgumentNullException(nameof(projectiles));
        }

        /// <summary>A soldier with an enemy in reach moves at this share of its speed.</summary>
        public int EngagedSpeedPermille { get; }

        /// <summary>A volley's hits land on soldiers of the target regiment this close to the aim point.</summary>
        public int ImpactRadiusMm { get; }

        /// <summary>Up to this share of range a volley hits at its near chance.</summary>
        public int CloseRangePermille { get; }

        /// <summary>Attackers turn on an enemy regiment whose outline comes this close.</summary>
        public int AttackerEngageMm { get; }

        /// <summary>Defenders leave their ground for an enemy regiment this close.</summary>
        public int DefenderEngageMm { get; }

        /// <summary>Ranged regiments fall back when enemy melee comes this close…</summary>
        public int RangedFallbackTriggerMm { get; }

        /// <summary>…this far, at most once per <see cref="RangedFallbackCooldownTicks"/>.</summary>
        public int RangedFallbackMm { get; }

        public int RangedFallbackCooldownTicks { get; }

        /// <summary>Open order (spec S2 2.2, D1): file and rank spacing × this; 1,000 (no change) without an order block.</summary>
        public int OpenSpacingPermille { get; internal set; } = 1000;

        /// <summary>The share of a volley's hits an open-order regiment takes (S2 2.2).</summary>
        public int OpenArrowHitsPermille { get; internal set; } = 1000;

        /// <summary>D15: whether regiments under <see cref="SmallRegimentSoldiers"/> block casts (the range rule).</summary>
        public bool SmallRegimentsBlockCasts { get; internal set; } = true;

        /// <summary>D15: a regiment that deployed with fewer soldiers than this is small.</summary>
        public int SmallRegimentSoldiers { get; internal set; }

        /// <summary>Cavalry charges (spec S2 10), or null: then no regiment charges.</summary>
        public ChargeRules? Charge { get; internal set; }

        public IReadOnlyList<ProjectileRules> Projectiles { get; }

        public ProjectileRules? Projectile(string id)
        {
            foreach (var p in Projectiles)
            {
                if (string.Equals(p.Id, id, StringComparison.Ordinal))
                {
                    return p;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Charges and trampling (spec S2 10, D7, D8; numbers are proposals): when a charging cavalry regiment starts, how fast
    /// its riders go, what stops it, and what a rider's contact does.
    /// </summary>
    public sealed class ChargeRules
    {
        /// <summary>A charge starts when the target's outline is between these (S2 10.1)...</summary>
        public int StartMinMm { get; internal set; }

        public int StartMaxMm { get; internal set; }

        /// <summary>...and no cell on the straight line has this much mud, this much water, or ice (D8).</summary>
        public int BlockedByMudAtLeast { get; internal set; }

        public int BlockedByWaterMm { get; internal set; }

        public int SpeedMmPerS { get; internal set; }

        public int AccelMmPerS2 { get; internal set; }

        /// <summary>A rider counts as charging while at least this fast.</summary>
        public int ChargingMinMmPerS { get; internal set; }

        /// <summary>A charge ends after its regiment has run this far.</summary>
        public int MaxRunMm { get; internal set; }

        public int CooldownTicks { get; internal set; }

        /// <summary>A regiment that has charged must be this far from the enemy to charge again.</summary>
        public int RechargeSeparationMm { get; internal set; }

        /// <summary>D7: a charging rider in this much mud or water stops dead, stunned, and his regiment's charge ends.</summary>
        public int StopMudAtLeast { get; internal set; }

        public int StopWaterAtLeastMm { get; internal set; }

        public int StopStunTicks { get; internal set; }

        /// <summary>S2 10.3: at most this many soldiers per rider per charge, in contact within the front arc.</summary>
        public int TargetsPerRider { get; internal set; }

        public int FrontArcCd { get; internal set; }

        public int LaunchPermille { get; internal set; }

        public int TrampleDamage { get; internal set; }

        public int TrampleStunTicks { get; internal set; }

        /// <summary>After his contact a rider slows to this and fights in melee.</summary>
        public int AfterContactMmPerS { get; internal set; }

        /// <summary>A braced target: standing (under this speed), close order, facing within the arc of the charge's reverse.</summary>
        public int BracedMaxSpeedMmPerS { get; internal set; }

        public int BracedFacingArcCd { get; internal set; }

        public int BracedLaunchPermille { get; internal set; }

        /// <summary>The rider takes this much per braced target.</summary>
        public int RiderDamagePerTarget { get; internal set; }

        /// <summary>
        /// Brief 2026-11-15: a close-order regiment of a bracing type that an enemy regiment is charging holds its ground
        /// until the charge ends (so it stands, and is braced when the charge comes from in front).
        /// </summary>
        public bool BraceHolds { get; internal set; }
    }

    /// <summary>Morale per regiment, routing, rallying and wiping (spec S2, section 7): <c>data/rules/morale.json</c>.</summary>
    public sealed class MoraleRules
    {
        public int Start { get; internal set; }

        public int RoutBelow { get; internal set; }

        /// <summary>Losing a share of the starting size costs this many morale per whole regiment lost.</summary>
        public int OwnLossFactor { get; internal set; }

        public int NearMm { get; internal set; }

        public int FarMm { get; internal set; }

        /// <summary>A friendly regiment's losses nearby cost count × this / own starting size, capped.</summary>
        public int NearbyLossFactor { get; internal set; }

        public int NearbyLossCap { get; internal set; }

        public int FriendRoutingPerS { get; internal set; }

        public int FriendWipedOnce { get; internal set; }

        public int EnemyRoutingPerS { get; internal set; }

        public int RecoveryPerS { get; internal set; }

        public int RecoveryQuietTicks { get; internal set; }

        public int RallyTimes { get; internal set; }

        public int RallyNoEnemyWithinMm { get; internal set; }

        public int RallySafeTicks { get; internal set; }

        public int RallyAwayFromLethalMm { get; internal set; }

        public int RallyMorale { get; internal set; }

        /// <summary>A regiment with fewer soldiers on the map than this share of its starting size is wiped.</summary>
        public int WipedBelowPermille { get; internal set; }
    }
}
