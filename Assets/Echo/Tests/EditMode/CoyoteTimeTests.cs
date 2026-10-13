using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class CoyoteTimeTests
    {
        // The single jump: with an air jump available, an airborne tap jumps at once (DoubleJumpTests).
        static readonly SimConfig Cfg = SimConfig.Default.WithAirJumps(0);

        /// <summary>Runs off the ledge and returns the state and tick right after the last grounded tick.</summary>
        static RunnerState RunToLedge(Echo.Core.Courses.Course course, out int tick)
        {
            var s = RunnerState.AtStart();
            tick = 0;
            var next = s;
            while (true)
            {
                Runner.Step(ref next, false, course, tick, Cfg.RunSpeedBase, Cfg);
                if (!next.Grounded) return s;
                s = next;
                tick++;
            }
        }

        // Taps on any of the CoyoteTicks ticks after the last grounded tick still jump.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void TapWithinCoyoteWindow_Jumps(int ticksAfterLastGrounded)
        {
            var course = TestCourses.Ledge();
            var s = RunToLedge(course, out int tick);
            int until = tick + ticksAfterLastGrounded - 1;
            TestCourses.Idle(ref s, course, ref tick, until, Cfg);

            TestCourses.StepOnce(ref s, true, course, ref tick, Cfg);
            Assert.AreEqual(Cfg.JumpVelocity - Cfg.Gravity, s.Vy);
        }

        [Test]
        public void TapAfterCoyoteWindow_DoesNotJump()
        {
            var course = TestCourses.Ledge();
            var s = RunToLedge(course, out int tick);
            TestCourses.Idle(ref s, course, ref tick, tick + Cfg.CoyoteTicks, Cfg);

            int vyBefore = s.Vy;
            TestCourses.StepOnce(ref s, true, course, ref tick, Cfg);
            Assert.AreEqual(vyBefore - Cfg.Gravity, s.Vy);
            Assert.Less(s.Vy, 0);
        }

        [Test]
        public void NoTap_FallsIntoGap_AndDiesBelowDeathFloor()
        {
            var course = TestCourses.Ledge();
            var s = RunnerState.AtStart();
            int tick = 0;
            while (s.Alive && tick < 300) TestCourses.StepOnce(ref s, false, course, ref tick, Cfg);

            Assert.IsFalse(s.Alive);
            Assert.Less(s.Y, Cfg.DeathFloorY);
            Assert.Less(s.LastSafeX, 3000, "last safe x is on the ledge");
        }
    }
}
