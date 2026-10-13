using System.Collections.Generic;
using System.Linq;
using Echo.Core.Achievements;
using Echo.Core.Courses;
using Echo.Core.Daily;
using Echo.Core.Ghosts;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>The Daily Haunt's rules (version 2.0): leaders first, distance only, its own ghosts, no extras.</summary>
    public class DailyRulesTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;

        static GhostRecord Run(int runIndex, int deathTick, int kills = 0) =>
            new GhostRecord { RunIndex = runIndex, DeathTick = deathTick, TapTicks = new[] { 30, 90 }, Kills = kills, LastKillRun = kills > 0 ? runIndex : 0 };

        [Test]
        public void Leaders_ClaimTheirMeetingPointsFirst_BestRankFirst_ThenKillers()
        {
            var ghosts = new List<GhostRecord>
            {
                Run(1, 300, kills: 4),
                Run(2, 300),
                DailyRules.AsLeader(Run(50, 900), 3, "Third"),
                DailyRules.AsLeader(Run(51, 900), 1, "First"),
            };
            ghosts.Sort((a, b) => a.RunIndex.CompareTo(b.RunIndex));
            int[] order = GhostManager.EncounterOrder(ghosts);
            Assert.AreEqual("First", ghosts[order[0]].Name);
            Assert.AreEqual("Third", ghosts[order[1]].Name);
            Assert.AreEqual(1, ghosts[order[2]].RunIndex, "then the killer");
        }

        [Test]
        public void ALeader_IsTheirBestRun_RenumberedAboveAnyOwnRun()
        {
            GhostRecord best = Run(17, 700, kills: 3);
            GhostRecord leader = DailyRules.AsLeader(best, 1, "SwiftGhost#4821");
            Assert.IsTrue(leader.Leader);
            Assert.AreEqual("SwiftGhost#4821", leader.Name);
            Assert.AreEqual(DailyRules.LeaderRunIndexBase + DailyRules.Leaders, leader.RunIndex);
            Assert.AreEqual(0, leader.Kills, "leaders start every day clean");
            Assert.AreEqual(17, best.RunIndex, "their own record is untouched");
            CollectionAssert.AreEqual(best.TapTicks, leader.TapTicks);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DailyRules.AsLeader(best, 11, "x"));
        }

        [Test]
        public void ADailyRun_HasNoContinueBanishOrDuel_AndIsNeverCommitted()
        {
            GhostSet ghosts = DailyRules.Ghosts(new[] { DailyRules.AsLeader(Run(5, 100), 1, "L") }, new[] { Run(1, 60, kills: 6) });
            var p = new Progress();
            RunSimulation run = DailyRules.Start(TestCourses.Flat(), ghosts, 2, TestRuns.Cfg);
            TestRuns.StepTo(run, 3000);
            Assert.IsTrue(run.IsDead);
            Assert.IsTrue(run.Options.Daily);
            Assert.IsFalse(RunRules.CanContinue(run));
            Assert.IsFalse(RunRules.CanBanish(run, p));
            Assert.IsFalse(RunRules.CanBanishTen(run, p, TestRuns.Cfg));
            Assert.IsFalse(Echo.Core.Challenges.ChallengeRuns.CanSend(run), "daily runs aren't sent as challenges");
            Assert.Throws<System.InvalidOperationException>(() => RunRules.Commit(p, run, 1, TestRuns.Cfg));
        }

        [Test]
        public void Check_RefusesRunsPlayedUnderOtherRules()
        {
            var good = new Reel { SpeedPct = 100, RunIndex = 3, Options = new RunOptions { Daily = true } };
            Assert.IsNull(DailyRules.Check(good));
            Reel Variant(System.Action<Reel> change)
            {
                var r = new Reel { SpeedPct = 100, RunIndex = 3, Options = new RunOptions { Daily = true } };
                change(r);
                return r;
            }
            Assert.AreEqual("options", DailyRules.Check(Variant(r => r.Options = new RunOptions())));
            Assert.AreEqual("options", DailyRules.Check(Variant(r => r.Options = new RunOptions { Daily = true, Twist = Twist.Mirror })));
            Assert.AreEqual("options", DailyRules.Check(Variant(r => r.Options = new RunOptions { Daily = true, Bounties = true })));
            Assert.AreEqual("power-ups", DailyRules.Check(Variant(r => r.ShieldTick = 40)));
            Assert.AreEqual("power-ups", DailyRules.Check(Variant(r => r.PhaseTick = 40)));
            Assert.AreEqual("power-ups", DailyRules.Check(Variant(r => r.ExtraLife = true)));
            Assert.AreEqual("power-ups", DailyRules.Check(Variant(r => r.ContinueTick = 400)));
            Assert.AreEqual("speed", DailyRules.Check(Variant(r => r.SpeedPct = 130)));
            Assert.AreEqual("run-index", DailyRules.Check(Variant(r => r.RunIndex = DailyRules.LeaderRunIndexBase + 1)));
        }

        [Test]
        public void SameGhosts_CatchesAnEasierCrowd()
        {
            var expected = new List<GhostRecord> { Run(1, 100), DailyRules.AsLeader(Run(9, 500), 1, "L") };
            var same = expected.Select(g => g.Clone()).ToList();
            Assert.IsTrue(DailyRules.SameGhosts(same, expected));
            Assert.IsFalse(DailyRules.SameGhosts(same.Take(1).ToList(), expected), "a ghost left out");
            var edited = expected.Select(g => g.Clone()).ToList();
            edited[1].TapTicks = new[] { 30 };
            Assert.IsFalse(DailyRules.SameGhosts(edited, expected), "a leader's taps changed");
            var demoted = expected.Select(g => g.Clone()).ToList();
            demoted[1].Leader = false;
            Assert.IsFalse(DailyRules.SameGhosts(demoted, expected), "a leader passed off as an ordinary ghost");
        }

        [Test]
        public void OwnGhosts_KeepTheNewestTwenty()
        {
            var own = new List<GhostRecord>();
            for (int i = 1; i <= 25; i++) DailyRules.AddOwnGhost(own, Run(i, 100));
            Assert.AreEqual(DailyRules.OwnGhostCap, own.Count);
            Assert.AreEqual(6, own[0].RunIndex);
            Assert.AreEqual(25, own[19].RunIndex);
        }

        [Test]
        public void Passing_IsByDistance()
        {
            int[] leaders = { 900, 640, 812, 300 };
            Assert.AreEqual(2, DailyRules.Passed(leaders, 700));
            Assert.AreEqual(812, DailyRules.NextToBeat(leaders, 700));
            Assert.AreEqual(812, DailyRules.NextToBeat(leaders, 812), "a tie hasn't passed it, so it is still the one to beat");
            Assert.AreEqual(-1, DailyRules.NextToBeat(leaders, 901));
        }

        [Test]
        public void Days_CountOnce_AndStreaksNeedConsecutiveDays()
        {
            var d = new DailyProgress();
            DailyRules.NotePlayed(d, 100);
            DailyRules.NotePlayed(d, 100);
            DailyRules.NotePlayed(d, 101);
            DailyRules.NotePlayed(d, 102);
            Assert.AreEqual((3, 3), (d.DaysPlayed, d.Streak));
            DailyRules.NotePlayed(d, 105);
            Assert.AreEqual((4, 1), (d.DaysPlayed, d.Streak));
        }

        [Test]
        public void TheOnlineAchievements_AndTheElementalsStillNeedOnlyThe30()
        {
            var p = new Progress();
            for (int day = 1; day <= 7; day++) AchievementRules.OnDailyRunEnded(p, day, day == 3 ? 1 : 0);
            CollectionAssert.IsSubsetOf(new[] { AchievementIds.HauntedByStrangers, AchievementIds.DailyGrind, AchievementIds.PassingThrough }, p.Achievements);
            CollectionAssert.AreEqual(new[] { AchievementIds.TopTen }, AchievementRules.OnDailyDayClosed(p, 7));
            Assert.IsEmpty(AchievementRules.OnDailyDayClosed(new Progress(), 11));
            Assert.AreEqual(0, AchievementCatalog.SinglePlayerEarned(p.Achievements), "online extras never count for the Elementals");
        }

        [Test]
        public void ADailyRun_ReplaysFromItsPacket_WithItsLeaders()
        {
            var course = new Course(TestCourses.Content(), 20261014);
            var leaders = ParityRuns.Ghosts(course, 3, 7, Cfg).All.Select((g, i) => DailyRules.AsLeader(g, i + 1, "L" + i)).ToList();
            var own = ParityRuns.Ghosts(course, 2, 8, Cfg).All.ToList();
            int matched = 0;
            foreach (uint s in ParityRuns.Seeds(30, 9))
            {
                GhostSet ghosts = DailyRules.Ghosts(leaders, own);
                RunSimulation run = DailyRules.Start(course, ghosts, 3, Cfg);
                var rng = new Xorshift32(s | 1);
                while (!run.IsDead && run.Tick < 6000) run.Step(rng.NextInt(25) == 0, rng.NextInt(3) != 0);
                if (!run.IsDead) continue;
                Reel reel = ReelRules.From(run, "cv", "", false);
                Assert.IsNull(DailyRules.Check(reel));
                Assert.IsTrue(DailyRules.SameGhosts(reel.Ghosts, leaders.Concat(own).ToList()));
                Assert.IsTrue(ReelPacket.TryDecode(ReelPacket.Encode(reel), out Reel back));
                Assert.IsTrue(back.Options.Daily);
                Assert.AreEqual(3, back.Ghosts.Count(g => g.Leader));
                Assert.AreEqual("L0", back.Ghosts.First(g => g.Leader && g.RunIndex == DailyRules.LeaderRunIndexBase + 10).Name);
                Assert.AreEqual(run.Distance, RunReplay.Replay(back, course, Cfg).Distance);
                matched++;
            }
            Assert.Greater(matched, 20);
        }
    }
}
