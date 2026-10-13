using System.Collections.Generic;
using Echo.Core.Challenges;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>The highlights reel (a user revision): a saved run plays back exactly, and the reel holds five.</summary>
    public class ReelTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;

        // A run of random taps and finger lifts on the real content, against ghosts that are themselves random runs.
        static (RunSimulation run, List<(int x, int y)> trace) Play(Course course, GhostSet ghosts, uint seed, bool powerUps)
        {
            var rng = new Xorshift32(seed);
            var run = new RunSimulation(course, ghosts, ghosts.Count + 1, 100, Cfg, new RunOptions { Bounties = true });
            if (powerUps) run.GrantExtraLife();
            var trace = new List<(int, int)>();
            bool down = false;
            while (run.Tick < 30_000)
            {
                if (run.IsDead)
                {
                    if (powerUps && !run.ContinueUsed) run.Revive();
                    else break;
                }
                if (powerUps && run.Tick == 30) run.UseShield();
                if (powerUps && run.Tick == 70) run.UsePhase();
                bool tap = !down && rng.NextInt(30) == 0;
                if (tap) down = true;
                else if (down && rng.NextInt(12) == 0) down = false;
                run.Step(tap, down);
                trace.Add((run.Player.X, run.Player.Y));
            }
            return (run, trace);
        }

        static List<(int x, int y)> PlayBack(Reel reel, Course course, out RunSimulation run)
        {
            var player = new ReelPlayer(reel, course, Cfg);
            var trace = new List<(int, int)>();
            while (!player.Finished)
            {
                if (player.ContinueDue) player.Continue();
                player.Step();
                trace.Add((player.Run.Player.X, player.Run.Player.Y));
            }
            run = player.Run;
            return trace;
        }

        static GhostSet RandomGhosts(Course course, int count)
        {
            var set = new GhostSet();
            for (int i = 1; i <= count; i++)
            {
                var (ghostRun, _) = Play(course, new GhostSet(), (uint)(i * 7919), false);
                GhostRecord g = ghostRun.ToGhostRecord();
                g.RunIndex = i;
                g.Kills = i == 2 ? 3 : 0;
                set.Add(g);
            }
            return set;
        }

        [TestCase(11u, false)]
        [TestCase(12u, true)]
        [TestCase(13u, true)]
        public void APlayedBackReel_MatchesTheRunEveryTick(uint seed, bool powerUps)
        {
            var course = new Course(TestCourses.Content(), 4242);
            GhostSet ghosts = RandomGhosts(course, 4);
            var kills = new int[ghosts.Count];
            for (int i = 0; i < kills.Length; i++) kills[i] = ghosts[i].Kills;

            var (run, trace) = Play(course, ghosts, seed, powerUps);
            Reel reel = ReelRules.From(run, "cv", "today", false);
            for (int i = 0; i < kills.Length; i++)
                Assert.AreEqual(kills[i], reel.Ghosts[i].Kills, "the reel keeps the ghosts as they were at the start");

            List<(int x, int y)> again = PlayBack(reel, course, out RunSimulation replay);
            CollectionAssert.AreEqual(trace, again);
            Assert.AreEqual(run.DeathTick, replay.DeathTick);
            Assert.AreEqual(run.Score, replay.Score);
            Assert.AreEqual(run.NearMisses, replay.NearMisses);
            Assert.AreEqual(run.Cause, replay.Cause);
            Assert.AreEqual(run.ContinueUsed, replay.ContinueUsed);
            Assert.AreEqual(run.LivesUsed, replay.LivesUsed);

            // Playing it back never changes the reel, so it plays the same every time.
            CollectionAssert.AreEqual(trace, PlayBack(reel, course, out _));
        }

        [Test]
        public void ThePowerUpsAndContinue_AreRecordedOnTheirTicks()
        {
            var course = new Course(TestCourses.Content(), 4242);
            var (run, _) = Play(course, RandomGhosts(course, 2), 12u, true);
            Reel reel = ReelRules.From(run, "cv", "", false);
            Assert.AreEqual(30, reel.ShieldTick);
            Assert.AreEqual(70, reel.PhaseTick);
            Assert.IsTrue(reel.ExtraLife);
            Assert.AreEqual(run.ContinueUsed ? run.ContinueTick : -1, reel.ContinueTick);
        }

        [Test]
        public void TheReelHoldsFive_ThenOneIsReplaced()
        {
            var reels = new List<Reel>();
            for (int i = 0; i < ReelRules.Capacity; i++) Assert.IsTrue(ReelRules.Add(reels, new Reel { Score = i }));
            Assert.IsTrue(ReelRules.IsFull(reels));
            var sixth = new Reel { Score = 99 };
            Assert.IsFalse(ReelRules.Add(reels, sixth));
            Assert.AreEqual(ReelRules.Capacity, reels.Count);
            ReelRules.Replace(reels, 2, sixth);
            Assert.AreSame(sixth, reels[2]);
            Assert.AreEqual(ReelRules.Capacity, reels.Count);
        }

        [Test]
        public void OnlyFinishedRunsThatAreNotDuels_CanBeSaved()
        {
            var course = new Course(TestCourses.Content(), 4242);
            var alive = new RunSimulation(course, new GhostSet(), 1, 100, Cfg);
            Assert.IsFalse(ReelRules.CanSave(alive));
            var (dead, _) = Play(course, new GhostSet(), 11u, false);
            Assert.IsTrue(ReelRules.CanSave(dead));
            var duel = new RunSimulation(course, new GhostSet(), 1, 100, Cfg, new RunOptions { Duel = true });
            Assert.IsFalse(ReelRules.CanSave(duel));
        }

        [Test]
        public void AReel_SendsAsAChallengeOnItsOwnCourse()
        {
            var course = new Course(TestCourses.Content(), 4242);
            var (run, _) = Play(course, new GhostSet(), 11u, false);
            Reel reel = ReelRules.From(run, "cv1", "", true);
            Challenge c = ChallengeRuns.FromReel(reel);
            Assert.AreEqual(4242u, c.Seed);
            Assert.AreEqual("cv1", c.ContentVersion);
            Assert.AreEqual(run.Distance, c.Distance);
            CollectionAssert.AreEqual(run.ToGhostRecord().TapTicks, c.Record.TapTicks);
            Assert.IsTrue(ChallengeCode.TryDecode(ChallengeCode.Encode(c), out _));
        }
    }
}
