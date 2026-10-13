using System.Linq;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class GhostCapTests
    {
        static readonly SimConfig Cfg = TestRuns.Cfg;

        static GhostSet Full(int count)
        {
            var set = new GhostSet();
            for (int i = 1; i <= count; i++) set.Add(TestRuns.Walker(i));
            return set;
        }

        [Test]
        public void At31_TheOldestIsRemoved()
        {
            var set = Full(31);
            var evicted = set.ApplyCap(Cfg.GhostCap);
            Assert.AreEqual(30, set.Count);
            Assert.AreEqual(1, evicted.Single().RunIndex);
            Assert.AreEqual(2, set[0].RunIndex);
        }

        [Test]
        public void TheNemesis_IsSkipped()
        {
            var set = Full(31);
            set[0].Kills = 2;
            var evicted = set.ApplyCap(Cfg.GhostCap);
            Assert.AreEqual(2, evicted.Single().RunIndex);
            Assert.AreEqual(1, set[0].RunIndex);
        }

        [Test]
        public void UnderTheCap_NothingIsRemoved()
        {
            var set = Full(30);
            Assert.IsEmpty(set.ApplyCap(Cfg.GhostCap));
            Assert.AreEqual(30, set.Count);
        }

        [Test]
        public void Set_StaysInRunIndexOrder_AndRejectsDuplicates()
        {
            var set = TestRuns.Set(TestRuns.Walker(5), TestRuns.Walker(2), TestRuns.Walker(9));
            CollectionAssert.AreEqual(new[] { 2, 5, 9 }, set.All.Select(g => g.RunIndex));
            Assert.Throws<System.InvalidOperationException>(() => set.Add(TestRuns.Walker(5)));
        }

        [Test]
        public void Commit_AppliesTheCap()
        {
            var p = TestRuns.ProgressOnFlat();
            for (int i = 1; i <= 30; i++) p.Ghosts.Add(TestRuns.Ghost(i, deathTick: 10));
            p.Best = 1_000_000;
            p.NextRunIndex = 31;

            // The ledge course drops the player into a gap; the ghosts all die on tick 10, before grace ends.
            var run = RunRules.StartRun(p, TestCourses.Ledge(), Cfg);
            Assert.AreEqual(30, run.GhostsAtStart);
            TestRuns.StepTo(run, 1000);
            Assert.AreEqual(DeathCause.Fall, run.Cause);

            CommitResult result = RunRules.Commit(p, run, 7, Cfg);
            Assert.AreEqual(30, p.Ghosts.Count);
            Assert.AreEqual(1, result.Evicted.Single().RunIndex);
            Assert.AreSame(result.AddedGhost, p.Ghosts[29]);
        }
    }
}
