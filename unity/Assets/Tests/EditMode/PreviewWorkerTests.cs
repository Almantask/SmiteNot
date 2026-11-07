using System.Diagnostics;
using System.Threading;
using GodGame.Bridge;
using GodGame.Sim;
using NUnit.Framework;

namespace GodGame.Tests.EditMode
{
    public class PreviewWorkerTests
    {
        private static Battle River(bool storm, int stormTick = 300)
        {
            string repo = RepoLoader.EditorRepoRoot;
            var rules = RepoLoader.LoadRules(repo);
            var map = RepoLoader.LoadMap(repo, "m3-river");
            string json = System.IO.File.ReadAllText(
                System.IO.Path.Combine(repo, "data", "plans", "m3-river-two-thousand.json")
            );
            if (storm)
            {
                json = json.Replace(
                    "\"casts\": []",
                    "\"casts\": [{\"id\": \"storm\", \"power\": \"rain\", \"tick\": "
                        + stormTick
                        + ", \"xMm\": 180000, \"yMm\": 200000, \"radiusMm\": 40000}]"
                );
                Assert.That(json, Does.Contain("storm"));
            }
            return new Battle(rules, map, BattlePlan.Load(json, map, rules), 1);
        }

        [Test]
        public void TheFutureFillsToTheEnd_AndACastEditResumesFromItsTick()
        {
            using var worker = new PreviewWorker(River(false), new SerialRunner(), new KeyframeStore(long.MaxValue), 600);

            worker.RunUntilIdle();
            var full = worker.ReadProgress();
            Assert.That(full.Complete, Is.True);
            Assert.That(full.Filled, Is.EqualTo(600));
            Assert.That(full.Fraction, Is.EqualTo(1f));

            worker.Edit(River(true).Plan);
            worker.RunUntilIdle();
            var edited = worker.ReadProgress();
            Assert.That(edited.Edits, Is.EqualTo(1));
            Assert.That(edited.ResumedAt, Is.EqualTo(300), "the keyframe at the storm's tick");
            Assert.That(edited.Complete && edited.FirstWindowReady, Is.True);
        }

        [Test]
        public void OnItsThread_TheWorkerNeverBlocksTheCaller_FillsTheFirst30sFirst_AndKeepsTheNewestEdit()
        {
            using var worker = new PreviewWorker(River(false), new ThreadedRunner(4), new KeyframeStore(long.MaxValue));
            worker.Start();
            var clock = Stopwatch.StartNew();
            worker.Edit(River(true, 1_200).Plan);
            worker.Edit(River(true, 1_000).Plan); // replaces the first if the worker hasn't picked it up
            long postMs = clock.ElapsedMilliseconds;

            var progress = worker.ReadProgress();
            while (clock.ElapsedMilliseconds < 120_000 && !(progress.Edits == worker.Posted && progress.Complete))
            {
                Thread.Sleep(50);
                progress = worker.ReadProgress();
            }
            worker.Stop();

            Assert.That(worker.Failure, Is.Null);
            Assert.That(postMs, Is.LessThan(50), "posting an edit never waits on the simulation");
            Assert.That(progress.Complete, Is.True, "the 3-minute future filled within two minutes");
            Assert.That(progress.FirstWindowMs, Is.InRange(0, progress.FillMs));
            TestContext.Out.WriteLine(
                $"m3-river, 1,700 soldiers: first 30 s after the edit in {progress.FirstWindowMs:0} ms, all 3 min in {progress.FillMs:0} ms (resumed at {progress.ResumedAt})"
            );
        }
    }
}
