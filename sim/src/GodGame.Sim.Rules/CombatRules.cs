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
