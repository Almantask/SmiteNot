using GodGame.Bridge;
using GodGame.Sim;
using NUnit.Framework;

namespace GodGame.Tests.EditMode
{
    public class PlanEditorTests
    {
        private static PlanEditor RiverEditor(out BattlePlan plan)
        {
            string repo = RepoLoader.EditorRepoRoot;
            var rules = RepoLoader.LoadRules(repo);
            var map = RepoLoader.LoadMap(repo, "m3-river");
            plan = RepoLoader.LoadPlan(repo, "m3-river-two-thousand.json", map, rules);
            return new PlanEditor(plan, map, rules);
        }

        [Test]
        public void TheEditor_RoundTripsThePlan_AndAcceptsAValidCast()
        {
            var editor = RiverEditor(out var plan);
            Assert.That(editor.Commit(), Is.True);
            Assert.That(editor.Plan.Hash, Is.EqualTo(plan.Hash), "the same layers give the same plan");

            editor.Casts.Add(new PlanEditor.Cast { Id = editor.NewId("rain"), Power = "rain", Tick = 20, XMm = 180000, YMm = 200000, RadiusMm = 40000 });
            Assert.That(editor.Commit(), Is.True);
            Assert.That(editor.Plan.Casts.Count, Is.EqualTo(1));
        }

        [Test]
        public void ARefusedEdit_KeepsTheLastValidPlan_AndSaysWhy()
        {
            var editor = RiverEditor(out var plan);
            editor.Army[0].XMm = 180000; // out of the attacker zone, into the river

            Assert.That(editor.Commit(), Is.False);
            Assert.That(editor.Error, Does.Contain("deployment zone"));
            Assert.That(editor.Plan, Is.SameAs(plan));
            Assert.That(editor.Army[0].XMm, Is.EqualTo(plan.Army[0].XMm), "the lists go back to the valid plan");
        }
    }
}
