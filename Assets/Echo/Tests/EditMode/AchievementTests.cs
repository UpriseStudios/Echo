using System.Linq;
using Echo.Core.Achievements;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>The 30 launch achievements (skins addendum): event-driven, with progress, unlocked once.</summary>
    public class AchievementTests
    {
        static readonly SimConfig Cfg = TestRuns.Cfg;

        static Progress WithGhosts(int count, int best = 1_000_000)
        {
            var p = TestRuns.ProgressOnFlat();
            for (int i = 1; i <= count; i++) p.Ghosts.Add(TestRuns.Ghost(i, deathTick: 10));
            p.Best = best;
            p.NextRunIndex = count + 1;
            return p;
        }

        /// <summary>Plays a run on <paramref name="course"/> until it dies, then commits it.</summary>
        static (RunSimulation run, CommitResult result) RunAndCommit(Progress p, Course course, int maxTicks = 100_000, params int[] taps)
        {
            var run = RunRules.StartRun(p, course, Cfg);
            TestRuns.StepTo(run, maxTicks, taps);
            Assert.IsTrue(run.IsDead, "run never ended");
            return (run, RunRules.Commit(p, run, 77, Cfg));
        }

        /// <summary>Flat floor for 76 units, then a fall.</summary>
        static Course LongLedge() => new Course(TestCourses.Library(
            @"{ ""id"": ""a"", ""tier"": 0, ""length"": 80000, ""gaps"": [{ ""x"": 76000, ""w"": 3000 }] }",
            TestCourses.FlatTier1), 1);

        [Test]
        public void Catalog_HasThirtySinglePlayerAchievements_ThreeHidden_PlusFourOnlineExtras()
        {
            Assert.AreEqual(34, AchievementCatalog.All.Length);
            Assert.AreEqual(34, AchievementCatalog.All.Select(d => d.Id).Distinct().Count());
            Assert.AreEqual(30, AchievementCatalog.SinglePlayerCount, "the Elementals need these 30");
            CollectionAssert.AreEquivalent(
                new[] { AchievementIds.HauntedByStrangers, AchievementIds.PassingThrough, AchievementIds.TopTen, AchievementIds.DailyGrind },
                AchievementCatalog.All.Where(d => d.Online).Select(d => d.Id));
            CollectionAssert.AreEquivalent(
                new[] { AchievementIds.ThatWasMe, AchievementIds.SwornEnemy, AchievementIds.UnfinishedBusiness },
                AchievementCatalog.All.Where(d => d.Hidden).Select(d => d.Id));
            Assert.IsTrue(AchievementCatalog.All.All(d => d.Target >= 1 && d.Title.Length > 0 && d.Description.Length > 0));
        }

        [Test]
        public void EachUnlocksOnce_InDisplayOrder()
        {
            var p = WithGhosts(30);
            var (run, result) = RunAndCommit(p, TestCourses.Ledge());
            var first = AchievementRules.OnRunEnded(run, result, p, sessionRuns: 1);
            CollectionAssert.AreEqual(new[]
            {
                AchievementIds.CrowdedHouse, AchievementIds.EchoChamber, AchievementIds.Haunted,
                AchievementIds.TheyreEverywhere, AchievementIds.ArmyOfMe, AchievementIds.GraveyardShift,
            }, first);
            Assert.IsEmpty(AchievementRules.OnRunEnded(run, result, p, sessionRuns: 1));
            CollectionAssert.AreEquivalent(first, p.Achievements);
        }

        [Test]
        public void Progress_IsKept_CappedAtTheTarget_AndNeverGoesDown()
        {
            var p = WithGhosts(7);
            var (run, result) = RunAndCommit(p, TestCourses.Ledge());
            AchievementRules.OnRunEnded(run, result, p, 1);
            Assert.Contains(AchievementIds.CrowdedHouse, p.Achievements);
            Assert.AreEqual(5, AchievementRules.ProgressOf(p, AchievementIds.CrowdedHouse));
            Assert.AreEqual(7, AchievementRules.ProgressOf(p, AchievementIds.EchoChamber));

            var fewer = WithGhosts(2);
            fewer.AchievementProgress[AchievementIds.EchoChamber] = 7;
            var (run2, result2) = RunAndCommit(fewer, TestCourses.Ledge());
            AchievementRules.OnRunEnded(run2, result2, fewer, 1);
            Assert.AreEqual(7, AchievementRules.ProgressOf(fewer, AchievementIds.EchoChamber));
        }

        [Test]
        public void TheSameGhostKillingYouThreeTimes_IsSeriously()
        {
            var walker = TestRuns.Walker(3);
            walker.Kills = 2;
            walker.LastKillRun = 4;
            var p = TestRuns.ProgressOnFlat(walker);
            p.Best = 1_000_000;
            p.NextRunIndex = 5;
            var (run, result) = RunAndCommit(p, TestCourses.Flat(), maxTicks: 1000);
            Assert.AreSame(walker, run.Killer);

            var earned = AchievementRules.OnRunEnded(run, result, p, 1);
            CollectionAssert.IsSubsetOf(new[] { AchievementIds.HelloAgain, AchievementIds.ThatWasMe, AchievementIds.DejaVu, AchievementIds.Seriously }, earned);
            CollectionAssert.DoesNotContain(earned, AchievementIds.SwornEnemy);
            Assert.AreEqual(3, AchievementRules.ProgressOf(p, AchievementIds.SwornEnemy));
        }

        [Test]
        public void Distance_CountsTowardsGettingStarted()
        {
            var p = WithGhosts(0);
            var (run, result) = RunAndCommit(p, LongLedge());
            AchievementRules.OnRunEnded(run, result, p, 1);
            Assert.AreEqual(run.Distance, AchievementRules.ProgressOf(p, AchievementIds.GettingStarted));
            Assert.That(run.Distance, Is.InRange(70, 99));
        }

        [Test]
        public void ANewBestPastGhostsWithoutTouchingAny_IsUntouchable_AndAFreshStart()
        {
            var p = WithGhosts(1, best: 0);
            var (run, result) = RunAndCommit(p, TestCourses.Ledge());
            Assert.IsTrue(result.NewBest);
            var earned = AchievementRules.OnRunEnded(run, result, p, 1);
            CollectionAssert.Contains(earned, AchievementIds.Untouchable);
            CollectionAssert.Contains(earned, AchievementIds.FreshStart);
        }

        [Test]
        public void DyingJustShortOfYourBest_IsSoClose()
        {
            var p = WithGhosts(0, best: 8);
            var (run, result) = RunAndCommit(p, TestCourses.Ledge());
            Assert.IsFalse(result.NewBest);
            CollectionAssert.Contains(AchievementRules.OnRunEnded(run, result, p, 1), AchievementIds.SoClose);
        }

        [Test]
        public void RunningPastAGhostsGrave_IsPerfectEcho()
        {
            var ghost = TestRuns.Ghost(1, deathTick: 10);
            ghost.DeathX = 1000;
            var p = TestRuns.ProgressOnFlat(ghost);
            p.Best = 1_000_000;
            p.NextRunIndex = 2;
            var (run, result) = RunAndCommit(p, TestCourses.Ledge());
            CollectionAssert.Contains(AchievementRules.OnRunEnded(run, result, p, 1), AchievementIds.PerfectEcho);

            var unknown = WithGhosts(1); // a v1 ghost with no recorded death x never counts
            var (run2, result2) = RunAndCommit(unknown, TestCourses.Ledge());
            CollectionAssert.DoesNotContain(AchievementRules.OnRunEnded(run2, result2, unknown, 1), AchievementIds.PerfectEcho);
        }

        [Test]
        public void TwentyFiveRunsInOneSession_IsJustOneMore()
        {
            var p = WithGhosts(0);
            var (run, result) = RunAndCommit(p, TestCourses.Ledge());
            CollectionAssert.DoesNotContain(AchievementRules.OnRunEnded(run, result, p, 24), AchievementIds.JustOneMore);
            var (run2, result2) = RunAndCommit(p, TestCourses.Ledge());
            CollectionAssert.Contains(AchievementRules.OnRunEnded(run2, result2, p, 25), AchievementIds.JustOneMore);
        }

        [Test]
        public void TwentyFiveQuickRestarts_IsICanFixThis()
        {
            var p = new Progress();
            for (int i = 0; i < 24; i++) Assert.IsEmpty(AchievementRules.OnQuickRestart(p));
            CollectionAssert.AreEqual(new[] { AchievementIds.ICanFixThis }, AchievementRules.OnQuickRestart(p));
        }

        [Test]
        public void ComingBackToThreeGraves_IsUnfinishedBusiness()
        {
            Assert.IsEmpty(AchievementRules.OnSessionStarted(WithGhosts(2)));
            CollectionAssert.AreEqual(new[] { AchievementIds.UnfinishedBusiness }, AchievementRules.OnSessionStarted(WithGhosts(3)));
        }
    }
}
