using System;
using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Sim;

namespace Echo.Core.Rules
{
    /// <summary>
    /// Schedules Copycat encounters so each one is always passable, the way EncounterPlanner does for ghosts.
    ///
    /// The copycat is met at the start of chunk c. It appears (off screen) once the player is past the standing band
    /// at the start of chunk c-1 and only copies taps from then on. From every standing entry in that band the
    /// planner searches the player's inputs (ground jumps and hops only, see JumpInputs) for a way to reach standing
    /// in the band at the end of chunk c without touching a hazard or the copycat. The copycat's moves follow from the
    /// player's last DelayTicks of inputs, so those are part of the search state. Boundaries next to a lethal ghost
    /// overtake are skipped, so each proof stands alone.
    /// </summary>
    public static class CopycatPlanner
    {
        const int MaxShift = 4;
        const int Attempts = 3;
        const int SlackTicks = 120;
        const int WindowMask = (1 << Copycat.DelayTicks) - 1;

        /// <summary>
        /// The next encounter at or after <paramref name="fromChunk"/>: the first passable boundary within MaxShift chunks,
        /// trying the next stretch when a whole stretch has none. False if none is found (the run simply has no copycat).
        /// </summary>
        /// <param name="offset">Player x minus tick * speed: 0 until a revive moves the player back.</param>
        /// <param name="nowTick">The next tick to be stepped; encounters must start after it.</param>
        /// <param name="ghosts">Lethal ghost overtakes to keep clear of, or null.</param>
        public static bool PlanFrom(Course course, Course floor, in SimConfig cfg, int speed, int offset, int nowTick,
                                    int fromChunk, GhostManager ghosts, out CopycatState plan)
        {
            for (int a = 0; a < Attempts; a++, fromChunk += MaxShift)
                if (TryPlan(course, floor, cfg, speed, offset, nowTick, fromChunk, ghosts, out plan)) return true;
            plan = default;
            return false;
        }

        static bool TryPlan(Course course, Course floor, in SimConfig cfg, int speed, int offset, int nowTick,
                                   int fromChunk, GhostManager ghosts, out CopycatState plan)
        {
            int band = ChunkValidator.BoundaryBand(cfg);
            for (int c = Math.Max(fromChunk, 2); c < fromChunk + MaxShift; c++)
            {
                int entryFrom = course.ChunkStartX(c - 1) - band;
                if (CeilDiv(entryFrom - offset, speed) <= nowTick) continue;
                if (ghosts != null && ghosts.LethalMeetNear(c, course, speed)) continue;

                plan = new CopycatState { Planned = true, Chunk = c };
                plan.CopyFrom = CeilDiv(entryFrom + 2 * band - offset, speed);
                plan.MeetTick = CeilDiv(course.ChunkStartX(c) - offset, speed);
                plan.EndTick = plan.MeetTick + Copycat.LingerTicks;
                plan.StartX = plan.MeetTick * speed + offset + (plan.MeetTick - plan.CopyFrom) * speed;
                plan.Body = RunnerState.AtStart();
                plan.PrevBody = plan.Body;
                if (Passable(course, floor, cfg, speed, offset, c, plan)) return true;
            }
            plan = default;
            return false;
        }

        static bool Passable(Course course, Course floor, in SimConfig cfg, int speed, int offset, int c, in CopycatState plan)
        {
            int band = ChunkValidator.BoundaryBand(cfg);
            int entryFrom = course.ChunkStartX(c - 1) - band;
            int exitFrom = course.ChunkStartX(c + 1) - band;
            int exitTo = exitFrom + 2 * band;
            int lastTick = CeilDiv(exitTo - offset, speed) + SlackTicks;

            // As in EncounterPlanner, the player's side is proved with the single jump; the copycat uses the real rules.
            SimConfig single = cfg.WithAirJumps(0);
            var search = new Search(course, floor, cfg, single, speed, exitFrom, exitTo, lastTick, plan);
            int firstTick = CeilDiv(entryFrom - offset, speed);
            var s = RunnerState.AtStart();
            s.X = firstTick * speed + offset;
            s.LastSafeX = s.X;
            for (int t = firstTick; s.X < entryFrom + 2 * band; t++)
            {
                if (!search.Clear(s, t, false, plan.Body, 0, 0)) return false;
                Runner.Step(ref s, false, course, t, speed, single);
                if (!s.Alive) return false;
            }
            return true;
        }

        static int CeilDiv(int a, int b) => a >= 0 ? (a + b - 1) / b : -(-a / b);

        readonly struct Key : IEquatable<Key>
        {
            readonly long _a, _b, _c;
            public Key(long a, long b, long c) { _a = a; _b = b; _c = c; }
            public bool Equals(Key o) => _a == o._a && _b == o._b && _c == o._c;
            public override bool Equals(object o) => o is Key k && Equals(k);
            public override int GetHashCode() => unchecked((int)(_a ^ (_a >> 32)) * 31 + (int)(_b ^ (_b >> 32)) * 17 + (int)(_c ^ (_c >> 32)));
        }

        sealed class Search
        {
            readonly Course _course, _floor;
            readonly SimConfig _cfg, _single;
            readonly int _speed, _exitFrom, _exitTo, _lastTick, _copyFrom, _startX;
            readonly Dictionary<Key, bool> _memo = new Dictionary<Key, bool>();

            public Search(Course course, Course floor, in SimConfig cfg, in SimConfig single, int speed, int exitFrom, int exitTo,
                          int lastTick, in CopycatState plan)
            {
                _course = course;
                _floor = floor;
                _cfg = cfg;
                _single = single;
                _speed = speed;
                _exitFrom = exitFrom;
                _exitTo = exitTo;
                _lastTick = lastTick;
                _copyFrom = plan.CopyFrom;
                _startX = plan.StartX;
            }

            static long RunnerKey(in RunnerState s) =>
                ((long)(s.Y + (1 << 19)) << 24) | ((long)(s.Vy + (1 << 11)) << 12) | ((long)s.Coyote << 8)
                | ((long)s.Buffer << 4) | ((long)(s.AirJumpsLeft & 3) << 2) | (s.Grounded ? 1L : 0L)
                | ((long)System.Math.Min(s.JumpAge, 7) << 44);

            /// <param name="taps">Bit i: the player tapped on tick t-1-i (only ticks from CopyFrom on).</param>
            /// <param name="downs">Bit i: the player's finger was down on tick t-1-i.</param>
            public bool Clear(RunnerState s, int t, bool held, RunnerState cat, int taps, int downs)
            {
                if (!s.Alive) return false;
                if (s.Grounded && s.X >= _exitFrom) return s.X < _exitTo;
                if (s.X >= _exitTo || t >= _lastTick) return false;

                // The finger is not canonicalised here: the copycat replays it, rising or not.
                var key = new Key(((long)t << 49) | (RunnerKey(s) << 1) | (held ? 1L : 0L),
                                  ((long)taps << 32) | (uint)downs, RunnerKey(cat));
                if (_memo.TryGetValue(key, out bool known)) return known;

                bool ok = false;
                for (int branch = 0; branch < JumpInputs.Count && !ok; branch++)
                {
                    if (!JumpInputs.Applies(branch, s, held)) continue;
                    bool tap = JumpInputs.Tap(branch), down = JumpInputs.Held(branch, held);
                    bool invulnerable = s.Invuln > 0;
                    var next = s;
                    Runner.Step(ref next, tap, _course, t, _speed, _single, down);
                    if (!next.Alive) continue;

                    var nextCat = cat;
                    int nextTaps = taps, nextDowns = downs;
                    if (t >= _copyFrom)
                    {
                        bool copyTap = (taps >> (Copycat.DelayTicks - 1) & 1) != 0;
                        bool copyDown = (downs >> (Copycat.DelayTicks - 1) & 1) != 0;
                        Runner.Step(ref nextCat, copyTap, _floor, t, _speed, _cfg, copyDown);
                        nextTaps = ((taps << 1) | (tap ? 1 : 0)) & WindowMask;
                        nextDowns = ((downs << 1) | (down ? 1 : 0)) & WindowMask;
                        if (!invulnerable && t >= _cfg.GhostGraceTicks && Runner.PlayerBox(next, _cfg).Overlaps(
                                Aabb.FromBottomCentre(_startX - nextCat.X, nextCat.Y, _cfg.GhostHitboxW, _cfg.GhostHitboxH)))
                            continue;
                    }
                    ok = Clear(next, t + 1, down, nextCat, nextTaps, nextDowns);
                }
                _memo[key] = ok;
                return ok;
            }
        }
    }
}
