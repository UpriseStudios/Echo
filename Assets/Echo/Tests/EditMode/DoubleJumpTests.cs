using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>The double jump (user revision of tech spec 5): one air jump per time in the air, ~80% height.</summary>
    public class DoubleJumpTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;

        /// <summary>Jumps on tick 0, then steps with no taps until <paramref name="until"/>.</summary>
        static RunnerState Airborne(Course course, ref int tick, int until)
        {
            var s = RunnerState.AtStart();
            TestCourses.StepOnce(ref s, true, course, ref tick, Cfg);
            while (tick < until) TestCourses.StepOnce(ref s, false, course, ref tick, Cfg);
            Assert.IsFalse(s.Grounded);
            return s;
        }

        [Test]
        public void TapInTheAir_JumpsAgainAtTheAirJumpVelocity()
        {
            var course = TestCourses.Flat();
            int tick = 0;
            var s = Airborne(course, ref tick, 15);
            Assert.AreEqual(1, s.AirJumpsLeft);

            TestCourses.StepOnce(ref s, true, course, ref tick, Cfg);
            Assert.AreEqual(Cfg.AirJumpVelocity - Cfg.Gravity, s.Vy);
            Assert.AreEqual(0, s.AirJumpsLeft);
        }

        [Test]
        public void SecondTapInTheAir_OnlyBuffers()
        {
            var course = TestCourses.Flat();
            int tick = 0;
            var s = Airborne(course, ref tick, 15);
            TestCourses.StepOnce(ref s, true, course, ref tick, Cfg);
            TestCourses.StepOnce(ref s, false, course, ref tick, Cfg);

            int vyBefore = s.Vy;
            TestCourses.StepOnce(ref s, true, course, ref tick, Cfg);
            Assert.AreEqual(vyBefore - Cfg.Gravity, s.Vy, "no third jump");
            Assert.Greater(s.Buffer, 0, "kept for a jump on landing");
        }

        [Test]
        public void Landing_RefillsTheAirJump()
        {
            var course = TestCourses.Flat();
            int tick = 0;
            var s = Airborne(course, ref tick, 15);
            TestCourses.StepOnce(ref s, true, course, ref tick, Cfg);
            while (!s.Grounded) TestCourses.StepOnce(ref s, false, course, ref tick, Cfg);
            TestCourses.StepOnce(ref s, false, course, ref tick, Cfg);
            Assert.AreEqual(Cfg.AirJumps, s.AirJumpsLeft);
        }

        [Test]
        public void TapInCoyoteTime_IsAGroundJump_AndKeepsTheAirJump()
        {
            var course = TestCourses.Ledge();
            var s = RunnerState.AtStart();
            int tick = 0;
            while (s.Grounded) TestCourses.StepOnce(ref s, false, course, ref tick, Cfg);

            TestCourses.StepOnce(ref s, true, course, ref tick, Cfg);
            Assert.AreEqual(Cfg.JumpVelocity - Cfg.Gravity, s.Vy);
            Assert.AreEqual(1, s.AirJumpsLeft);
        }

        [Test]
        public void AirJump_RisesAbout80PercentAsHighAsAGroundJump()
        {
            int groundRise = Rise(Cfg.JumpVelocity);
            int airRise = Rise(Cfg.AirJumpVelocity);
            Assert.That(airRise / (float)groundRise, Is.InRange(0.75f, 0.85f));
        }

        // Height gained from a launch at vy until the top of the arc, with this sim's gravity.
        static int Rise(int vy)
        {
            int y = 0;
            while (vy > 0)
            {
                vy -= Cfg.Gravity;
                y += vy;
            }
            return y;
        }

        [Test]
        public void ADoubleJumpingRun_ReplaysExactlyAsAGhost()
        {
            Course course = TestCourses.Ledge();
            var run = new RunSimulation(course, TestRuns.Set(), 1, 100, Cfg.WithLives(1_000_000, 0));
            // Jump, air-jump on the way up, then come down inside the 5-unit gap and fall.
            while (!run.IsDead && run.Tick < 5000) run.Step(run.Tick == 2 || run.Tick == 18);
            Assert.IsTrue(run.IsDead);

            var ghosts = new GhostManager();
            ghosts.Begin(new[] { run.ToGhostRecord() }, Cfg, course, Cfg.SpeedForPct(100));
            Assert.AreEqual(run.Player.X, ghosts.DeathX(0, course, Cfg));
        }
    }
}
