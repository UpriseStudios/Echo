using Echo.Core.Ghosts;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class NemesisTests
    {
        static GhostRecord G(int runIndex, int kills, int lastKillRun) =>
            new GhostRecord { RunIndex = runIndex, Kills = kills, LastKillRun = lastKillRun };

        [Test]
        public void NoNemesis_BelowTwoKills()
        {
            Assert.IsNull(TestRuns.Set(G(1, 1, 5), G(2, 0, 0)).Nemesis);
        }

        [Test]
        public void MostKills_IsTheNemesis()
        {
            var top = G(2, 3, 4);
            Assert.AreSame(top, TestRuns.Set(G(1, 2, 9), top, G(3, 2, 8)).Nemesis);
        }

        [Test]
        public void Ties_GoToTheMostRecentKiller()
        {
            var recent = G(1, 2, 9);
            Assert.AreSame(recent, TestRuns.Set(recent, G(2, 2, 6), G(3, 2, 8)).Nemesis);
        }

        [Test]
        public void IsRecomputedOnDeath()
        {
            // Second kill by the same ghost makes it the Nemesis immediately, while the run is still pending.
            var walker = TestRuns.Walker();
            walker.Kills = 1;
            walker.LastKillRun = 1;
            var set = TestRuns.Set(walker);
            Assert.IsNull(set.Nemesis);

            var run = TestRuns.OnFlat(set, runIndex: 2);
            TestRuns.StepTo(run, 1000);
            Assert.AreSame(walker, set.Nemesis);
        }

        [TestCase(1, "The First")]
        [TestCase(7, "The Seventh")]
        [TestCase(12, "The Twelfth")]
        [TestCase(20, "The Twentieth")]
        [TestCase(21, "The 21st")]
        [TestCase(22, "The 22nd")]
        [TestCase(23, "The 23rd")]
        [TestCase(34, "The 34th")]
        [TestCase(101, "The 101st")]
        [TestCase(111, "The 111th")]
        [TestCase(112, "The 112th")]
        [TestCase(113, "The 113th")]
        public void Names(int runIndex, string expected)
        {
            Assert.AreEqual(expected, NemesisNames.For(runIndex));
        }
    }
}
