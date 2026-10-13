using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class BanishTests
    {
        static readonly SimConfig Cfg = TestRuns.Cfg;

        /// <summary>Run 2 on flat floor, killed on tick 60 by the walker from run 1. Best is out of reach.</summary>
        static (Progress p, RunSimulation run, GhostRecord killer) KilledByWalker(int priorKills = 0)
        {
            var walker = TestRuns.Walker(1);
            walker.Kills = priorKills;
            var p = TestRuns.ProgressOnFlat(walker);
            p.Best = 1_000_000;
            p.NextRunIndex = 2;
            var run = RunRules.StartRun(p, TestCourses.Flat(), Cfg);
            TestRuns.StepTo(run, 1000);
            Assert.AreEqual(DeathCause.Ghost, run.Cause);
            return (p, run, walker);
        }

        [Test]
        public void KilledByAnOrdinaryGhost_CanBanish_AndItIsRemoved()
        {
            var (p, run, killer) = KilledByWalker();
            Assert.IsTrue(RunRules.CanBanish(run, p));

            RunRules.Banish(run, p);
            Assert.IsFalse(p.Ghosts.Contains(killer));
            Assert.AreEqual(1, p.CycleBanishes);
            Assert.IsTrue(run.BanishUsed);
            Assert.IsFalse(RunRules.CanBanish(run, p), "once per run");
            Assert.IsFalse(RunRules.CanContinue(run), "banishing ends the run");

            CommitResult result = RunRules.Commit(p, run, 5, Cfg);
            Assert.AreEqual(1, p.Ghosts.Count);
            Assert.AreSame(result.AddedGhost, p.Ghosts[0]);
            Assert.AreEqual(1, p.CycleBanishes);
        }

        [Test]
        public void TheNemesis_CannotBeBanished()
        {
            var (p, run, killer) = KilledByWalker(priorKills: 1);
            Assert.AreSame(killer, p.Ghosts.Nemesis);
            Assert.IsFalse(RunRules.CanBanish(run, p));
            Assert.Throws<System.InvalidOperationException>(() => RunRules.Banish(run, p));
        }

        [Test]
        public void HazardDeath_CannotBanish()
        {
            var p = TestRuns.ProgressOnFlat(TestRuns.Walker(1));
            p.NextRunIndex = 2;
            var run = RunRules.StartRun(p, TestCourses.Ledge(), Cfg);
            TestRuns.StepTo(run, 1000);
            Assert.AreEqual(DeathCause.Fall, run.Cause);
            Assert.IsFalse(RunRules.CanBanish(run, p));
        }

        [Test]
        public void ThreePerCycle_ThenUnavailable()
        {
            var (p, run, _) = KilledByWalker();
            p.CycleBanishes = RunRules.MaxBanishesPerCycle - 1;
            Assert.IsTrue(RunRules.CanBanish(run, p));
            p.CycleBanishes = RunRules.MaxBanishesPerCycle;
            Assert.IsFalse(RunRules.CanBanish(run, p));
        }

        [Test]
        public void NotAfterCommit()
        {
            var (p, run, _) = KilledByWalker();
            RunRules.Commit(p, run, 5, Cfg);
            Assert.IsFalse(RunRules.CanBanish(run, p));
        }

        [Test]
        public void NewBest_ResetsTheCycle()
        {
            var (p, run, _) = KilledByWalker();
            p.CycleBanishes = 2;
            p.Best = 0;
            RunRules.Commit(p, run, 5, Cfg);
            Assert.AreEqual(0, p.CycleBanishes);
        }
    }
}
