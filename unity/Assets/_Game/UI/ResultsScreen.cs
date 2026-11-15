using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GodGame.Bridge;
using GodGame.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace GodGame.UI
{
    /// <summary>
    /// UI Toolkit overlay displaying battle verdict (S2 7.5), regiment/casualty breakdown, and causes of death &amp;
    /// hazard counts with clickable causality traces. Gracefully displays unknown <see cref="CauseKind"/> values.
    /// </summary>
    public sealed class ResultsScreen
    {
        private readonly VisualElement _backdrop;
        private readonly Label _headline;
        private readonly Label _subhead;
        private readonly Label _stats;
        private readonly VisualElement _causeList;
        private readonly Label _traceBox;
        private readonly Button _closeButton;

        private CauseBreakdown _breakdown;
        private CauseEvent[] _events = Array.Empty<CauseEvent>();
        private BattlePlan _plan;
        private int _selectedOccurrenceCursor;
        private int _selectedGroupIndex = -1;

        public ResultsScreen(VisualElement hostRoot)
        {
            if (hostRoot == null)
            {
                throw new ArgumentNullException(nameof(hostRoot));
            }

            _backdrop = new VisualElement { name = "results-backdrop" };
            _backdrop.style.position = Position.Absolute;
            _backdrop.style.left = 0;
            _backdrop.style.top = 0;
            _backdrop.style.right = 0;
            _backdrop.style.bottom = 0;
            _backdrop.style.backgroundColor = new Color(0.03f, 0.04f, 0.06f, 0.72f);
            _backdrop.style.alignItems = Align.Center;
            _backdrop.style.justifyContent = Justify.Center;
            _backdrop.style.display = DisplayStyle.None;

            var card = new VisualElement { name = "results-card" };
            card.style.width = new Length(78, LengthUnit.Percent);
            card.style.maxWidth = 860;
            card.style.maxHeight = new Length(86, LengthUnit.Percent);
            card.style.backgroundColor = new Color(0.09f, 0.10f, 0.13f, 0.96f);
            card.style.borderTopWidth = 2;
            card.style.borderBottomWidth = 2;
            card.style.borderLeftWidth = 2;
            card.style.borderRightWidth = 2;
            var borderCol = new Color(0.35f, 0.40f, 0.48f, 1f);
            card.style.borderTopColor = borderCol;
            card.style.borderBottomColor = borderCol;
            card.style.borderLeftColor = borderCol;
            card.style.borderRightColor = borderCol;
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            card.style.paddingLeft = 18;
            card.style.paddingRight = 18;
            card.style.paddingTop = 14;
            card.style.paddingBottom = 14;
            card.style.flexDirection = FlexDirection.Column;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 6;

            var titleCol = new VisualElement();
            _headline = new Label("BATTLE RESULTS") { name = "results-headline" };
            _headline.style.fontSize = 20;
            _headline.style.unityFontStyleAndWeight = FontStyle.Bold;
            _headline.style.color = Color.white;

            _subhead = new Label("") { name = "results-subhead" };
            _subhead.style.fontSize = 13;
            _subhead.style.color = new Color(0.82f, 0.85f, 0.90f, 1f);

            titleCol.Add(_headline);
            titleCol.Add(_subhead);

            _closeButton = new Button(Hide) { text = "Close [Tab]", name = "results-close" };
            _closeButton.style.height = 28;
            _closeButton.style.paddingLeft = 12;
            _closeButton.style.paddingRight = 12;

            headerRow.Add(titleCol);
            headerRow.Add(_closeButton);
            card.Add(headerRow);

            _stats = new Label("") { name = "results-stats" };
            _stats.style.fontSize = 12;
            _stats.style.color = new Color(0.88f, 0.90f, 0.94f, 1f);
            _stats.style.backgroundColor = new Color(0.13f, 0.15f, 0.19f, 0.9f);
            _stats.style.paddingLeft = 10;
            _stats.style.paddingRight = 10;
            _stats.style.paddingTop = 8;
            _stats.style.paddingBottom = 8;
            _stats.style.marginBottom = 10;
            _stats.style.whiteSpace = WhiteSpace.Normal;
            card.Add(_stats);

            var columns = new VisualElement();
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.flexGrow = 1;
            columns.style.minHeight = 220;

            var leftPane = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "results-causes-scroll",
            };
            leftPane.style.flexBasis = new Length(52, LengthUnit.Percent);
            leftPane.style.marginRight = 10;
            leftPane.style.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 0.9f);
            leftPane.style.paddingLeft = 6;
            leftPane.style.paddingRight = 6;
            leftPane.style.paddingTop = 6;
            leftPane.style.paddingBottom = 6;
            _causeList = new VisualElement { name = "results-causes-list" };
            leftPane.Add(_causeList);

            var rightPane = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "results-trace-scroll",
            };
            rightPane.style.flexBasis = new Length(48, LengthUnit.Percent);
            rightPane.style.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 0.9f);
            rightPane.style.paddingLeft = 10;
            rightPane.style.paddingRight = 10;
            rightPane.style.paddingTop = 8;
            rightPane.style.paddingBottom = 8;

            _traceBox = new Label("Select a cause to inspect its causality chain.")
            {
                name = "results-trace-text",
            };
            _traceBox.style.fontSize = 12;
            _traceBox.style.color = new Color(0.92f, 0.94f, 0.98f, 1f);
            _traceBox.style.whiteSpace = WhiteSpace.Normal;
            rightPane.Add(_traceBox);

            columns.Add(leftPane);
            columns.Add(rightPane);
            card.Add(columns);

            _backdrop.Add(card);
            hostRoot.Add(_backdrop);
        }

        public bool IsVisible => _backdrop.style.display == DisplayStyle.Flex;
        public string HeadlineText => _headline.text;
        public string SubheadText => _subhead.text;
        public string StatsText => _stats.text;
        public string TraceText => _traceBox.text;
        public CauseBreakdown CurrentBreakdown => _breakdown;

        public void Show() => _backdrop.style.display = DisplayStyle.Flex;

        public void Hide() => _backdrop.style.display = DisplayStyle.None;

        public void Toggle()
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        /// <summary>Updates headline, regiment counts, cause breakdown rows and causality trace preview.</summary>
        public void SetData(
            BattleOutcomeResult outcome,
            CauseEvent[] events,
            BattlePlan plan,
            LevelData level = null
        )
        {
            _events = events ?? Array.Empty<CauseEvent>();
            _plan = plan;
            int horizon = outcome != null ? outcome.EvaluatedTick : int.MaxValue;
            _breakdown = CauseBreakdown.Analyze(_events, plan, horizon);

            _headline.text = FormatHeadline(outcome);
            _headline.style.color = HeadlineColor(outcome);
            _subhead.text = FormatSubhead(outcome, level);
            _stats.text = FormatStats(outcome, _breakdown);

            RebuildCauseRows();
        }

        /// <summary>Selects a cause group (from <see cref="CauseBreakdown.AllCauses"/>) and cycles occurrences.</summary>
        public void SelectCauseGroup(int groupIndex)
        {
            if (_breakdown == null || groupIndex < 0 || groupIndex >= _breakdown.AllCauses.Count)
            {
                _traceBox.text = "No cause selected.";
                return;
            }
            if (_selectedGroupIndex == groupIndex)
            {
                _selectedOccurrenceCursor++;
            }
            else
            {
                _selectedGroupIndex = groupIndex;
                _selectedOccurrenceCursor = 0;
            }
            var group = _breakdown.AllCauses[groupIndex];
            if (group.EventIndices.Count == 0)
            {
                _traceBox.text = group.Label + "\n(no events recorded)";
                return;
            }
            int pick = _selectedOccurrenceCursor % group.EventIndices.Count;
            int eventIdx = group.EventIndices[pick];
            var sb = new StringBuilder();
            sb.Append(group.Label);
            if (!group.IsKnown)
            {
                sb.Append(" [unrecognized kind #").Append(group.RawKind).Append(']');
            }
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "\nOccurrence {0} of {1} (click row again to cycle)\n\n",
                pick + 1,
                group.EventIndices.Count
            );
            var chain = CauseTrace.Walk(_events, eventIdx);
            for (int i = 0; i < chain.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append("\n  <- ");
                }
                sb.Append(CauseTrace.Describe(_events[chain[i]], _plan));
            }
            _traceBox.text = sb.ToString();
        }

        private void RebuildCauseRows()
        {
            _causeList.Clear();
            if (_breakdown == null || _breakdown.AllCauses.Count == 0)
            {
                var empty = new Label("No causes recorded in this preview yet.");
                empty.style.color = new Color(0.75f, 0.78f, 0.84f, 1f);
                empty.style.fontSize = 12;
                _causeList.Add(empty);
                _traceBox.text = "No causality trace available yet.";
                _selectedGroupIndex = -1;
                return;
            }

            for (int i = 0; i < _breakdown.AllCauses.Count; i++)
            {
                int capturedIndex = i;
                var g = _breakdown.AllCauses[i];
                string kindTag = g.IsFatal ? "[DEATH] " : (g.IsKnown ? "[EVENT] " : "[NEW KIND] ");
                string text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}{1}: {2}  (Atk {3} / Def {4}{5})",
                    kindTag,
                    g.Label,
                    g.TotalCount,
                    g.AttackerCount,
                    g.DefenderCount,
                    g.UnattributedCount > 0 ? " / World " + g.UnattributedCount : ""
                );
                var btn = new Button(() => SelectCauseGroup(capturedIndex))
                {
                    text = text,
                    name = "cause-row-" + g.RawKind,
                };
                btn.style.unityTextAlign = TextAnchor.MiddleLeft;
                btn.style.fontSize = 12;
                btn.style.marginBottom = 4;
                btn.style.paddingTop = 4;
                btn.style.paddingBottom = 4;
                btn.style.paddingLeft = 8;
                btn.style.paddingRight = 8;
                btn.style.whiteSpace = WhiteSpace.Normal;
                btn.style.backgroundColor = g.IsFatal
                    ? new Color(0.28f, 0.13f, 0.13f, 0.92f)
                    : (
                        !g.IsKnown
                            ? new Color(0.22f, 0.20f, 0.12f, 0.92f)
                            : new Color(0.14f, 0.17f, 0.22f, 0.92f)
                    );
                btn.style.color = Color.white;
                _causeList.Add(btn);
            }

            SelectCauseGroup(0);
        }

        private static string FormatHeadline(BattleOutcomeResult outcome)
        {
            if (outcome == null)
            {
                return "BATTLE RESULTS";
            }
            switch (outcome.Kind)
            {
                case BattleOutcomeKind.DefenderVictory:
                    return "DEFENDER VICTORY";
                case BattleOutcomeKind.AttackerVictory:
                    return "ATTACKER VICTORY";
                default:
                    return "BATTLE IN PROGRESS";
            }
        }

        private static Color HeadlineColor(BattleOutcomeResult outcome)
        {
            if (outcome == null)
            {
                return Color.white;
            }
            switch (outcome.Kind)
            {
                case BattleOutcomeKind.DefenderVictory:
                    return new Color(0.45f, 0.78f, 1.0f, 1f);
                case BattleOutcomeKind.AttackerVictory:
                    return new Color(1.0f, 0.56f, 0.34f, 1f);
                default:
                    return new Color(0.95f, 0.85f, 0.45f, 1f);
            }
        }

        private static string FormatSubhead(BattleOutcomeResult outcome, LevelData level)
        {
            string title = level != null ? level.Title + " — " : "";
            if (outcome == null)
            {
                return title + "Waiting for simulation...";
            }
            string at = CauseTrace.Clock(
                outcome.DecidedTick > 0 ? outcome.DecidedTick : outcome.EvaluatedTick
            );
            switch (outcome.Reason)
            {
                case BattleOutcomeReason.AttackerArmyRouted:
                    return title + "Attacker army routed (losses/rout >= 700 permille) at " + at;
                case BattleOutcomeReason.HeldUntilSunset:
                    return title + "Defenders held the field until sunset (" + at + ")";
                case BattleOutcomeReason.AllDefendersWiped:
                    return title + "All defending regiments wiped at " + at;
                case BattleOutcomeReason.ObjectiveCaptured:
                    return title + "Attackers captured the objective at " + at;
                default:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}Preview simulated to {1} / Sunset {2}",
                        title,
                        CauseTrace.Clock(outcome.EvaluatedTick),
                        CauseTrace.Clock(outcome.SunsetTick)
                    );
            }
        }

        private static string FormatStats(BattleOutcomeResult outcome, CauseBreakdown breakdown)
        {
            if (outcome == null)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Recorded events: {0} (Atk fatal {1}, Def fatal {2})",
                    breakdown.TotalEvents,
                    breakdown.AttackerLosses,
                    breakdown.DefenderLosses
                );
            }
            return string.Format(
                CultureInfo.InvariantCulture,
                "Attackers: {0}/{1} alive ({2} steadies, {3} permille lost/routed) | Regiments: {4} routed, {5} wiped of {6}\n"
                    + "Defenders: {7}/{8} alive ({9}/{10} regiments active, {11} routed, {12} wiped) | Fatal cause logs: Atk {13}, Def {14}{15}",
                outcome.AttackerAliveSoldiers,
                outcome.AttackerStartingSoldiers,
                outcome.AttackerNonRoutingAlive,
                outcome.AttackerLostOrRoutedPermille,
                outcome.AttackerRoutedRegiments,
                outcome.AttackerWipedRegiments,
                outcome.AttackerTotalRegiments,
                outcome.DefenderAliveSoldiers,
                outcome.DefenderStartingSoldiers,
                outcome.DefenderLiveRegiments,
                outcome.DefenderTotalRegiments,
                outcome.DefenderRoutedRegiments,
                outcome.DefenderWipedRegiments,
                breakdown.AttackerLosses,
                breakdown.DefenderLosses,
                outcome.ObjectivePeakHoldTicks > 0
                    ? string.Format(
                        CultureInfo.InvariantCulture,
                        "\nObjective hold peak: {0:F1} s",
                        outcome.ObjectivePeakHoldTicks / (float)SimConstants.TicksPerSecond
                    )
                    : ""
            );
        }
    }
}
