using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class GhostReplayTests
    {
        static readonly SimConfig Cfg = TestRuns.Cfg;

        /// <summary>Plays a run, snapshotting the player after every tick. Revives once on the first death if asked.</summary>
        static List<RunnerState> Play(RunSimulation run, System.Func<int, bool> tapAt, bool reviveOnce, int maxTicks = 20_000)
        {
            var states = new List<RunnerState>();
            while (run.Tick < maxTicks)
            {
                run.Step(tapAt(run.Tick));
                states.Add(run.Player);
                if (!run.IsDead) continue;
                if (!reviveOnce || run.ContinueUsed) break;
                run.Revive();
            }
            Assert.IsTrue(run.IsDead, "test run never died");
            return states;
        }

        /// <summary>Replays the record alone and checks every tick against the original player states.</summary>
        static void AssertReplayMatches(GhostRecord record, Course course, List<RunnerState> original)
        {
            var ghosts = new GhostManager();
            ghosts.Begin(new[] { record }, Cfg);
            for (int tick = 0; tick < original.Count; tick++)
            {
                ghosts.Step(course, tick, Cfg);
                RunnerState g = ghosts.ReplayAt(0).State, p = original[tick];
                // Alive is excluded: a ghost can't be killed by another ghost, so it differs only on a ghost-kill tick.
                Assert.AreEqual(p.X, g.X, $"x at tick {tick}");
                Assert.AreEqual(p.Y, g.Y, $"y at tick {tick}");
                Assert.AreEqual(p.Vy, g.Vy, $"vy at tick {tick}");
                Assert.AreEqual(p.Grounded, g.Grounded, $"grounded at tick {tick}");
                Assert.AreEqual(p.LastSafeX, g.LastSafeX, $"lastSafeX at tick {tick}");
                Assert.AreEqual(p.Invuln, g.Invuln, $"invuln at tick {tick}");
                Assert.AreEqual(p.Coyote, g.Coyote, $"coyote at tick {tick}");
                Assert.AreEqual(p.Buffer, g.Buffer, $"buffer at tick {tick}");
                bool shouldReplay = tick < record.DeathTick;
                Assert.AreEqual(shouldReplay, ghosts.ReplayAt(0).Phase == GhostPhase.Replaying, $"phase at tick {tick}");
            }
            Assert.AreEqual(original.Count - 1, record.DeathTick, "ghost dies on the same tick as the run");
        }

        [Test]
        public void Replay_MatchesHazardDeath_EveryTick()
        {
            var course = new Course(TestCourses.Content(), 11);
            var run = new RunSimulation(course, new GhostSet(), 1, 100, Cfg);
            var states = Play(run, t => t % 37 == 20, reviveOnce: false);

            Assert.AreNotEqual(DeathCause.Ghost, run.Cause);
            AssertReplayMatches(run.ToGhostRecord(), new Course(TestCourses.Content(), 11), states);
        }

        [Test]
        public void Replay_MatchesRunWithHazardRevive_EveryTick()
        {
            var course = new Course(TestCourses.Content(), 11);
            var run = new RunSimulation(course, new GhostSet(), 1, 100, Cfg);
            var states = Play(run, t => t % 37 == 20, reviveOnce: true);

            GhostRecord record = run.ToGhostRecord();
            Assert.AreEqual(1, record.ReviveTicks.Length);
            AssertReplayMatches(record, new Course(TestCourses.Content(), 11), states);
        }

        [Test]
        public void Replay_MatchesRunRevivedAfterAGhostKill_EveryTick()
        {
            // Killed by the walker at tick 60, revived on 61, jumps on 100 and dies coming down onto it on 132 (k = 33).
            var run = TestRuns.OnFlat(TestRuns.Set(TestRuns.Walker()), runIndex: 2);
            var states = Play(run, t => t == 100, reviveOnce: true);

            GhostRecord record = run.ToGhostRecord();
            CollectionAssert.AreEqual(new[] { 61 }, record.ReviveTicks);
            CollectionAssert.AreEqual(new[] { 100 }, record.TapTicks);
            Assert.AreEqual(132, record.DeathTick);
            AssertReplayMatches(record, TestCourses.Flat(), states);
        }

        [Test]
        public void Replay_AtFasterSpeed_UsesTheRecordedSpeed()
        {
            var course = new Course(TestCourses.Content(), 4);
            var run = new RunSimulation(course, new GhostSet(), 1, 130, Cfg);
            var states = Play(run, t => t % 29 == 10, reviveOnce: false);
            GhostRecord record = run.ToGhostRecord();
            Assert.AreEqual(130, record.SpeedPct);
            AssertReplayMatches(record, new Course(TestCourses.Content(), 4), states);
        }

        [Test]
        public void Ghost_FadesFor18TicksAfterDeath_ThenIsGone()
        {
            var ghosts = new GhostManager();
            ghosts.Begin(new[] { TestRuns.Ghost(1, 50) }, Cfg);
            var course = TestCourses.Flat();
            for (int t = 0; t <= 50; t++) ghosts.Step(course, t, Cfg);
            Assert.AreEqual(GhostPhase.Fading, ghosts.ReplayAt(0).Phase);
            Assert.IsFalse(ghosts.IsActive(0));

            for (int t = 51; t < 50 + GhostManager.FadeTicks; t++) ghosts.Step(course, t, Cfg);
            Assert.AreEqual(GhostPhase.Fading, ghosts.ReplayAt(0).Phase);
            Assert.AreEqual(1, ghosts.VisibleCount);

            ghosts.Step(course, 50 + GhostManager.FadeTicks, Cfg);
            Assert.AreEqual(GhostPhase.Gone, ghosts.ReplayAt(0).Phase);
            Assert.AreEqual(0, ghosts.VisibleCount);
        }

        [Test]
        public void Ghosts_IgnoreEachOther()
        {
            var ghosts = new GhostManager();
            ghosts.Begin(new[] { TestRuns.Walker(1), TestRuns.Walker(2), TestRuns.Ghost(3, 100_000, 70) }, Cfg);
            var course = TestCourses.Flat();
            for (int t = 0; t < 300; t++) ghosts.Step(course, t, Cfg);
            for (int i = 0; i < 3; i++) Assert.IsTrue(ghosts.IsActive(i), $"ghost {i}");
        }

        [Test]
        public void Recorder_KeepsOneTapPerTick_AndTheFinalDeath()
        {
            var r = new RunRecorder();
            r.RecordTap(3);
            r.RecordTap(3);
            r.RecordTap(9);
            r.RecordDeath(40);
            r.RecordRevive(41);
            r.RecordDeath(80);
            GhostRecord g = r.ToRecord(7, 115);
            CollectionAssert.AreEqual(new[] { 3, 9 }, g.TapTicks);
            CollectionAssert.AreEqual(new[] { 41 }, g.ReviveTicks);
            Assert.AreEqual(80, g.DeathTick);
            Assert.AreEqual(7, g.RunIndex);
            Assert.AreEqual(115, g.SpeedPct);
            Assert.AreEqual(0, g.Kills);
        }

        [Test]
        public void RunWithNoTaps_StillBecomesAGhost()
        {
            var p = TestRuns.ProgressOnFlat(TestRuns.Walker());
            p.Best = 1_000_000;
            p.NextRunIndex = 2;
            var run = RunRules.StartRun(p, TestCourses.Flat(), Cfg);
            TestRuns.StepTo(run, 1000);
            CommitResult result = RunRules.Commit(p, run, 99, Cfg);

            Assert.IsNotNull(result.AddedGhost);
            Assert.IsEmpty(result.AddedGhost.TapTicks);
            Assert.AreEqual(2, p.Ghosts.Count);
        }
    }
}
