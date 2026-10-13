using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// The variable jump (user revision): rising with the finger lifted uses ReleaseGravity, so a quick tap is a
    /// short hop, a held tap the full jump as before, and anything in between lands in between.
    /// </summary>
    public class VariableJumpTests
    {
        static readonly SimConfig Cfg = SimConfig.Default.WithAirJumps(0);

        /// <summary>Jumps on tick 0, lifts the finger at <paramref name="releaseTick"/> (or never), returns the peak.</summary>
        static int Peak(int releaseTick)
        {
            var course = TestCourses.Flat();
            var s = RunnerState.AtStart();
            int peak = 0;
            for (int tick = 0; tick < 120; tick++)
            {
                bool held = releaseTick < 0 || tick < releaseTick;
                Runner.Step(ref s, tick == 0, course, tick, Cfg.RunSpeedBase, Cfg, held);
                if (s.Y > peak) peak = s.Y;
                if (tick > 0 && s.Grounded) break;
            }
            return peak;
        }

        [Test]
        public void AHeldJump_IsExactlyTheJumpItAlwaysWas()
        {
            var course = TestCourses.Flat();
            var a = RunnerState.AtStart();
            var b = RunnerState.AtStart();
            for (int tick = 0; tick < 60; tick++)
            {
                Runner.Step(ref a, tick == 0, course, tick, Cfg.RunSpeedBase, Cfg);
                Runner.Step(ref b, tick == 0, course, tick, Cfg.RunSpeedBase, Cfg, held: true);
                Assert.AreEqual(a.Hash(), b.Hash(), $"tick {tick}");
            }
        }

        [Test]
        public void AQuickTap_HopsAboutTwoThirdsAsHigh()
        {
            float ratio = Peak(0) / (float)Peak(-1);
            Assert.That(ratio, Is.InRange(0.58f, 0.70f));
        }

        // Device testing: a thumb is down for a few frames even on the quickest tap, and the hop chunks were only
        // passable with a one-frame lift. Any lift within HopHoldTicks now makes exactly the same hop.
        [Test]
        public void EveryQuickTap_HopsTheSameHeight()
        {
            int hop = Peak(0);
            for (int lift = 1; lift <= Cfg.HopHoldTicks; lift++) Assert.AreEqual(hop, Peak(lift), $"lift on tick {lift}");
            Assert.Greater(Peak(Cfg.HopHoldTicks + 3), hop, "holding longer still goes higher");
        }

        [Test]
        public void LiftingMidRise_LandsInBetween_AndAfterTheTopChangesNothing()
        {
            int hop = Peak(0), full = Peak(-1);
            int mid = Peak(8);
            Assert.Greater(mid, hop);
            Assert.Less(mid, full);
            Assert.AreEqual(full, Peak(30), "past the apex the finger no longer matters");
        }

        [Test]
        public void ARunWithHops_ReplaysExactlyAsAGhost()
        {
            Course course = TestCourses.Ledge();
            var cfg = SimConfig.Default;
            var run = new RunSimulation(course, TestRuns.Set(), 1, 100, cfg.WithLives(1_000_000, 0));
            // A quick hop on tick 2, then a jump held for 6 ticks from tick 24, then into the gap.
            while (!run.IsDead && run.Tick < 5000)
            {
                int t = run.Tick;
                run.Step(t == 2 || t == 24, held: t >= 24 && t < 30);
            }
            Assert.IsTrue(run.IsDead);
            GhostRecord record = run.ToGhostRecord();
            CollectionAssert.AreEqual(new[] { 2, 30 }, record.ReleaseTicks);

            var ghosts = new GhostManager();
            ghosts.Begin(new[] { record }, cfg, course, cfg.SpeedForPct(100));
            Assert.AreEqual(run.Player.X, ghosts.DeathX(0, course, cfg));
        }

        [Test]
        public void TheOldOneArgumentStep_HoldsEveryJump_AndRecordsNoRelease()
        {
            var run = new RunSimulation(TestCourses.Ledge(), TestRuns.Set(), 1, 100, SimConfig.Default);
            while (!run.IsDead && run.Tick < 5000) run.Step(run.Tick == 2);
            Assert.IsEmpty(run.ToGhostRecord().ReleaseTicks);
        }
    }
}
