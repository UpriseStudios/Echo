using Echo.Core.Courses;
using Echo.Core.Daily;
using Echo.Core.Ghosts;
using Echo.Core.Modes;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// One Life's Core rules (modes doc): the Daily Haunt's rules with no 1-UPs, only leader ghosts, run index 1; a run cut
    /// off by closing the game replays to the tick it was closed at; the new packet bits round-trip.
    /// </summary>
    public class OneLifeRulesTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;

        static RunSimulation Walk(RunSimulation run, int ticks)
        {
            for (int t = 0; t < ticks && !run.IsDead; t++) run.Step(false, false);
            return run;
        }

        [Test]
        public void OneLife_EarnsNo1Ups()
        {
            SimConfig generous = Cfg.WithLives(1, 3);
            var oneLife = new RunSimulation(TestCourses.Flat(), new GhostSet(), 1, 100, generous, new RunOptions { Daily = true, OneLife = true });
            var daily = new RunSimulation(TestCourses.Flat(), new GhostSet(), 1, 100, generous, new RunOptions { Daily = true });
            Walk(oneLife, 600);
            Walk(daily, 600);
            Assert.Greater(daily.LivesEarned, 0, "the Daily Haunt earns them");
            Assert.AreEqual(0, oneLife.LivesEarned, "one life");
        }

        [Test]
        public void TheChecks_AllowOnlyAOneLifeRun_AgainstLeaders()
        {
            RunSimulation sim = OneLifeRules.Start(TestCourses.Flat(), new GhostRecord[0], Cfg);
            Walk(sim, 300);
            Reel reel = ReelRules.Abandoned(sim, "v");
            Assert.IsNull(OneLifeRules.Check(reel));

            Reel daily = ReelRules.Abandoned(sim, "v");
            daily.Options = new RunOptions { Daily = true };
            Assert.AreEqual("options", OneLifeRules.Check(daily));
            Reel own = ReelRules.Abandoned(sim, "v");
            own.Ghosts.Add(new GhostRecord { RunIndex = 2, DeathTick = 100, TapTicks = new int[0] });
            Assert.AreEqual("ghosts", OneLifeRules.Check(own), "only today's leaders, never personal ghosts");
            Reel later = ReelRules.Abandoned(sim, "v");
            later.RunIndex = 2;
            Assert.AreEqual("run-index", OneLifeRules.Check(later));
            Reel continued = ReelRules.Abandoned(sim, "v");
            continued.ContinueTick = 40;
            Assert.AreEqual("power-ups", OneLifeRules.Check(continued));
        }

        [Test]
        public void AnAbandonedRun_ReplaysToWhereTheGameWasClosed_AndTheBitsRoundTrip()
        {
            Course course = TestCourses.Flat();
            RunSimulation sim = OneLifeRules.Start(course, new GhostRecord[0], Cfg);
            Walk(sim, 300);
            Reel reel = ReelRules.Abandoned(sim, "v");
            reel.Run.DeathTick = 120;   // as if the game had been closed two seconds in

            Assert.IsTrue(ReelPacket.TryDecode(ReelPacket.Encode(reel), out Reel back));
            Assert.IsTrue(back.Abandoned);
            Assert.IsTrue(back.Options.OneLife && back.Options.Daily);

            ReplayResult cut = RunReplay.Replay(back, course, Cfg);
            Assert.IsTrue(cut.Finished, "it counts as over where it stopped");
            RunSimulation twoSeconds = Walk(OneLifeRules.Start(course, new GhostRecord[0], Cfg), 120);
            Assert.AreEqual(twoSeconds.Distance, cut.Distance);
        }
    }
}
