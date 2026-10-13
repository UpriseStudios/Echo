using Echo.Core.Ghosts;
using Echo.Game.Flow;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class DeathScreenTextTests
    {
        [Test]
        public void OrdinaryGhost_NamesTheRun()
        {
            var walker = TestRuns.Walker(7);
            var run = TestRuns.OnFlat(TestRuns.Set(walker), runIndex: 8);
            TestRuns.StepTo(run, 1000);
            Assert.AreEqual("Killed by: run #7", DeathScreenText.KillerLine(run, null));
        }

        [Test]
        public void Nemesis_GetsItsNameAndKillCount()
        {
            var walker = TestRuns.Walker(7);
            walker.Kills = 3;
            var set = TestRuns.Set(walker);
            var run = TestRuns.OnFlat(set, runIndex: 8);
            TestRuns.StepTo(run, 1000);
            Assert.AreEqual("The Seventh got you again (4)", DeathScreenText.KillerLine(run, set.Nemesis));
        }

        [Test]
        public void HazardDeath_HasNoKillerLine()
        {
            var run = new Echo.Core.Rules.RunSimulation(TestCourses.Ledge(), new GhostSet(), 1, 100, TestRuns.Cfg);
            TestRuns.StepTo(run, 1000);
            Assert.AreEqual("", DeathScreenText.KillerLine(run, null));
        }
    }
}
