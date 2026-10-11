using GodGame.Bridge;
using GodGame.Sim;
using NUnit.Framework;

namespace GodGame.Tests.EditMode
{
    public class SimHostTests
    {
        [Test]
        public void Advance_OneSecond_RunsTwentyTicks()
        {
            var host = new SimHost(new PlaceholderSetup());

            Assert.That(host.Advance(1.0 + 1e-9), Is.EqualTo(SimConstants.TicksPerSecond));
            Assert.That(host.Tick, Is.EqualTo(SimConstants.TicksPerSecond));
        }

        [Test]
        public void Advance_UnevenFrames_RunWholeTicksOnly()
        {
            var host = new SimHost(new PlaceholderSetup());
            for (int frame = 0; frame < 7; frame++)
            {
                host.Advance(0.016);
            }

            Assert.That(host.Tick, Is.EqualTo(2)); // 0.112 s holds two whole 0.05 s ticks
            Assert.That(host.Alpha, Is.InRange(0.0, 1.0));
        }

        [Test]
        public void Advance_LongFrame_IsCapped()
        {
            var host = new SimHost(new PlaceholderSetup());

            Assert.That(host.Advance(10.0), Is.EqualTo(SimHost.MaxTicksPerAdvance));
        }

        [Test]
        public void Host_MatchesTheSimulationRunDirectly()
        {
            var setup = new PlaceholderSetup(seed: 3, unitCount: 4);
            var host = new SimHost(setup);
            var direct = new PlaceholderBattle(setup);

            for (int i = 0; i < 100; i++)
            {
                host.Advance(SimHost.SecondsPerTick);
            }
            direct.Step(host.Tick);

            Assert.That(host.Battle.StateHash(), Is.EqualTo(direct.StateHash()));
        }
    }
}
