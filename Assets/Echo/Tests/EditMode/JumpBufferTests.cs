using Echo.Core.Courses;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class JumpBufferTests
    {
        // The single jump: with an air jump available, an airborne tap jumps at once (DoubleJumpTests).
        static readonly SimConfig Cfg = SimConfig.Default.WithAirJumps(0);

        /// <summary>Jumps on tick 5 and returns the tick on which the runner lands (first grounded tick).</summary>
        static int LandingTick(Course course)
        {
            var s = RunnerState.AtStart();
            int tick = 0;
            TestCourses.Idle(ref s, course, ref tick, 5);
            TestCourses.StepOnce(ref s, true, course, ref tick);
            while (!s.Grounded) TestCourses.StepOnce(ref s, false, course, ref tick);
            return tick - 1;
        }

        /// <summary>Same jump, plus one extra tap on <paramref name="tapTick"/>; returns state after landing + 1.</summary>
        static RunnerState RunWithBufferedTap(Course course, int tapTick, int landing)
        {
            var s = RunnerState.AtStart();
            for (int tick = 0; tick <= landing + 1; tick++)
                Runner.Step(ref s, tick == 5 || tick == tapTick, course, tick, Cfg.RunSpeedBase, Cfg);
            return s;
        }

        // A tap counts on its own tick and the JumpBufferTicks - 1 ticks after it, so a tap up to
        // 4 ticks before landing fires on the first tick back on the ground.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void TapShortlyBeforeLanding_JumpsOnTheNextTick(int ticksBeforeLanding)
        {
            var course = TestCourses.Flat();
            int landing = LandingTick(course);
            var s = RunWithBufferedTap(course, landing - ticksBeforeLanding, landing);

            Assert.IsFalse(s.Grounded);
            Assert.AreEqual(Cfg.JumpVelocity - Cfg.Gravity, s.Vy);
        }

        [Test]
        public void TapTooEarlyBeforeLanding_IsDropped()
        {
            var course = TestCourses.Flat();
            int landing = LandingTick(course);
            var s = RunWithBufferedTap(course, landing - Cfg.JumpBufferTicks + 1, landing);

            Assert.IsTrue(s.Grounded);
            Assert.AreEqual(0, s.Vy);
        }

        [Test]
        public void SeveralTapsInTheAir_GiveOneJumpAfterLanding()
        {
            var course = TestCourses.Flat();
            int landing = LandingTick(course);
            var s = RunnerState.AtStart();
            int jumps = 0;
            for (int tick = 0; tick <= landing + 10; tick++)
            {
                bool tap = tick == 5 || (tick >= landing - 3 && tick <= landing);
                int vyBefore = s.Vy;
                Runner.Step(ref s, tap, course, tick, Cfg.RunSpeedBase, Cfg);
                if (s.Vy == Cfg.JumpVelocity - Cfg.Gravity && vyBefore != s.Vy) jumps++;
            }
            Assert.AreEqual(2, jumps);
        }
    }
}
