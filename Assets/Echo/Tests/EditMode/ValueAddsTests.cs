using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Scoring;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>Value-adds doc, Phase C: Ghost Bounties, Weekly Twists, Rogues' Gallery, Nemesis Duel.</summary>
    public class ValueAddsTests
    {
        static readonly SimConfig Lockstep = TestRuns.Cfg;

        static Progress WithGhosts(int count, int best = 1_000_000)
        {
            var p = TestRuns.ProgressOnFlat();
            for (int i = 1; i <= count; i++) p.Ghosts.Add(TestRuns.Ghost(i, deathTick: 10));
            p.Best = best;
            p.NextRunIndex = count + 1;
            return p;
        }

        // ---- Ghost Bounties ----

        [Test]
        public void BountyNearMiss_ScoresFiftyInsteadOfFive()
        {
            Assert.AreEqual(100 + 5, ScoreRules.Score(100, 1, 0));
            Assert.AreEqual(100 + 50, ScoreRules.Score(100, 1, 0, bounties: 1));
        }

        [Test]
        public void ABounty_NeedsThreeGhosts_IsSeeded_AndIsNeverTheNemesis()
        {
            var two = WithGhosts(2);
            Assert.AreEqual(-1, RunRules.StartRun(two, TestCourses.Flat(), Lockstep, new RunOptions { Bounties = true }).BountyIndex);

            var p = WithGhosts(4);
            p.Ghosts[2].Kills = 3; // the Nemesis
            p.Ghosts[2].LastKillRun = 3;
            var options = new RunOptions { Bounties = true };
            var a = RunRules.StartRun(p, TestCourses.Flat(), Lockstep, options);
            var b = RunRules.StartRun(p, TestCourses.Flat(), Lockstep, options);
            Assert.GreaterOrEqual(a.BountyIndex, 0);
            Assert.AreEqual(a.BountyIndex, b.BountyIndex, "seeded from the course and run");
            Assert.AreNotSame(p.Ghosts.Nemesis, a.Ghosts.RecordAt(a.BountyIndex));
            Assert.AreEqual(-1, RunRules.StartRun(p, TestCourses.Flat(), Lockstep).BountyIndex, "off unless flagged");
        }

        // ---- Weekly Twists ----

        [Test]
        public void Weeks_StartOnMonday_AndTheRotationCyclesEveryTwist()
        {
            Assert.AreEqual(0, WeeklyTwists.WeekOf(4), "1970-01-05 was the first Monday");
            Assert.AreEqual(0, WeeklyTwists.WeekOf(10));
            Assert.AreEqual(1, WeeklyTwists.WeekOf(11));
            var seen = new HashSet<Twist>();
            for (int w = 0; w < 6; w++) seen.Add(WeeklyTwists.ForWeek(w));
            Assert.AreEqual(6, seen.Count);
            Assert.AreEqual(WeeklyTwists.ForWeek(3), WeeklyTwists.ForWeek(9));
            CollectionAssert.DoesNotContain(seen, Twist.None);
        }

        [Test]
        public void CrowdControl_KeepsGhostsHarmlessUntil300m()
        {
            // A lockstep walker normally kills the player as soon as the 1 s grace period ends.
            var classic = RunRules.StartRun(TestRuns.ProgressOnFlat(TestRuns.Walker(1)), TestCourses.Flat(), Lockstep);
            TestRuns.StepTo(classic, 200);
            Assert.IsTrue(classic.IsDead);

            var p = TestRuns.ProgressOnFlat(TestRuns.Walker(1));
            var twisted = RunRules.StartRun(p, TestCourses.Flat(), Lockstep, new RunOptions { Twist = Twist.CrowdControl });
            TestRuns.StepTo(twisted, 2700); // 297 m
            Assert.IsFalse(twisted.IsDead);
            TestRuns.StepTo(twisted, 2800);
            Assert.IsTrue(twisted.IsDead, "lethal from 300 m");
        }

        [Test]
        public void GiantGhosts_HaveHitboxesHalfAsBigAgain()
        {
            SimConfig giant = Lockstep.WithGhostScale(WeeklyTwists.GiantGhostPct);
            Assert.AreEqual(Lockstep.GhostHitboxW * 3 / 2, giant.GhostHitboxW);
            Assert.AreEqual(Lockstep.GhostHitboxH * 3 / 2, giant.GhostHitboxH);
            Assert.AreEqual(Lockstep.PlayerHitboxW, giant.PlayerHitboxW);
        }

        [Test]
        public void ATwistRun_HasItsOwnWeeklyBest_AndNeverClearsGhosts()
        {
            var p = WithGhosts(3, best: 1);
            var twist = new RunOptions { Twist = Twist.Mirror, TwistWeek = 7 };
            var run = RunRules.StartRun(p, TestCourses.Ledge(), Lockstep, twist);
            while (!run.IsDead) run.Step(false);
            CommitResult r = RunRules.Commit(p, run, 9, Lockstep);

            Assert.IsFalse(r.NewBest, "the classic best belongs to classic runs");
            Assert.IsTrue(r.NewTwistBest);
            Assert.AreEqual(1, p.Best);
            Assert.AreEqual(run.Score, p.TwistBest);
            Assert.AreEqual(4, p.Ghosts.Count, "ghosts stay, and the twist run adds one");

            var nextWeek = RunRules.StartRun(p, TestCourses.Ledge(), Lockstep, new RunOptions { Twist = Twist.Mirror, TwistWeek = 8 });
            Assert.AreEqual(0, RunRules.BestFor(nextWeek, p), "a new week starts a new board");
        }

        // ---- Rogues' Gallery ----

        [Test]
        public void TheGallery_KeepsMostKillsFirst_AndAtMostFifty()
        {
            var gallery = new List<GalleryEntry>();
            for (int i = 1; i <= 60; i++)
                RoguesGallery.Add(gallery, RoguesGallery.Entry(new GhostRecord { RunIndex = i, Kills = i % 7 + 2 }, 1, "v", "1 Jan 2026"));
            Assert.AreEqual(RoguesGallery.Cap, gallery.Count);
            for (int i = 1; i < gallery.Count; i++) Assert.GreaterOrEqual(gallery[i - 1].Kills, gallery[i].Kills);
            Assert.AreEqual(8, gallery[0].Kills);
        }

        [Test]
        public void ANewBestOverANemesis_ReportsItForTheGallery()
        {
            var p = WithGhosts(2, best: 0);
            p.Ghosts[0].Kills = 2;
            var run = RunRules.StartRun(p, TestCourses.Ledge(), Lockstep);
            GhostRecord nemesis = p.Ghosts.Nemesis;
            while (!run.IsDead) run.Step(false);
            CommitResult r = RunRules.Commit(p, run, 9, Lockstep);
            Assert.AreSame(nemesis, r.DefeatedNemesis);
        }

        // ---- Nemesis Duel ----

        static (Progress p, GhostRecord nemesis, RunSimulation deadRun) NemesisWithKills(int kills, int deathTick)
        {
            var nemesis = TestRuns.Ghost(1, deathTick);
            nemesis.Kills = kills;
            nemesis.LastKillRun = 1;
            var p = TestRuns.ProgressOnFlat(nemesis);
            p.Best = 1_000_000;
            p.NextRunIndex = 2;
            var run = RunRules.StartRun(p, TestCourses.Ledge(), Lockstep);
            while (!run.IsDead) run.Step(false);
            return (p, nemesis, run);
        }

        [Test]
        public void TheDuel_IsOfferedAtFiveKills_OncePerCycle()
        {
            var (four, _, run4) = NemesisWithKills(4, 100_000);
            Assert.IsFalse(RunRules.CanDuel(run4, four));

            var (p, _, run) = NemesisWithKills(5, 100_000);
            Assert.IsTrue(RunRules.CanDuel(run, p));
            RunRules.Commit(p, run, 9, Lockstep);
            RunRules.StartDuel(p, TestCourses.Flat(), Lockstep);
            Assert.IsTrue(p.DuelUsed);
            var (_, _, again) = NemesisWithKills(5, 100_000);
            Assert.IsFalse(RunRules.CanDuel(again, p), "used up until the next new best");
        }

        [Test]
        public void GettingPastWhereTheNemesisFell_BanishesItForFree()
        {
            var (p, nemesis, run) = NemesisWithKills(5, deathTick: 10);
            RunRules.Commit(p, run, 9, Lockstep);
            var duel = RunRules.StartDuel(p, TestCourses.Flat(), Lockstep);
            Assert.AreEqual(1, duel.Ghosts.Count, "only the Nemesis");
            while (!duel.DuelWon && !duel.IsDead && duel.Tick < 1000) duel.Step(false);
            Assert.IsTrue(duel.DuelWon);

            CommitResult r = RunRules.CommitDuel(p, duel, nemesis);
            Assert.IsTrue(r.DuelWon);
            Assert.IsFalse(p.Ghosts.Contains(nemesis));
            Assert.AreSame(nemesis, r.DefeatedNemesis);
        }

        [Test]
        public void LosingTheDuel_GivesTheNemesisExactlyOneMoreKill_AndRecordsNoGhost()
        {
            var (p, nemesis, run) = NemesisWithKills(5, 100_000);
            RunRules.Commit(p, run, 9, Lockstep);
            int ghosts = p.Ghosts.Count;
            var duel = RunRules.StartDuel(p, TestCourses.Flat(), Lockstep);
            TestRuns.StepTo(duel, 1000); // the lockstep walker kills at the end of the grace period
            Assert.IsTrue(duel.IsDead);
            Assert.AreSame(nemesis, duel.Killer);

            RunRules.CommitDuel(p, duel, nemesis);
            Assert.AreEqual(6, nemesis.Kills);
            Assert.AreEqual(ghosts, p.Ghosts.Count);
        }
    }
}
