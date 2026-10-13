using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class CommitTests
    {
        static readonly SimConfig Cfg = TestRuns.Cfg;

        /// <summary>Run 5 on flat floor with the run-3 walker as Nemesis (2 kills). It kills the player again on tick 60.</summary>
        static (Progress p, RunSimulation run, GhostRecord nemesis) NemesisKillsRun5(int best)
        {
            var nemesis = TestRuns.Walker(3);
            nemesis.Kills = 2;
            nemesis.LastKillRun = 4;
            nemesis.SpeedPct = 110; // recorded since the last clear, so at the same speed as this run
            var p = TestRuns.ProgressOnFlat(nemesis);
            p.Best = best;
            p.Clears = 2;
            p.CycleBanishes = 2;
            p.NextRunIndex = 5;
            var run = RunRules.StartRun(p, TestCourses.Flat(), Cfg);
            TestRuns.StepTo(run, 1000);
            Assert.AreSame(nemesis, run.Killer);
            return (p, run, nemesis);
        }

        [Test]
        public void NewBest_ClearsGhosts_ResetsCycle_NewSeed_CountsClear_AndAddsNoGhost()
        {
            var (p, run, _) = NemesisKillsRun5(best: 0);
            int score = run.Score;
            CommitResult r = RunRules.Commit(p, run, 999, Cfg);

            Assert.IsTrue(r.NewBest);
            Assert.AreEqual(score, r.Score);
            Assert.AreEqual(score, p.Best);
            Assert.AreEqual(0, r.PreviousBest);
            Assert.IsTrue(r.NemesisDefeated);
            Assert.AreEqual("The Third", r.DefeatedNemesisName);
            Assert.AreEqual(1, r.GhostsCleared);
            Assert.IsNull(r.AddedGhost);
            Assert.IsNull(r.Nemesis);
            Assert.AreEqual(0, p.Ghosts.Count);
            Assert.AreEqual(0, p.CycleBanishes);
            Assert.AreEqual(999u, p.Seed);
            Assert.AreEqual(3, p.Clears);
            Assert.AreEqual(6, p.NextRunIndex);
            Assert.AreEqual(1, p.Stats.TotalRuns);
            Assert.AreEqual(1, p.Stats.NemesesDefeated);
        }

        [Test]
        public void NotABest_AddsTheGhost_AndKeepsEverythingElse()
        {
            var (p, run, nemesis) = NemesisKillsRun5(best: 1_000_000);
            CommitResult r = RunRules.Commit(p, run, 999, Cfg);

            Assert.IsFalse(r.NewBest);
            Assert.AreEqual(5, r.AddedGhost.RunIndex);
            Assert.AreEqual(110, r.AddedGhost.SpeedPct, "speed from 2 clears");
            Assert.AreEqual(60, r.AddedGhost.DeathTick);
            Assert.AreEqual(2, p.Ghosts.Count);
            Assert.AreSame(nemesis, r.Nemesis);
            Assert.AreEqual(1_000_000, p.Best);
            Assert.AreEqual(1u, p.Seed);
            Assert.AreEqual(2, p.Clears);
            Assert.AreEqual(2, p.CycleBanishes);
            Assert.AreEqual(6, p.NextRunIndex);
            Assert.AreEqual(1, p.Stats.TotalRuns);
            Assert.AreEqual(0, p.Stats.NemesesDefeated);
        }

        [Test]
        public void EqualToBest_IsNotANewBest()
        {
            var (p, run, _) = NemesisKillsRun5(best: 0);
            p.Best = run.Score;
            Assert.IsFalse(RunRules.Commit(p, run, 999, Cfg).NewBest);
        }

        [Test]
        public void NewBestWithoutANemesis_IsNotANemesisDefeat()
        {
            var p = TestRuns.ProgressOnFlat(TestRuns.Walker(1));
            p.NextRunIndex = 2;
            var run = RunRules.StartRun(p, TestCourses.Flat(), Cfg);
            TestRuns.StepTo(run, 1000);
            CommitResult r = RunRules.Commit(p, run, 3, Cfg);
            Assert.IsTrue(r.NewBest);
            Assert.IsFalse(r.NemesisDefeated);
            Assert.AreEqual(0, p.Stats.NemesesDefeated);
        }

        [Test]
        public void WorstNemesis_IsTracked()
        {
            var (p, run, _) = NemesisKillsRun5(best: 1_000_000);
            RunRules.Commit(p, run, 999, Cfg);
            Assert.AreEqual(3, p.Stats.WorstNemesisKills);
            Assert.AreEqual("The Third", p.Stats.WorstNemesisName);
        }

        [Test]
        public void RunInProgress_CannotBeCommitted()
        {
            var p = TestRuns.ProgressOnFlat();
            var run = RunRules.StartRun(p, TestCourses.Flat(), Cfg);
            run.Step(false);
            Assert.Throws<System.InvalidOperationException>(() => RunRules.Commit(p, run, 1, Cfg));
            Assert.AreEqual(1, p.NextRunIndex);
        }

        [Test]
        public void Run_CommitsOnlyOnce()
        {
            var (p, run, _) = NemesisKillsRun5(best: 1_000_000);
            RunRules.Commit(p, run, 999, Cfg);
            Assert.Throws<System.InvalidOperationException>(() => RunRules.Commit(p, run, 999, Cfg));
        }

        [Test]
        public void StartRun_UsesNextRunIndex_SpeedFromClears_AndMatchingSeed()
        {
            var p = TestRuns.ProgressOnFlat();
            p.NextRunIndex = 9;
            p.Clears = 20;
            var run = RunRules.StartRun(p, TestCourses.Flat(), Cfg);
            Assert.AreEqual(9, run.RunIndex);
            Assert.AreEqual(Cfg.MaxSpeedPct, run.SpeedPct);

            p.Seed = 2;
            Assert.Throws<System.ArgumentException>(() => RunRules.StartRun(p, TestCourses.Flat(), Cfg));
        }
    }
}
