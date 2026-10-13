using System;
using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Sim;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// The fairness guarantee behind the echo model: with ghosts from earlier runs that died anywhere, a
    /// survivable path always exists. Checked with an exhaustive search over the player's taps against the
    /// real GhostManager, at base and max speed. Lockstep ghosts failed this with a single ghost.
    /// </summary>
    public class EchoFairnessTests
    {
        static readonly SimConfig Cfg = SimConfig.Default;
        const int Horizon = 60 * 25;

        [TestCase(100, 10, 1)]
        [TestCase(100, 30, 2)]
        [TestCase(150, 10, 3)]
        [TestCase(150, 30, 4)]
        public void EveryRun_HasASurvivablePath(int speedPct, int ghosts, int seed)
        {
            var lib = TestCourses.Content();
            var rng = new Random(seed);
            int speed = Cfg.SpeedForPct(speedPct);
            for (int trial = 0; trial < 3; trial++)
            {
                uint courseSeed = (uint)rng.Next(1, int.MaxValue);
                var records = new List<GhostRecord>();
                for (int g = 0; g < ghosts; g++)
                {
                    int death = rng.Next(60, 3000);
                    var held = new bool[death + 5];
                    bool[] taps = Survive(new Course(lib, courseSeed), speed, death + 5, rng, held);
                    var tapTicks = new List<int>();
                    var releaseTicks = new List<int>();
                    for (int t = 0; t < death; t++)
                    {
                        if (taps[t]) tapTicks.Add(t);
                        if ((taps[t] || (t > 0 && held[t - 1])) && !held[t]) releaseTicks.Add(t);
                    }
                    records.Add(new GhostRecord { RunIndex = g + 1, TapTicks = tapTicks.ToArray(), ReleaseTicks = releaseTicks.ToArray(), DeathTick = death, SpeedPct = speedPct });
                }

                var course = new Course(lib, courseSeed);
                var manager = new GhostManager();
                manager.Begin(records, Cfg, course, speed);
                var boxes = new List<Aabb?[]>();
                for (int t = 0; t < Horizon; t++)
                {
                    manager.Step(course, t, Cfg);
                    var row = new Aabb?[ghosts];
                    for (int i = 0; i < ghosts; i++)
                        if (manager.IsLethal(i)) row[i] = Runner.GhostBox(manager.ReplayAt(i).State, Cfg);
                    boxes.Add(row);
                }

                Assert.IsTrue(new Solver(course, speed, boxes, Horizon, null, null, null).Solve(RunnerState.AtStart(), 0),
                    $"seed {courseSeed}: no survivable path with {ghosts} ghosts at {speedPct}%");
            }
        }

        /// <summary>A ghost run: random tapping, kept alive by rewriting the last stretch whenever it dies.</summary>
        static bool[] Survive(Course course, int speed, int ticks, Random rng, bool[] held)
        {
            var taps = new bool[ticks];
            for (int t = 0; t < ticks; t++)
            {
                taps[t] = rng.Next(30) == 0;
                held[t] = taps[t] || (t > 0 && held[t - 1] && rng.Next(10) != 0);
            }
            var states = new RunnerState[ticks + 1];
            var s = RunnerState.AtStart();
            states[0] = s;
            int tick = 0;
            while (tick < ticks)
            {
                Runner.Step(ref s, taps[tick], course, tick, speed, Cfg, held[tick]);
                states[++tick] = s;
                if (s.Alive) continue;
                bool fixedIt = false;
                foreach (int back in new[] { 60, 120, 240, 480 })
                {
                    int from = Math.Max(0, tick - 1 - back);
                    if (!new Solver(course, speed, null, Math.Min(ticks, tick + 60), taps, held, rng).Solve(states[from], from)) continue;
                    tick = from;
                    s = states[from];
                    fixedIt = true;
                    break;
                }
                if (!fixedIt) break;
            }
            return taps;
        }

        sealed class Solver
        {
            readonly Course _course;
            readonly int _speed, _until;
            readonly List<Aabb?[]> _ghosts;
            readonly bool[] _taps, _held;
            readonly Random _rng;
            readonly HashSet<long> _dead = new HashSet<long>();

            public Solver(Course course, int speed, List<Aabb?[]> ghosts, int until, bool[] taps, bool[] held, Random rng)
            {
                _held = held;
                _course = course;
                _speed = speed;
                _ghosts = ghosts;
                _until = until;
                _taps = taps;
                _rng = rng;
            }

            bool TouchesGhost(in RunnerState p, int t)
            {
                if (_ghosts == null || t < Cfg.GhostGraceTicks) return false;
                Aabb box = Runner.PlayerBox(p, Cfg);
                foreach (Aabb? g in _ghosts[t])
                    if (g.HasValue && box.Overlaps(g.Value)) return true;
                return false;
            }

            public bool Solve(RunnerState s, int t) => Solve(s, t, _held != null && t > 0 && _held[t - 1]);

            // With hops (JumpInputs), as the EncounterPlanner proves overtakes.
            bool Solve(RunnerState s, int t, bool heldBefore)
            {
                if (!s.Alive) return false;
                heldBefore = JumpInputs.Canonical(s, heldBefore);
                if (t >= _until) return true;
                long key = ((long)t << 47) | ((long)System.Math.Min(s.JumpAge, 7) << 44) | ((long)(s.Y + (1 << 19)) << 24) | ((long)(s.Vy + (1 << 11)) << 12)
                           | ((long)s.Coyote << 8) | ((long)s.Buffer << 4) | ((long)s.AirJumpsLeft << 2)
                           | (heldBefore ? 2L : 0L) | (s.Grounded ? 1L : 0L);
                if (_dead.Contains(key)) return false;
                int first = _rng != null && _rng.Next(4) == 0 ? 1 + _rng.Next(2) : 0;
                for (int i = 0; i < JumpInputs.Count; i++)
                {
                    int branch = (first + i) % JumpInputs.Count;
                    if (!JumpInputs.Applies(branch, s, heldBefore)) continue; // ground jumps only: an air press is the air jump
                    bool tap = JumpInputs.Tap(branch), down = JumpInputs.Held(branch, heldBefore);
                    var next = s;
                    Runner.Step(ref next, tap, _course, t, _speed, Cfg, down);
                    if (next.Alive && TouchesGhost(next, t)) continue;
                    if (_taps != null) _taps[t] = tap;
                    if (_held != null) _held[t] = down;
                    if (Solve(next, t + 1, down)) return true;
                }
                _dead.Add(key);
                return false;
            }
        }
    }
}
