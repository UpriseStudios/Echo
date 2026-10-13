using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>Graves mark where each ghost's run ended, recovered from its inputs alone.</summary>
    public class GhostGraveTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;

        static (GhostRecord record, int deathX) PlayUntilDeath(Course course, System.Func<int, bool> tapAt)
        {
            var run = new RunSimulation(course, TestRuns.Set(), 1, 100, Cfg);
            while (!run.IsDead && run.Tick < 20_000) run.Step(tapAt(run.Tick));
            Assert.IsTrue(run.IsDead, "test run never died");
            return (run.ToGhostRecord(), run.Player.X);
        }

        [Test]
        public void DeathX_IsWhereTheRecordedRunEnded()
        {
            Course course = TestCourses.Ledge();
            var (record, deathX) = PlayUntilDeath(course, _ => false);

            var ghosts = new GhostManager();
            ghosts.Begin(new[] { record }, Cfg, course, Cfg.SpeedForPct(100));
            Assert.AreEqual(deathX, ghosts.DeathX(0, course, Cfg));
        }

        [Test]
        public void DeathX_FollowsTheTaps()
        {
            Course course = TestCourses.Ledge();
            // A jump near the ledge carries the runner further before it falls.
            var (record, deathX) = PlayUntilDeath(course, t => t == 20);

            var ghosts = new GhostManager();
            ghosts.Begin(new[] { record }, Cfg, course, Cfg.SpeedForPct(100));
            Assert.AreEqual(deathX, ghosts.DeathX(0, course, Cfg));
        }

        [Test]
        public void DeathX_DoesNotDisturbTheLiveReplay()
        {
            Course course = TestCourses.Flat();
            var ghosts = new GhostManager();
            ghosts.Begin(new[] { TestRuns.Ghost(1, 300) }, Cfg, course, Cfg.SpeedForPct(100));
            RunnerState before = ghosts.ReplayAt(0).State;
            int own = ghosts.ReplayAt(0).OwnTick;

            Assert.AreEqual(301 * Cfg.SpeedForPct(100), ghosts.DeathX(0, course, Cfg), "flat course: walked 301 ticks");
            Assert.AreEqual(before.X, ghosts.ReplayAt(0).State.X);
            Assert.AreEqual(own, ghosts.ReplayAt(0).OwnTick);
        }
    }
}
