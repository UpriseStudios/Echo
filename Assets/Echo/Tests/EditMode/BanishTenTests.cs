using System.Collections.Generic;
using System.Linq;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>Balance addendum: Banish 10 at a full graveyard, once per high-score cycle, never the Nemesis.</summary>
    public class BanishTenTests
    {
        static readonly SimConfig Cfg = TestRuns.Cfg;

        // A graveyard of `count` walkers (run indices 1..count), the player's best out of reach, then a run that dies.
        static (Progress p, RunSimulation run) DiesWith(int count, int best = 1_000_000)
        {
            var ghosts = new GhostRecord[count];
            for (int i = 0; i < count; i++) ghosts[i] = TestRuns.Walker(i + 1);
            var p = TestRuns.ProgressOnFlat(ghosts);
            p.Best = best;
            p.NextRunIndex = count + 1;
            var run = RunRules.StartRun(p, TestCourses.Flat(), Cfg);
            TestRuns.StepTo(run, 1000);
            Assert.IsTrue(run.IsDead);
            return (p, run);
        }

        [Test]
        public void TakesTheDeadliest_TiesToTheOldest_ThenTheOldest_NeverTheNemesis()
        {
            var set = new GhostSet();
            for (int i = 1; i <= 30; i++) set.Add(new GhostRecord { RunIndex = i, DeathTick = 100 });
            set[9].Kills = 6;                                  // run 10: the Nemesis
            set[19].Kills = 3; set[20].Kills = 3; set[21].Kills = 3;
            set[4].Kills = 1; set[5].Kills = 1; set[0].Kills = 1;
            Assert.AreEqual(10, set.Nemesis.RunIndex);

            List<int> chosen = RunRules.ChooseBanishTen(set).Select(g => g.RunIndex).ToList();
            CollectionAssert.AreEqual(new[] { 20, 21, 22, 1, 5, 6, 2, 3, 4, 7 }, chosen);
        }

        [Test]
        public void OfferedWhenTheGraveyardWillBeFull_TenRemoved_LeavingTwenty()
        {
            var (p, run) = DiesWith(29);
            Assert.IsTrue(RunRules.CanBanishTen(run, p, Cfg), "29 ghosts plus this run makes 30");
            Assert.Throws<System.InvalidOperationException>(() => RunRules.BanishTen(run, p, Cfg), "the run is committed first");

            RunRules.Commit(p, run, 5, Cfg);
            Assert.AreEqual(30, p.Ghosts.Count);
            GhostRecord nemesis = p.Ghosts.Nemesis;
            List<GhostRecord> removed = RunRules.BanishTen(run, p, Cfg);

            Assert.AreEqual(10, removed.Count);
            Assert.AreEqual(20, p.Ghosts.Count);
            if (nemesis != null) Assert.IsTrue(p.Ghosts.Contains(nemesis), "the Nemesis is never removed");
            int fewestRemoved = removed.Min(g => g.Kills);
            Assert.IsTrue(p.Ghosts.All.Where(g => g != nemesis).All(g => g.Kills <= fewestRemoved), "the deadliest go");
            Assert.IsTrue(p.BanishTenUsed);
            Assert.IsFalse(RunRules.CanBanishTen(run, p, Cfg), "once per cycle");
        }

        [Test]
        public void NotOfferedBelowTheCap()
        {
            var (p, run) = DiesWith(27);
            Assert.IsFalse(RunRules.CanBanishTen(run, p, Cfg));
        }

        [Test]
        public void NotOfferedOnANewBest_WhichClearsEveryGhostAnyway()
        {
            var (p, run) = DiesWith(29, best: 0);
            Assert.IsFalse(RunRules.CanBanishTen(run, p, Cfg));
        }

        [Test]
        public void NotInDuelsOrChallenges()
        {
            var (p, _) = DiesWith(29);
            foreach (var options in new[] { new RunOptions { Duel = true }, new RunOptions { Challenge = true } })
            {
                var run = new RunSimulation(TestCourses.Flat(), p.Ghosts, p.NextRunIndex, 100, Cfg, options);
                TestRuns.StepTo(run, 1000);
                Assert.IsFalse(RunRules.CanBanishTen(run, p, Cfg));
            }
        }

        [Test]
        public void ANewBest_ResetsIt_ForTheNextCycle()
        {
            var (p, run) = DiesWith(29);
            RunRules.Commit(p, run, 5, Cfg);
            RunRules.BanishTen(run, p, Cfg);
            Assert.AreEqual(1, p.RunsSinceBest);

            p.Best = 0;
            var best = RunRules.StartRun(p, TestCourses.Flat(), Cfg);
            TestRuns.StepTo(best, 1000);
            Assert.IsTrue(RunRules.Commit(p, best, 1, Cfg).NewBest);
            Assert.IsFalse(p.BanishTenUsed);
            Assert.AreEqual(0, p.RunsSinceBest);
        }

        [Test]
        public void TheSingleBanish_StillWorks_AfterBanishTenIsUsed()
        {
            var (p, run) = DiesWith(29);
            RunRules.Commit(p, run, 5, Cfg);
            RunRules.BanishTen(run, p, Cfg);
            var next = RunRules.StartRun(p, TestCourses.Flat(), Cfg);
            TestRuns.StepTo(next, 1000);
            Assert.IsFalse(RunRules.CanBanishTen(next, p, Cfg));
            if (next.Killer != null && next.Killer != p.Ghosts.Nemesis) Assert.IsTrue(RunRules.CanBanish(next, p));
        }
    }
}
