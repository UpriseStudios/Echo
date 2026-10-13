using System;
using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Sim;

namespace Echo.Tests.PlayMode
{
    /// <summary>
    /// Builds a schedule that survives a given number of ticks on a course: random taps held for a random time,
    /// and on a death a memoised search (with hops, JumpInputs) rewrites the last stretch. Test support only.
    /// </summary>
    internal static class SurvivingSchedule
    {
        static readonly int[] RewindTicks = { 60, 120, 240, 480 };

        /// <param name="dies">Extra death rule checked after each step (tick, state), e.g. ghost contact; null for none.</param>
        public static Schedule Build(Course course, int speed, int ticks, Random rng, in SimConfig cfg,
                                     Func<int, RunnerState, bool> dies = null)
        {
            var schedule = new Schedule(ticks);
            bool[] taps = schedule.Taps, held = schedule.Held;
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
                // Only a tap presses (RunSimulation's rule), so a hold left over from before a rewrite cannot outlive a lift.
                held[tick] = held[tick] && (taps[tick] || (tick > 0 && held[tick - 1]));
                Runner.Step(ref s, taps[tick], course, tick, speed, cfg, held[tick]);
                if (s.Alive && dies != null && dies(tick, s)) s.Alive = false;
                states[++tick] = s;
                if (s.Alive) continue;

                bool solved = false;
                foreach (int back in RewindTicks)
                {
                    int from = Math.Max(0, tick - 1 - back);
                    if (!new Solver(course, cfg, speed, schedule, rng, Math.Min(ticks, tick + 60), dies).Solve(states[from], from)) continue;
                    tick = from;
                    s = states[from];
                    solved = true;
                    break;
                }
                if (!solved) throw new InvalidOperationException("no surviving schedule found");
            }
            return schedule;
        }

        sealed class Solver
        {
            readonly Course _course;
            readonly SimConfig _cfg;
            readonly int _speed, _until;
            readonly bool[] _taps, _held;
            readonly Random _rng;
            readonly Func<int, RunnerState, bool> _dies;
            readonly HashSet<long> _dead = new HashSet<long>();

            public Solver(Course course, in SimConfig cfg, int speed, Schedule schedule, Random rng, int until, Func<int, RunnerState, bool> dies)
            {
                _held = schedule.Held;
                _dies = dies;
                _course = course;
                _cfg = cfg;
                _speed = speed;
                _taps = schedule.Taps;
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
                int first = _rng.Next(4) == 0 ? 1 + _rng.Next(2) : 0;
                for (int i = 0; i < JumpInputs.Count; i++)
                {
                    int branch = (first + i) % JumpInputs.Count;
                    if (!JumpInputs.Applies(branch, s, heldBefore)) continue; // ground jumps only: an air press is the air jump
                    bool tap = JumpInputs.Tap(branch), down = JumpInputs.Held(branch, heldBefore);
                    var next = s;
                    Runner.Step(ref next, tap, _course, t, _speed, _cfg, down);
                    if (next.Alive && _dies != null && _dies(t, next)) next.Alive = false;
                    _taps[t] = tap;
                    _held[t] = down;
                    if (Solve(next, t + 1, down)) return true;
                }
                _dead.Add(key);
                return false;
            }
        }
    }
}
