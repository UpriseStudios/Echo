using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// The gameplay revision: ghosts replay on a ¼-speed clock with a head start, so the player overtakes each
    /// at the chunk boundary just before its gravestone, up to two at a boundary: ghosts that have killed the
    /// player claim boundaries first (most recent killer first), then the rest newest first. A ghost with no
    /// boundary left near its grave is a harmless echo that falls onto its grave as the player arrives.
    /// On flat 10-unit chunks at 110 sub/tick, chunk 2 starts at x = 20000, reached on tick 182.
    /// </summary>
    public class EchoGhostTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;

        /// <summary>A ghost that walked the flat course and fell 2 units into chunk <paramref name="graveChunk"/>.</summary>
        static GhostRecord Walker(int runIndex, int graveChunk = 2) => TestRuns.Ghost(runIndex, (graveChunk * 10_000 + 2000) / 110);

        static RunSimulation OnFlat(params GhostRecord[] ghosts) =>
            new RunSimulation(TestCourses.Flat(), TestRuns.Set(ghosts), ghosts.Length + 1, 100, Cfg);

        [Test]
        public void AGhost_IsMetAtTheBoundaryJustBeforeItsGrave()
        {
            var run = OnFlat(Walker(1));
            ref readonly GhostReplay g = ref run.Ghosts.ReplayAt(0);
            Assert.AreEqual(182, g.MeetTick);
            Assert.AreEqual(182 - 182 / 4, g.HeadStart, "own ticks played before the run starts");
            Assert.Greater(g.State.X, run.Player.X, "the ghost starts ahead");
            Assert.IsFalse(g.Harmless);

            Assert.AreEqual(546, OnFlat(Walker(1, graveChunk: 6)).Ghosts.ReplayAt(0).MeetTick, "chunk 6 starts at 60000");
        }

        [Test]
        public void GhostRunsAtAQuarterSpeed_AndIsOvertakenAtTheMeetTick()
        {
            var run = OnFlat(Walker(1));
            int startX = run.Ghosts.ReplayAt(0).State.X;
            TestRuns.StepTo(run, 40, 0); // a jump keeps the player clear while we look
            Assert.AreEqual(startX + 10 * 110, run.Ghosts.ReplayAt(0).State.X, "40 player ticks = 10 ghost ticks");

        }

        [Test]
        public void TwoGhostsShareABoundary_TheNextMoveBack_AndTheRestAreEchoes()
        {
            // Five ghosts that all fell in chunk 4. Indices are oldest first, so newest first is 4, 3, 2, 1, 0.
            var run = OnFlat(Walker(1, 4), Walker(2, 4), Walker(3, 4), Walker(4, 4), Walker(5, 4));
            GhostManager g = run.Ghosts;
            Assert.AreEqual(364, g.ReplayAt(4).MeetTick);
            Assert.AreEqual(364, g.ReplayAt(3).MeetTick, "two can be overtaken together");
            // Chunk 3 is next to a full boundary, so the next two go back to chunk 2, still in sight of the grave.
            Assert.AreEqual(182, g.ReplayAt(2).MeetTick);
            Assert.AreEqual(182, g.ReplayAt(1).MeetTick);
            for (int i = 1; i <= 4; i++) Assert.IsFalse(g.ReplayAt(i).Harmless);
            Assert.IsTrue(g.ReplayAt(0).Harmless, "no boundary left near its grave");
        }

        [Test]
        public void EveryGraveYouReach_HasItsGhostNearIt()
        {
            // Twelve ghosts that fell all over the first ten chunks: each is met within two chunks of its grave
            // or arrives at it as an echo, so none is long gone or far away when the player gets there.
            var ghosts = new GhostRecord[12];
            for (int i = 0; i < ghosts.Length; i++) ghosts[i] = Walker(i + 1, graveChunk: i * 7 % 10);
            var run = OnFlat(ghosts);
            for (int i = 0; i < ghosts.Length; i++)
            {
                ref readonly GhostReplay r = ref run.Ghosts.ReplayAt(i);
                int graveX = run.Ghosts.DeathX(i, TestCourses.Flat(), Cfg);
                int meetX = r.MeetTick * 110;
                Assert.That(graveX - meetX, Is.InRange(0, 3 * 10_000), $"ghost {i}: met at {meetX}, grave at {graveX}");
            }
        }

        [Test]
        public void RunningIntoAGroundedGhost_Kills_JumpingOverIt_PassesWithANearMiss()
        {
            var stays = OnFlat(Walker(1));
            TestRuns.StepTo(stays, 1000);
            Assert.AreEqual(DeathCause.Ghost, stays.Cause);
            Assert.That(stays.DeathTick, Is.InRange(170, 182), "caught it just before the meet tick");

            // Jump on 170: from tick 172 the player is above the 640-high ghost until well after passing it.
            var jumps = OnFlat(Walker(1));
            TestRuns.StepTo(jumps, 1000, 170);
            Assert.IsFalse(jumps.IsDead);
            Assert.AreEqual(1, jumps.NearMisses);
            Assert.Less(jumps.Ghosts.ReplayAt(0).State.X, jumps.Player.X, "the ghost is behind after the pass");
        }

        [Test]
        public void AGhostThatFellBeforeChunk2_IsAHarmlessEcho_ThatFallsOnItsGraveAsYouArrive()
        {
            // Died on its own tick 51 (like a first-spike death), at x = 5610.
            var run = OnFlat(TestRuns.Ghost(1, deathTick: 51));
            int graveX = run.Ghosts.DeathX(0, TestCourses.Flat(), Cfg);
            Assert.IsTrue(run.Ghosts.ReplayAt(0).Harmless);
            Assert.AreEqual(GhostPhase.Replaying, run.Ghosts.ReplayAt(0).Phase, "still on its way to its grave");
            while (run.Ghosts.ReplayAt(0).Phase == GhostPhase.Replaying) run.Step(false);
            Assert.That(graveX - run.Player.X, Is.InRange(GhostManager.EchoLeadX - 1000, GhostManager.EchoLeadX + 500),
                "it falls just ahead of the player");
            TestRuns.StepTo(run, 1000);
            Assert.IsFalse(run.IsDead);
        }

        [Test]
        public void EchoReplay_VisitsExactlyTheOriginalStates_OnItsOwnClock()
        {
            // 76 units of floor, then a fall at about tick 707: long enough to outlive the 615-tick head start.
            Course LongLedge() => new Course(TestCourses.Library(
                @"{ ""id"": ""a"", ""tier"": 0, ""length"": 80000, ""gaps"": [{ ""x"": 76000, ""w"": 3000 }] }",
                TestCourses.FlatTier1), 1);
            var course = LongLedge();
            var original = new RunSimulation(course, new GhostSet(), 1, 100, Cfg);
            var states = new List<RunnerState>();
            int tick = 0;
            while (!original.IsDead && tick < 5000)
            {
                original.Step(tick == 100 || tick == 300 || tick == 650);
                states.Add(original.Player);
                tick++;
            }
            GhostRecord record = original.ToGhostRecord();

            var ghosts = new GhostManager();
            var replayCourse = LongLedge();
            ghosts.Begin(new[] { record }, Cfg, replayCourse, Cfg.RunSpeedBase);
            Assert.Less(ghosts.ReplayAt(0).HeadStart, record.DeathTick, "the comparison needs the ghost alive after its head start");
            for (int t = 0; ghosts.ReplayAt(0).Phase == GhostPhase.Replaying && t < 40_000; t++)
            {
                ghosts.Step(replayCourse, t, Cfg);
                int own = ghosts.ReplayAt(0).OwnTick;
                if (own == 0 || own > states.Count) continue;
                RunnerState g = ghosts.ReplayAt(0).State, p = states[own - 1];
                Assert.AreEqual(p.X, g.X, $"x at own tick {own - 1}");
                Assert.AreEqual(p.Y, g.Y, $"y at own tick {own - 1}");
            }
            Assert.AreNotEqual(GhostPhase.Replaying, ghosts.ReplayAt(0).Phase);
            Assert.AreEqual(states.Count, ghosts.ReplayAt(0).OwnTick, "dies on its own death tick");
        }

        [Test]
        public void LockstepConfig_KeepsTheOriginalTechSpecBehaviour()
        {
            var run = new RunSimulation(TestCourses.Flat(), TestRuns.Set(TestRuns.Walker(1)), 2, 100, Cfg.WithLockstepGhosts());
            Assert.AreEqual(0, run.Ghosts.ReplayAt(0).HeadStart);
            TestRuns.StepTo(run, 1000);
            Assert.AreEqual(Cfg.GhostGraceTicks, run.DeathTick);
        }

        [Test]
        public void GhostsThatKilledYou_AreMetFirst_MostRecentKillerFirst_ThenNewest()
        {
            var oldKiller = Walker(1);
            oldKiller.Kills = 1;
            oldKiller.LastKillRun = 3;
            var recentKiller = Walker(2);
            recentKiller.Kills = 1;
            recentKiller.LastKillRun = 5;
            var records = new[] { oldKiller, recentKiller, Walker(3), Walker(4) };

            // Indices are oldest first: 1 = recent killer, 0 = old killer, then 3 and 2 newest first.
            CollectionAssert.AreEqual(new[] { 1, 0, 3, 2 }, GhostManager.EncounterOrder(records));
            CollectionAssert.AreEqual(new[] { 3, 2, 1, 0 }, GhostManager.EncounterOrder(new[]
            {
                TestRuns.Walker(1), TestRuns.Walker(2), TestRuns.Walker(3), TestRuns.Walker(4),
            }), "with no kills the newest is met first, as before");

            // All four fell in chunk 2, which holds two: the killers claim it, the others arrive as echoes.
            var run = OnFlat(records);
            Assert.AreEqual(182, run.Ghosts.ReplayAt(1).MeetTick, "the most recent killer waits at chunk 2");
            Assert.AreEqual(182, run.Ghosts.ReplayAt(0).MeetTick);
            Assert.IsFalse(run.Ghosts.ReplayAt(1).Harmless);
            Assert.IsFalse(run.Ghosts.ReplayAt(0).Harmless);
            Assert.IsTrue(run.Ghosts.ReplayAt(3).Harmless);
            Assert.IsTrue(run.Ghosts.ReplayAt(2).Harmless);
        }

        // The reported bug: dying to a ghost let the new run's ghost take its slot at chunk 2, pushing the
        // killer out to chunk 4, so it never met the player again and no Nemesis could form.
        [Test]
        public void TheGhostThatKilledYou_IsWaitingAtTheSameSpot_SoANemesisForms()
        {
            Course course = TestCourses.Flat();
            var killer = Walker(1);
            var p = TestRuns.ProgressOnFlat(killer);
            p.Best = 1_000_000;
            p.NextRunIndex = 2;

            for (int attempt = 1; attempt <= 2; attempt++)
            {
                var run = RunRules.StartRun(p, course, Cfg);
                int meet = run.Ghosts.ReplayAt(0).MeetTick;
                Assert.AreEqual(182, meet, $"run {attempt}: the killer is met at chunk 2, before its grave");
                TestRuns.StepTo(run, 1000);
                Assert.AreSame(killer, run.Killer, $"run {attempt}");
                RunRules.Commit(p, run, p.Seed, Cfg);
                Assert.IsTrue(p.Ghosts.Contains(killer), "ghosts persist until a new best");
            }

            Assert.AreEqual(2, killer.Kills);
            Assert.AreSame(killer, p.Ghosts.Nemesis);
            Assert.AreEqual(3, p.Ghosts.Count, "the killer and both new runs");
        }

        // Device testing: after a 1-UP put the player back, overtakes landed in tunnels. Holding every ghost's clock
        // back by the running time the revive undid puts each ghost where it was when the player last stood there.
        [Test]
        public void AReviveHoldsTheGhostsBack_SoEveryOvertakeStaysWhereItWasProved()
        {
            Course course = TestCourses.Flat();
            var ghosts = new GhostManager();
            ghosts.Begin(new[] { Walker(1, 4), Walker(2, 6) }, Cfg, course, 110);
            var at60 = new int[2];
            for (int t = 0; t < 150; t++)
            {
                ghosts.Step(course, t, Cfg);
                if (t == 59) { at60[0] = ghosts.ReplayAt(0).State.X; at60[1] = ghosts.ReplayAt(1).State.X; }
            }
            int meetBefore = ghosts.ReplayAt(0).MeetTick;
            ghosts.HoldBack(90, 150, course, Cfg);
            Assert.AreEqual(at60[0], ghosts.ReplayAt(0).State.X);
            Assert.AreEqual(at60[1], ghosts.ReplayAt(1).State.X);
            Assert.AreEqual(meetBefore + 90, ghosts.ReplayAt(0).MeetTick);
        }
    }
}
