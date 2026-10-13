using System;
using System.Collections.Generic;
using Echo.Core.Sim;

namespace Echo.Core.Courses
{
    /// <summary>
    /// Proves a chunk is clearable (tech spec 6) with a memoised depth-first search over
    /// (tick, y, vy, grounded, coyote, buffer, held, air jumps), trying every input on every tick (see JumpInputs)
    /// with the real Runner.Step, short hops included.
    ///
    /// Contract, which is what makes chunks join in any order: from standing anywhere in the boundary
    /// band [-B, B) — clear floor either side of the chunk start, B = edge clearance minus half the hitbox —
    /// the runner can reach standing in the band [L - B, L + B) around the chunk end, which is the next
    /// chunk's entry band. A jump over the last obstacle may land in the next chunk's clear floor, but no further.
    ///
    /// Checked at every reachable run speed, every entry position (for movers, a sample of positions),
    /// and for movers every entry phase of their combined period.
    /// </summary>
    public static class ChunkValidator
    {
        const int MaxPhases = 4096;
        const int AirborneSlackTicks = 120;
        const int MoverEntrySamples = 5;

        static readonly Chunk Filler = new Chunk("~validator_flat", 1, 10000, null, null, null, null);

        public static int BoundaryBand(in SimConfig cfg) => ChunkParser.EdgeClearance - cfg.PlayerHitboxW / 2;

        public static ChunkValidationResult Validate(Chunk chunk, in SimConfig cfg)
        {
            // The chunk goes first as the only "tutorial" chunk, so it sits at x = 0 with plain floor behind it.
            var asFirst = new Chunk(chunk.Id, 0, chunk.Length, chunk.Gaps, chunk.Spikes, chunk.Ceilings, chunk.Movers);
            var course = new Course(new ChunkLibrary(new[] { asFirst, Filler }), 1);

            int band = BoundaryBand(cfg);
            int phases = PhaseCount(chunk);
            bool movers = chunk.Movers.Length > 0;
            int explored = 0;

            foreach (int speed in ReachableSpeeds(cfg))
            {
                // Every entry x in [-band, band) lies on one of `speed` run-in lines, x = -band + r + j * speed.
                // One search per line checks every point on it, sharing the memo.
                foreach (int r in RunInLines(speed, movers))
                {
                    for (int phase = 0; phase < phases; phase++)
                    {
                        int startX = -band + r;
                        var search = new Search(course, cfg, speed, phase, startX, chunk.Length - band, chunk.Length + band,
                            (chunk.Length + 2 * band) / speed + AirborneSlackTicks);
                        var s = RunnerState.AtStart();
                        s.X = startX;
                        s.LastSafeX = startX;
                        for (int j = 0; s.X < band; j++)
                        {
                            bool ok = search.Clear(s, j);
                            if (!ok)
                            {
                                explored += search.Explored;
                                return new ChunkValidationResult(false, speed, phase, s.X, explored);
                            }
                            Runner.Step(ref s, false, course, phase + j, speed, cfg);
                        }
                        explored += search.Explored;
                    }
                }
            }
            return new ChunkValidationResult(true, 0, 0, 0, explored);
        }

        /// <summary>
        /// Every run speed a player can reach, one per clear count up to the cap. The spec asks for base and max
        /// only, but a chunk can pass both and still fail in between (it happened at 110%), so all are checked.
        /// </summary>
        public static List<int> ReachableSpeeds(in SimConfig cfg)
        {
            var speeds = new List<int>();
            for (int clears = 0; ; clears++)
            {
                int pct = cfg.SpeedPctForClears(clears);
                int speed = cfg.SpeedForPct(pct);
                if (!speeds.Contains(speed)) speeds.Add(speed);
                if (pct >= cfg.MaxSpeedPct || clears > 1000) return speeds;
            }
        }

        /// <summary>Run-in lines to check: all of them, or evenly spaced samples for mover chunks.</summary>
        public static List<int> RunInLines(int speed, bool sampleOnly)
        {
            var lines = new List<int>();
            if (!sampleOnly)
            {
                for (int r = 0; r < speed; r++) lines.Add(r);
                return lines;
            }
            for (int i = 0; i < MoverEntrySamples; i++)
            {
                int r = (speed - 1) * i / (MoverEntrySamples - 1);
                if (!lines.Contains(r)) lines.Add(r);
            }
            return lines;
        }

        /// <summary>Least common multiple of all mover periods, or 1 without movers.</summary>
        public static int PhaseCount(Chunk chunk)
        {
            long lcm = 1;
            foreach (var m in chunk.Movers)
            {
                lcm = lcm / Gcd(lcm, m.Period) * m.Period;
                if (lcm > MaxPhases) throw new ChunkFormatException($"chunk \"{chunk.Id}\": mover periods combine to more than {MaxPhases} phases");
            }
            return (int)lcm;
        }

        static long Gcd(long a, long b)
        {
            while (b != 0) { long t = a % b; a = b; b = t; }
            return a;
        }

        sealed class Search
        {
            readonly Course _course;
            readonly SimConfig _cfg;
            readonly int _speed, _phase, _startX, _exitFrom, _exitTo, _maxTicks;
            readonly Dictionary<long, bool> _memo = new Dictionary<long, bool>();

            public int Explored;

            public Search(Course course, in SimConfig cfg, int speed, int phase, int startX, int exitFrom, int exitTo, int maxTicks)
            {
                _course = course;
                _cfg = cfg;
                _speed = speed;
                _phase = phase;
                _startX = startX;
                _exitFrom = exitFrom;
                _exitTo = exitTo;
                _maxTicks = maxTicks;
            }

            public bool Clear(RunnerState s, int k, bool held = false)
            {
                if (!s.Alive) return false;
                held = JumpInputs.Canonical(s, held);
                if (s.Grounded && s.X >= _exitFrom) return s.X < _exitTo;
                if (s.X >= _exitTo || k >= _maxTicks) return false;

                // x is always startX + k * speed on this run-in line, so k stands in for it in the key.
                long key = Key(k, s, held);
                if (_memo.TryGetValue(key, out bool known)) return known;
                Explored++;

                bool ok = false;
                for (int branch = 0; branch < JumpInputs.Count && !ok; branch++)
                {
                    if (!JumpInputs.Applies(branch, s, held)) continue;
                    bool tap = JumpInputs.Tap(branch), down = JumpInputs.Held(branch, held);
                    var next = s;
                    Runner.Step(ref next, tap, _course, _phase + k, _speed, _cfg, down);
                    ok = Clear(next, k + 1, down);
                }
                _memo[key] = ok;
                return ok;
            }

            static long Key(int k, in RunnerState s, bool held)
            {
                if ((uint)k > 0xFFF || s.Coyote > 15 || s.Buffer > 15) throw new InvalidOperationException("validator state out of range");
                long y = s.Y + (1 << 19);       // 20 bits
                long vy = s.Vy + (1 << 11);     // 12 bits
                return ((long)k << 47) | ((long)System.Math.Min(s.JumpAge, 7) << 44) | (y << 24) | (vy << 12) | ((long)s.Coyote << 8) | ((long)s.Buffer << 4)
                       | ((long)(s.AirJumpsLeft & 3) << 2) | (held ? 2L : 0L) | (s.Grounded ? 1L : 0L);
            }
        }
    }
}
