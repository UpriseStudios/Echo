using Echo.Core.Courses;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class RunnerHazardTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;

        static Course Single(string body) => new Course(TestCourses.Library(
            @"{ ""id"": ""a"", ""tier"": 0, ""length"": 10000, " + body + " }", TestCourses.FlatTier1), 1);

        static RunnerState Run(Course course, int ticks, params int[] tapTicks)
        {
            var s = RunnerState.AtStart();
            for (int t = 0; t < ticks && s.Alive; t++)
                Runner.Step(ref s, System.Array.IndexOf(tapTicks, t) >= 0, course, t, Cfg.RunSpeedBase, Cfg);
            return s;
        }

        [Test]
        public void RunningIntoSpike_Kills()
        {
            var s = Run(Single(@"""spikes"": [{ ""x"": 5000, ""w"": 600, ""h"": 600 }]"), 200);
            Assert.IsFalse(s.Alive);
            Assert.AreEqual(0, s.Y);
            Assert.Greater(s.X + Cfg.PlayerHitboxW / 2, 5000);
        }

        [Test]
        public void JumpingOverSpike_Survives()
        {
            // Spike [5000, 5600). Jumping on tick 31 (x = 3410) keeps y >= 600 for arc ticks 3-32, which spans it.
            var s = Run(Single(@"""spikes"": [{ ""x"": 5000, ""w"": 600, ""h"": 600 }]"), 200, 3500 / 110);
            Assert.IsTrue(s.Alive);
        }

        [Test]
        public void JumpingUnderLowCeiling_Kills_RunningUnderIt_Survives()
        {
            var course = Single(@"""ceilings"": [{ ""x"": 3000, ""w"": 3000, ""y"": 1400 }]");
            Assert.IsTrue(Run(course, 200).Alive, "a grounded runner fits under y = 1400");
            Assert.IsFalse(Run(course, 200, 3500 / 110).Alive, "jumping under it hits it");
        }

        [Test]
        public void Invulnerability_IgnoresHazards_ThenCountsDown()
        {
            var course = Single(@"""spikes"": [{ ""x"": 2000, ""w"": 600, ""h"": 600 }]");
            var s = RunnerState.AtStart();
            s.Invuln = 1000;
            for (int t = 0; t < 40; t++) Runner.Step(ref s, false, course, t, Cfg.RunSpeedBase, Cfg);
            Assert.IsTrue(s.Alive);
            Assert.AreEqual(960, s.Invuln);
        }

        [Test]
        public void DeadRunner_DoesNotMove()
        {
            var course = TestCourses.Flat();
            var s = RunnerState.AtStart();
            s.Alive = false;
            Runner.Step(ref s, true, course, 0, Cfg.RunSpeedBase, Cfg);
            Assert.AreEqual(0, s.X);
            Assert.AreEqual(0, s.Vy);
        }

        [Test]
        public void Mover_FollowsIntegerTriangleWave()
        {
            var m = new Mover(0, 800, 800, 0, 2400, 120, 0);
            Assert.AreEqual(0, m.BottomAt(0));
            Assert.AreEqual(1200, m.BottomAt(30));
            Assert.AreEqual(2400, m.BottomAt(60));
            Assert.AreEqual(1200, m.BottomAt(90));
            Assert.AreEqual(0, m.BottomAt(120));
            Assert.AreEqual(40, m.BottomAt(1));

            var phased = new Mover(0, 800, 800, 100, 2500, 120, 30);
            Assert.AreEqual(1300, phased.BottomAt(0));
            Assert.AreEqual(2500, phased.BottomAt(30));
        }

        [Test]
        public void Mover_KillsOnlyWhenItsBoxReachesTheRunner()
        {
            // Mover at x [5000, 5800). The runner's box spans it on ticks 42-54 (x = 4730..6050).
            var low = Single(@"""movers"": [{ ""x"": 5000, ""w"": 800, ""h"": 800, ""yMin"": 0, ""yMax"": 2400, ""period"": 120, ""phase"": 60 }]");
            var high = Single(@"""movers"": [{ ""x"": 5000, ""w"": 800, ""h"": 800, ""yMin"": 0, ""yMax"": 2400, ""period"": 120, ""phase"": 0 }]");
            // phase 0: bottom passes 800 at tick 20 and stays above until tick 100, so the runner passes under.
            Assert.IsTrue(Run(high, 200).Alive);
            // phase 60: at tick 42 the bottom is at 720, below the top of the runner (800).
            Assert.IsFalse(Run(low, 200).Alive);
        }
    }
}
