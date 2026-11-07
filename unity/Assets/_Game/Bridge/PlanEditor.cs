using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GodGame.Sim;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Bridge
{
    /// <summary>
    /// The plan as the planning interface edits it (plan, section 3.8): the three layers as plain lists, written back to
    /// plan JSON and checked by the simulation's own loader. Unity writes only the plan; every rule (the deployment
    /// zones, the 100 m buffer, no full traps, a cast's size and time) is the loader's, and a refused edit leaves the
    /// last valid plan in place with the loader's reason.
    /// </summary>
    public sealed class PlanEditor
    {
        public sealed class Regiment
        {
            public string Id;
            public string Side;
            public string UnitType;
            public int Soldiers;
            public int XMm;
            public int YMm;
            public int FacingCentiDegrees;
        }

        public sealed class Piece
        {
            public string Id;
            public string Kind;
            public List<(int X, int Y)> Points = new List<(int X, int Y)>();
            public int WidthMm;
            public int DepthMm;
            public int HeightMm;
            public int LitresPerSecond;
        }

        public sealed class Cast
        {
            public string Id;
            public string Power;
            public int Tick;
            public int XMm;
            public int YMm;
            public int RadiusMm;
        }

        private readonly MapData _map;
        private readonly Ruleset _rules;
        private int _nextId;

        public PlanEditor(BattlePlan plan, MapData map, Ruleset rules)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            foreach (var r in plan.Army)
            {
                Army.Add(
                    new Regiment
                    {
                        Id = r.Id,
                        Side = r.Side == GodGame.Sim.Side.Attacker ? "attacker" : "defender",
                        UnitType = r.UnitType,
                        Soldiers = r.Soldiers,
                        XMm = r.XMm,
                        YMm = r.YMm,
                        FacingCentiDegrees = r.FacingCentiDegrees,
                    }
                );
            }
            foreach (var p in plan.Terrain)
            {
                var piece = new Piece
                {
                    Id = p.Id,
                    Kind = p.Rules.Id,
                    WidthMm = p.WidthMm,
                    DepthMm = p.DepthMm,
                    HeightMm = p.HeightMm,
                    LitresPerSecond = p.LitresPerSecond,
                };
                for (int k = 0; k < p.PointCount; k++)
                {
                    piece.Points.Add((p.PointsMm[2 * k], p.PointsMm[2 * k + 1]));
                }
                Terrain.Add(piece);
            }
            foreach (var c in plan.Casts)
            {
                Casts.Add(
                    new Cast
                    {
                        Id = c.Id,
                        Power = c.Power.Id,
                        Tick = c.Tick,
                        XMm = c.XMm,
                        YMm = c.YMm,
                        RadiusMm = c.RadiusMm,
                    }
                );
            }
        }

        public List<Regiment> Army { get; } = new List<Regiment>();

        public List<Piece> Terrain { get; } = new List<Piece>();

        public List<Cast> Casts { get; } = new List<Cast>();

        /// <summary>The last plan the loader accepted.</summary>
        public BattlePlan Plan { get; private set; }

        /// <summary>Why the last edit was refused, or null.</summary>
        public string Error { get; private set; }

        /// <summary>A fresh id with the given prefix, unique in this plan.</summary>
        public string NewId(string prefix)
        {
            while (true)
            {
                string id = prefix + (++_nextId).ToString(CultureInfo.InvariantCulture);
                if (
                    Army.TrueForAll(r => r.Id != id)
                    && Terrain.TrueForAll(p => p.Id != id)
                    && Casts.TrueForAll(c => c.Id != id)
                )
                {
                    return id;
                }
            }
        }

        /// <summary>
        /// Checks the edited lists with the simulation's loader. True and a new <see cref="Plan"/> when it accepts them;
        /// false, <see cref="Error"/> set and the lists put back to the last accepted plan when it refuses.
        /// </summary>
        public bool Commit()
        {
            try
            {
                Plan = BattlePlan.Load(ToJson(), _map, _rules);
                Error = null;
                return true;
            }
            catch (DataFormatException e)
            {
                Error = e.Message;
                var restored = new PlanEditor(Plan, _map, _rules) { _nextId = _nextId };
                Army.Clear();
                Army.AddRange(restored.Army);
                Terrain.Clear();
                Terrain.AddRange(restored.Terrain);
                Casts.Clear();
                Casts.AddRange(restored.Casts);
                return false;
            }
        }

        public string ToJson()
        {
            var json = new StringBuilder("{\"version\":0,\"map\":\"")
                .Append(_map.Id)
                .Append("\",\"army\":[");
            for (int i = 0; i < Army.Count; i++)
            {
                var r = Army[i];
                json.Append(i == 0 ? "" : ",")
                    .Append(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{{\"id\":\"{0}\",\"side\":\"{1}\",\"unitType\":\"{2}\",\"soldiers\":{3},\"xMm\":{4},\"yMm\":{5},\"facingCentiDegrees\":{6}}}",
                            r.Id,
                            r.Side,
                            r.UnitType,
                            r.Soldiers,
                            r.XMm,
                            r.YMm,
                            r.FacingCentiDegrees
                        )
                    );
            }
            json.Append("],\"terrain\":[");
            for (int i = 0; i < Terrain.Count; i++)
            {
                var p = Terrain[i];
                json.Append(i == 0 ? "" : ",")
                    .Append("{\"id\":\"")
                    .Append(p.Id)
                    .Append("\",\"piece\":\"")
                    .Append(p.Kind)
                    .Append('"');
                if (p.Kind == "spring" || p.Kind == "boulder")
                {
                    json.Append(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            ",\"xMm\":{0},\"yMm\":{1}",
                            p.Points[0].X,
                            p.Points[0].Y
                        )
                    );
                    if (p.Kind == "spring")
                    {
                        json.Append(",\"litresPerSecond\":")
                            .Append(p.LitresPerSecond.ToString(CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    json.Append(",\"points\":[");
                    for (int k = 0; k < p.Points.Count; k++)
                    {
                        json.Append(k == 0 ? "" : ",")
                            .Append(
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "[{0},{1}]",
                                    p.Points[k].X,
                                    p.Points[k].Y
                                )
                            );
                    }
                    json.Append(']');
                    if (p.Kind == "channel")
                    {
                        json.Append(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                ",\"widthMm\":{0},\"depthMm\":{1}",
                                p.WidthMm,
                                p.DepthMm
                            )
                        );
                    }
                    else if (p.Kind == "ridge")
                    {
                        json.Append(",\"heightMm\":")
                            .Append(p.HeightMm.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        json.Append(",\"depthMm\":")
                            .Append(p.DepthMm.ToString(CultureInfo.InvariantCulture));
                    }
                }
                json.Append('}');
            }
            json.Append("],\"casts\":[");
            for (int i = 0; i < Casts.Count; i++)
            {
                var c = Casts[i];
                json.Append(i == 0 ? "" : ",")
                    .Append(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{{\"id\":\"{0}\",\"power\":\"{1}\",\"tick\":{2},\"xMm\":{3},\"yMm\":{4},\"radiusMm\":{5}}}",
                            c.Id,
                            c.Power,
                            c.Tick,
                            c.XMm,
                            c.YMm,
                            c.RadiusMm
                        )
                    );
            }
            return json.Append("]}").ToString();
        }
    }

    /// <summary>Walks published cause events back to their roots and words them for the trace panel (M4 item 6).</summary>
    public static class CauseTrace
    {
        /// <summary>The event and everything it links back to, each once, cause before second cause.</summary>
        public static List<int> Walk(CauseEvent[] events, int index)
        {
            var trace = new List<int>();
            var seen = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(index);
            while (stack.Count > 0)
            {
                int e = stack.Pop();
                if (e < 0 || e >= events.Length || !seen.Add(e))
                {
                    continue;
                }
                trace.Add(e);
                stack.Push(events[e].Cause2);
                stack.Push(events[e].Cause);
            }
            return trace;
        }

        /// <summary>One event in words: "rain 'storm' cast at 0:50", "regiment a003 swept at 2:31".</summary>
        public static string Describe(CauseEvent e, BattlePlan plan)
        {
            string at = Clock(e.Tick);
            switch (e.Kind)
            {
                case CauseKind.CastFired:
                case CauseKind.CastFizzled:
                    string cast =
                        e.Subject < plan.Casts.Count
                            ? plan.Casts[e.Subject].Power.Id
                                + " \""
                                + plan.Casts[e.Subject].Id
                                + "\""
                            : "a cast";
                    return e.Kind == CauseKind.CastFired
                        ? cast + " cast at " + at
                        : cast + " fizzled at " + at + " (" + (FizzleReasons)e.Detail + ")";
                case CauseKind.RiverOverflowed:
                    return "the river overflowed (tile " + e.Subject + ") at " + at;
                case CauseKind.RegimentSwept:
                    return Regiment(e.Subject, plan) + " swept at " + at;
                case CauseKind.SoldierPushedOver:
                    return "pushed over by the crowd at " + at;
                case CauseKind.SoldierDrowned:
                    return "a soldier of " + Regiment(e.Subject, plan) + " drowned at " + at;
                default:
                    return e.Kind + " at " + at;
            }
        }

        public static string Regiment(int slot, BattlePlan plan) =>
            slot >= 0 && slot < plan.Army.Count
                ? "regiment " + plan.Army[slot].Id
                : "regiment " + slot;

        public static string Clock(int tick)
        {
            int seconds = tick / SimConstants.TicksPerSecond;
            return (seconds / 60).ToString(CultureInfo.InvariantCulture)
                + ":"
                + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
