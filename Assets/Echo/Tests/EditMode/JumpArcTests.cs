using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class JumpArcTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;

        // Gravity is applied on the jump tick itself (tech spec 5, steps 3 then 5), so after k airborne
        // ticks y = k*J - G*k(k+1)/2. With J = 252 and G = 14 that peaks at 2142 on ticks 17-18 and lands
        // at exactly y = 0 on tick 35. Tech spec 4's "about 2.27 / 36 ticks" is the continuous v^2/2g estimate.
        const int ExpectedApex = 2142;
        const int ExpectedAirTicks = 35;

        [Test]
        public void Jump_FollowsDiscreteArc_ApexAndAirtime()
        {
            var course = TestCourses.Flat();
            var s = RunnerState.AtStart();
            int tick = 0;
            TestCourses.Idle(ref s, course, ref tick, 10);

            int startX = s.X;
            TestCourses.StepOnce(ref s, true, course, ref tick);
            Assert.IsFalse(s.Grounded);

            int apex = s.Y, air = 1;
            while (!s.Grounded)
            {
                int expectedY = 0;
                for (int k = 1; k <= air; k++) expectedY += Cfg.JumpVelocity - Cfg.Gravity * k;
                Assert.AreEqual(expectedY, s.Y, $"y after {air} airborne ticks");

                TestCourses.StepOnce(ref s, false, course, ref tick);
                if (!s.Grounded) air++;
                if (s.Y > apex) apex = s.Y;
                Assert.Less(air, 100, "never landed");
            }
            air++; // the landing tick is airborne movement too

            Assert.AreEqual(ExpectedApex, apex);
            Assert.AreEqual(ExpectedAirTicks, air);
            Assert.AreEqual(0, s.Y);
            Assert.AreEqual(0, s.Vy);
            Assert.AreEqual(ExpectedAirTicks * Cfg.RunSpeedBase, s.X - startX, "jump length");
            Assert.IsTrue(s.Alive);
        }

        [Test]
        public void TapWhileAirborne_WithoutAirJumps_DoesNotJumpAgain()
        {
            var course = TestCourses.Flat();
            var s = RunnerState.AtStart();
            int tick = 0;
            var single = Cfg.WithAirJumps(0);
            TestCourses.StepOnce(ref s, true, course, ref tick, single);
            TestCourses.Idle(ref s, course, ref tick, 15, single);

            int vyBefore = s.Vy;
            TestCourses.StepOnce(ref s, true, course, ref tick, single);
            Assert.AreEqual(vyBefore - Cfg.Gravity, s.Vy);
        }

        [Test]
        public void SpeedPct_RisesPerClear_CappedAt150()
        {
            Assert.AreEqual(100, Cfg.SpeedPctForClears(0));
            Assert.AreEqual(105, Cfg.SpeedPctForClears(1));
            Assert.AreEqual(150, Cfg.SpeedPctForClears(10));
            Assert.AreEqual(150, Cfg.SpeedPctForClears(50));
            Assert.AreEqual(110, Cfg.SpeedForPct(100));
            Assert.AreEqual(165, Cfg.SpeedForPct(150));
        }
    }
}
