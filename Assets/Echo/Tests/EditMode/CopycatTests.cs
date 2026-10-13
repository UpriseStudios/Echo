using System;
using System.Collections.Generic;
using System.Threading;
using Echo.Core.Courses;
using Echo.Core.Rules;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// The Copycat twist, redesigned head-on: a mirror of the player runs at them from ahead, copying their taps
    /// 0.5 s late, and is met on clear floor at a chunk boundary. On flat 10-unit chunks at 110 sub/tick the
    /// first one appears at tick 189 (past the band at the start of chunk 2) and meets the player at the start
    /// of chunk 3, x = 30000, on tick 273.
    /// </summary>
    public class CopycatTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;
        static readonly RunOptions Twisted = new RunOptions { Twist = Twist.Copycat };

        static RunSimulation OnFlat() => new RunSimulation(TestCourses.Flat(), TestRuns.Set(), 1, 100, Cfg, Twisted);

        /// <summary>Steps to <paramref name="until"/>, tapping (held for <paramref name="hold"/> ticks) on the given ticks.</summary>
        static void Run(RunSimulation run, int until, int hold, params int[] taps)
        {
            int lastTap = -1000;
            while (!run.IsDead && run.Tick < until)
            {
                bool tap = Array.IndexOf(taps, run.Tick) >= 0;
                if (tap) lastTap = run.Tick;
                run.Step(tap, run.Tick - lastTap < hold);
            }
        }

        [Test]
        public void TheFirstCopycat_IsMetAtTheStartOfChunk3()
        {
            var run = OnFlat();
            Assert.IsTrue(run.Copycat.Planned);
            Assert.AreEqual(3, run.Copycat.Chunk);
            Assert.AreEqual(189, run.Copycat.CopyFrom);
            Assert.AreEqual(273, run.Copycat.MeetTick);
            Assert.IsFalse(run.Copycat.Active, "off the course until it appears");

            Run(run, 190, 0);
            Assert.IsTrue(run.Copycat.Active);
            Assert.Greater(run.Copycat.X - run.Player.X, 9000, "it appears well off screen");
        }

        [Test]
        public void NotOnAClassicRun()
        {
            var run = new RunSimulation(TestCourses.Flat(), TestRuns.Set(), 1, 100, Cfg);
            Assert.IsFalse(run.Copycat.Planned);
            Run(run, 400, 0);
            Assert.IsFalse(run.IsDead);
        }

        [Test]
        public void StandingStill_ItRunsStraightIntoYou()
        {
            var run = OnFlat();
            Run(run, 400, 0);
            Assert.IsTrue(run.IsDead);
            Assert.AreEqual(DeathCause.Copycat, run.Cause);
            Assert.IsNull(run.Killer);
            Assert.That(run.DeathTick, Is.InRange(266, 273), "caught just before the meeting point");
            Assert.AreEqual(DeathCause.Copycat, run.ToGhostRecord().Cause);
        }

        [Test]
        public void JumpingEarly_ClearsIt_AndCountsAsANearMiss()
        {
            var run = OnFlat();
            Run(run, 300, 60, 261);
            Assert.IsFalse(run.IsDead);
            Assert.AreEqual(1, run.NearMisses);
            Assert.AreEqual(1, run.CopycatsPassed);
            Assert.Less(run.Copycat.X, run.Player.X, "behind you now");
            Run(run, 400, 60);
            Assert.IsFalse(run.IsDead);
            Assert.Greater(run.Copycat.Chunk, 3, "it left, and the next one is on its way");
        }

        [Test]
        public void ItCopiesYourTapsHalfASecondLate()
        {
            var run = OnFlat();
            Run(run, 195, 60);
            Run(run, 225, 60, 195); // jump at 195
            Assert.AreEqual(0, run.Copycat.Body.Y, "still on the ground 30 ticks later");
            Run(run, 226, 60);
            Assert.Greater(run.Copycat.Body.Y, 0, "jumps on tick 225");
        }

        [Test]
        public void TapsBeforeItAppears_AreNotCopied()
        {
            var run = OnFlat();
            Run(run, 230, 60, 180);
            Assert.IsTrue(run.Copycat.Active);
            Assert.AreEqual(0, run.Copycat.Body.Y);
        }

        [Test]
        public void AHopIsCopiedAsAHop()
        {
            int CopyPeak(int hold)
            {
                var run = OnFlat();
                Run(run, 195, hold);
                Run(run, 196, hold, 195);
                int peak = 0;
                while (!run.IsDead && run.Tick < 270)
                {
                    run.Step(false, run.Tick < 195 + hold);
                    peak = Math.Max(peak, run.Copycat.Body.Y);
                }
                return peak;
            }
            int hop = CopyPeak(1), full = CopyPeak(60);
            Assert.Greater(hop, 0);
            Assert.Less(hop, full * 3 / 4);
        }

        [Test]
        public void ARevive_SendsItAway_AndTheNextOneIsPlannedFurtherOn()
        {
            var run = OnFlat();
            Run(run, 400, 0);
            Assert.AreEqual(DeathCause.Copycat, run.Cause);
            run.Revive();
            Assert.IsFalse(run.Copycat.Active);
            Assert.IsTrue(run.Copycat.Planned);
            Assert.Greater(run.Copycat.Chunk, 3);
            Assert.Greater(run.Copycat.CopyFrom, run.Tick, "it appears after the revive, never on top of it");
        }

        [Test]
        public void CopycatsKeepComing_EveryFewChunks()
        {
            var run = OnFlat();
            var chunks = new List<int>();
            while (!run.IsDead && run.Tick < 3000)
            {
                CopycatState c = run.Copycat;
                if (c.Planned && (chunks.Count == 0 || chunks[chunks.Count - 1] != c.Chunk)) chunks.Add(c.Chunk);
                run.Step(c.Planned && run.Tick == c.MeetTick - 12);
            }
            Assert.IsFalse(run.IsDead);
            Assert.GreaterOrEqual(run.CopycatsPassed, 5);
            for (int i = 1; i < chunks.Count; i++) Assert.GreaterOrEqual(chunks[i] - chunks[i - 1], Copycat.EveryChunks);
        }

        // ---- Fairness: every scheduled copycat on real content is passable without the double jump ----

        [TestCase(100, 1)]
        [TestCase(150, 2)]
        public void EveryCopycat_IsPassable_OnRealCourses(int speedPct, int seed)
        {
            var lib = TestCourses.Content();
            var rng = new Random(seed);
            int speed = Cfg.SpeedForPct(speedPct);
            for (int trial = 0; trial < 4; trial++)
            {
                var course = new Course(lib, (uint)rng.Next(1, int.MaxValue));
                var floor = Copycat.PhantomFloor();
                var plans = new List<CopycatState>();
                int from = Copycat.FirstChunk, now = 0;
                while (plans.Count < 6 && CopycatPlanner.PlanFrom(course, floor, Cfg, speed, 0, now, from, null, out CopycatState plan))
                {
                    plans.Add(plan);
                    now = plan.EndTick;
                    from = plan.Chunk + Copycat.EveryChunks;
                }
                Assert.GreaterOrEqual(plans.Count, 4, "copycats keep coming");
                int horizon = plans[plans.Count - 1].EndTick;
                var solver = new Solver(course, floor, speed, plans, horizon);
                // The search recurses once per tick over the whole run, deeper than Unity's main-thread stack allows.
                bool solved = false;
                var thread = new Thread(() => solved = solver.Solve(RunnerState.AtStart(), 0, false, default, 0, 0), 256 * 1024 * 1024);
                thread.Start();
                thread.Join();
                Assert.IsTrue(solved, $"trial {trial} at {speedPct}%: stuck at tick {solver.Deepest}");
            }
        }

        /// <summary>
        /// An independent search over the whole run: ground jumps and hops only (no double jump), the copycat
        /// replaying the inputs from 30 ticks before, exactly as RunSimulation steps it.
        /// </summary>
        sealed class Solver
        {
            readonly Course _course, _floor;
            readonly int _speed, _until;
            readonly List<CopycatState> _plans;
            readonly HashSet<(long, long, long)> _dead = new HashSet<(long, long, long)>();
            readonly SimConfig _single = Cfg.WithAirJumps(0);
            public int Deepest;

            public Solver(Course course, Course floor, int speed, List<CopycatState> plans, int until)
            {
                _course = course;
                _floor = floor;
                _speed = speed;
                _plans = plans;
                _until = until;
            }

            CopycatState? At(int t)
            {
                foreach (CopycatState p in _plans)
                    if (t >= p.CopyFrom && t < p.EndTick) return p;
                return null;
            }

            static long Key(in RunnerState s) =>
                ((long)(s.Y + (1 << 19)) << 24) | ((long)(s.Vy + (1 << 11)) << 12) | ((long)s.Coyote << 8)
                | ((long)s.Buffer << 4) | ((long)(s.AirJumpsLeft & 3) << 2) | (s.Grounded ? 1L : 0L)
                | ((long)System.Math.Min(s.JumpAge, 7) << 44);

            public bool Solve(RunnerState s, int t, bool held, RunnerState cat, int taps, int downs)
            {
                if (!s.Alive) return false;
                if (t >= _until) return true;
                if (t > Deepest) Deepest = t;
                CopycatState? plan = At(t);
                if (plan.HasValue && t == plan.Value.CopyFrom) { cat = RunnerState.AtStart(); taps = 0; downs = 0; }
                var key = (((long)t << 49) | (Key(s) << 1) | (held ? 1L : 0L), ((long)taps << 32) | (uint)downs, Key(cat));
                if (_dead.Contains(key)) return false;

                for (int branch = 0; branch < JumpInputs.Count; branch++)
                {
                    if (!JumpInputs.Applies(branch, s, held)) continue;
                    bool tap = JumpInputs.Tap(branch), down = JumpInputs.Held(branch, held);
                    var next = s;
                    Runner.Step(ref next, tap, _course, t, _speed, _single, down);
                    if (!next.Alive) continue;
                    var nextCat = cat;
                    int nextTaps = taps, nextDowns = downs;
                    if (plan.HasValue)
                    {
                        Runner.Step(ref nextCat, (taps >> 29 & 1) != 0, _floor, t, _speed, Cfg, (downs >> 29 & 1) != 0);
                        nextTaps = ((taps << 1) | (tap ? 1 : 0)) & ((1 << 30) - 1);
                        nextDowns = ((downs << 1) | (down ? 1 : 0)) & ((1 << 30) - 1);
                        var box = Aabb.FromBottomCentre(plan.Value.StartX - nextCat.X, nextCat.Y, Cfg.GhostHitboxW, Cfg.GhostHitboxH);
                        if (Runner.PlayerBox(next, Cfg).Overlaps(box)) continue;
                    }
                    if (Solve(next, t + 1, down, nextCat, nextTaps, nextDowns)) return true;
                }
                _dead.Add(key);
                return false;
            }
        }
    }
}
