using System;
using System.Collections.Generic;
using System.Text;
using GodGame.Sim;

namespace GodGame.Bridge
{
    /// <summary>
    /// Aggregates <see cref="CauseEvent"/> records into casualty and hazard summaries for the Results screen,
    /// handling known causes (1..22) and any unknown future <see cref="CauseKind"/> values gracefully.
    /// </summary>
    public sealed class CauseBreakdown
    {
        public sealed class CauseGroup
        {
            public CauseGroup(
                byte rawKind,
                CauseKind kind,
                string label,
                bool isFatal,
                bool isKnown
            )
            {
                RawKind = rawKind;
                Kind = kind;
                Label = label;
                IsFatal = isFatal;
                IsKnown = isKnown;
            }

            public byte RawKind { get; }
            public CauseKind Kind { get; }
            public string Label { get; }
            public bool IsFatal { get; }
            public bool IsKnown { get; }
            public int AttackerCount { get; internal set; }
            public int DefenderCount { get; internal set; }
            public int UnattributedCount { get; internal set; }
            public int TotalCount => AttackerCount + DefenderCount + UnattributedCount;
            public List<int> EventIndices { get; } = new List<int>();
        }

        private CauseBreakdown(
            int totalEvents,
            int attackerLosses,
            int defenderLosses,
            int unknownKindEvents,
            IReadOnlyList<CauseGroup> deathCauses,
            IReadOnlyList<CauseGroup> allCauses
        )
        {
            TotalEvents = totalEvents;
            AttackerLosses = attackerLosses;
            DefenderLosses = defenderLosses;
            UnknownKindEvents = unknownKindEvents;
            DeathCauses = deathCauses;
            AllCauses = allCauses;
        }

        public int TotalEvents { get; }
        public int AttackerLosses { get; }
        public int DefenderLosses { get; }
        public int UnknownKindEvents { get; }
        public IReadOnlyList<CauseGroup> DeathCauses { get; }
        public IReadOnlyList<CauseGroup> AllCauses { get; }

        public static bool IsKnownKind(CauseKind kind)
        {
            byte raw = (byte)kind;
            return raw >= (byte)CauseKind.CastFired && raw <= (byte)CauseKinds.Last;
        }

        public static bool IsFatalKind(CauseKind kind)
        {
            switch (kind)
            {
                case CauseKind.SoldierPushedOver:
                case CauseKind.SoldierDrowned:
                case CauseKind.SoldierKilled:
                case CauseKind.SoldierFled:
                case CauseKind.SoldierFell:
                case CauseKind.SoldierCrushed:
                case CauseKind.SoldierTrampled:
                case CauseKind.SoldierBurned:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsFatalEvent(CauseEvent e) =>
            IsFatalKind(e.Kind)
            || (e.Kind == CauseKind.SoldierLanded && Landing.IsLethal(Landing.Outcome(e.Detail)));

        public static bool HasRegimentSubject(CauseKind kind)
        {
            switch (kind)
            {
                case CauseKind.RegimentSwept:
                case CauseKind.SoldierPushedOver:
                case CauseKind.SoldierDrowned:
                case CauseKind.SoldierKilled:
                case CauseKind.SoldierFled:
                case CauseKind.RegimentRouted:
                case CauseKind.RegimentRallied:
                case CauseKind.RegimentWiped:
                case CauseKind.SpecialistStarted:
                case CauseKind.SpecialistEnded:
                case CauseKind.SpecialistEditSkipped:
                case CauseKind.SoldierLaunched:
                case CauseKind.SoldierLanded:
                case CauseKind.SoldierFell:
                case CauseKind.StoneLanded:
                case CauseKind.SoldierCrushed:
                case CauseKind.EngineWrecked:
                case CauseKind.EngineAbandoned:
                case CauseKind.SoldierTrampled:
                case CauseKind.SoldierBurned:
                case CauseKind.BlockerCleared:
                    return true;
                default:
                    return false;
            }
        }

        public static string DisplayName(CauseKind kind)
        {
            switch (kind)
            {
                case CauseKind.CastFired:
                    return "God power cast";
                case CauseKind.CastFizzled:
                    return "Cast fizzled (100 m rule)";
                case CauseKind.RiverOverflowed:
                    return "River overflowed";
                case CauseKind.RegimentSwept:
                    return "Regiment swept";
                case CauseKind.SoldierPushedOver:
                    return "Pushed over lethal cliff";
                case CauseKind.SoldierDrowned:
                    return "Drowned in water";
                case CauseKind.SoldierKilled:
                    return "Slain in combat";
                case CauseKind.SoldierFled:
                    return "Fled the field";
                case CauseKind.RegimentRouted:
                    return "Regiment routed";
                case CauseKind.RegimentRallied:
                    return "Regiment rallied";
                case CauseKind.RegimentWiped:
                    return "Regiment wiped";
                case CauseKind.SpecialistStarted:
                    return "Specialist work started";
                case CauseKind.SpecialistEnded:
                    return "Specialist work ended";
                case CauseKind.SpecialistEditSkipped:
                    return "Specialist edit skipped";
                case CauseKind.SegmentFailed:
                    return "Deck / ice collapsed";
                case CauseKind.SoldierLaunched:
                    return "Launched airborne";
                case CauseKind.SoldierLanded:
                    return "Airborne landing";
                case CauseKind.SoldierFell:
                    return "Killed by fall / landing";
                case CauseKind.StoneLanded:
                    return "Siege stone landed";
                case CauseKind.SoldierCrushed:
                    return "Crushed by falling stone / tree";
                case CauseKind.EngineWrecked:
                    return "Siege engine wrecked";
                case CauseKind.EngineAbandoned:
                    return "Siege engine abandoned";
                case CauseKind.LightningStruck:
                    return "Lightning strike";
                case CauseKind.TreeFelled:
                    return "Tree felled";
                case CauseKind.FireStarted:
                    return "Fire started";
                case CauseKind.HerdAgitated:
                    return "Herd agitated";
                case CauseKind.HerdStampeded:
                    return "Herd stampeded";
                case CauseKind.HerdScattered:
                    return "Herd scattered";
                case CauseKind.SoldierTrampled:
                    return "Trampled";
                case CauseKind.SoldierBurned:
                    return "Burned in fire";
                case CauseKind.BlockerCleared:
                    return "Wreckage / obstacle cleared";
                default:
                    return Enum.IsDefined(typeof(CauseKind), kind)
                        ? HumanizePascal(kind.ToString())
                        : "Other cause #" + (byte)kind;
            }
        }

        public static CauseBreakdown Analyze(
            CauseEvent[] events,
            BattlePlan plan,
            int maxTick = int.MaxValue
        )
        {
            var groupsByRaw = new Dictionary<byte, CauseGroup>();
            var orderedGroups = new List<CauseGroup>();
            int total = 0;
            int attackerLosses = 0;
            int defenderLosses = 0;
            int unknownEvents = 0;

            if (events != null)
            {
                for (int i = 0; i < events.Length; i++)
                {
                    var e = events[i];
                    if (e.Tick > maxTick)
                    {
                        continue;
                    }
                    total++;
                    byte raw = (byte)e.Kind;
                    bool known = IsKnownKind(e.Kind);
                    if (!known)
                    {
                        unknownEvents++;
                    }
                    if (!groupsByRaw.TryGetValue(raw, out var group))
                    {
                        group = new CauseGroup(
                            raw,
                            e.Kind,
                            DisplayName(e.Kind),
                            IsFatalKind(e.Kind),
                            known
                        );
                        groupsByRaw.Add(raw, group);
                        orderedGroups.Add(group);
                    }
                    group.EventIndices.Add(i);

                    bool countedSide = false;
                    bool fatalEvent = IsFatalEvent(e);
                    if (
                        plan != null
                        && HasRegimentSubject(e.Kind)
                        && e.Subject >= 0
                        && e.Subject < plan.Army.Count
                    )
                    {
                        var side = plan.Army[e.Subject].Side;
                        if (side == Side.Attacker)
                        {
                            group.AttackerCount++;
                            if (fatalEvent)
                            {
                                attackerLosses++;
                            }
                            countedSide = true;
                        }
                        else if (side == Side.Defender)
                        {
                            group.DefenderCount++;
                            if (fatalEvent)
                            {
                                defenderLosses++;
                            }
                            countedSide = true;
                        }
                    }
                    if (!countedSide)
                    {
                        group.UnattributedCount++;
                    }
                }
            }

            orderedGroups.Sort(
                (a, b) =>
                {
                    int cmp = b.TotalCount.CompareTo(a.TotalCount);
                    return cmp != 0 ? cmp : a.RawKind.CompareTo(b.RawKind);
                }
            );

            var deaths = new List<CauseGroup>();
            for (int i = 0; i < orderedGroups.Count; i++)
            {
                if (orderedGroups[i].IsFatal)
                {
                    deaths.Add(orderedGroups[i]);
                }
            }

            return new CauseBreakdown(
                total,
                attackerLosses,
                defenderLosses,
                unknownEvents,
                deaths,
                orderedGroups
            );
        }

        private static string HumanizePascal(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "Unknown cause";
            }
            var sb = new StringBuilder(name.Length + 6);
            sb.Append(name[0]);
            for (int i = 1; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c))
                {
                    sb.Append(' ').Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
