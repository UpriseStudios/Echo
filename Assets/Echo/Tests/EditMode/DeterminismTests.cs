using System;
using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    public class DeterminismTests
    {
        const int Runs = 500;
        const int MaxTicks = 60 * 150;
        static readonly int[] RewindTicks = { 60, 120, 240, 480 };

        // tech spec 17: 500 random tap schedules on random seeds, each simulated twice,
        // with a state hash compared every tick. Each copy builds its own lazily generated course.
        // Pure random tapping rarely leaves the tutorial, so every second schedule is kept alive: on a
        // death it rewinds and a small solver rewrites the taps just far enough to survive, then random
        // tapping resumes. Those runs cover every tier at every speed.
        [Test]
        public void RandomRuns_SimulatedTwice_MatchEveryTick()
        {
            var cfg = SimConfig.Default;
            var library = TestCourses.Content();
            var rng = new Random(20261003); // test-side only; Core never sees System.Random
            var hashes = new uint[MaxTicks];
            int furthestX = 0, survivorsDied = 0;

            for (int run = 0; run < Runs; run++)
            {
                uint seed = (uint)rng.Next() ^ ((uint)rng.Next() << 16);
                int speed = cfg.SpeedForPct(cfg.SpeedPctForClears(rng.Next(0, 12)));
                bool survivor = run % 2 == 1;
                bool[] taps = RandomTaps(rng, rng.Next(4, 40));
                bool[] held = RandomHolds(rng, taps);
                if (survivor) KeepAlive(taps, held, rng, new Course(library, seed), speed, cfg);

                int deathA = Simulate(new Course(library, seed), taps, held, speed, cfg, hashes, true, out int xA);
                int deathB = Simulate(new Course(library, seed), taps, held, speed, cfg, hashes, false, out int xB);

                Assert.AreEqual(deathA, deathB, $"run {run} seed {seed}: death tick differs");
                Assert.AreEqual(xA, xB);
                furthestX = Math.Max(furthestX, xA);
                if (survivor && deathA < MaxTicks) survivorsDied++;
            }

            // Every chunk joins any other at every speed (ChunkValidator), so a survivable path always exists.
            Assert.AreEqual(0, survivorsDied, "a kept-alive run died: some chunk sequence is unclearable");
            Assert.GreaterOrEqual(furthestX, 700_000, "no run reached tier 4 (700 m)");
        }

        static bool[] RandomTaps(Random rng, int oneIn)
        {
            var taps = new bool[MaxTicks];
            for (int t = 0; t < MaxTicks; t++) taps[t] = rng.Next(oneIn) == 0;
            return taps;
        }

        /// <summary>The finger goes down on each tap and lifts after a random time, so hops and full jumps both occur.</summary>
        static bool[] RandomHolds(Random rng, bool[] taps)
        {
            var held = new bool[MaxTicks];
            for (int t = 0; t < MaxTicks; t++) held[t] = taps[t] || (t > 0 && held[t - 1] && rng.Next(10) != 0);
            return held;
        }

        static void KeepAlive(bool[] taps, bool[] held, Random rng, Course course, int speed, in SimConfig cfg)
        {
            var states = new RunnerState[MaxTicks + 1];
            var s = RunnerState.AtStart();
            states[0] = s;
            int tick = 0;
            while (tick < MaxTicks)
            {
                Runner.Step(ref s, taps[tick], course, tick, speed, cfg, held[tick]);
                states[++tick] = s;
                if (s.Alive) continue;

                bool solved = false;
                foreach (int back in RewindTicks)
                {
                    int from = Math.Max(0, tick - 1 - back);
                    var solver = new Solver(course, cfg, speed, taps, held, rng, Math.Min(MaxTicks, tick + 60));
                    if (!solver.Solve(states[from], from)) continue;
                    tick = from;
                    s = states[from];
                    solved = true;
                    break;
                }
                if (!solved) return;
            }
        }

        /// <summary>Finds taps from a state that keep the runner alive until a given tick, branch order randomised.</summary>
        sealed class Solver
        {
            readonly Course _course;
            readonly SimConfig _cfg;
            readonly int _speed, _until;
            readonly bool[] _taps, _held;
            readonly Random _rng;
            readonly HashSet<long> _dead = new HashSet<long>();

            public Solver(Course course, in SimConfig cfg, int speed, bool[] taps, bool[] held, Random rng, int until)
            {
                _held = held;
                _course = course;
                _cfg = cfg;
                _speed = speed;
                _taps = taps;
                _rng = rng;
                _until = until;
            }

            public bool Solve(RunnerState s, int t) => Solve(s, t, t > 0 && _held[t - 1]);

            bool Solve(RunnerState s, int t, bool heldBefore)
            {
                if (!s.Alive) return false;
                heldBefore = JumpInputs.Canonical(s, heldBefore);
                if (t >= _until) return true;
                long key = ((long)t << 47) | ((long)System.Math.Min(s.JumpAge, 7) << 44) | ((long)(s.Y + (1 << 19)) << 24) | ((long)(s.Vy + (1 << 11)) << 12)
                           | ((long)s.Coyote << 8) | ((long)s.Buffer << 4) | ((long)s.AirJumpsLeft << 2)
                           | (heldBefore ? 2L : 0L) | (s.Grounded ? 1L : 0L);
                if (_dead.Contains(key)) return false;

                // JumpInputs: no tap, tap and hold, tap and lift (a hop). Usually try no tap first.
                int first = _rng.Next(4) == 0 ? 1 + _rng.Next(2) : 0;
                for (int i = 0; i < JumpInputs.Count; i++)
                {
                    int branch = (first + i) % JumpInputs.Count;
                    if (!JumpInputs.Applies(branch, s, heldBefore)) continue; // ground jumps only: an air press is the air jump
                    bool tap = JumpInputs.Tap(branch), down = JumpInputs.Held(branch, heldBefore);
                    var next = s;
                    Runner.Step(ref next, tap, _course, t, _speed, _cfg, down);
                    _taps[t] = tap;
                    _held[t] = down;
                    if (Solve(next, t + 1, down)) return true;
                }
                _dead.Add(key);
                return false;
            }
        }

        static int Simulate(Course course, bool[] taps, bool[] held, int speed, in SimConfig cfg, uint[] hashes, bool record, out int x)
        {
            var s = RunnerState.AtStart();
            int tick = 0;
            for (; tick < MaxTicks && s.Alive; tick++)
            {
                Runner.Step(ref s, taps[tick], course, tick, speed, cfg, held[tick]);
                uint h = s.Hash();
                if (record) hashes[tick] = h;
                else if (hashes[tick] != h) Assert.Fail($"seed {course.Seed}: state hash differs at tick {tick}");
            }
            x = s.X;
            return tick;
        }

        [Test]
        public void StateHash_ChangesWithEveryField()
        {
            var a = RunnerState.AtStart();
            uint h = a.Hash();
            var b = a; b.X = 1; Assert.AreNotEqual(h, b.Hash());
            b = a; b.Y = 1; Assert.AreNotEqual(h, b.Hash());
            b = a; b.Vy = 1; Assert.AreNotEqual(h, b.Hash());
            b = a; b.LastSafeX = 1; Assert.AreNotEqual(h, b.Hash());
            b = a; b.Coyote = 1; Assert.AreNotEqual(h, b.Hash());
            b = a; b.Buffer = 1; Assert.AreNotEqual(h, b.Hash());
            b = a; b.Invuln = 1; Assert.AreNotEqual(h, b.Hash());
            b = a; b.Grounded = false; Assert.AreNotEqual(h, b.Hash());
            b = a; b.Alive = false; Assert.AreNotEqual(h, b.Hash());
        }
    }
}
