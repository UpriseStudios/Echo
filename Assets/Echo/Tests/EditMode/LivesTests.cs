using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// 1-UP (user revision): a life every LifeEveryScore points, at most MaxLives held, spent automatically
    /// on death as a recorded revive. Small milestones here so short test runs reach them; with no ghosts
    /// the score is the distance in metres.
    /// </summary>
    public class LivesTests
    {
        static RunSimulation Run(Course course, int every, int max) =>
            new RunSimulation(course, TestRuns.Set(), 1, 100, SimConfig.Default.WithLives(every, max));

        static void StepUntil(RunSimulation run, System.Func<bool> done, int limit = 20_000)
        {
            while (!done() && run.Tick < limit) run.Step(false);
            Assert.IsTrue(done(), "condition never reached");
        }

        [Test]
        public void ALifeIsEarnedAtTheMilestone()
        {
            var run = Run(TestCourses.Flat(), 20, 1);
            StepUntil(run, () => run.Score >= 19);
            Assert.AreEqual(0, run.Lives);
            StepUntil(run, () => run.Score >= 20);
            Assert.AreEqual(1, run.Lives);
            Assert.AreEqual(1, run.LivesEarned);
            Assert.AreEqual(40, run.NextLifeScore);
        }

        [Test]
        public void OnlyOneLifeIsHeld()
        {
            var run = Run(TestCourses.Flat(), 20, 1);
            StepUntil(run, () => run.Score >= 65);
            Assert.AreEqual(1, run.Lives);
            Assert.AreEqual(1, run.LivesEarned, "milestones at the cap pass without a life");
            Assert.AreEqual(80, run.NextLifeScore);
        }

        [Test]
        public void DeathWithALife_RevivesAutomatically_AndKeepsContinueAvailable()
        {
            // The ledge drops away at 3 m; a life at 2 m catches the first fall.
            var run = Run(TestCourses.Ledge(), 2, 1);
            StepUntil(run, () => run.LivesUsed == 1);
            Assert.IsFalse(run.IsDead, "revived in the same tick");
            Assert.AreEqual(0, run.Lives);
            Assert.IsTrue(run.Player.Grounded);
            Assert.AreEqual(SimConfig.Default.ReviveInvulnTicks, run.Player.Invuln);
            Assert.IsFalse(run.ContinueUsed);

            StepUntil(run, () => run.IsDead);
            Assert.AreEqual(DeathCause.Fall, run.Cause);
            Assert.IsTrue(RunRules.CanContinue(run), "the ad Continue is separate from lives");
        }

        [Test]
        public void ARunWithA1Up_ReplaysExactlyAsAGhost()
        {
            Course course = TestCourses.Ledge();
            var run = Run(course, 2, 1);
            StepUntil(run, () => run.IsDead);
            GhostRecord record = run.ToGhostRecord();
            Assert.AreEqual(1, record.ReviveTicks.Length);

            var ghosts = new GhostManager();
            ghosts.Begin(new[] { record }, SimConfig.Default, course, SimConfig.Default.SpeedForPct(100));
            Assert.AreEqual(run.Player.X, ghosts.DeathX(0, course, SimConfig.Default));
        }

        [Test]
        public void ACapOfZero_TurnsLivesOff()
        {
            var run = Run(TestCourses.Ledge(), 1, 0);
            StepUntil(run, () => run.IsDead);
            Assert.AreEqual(0, run.LivesEarned);
            Assert.AreEqual(0, run.LivesUsed);
        }

        [Test]
        public void TheDefaultMilestoneIs1000Points_HoldingOne()
        {
            Assert.AreEqual(1000, SimConfig.Default.LifeEveryScore);
            Assert.AreEqual(1, SimConfig.Default.MaxLives);
        }
    }
}
