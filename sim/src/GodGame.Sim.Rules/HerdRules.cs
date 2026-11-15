namespace GodGame.Sim.Rules
{
    /// <summary>A fear source's reach (spec S2 11.2): full fear within <see cref="FullWithinMm"/>, falling linearly to 0 at <see cref="ZeroAtMm"/>.</summary>
    public sealed class FearSourceRules
    {
        public FearSourceRules(int fear, int fullWithinMm, int zeroAtMm, int decayPerS)
        {
            Fear = fear;
            FullWithinMm = fullWithinMm;
            ZeroAtMm = zeroAtMm;
            DecayPerS = decayPerS;
        }

        public int Fear { get; }

        public int FullWithinMm { get; }

        public int ZeroAtMm { get; }

        /// <summary>A strike's fear falls by this much a second after it; 0 for lasting sources.</summary>
        public int DecayPerS { get; }

        /// <summary>The fear at <paramref name="distanceMm"/>, before any decay.</summary>
        public int At(long distanceMm)
        {
            if (distanceMm <= FullWithinMm)
            {
                return Fear;
            }
            if (distanceMm >= ZeroAtMm || ZeroAtMm <= FullWithinMm)
            {
                return 0;
            }
            return (int)((long)Fear * (ZeroAtMm - distanceMm) / (ZeroAtMm - FullWithinMm));
        }
    }

    /// <summary>Herds and stampedes, from <c>data/rules/herds.json</c> (spec S2 section 11).</summary>
    public sealed class HerdRules
    {
        public UnitTypeRules AnimalType { get; internal set; } = null!;

        public int MaxAnimalsPerMap { get; internal set; }

        public int GrazeMmPerS { get; internal set; }

        public int WaypointTicks { get; internal set; }

        public int DrivenMmPerS { get; internal set; }

        public int DrivenFearAtLeast { get; internal set; }

        public int ThresholdFear { get; internal set; }

        public FearSourceRules Fire { get; internal set; } = null!;

        public FearSourceRules Strike { get; internal set; } = null!;

        public FearSourceRules Drovers { get; internal set; } = null!;

        public int AgitationTicks { get; internal set; }

        public int StampedeMmPerS { get; internal set; }

        public int LookAheadMm { get; internal set; }

        public int TurnStepCd { get; internal set; }

        public int DownhillMinSlopePermille { get; internal set; }

        public int DownhillArcCd { get; internal set; }

        public int DownhillTurnCdPerS { get; internal set; }

        public int RunMaxMm { get; internal set; }

        public int RunMaxTicks { get; internal set; }

        public int MinImpactMmPerS { get; internal set; }

        public int LaunchPermille { get; internal set; }

        public int TrampleDamage { get; internal set; }

        public int TrampleStunTicks { get; internal set; }

        public int VictimCooldownTicks { get; internal set; }

        public int SlowAfterHitPermille { get; internal set; }

        public int SlowAfterHitTicks { get; internal set; }
    }

    /// <summary>Lightning v0 (spec S2 12.1): what the strike fells and lights.</summary>
    public sealed class LightningRules
    {
        public int TreeSearchMm { get; internal set; }

        public int FallTicks { get; internal set; }

        public int IgnitionRadiusMm { get; internal set; }

        /// <summary>Fuel at or above this wetness (0-65,535) won't catch: rain counters the fire.</summary>
        public int IgnitionMaxWetness { get; internal set; }

        public int BurnTicks { get; internal set; }
    }

    /// <summary>Drovers (spec S2 14.8): walk a herd off the army's path, or onto the enemy's.</summary>
    public sealed class DriveHerdRules
    {
        public int AheadMm { get; internal set; }

        public int NearPathMm { get; internal set; }

        public int ClearOfPathMm { get; internal set; }

        public int HoldTicks { get; internal set; }

        /// <summary>Where the drovers stand: this far beyond the herd's centroid, on the side it is walked from.</summary>
        public int StandOffMm { get; internal set; }

        public int AttackerJobs { get; internal set; }

        public int DefenderJobs { get; internal set; }

        public int ScatterWithinMm { get; internal set; }

        public int ScatterTicks { get; internal set; }

        public int ReleaseWithinMm { get; internal set; }
    }
}
