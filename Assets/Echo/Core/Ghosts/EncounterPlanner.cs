using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Sim;

namespace Echo.Core.Ghosts
{
    /// <summary>
    /// Chooses where the player overtakes each ghost so every overtake is always possible. One planner per run.
    ///
    /// Placement (gameplay revision): a ghost is met at the chunk boundary just before its own gravestone, so it
    /// is seen running to the spot where it fell, and up to <see cref="MaxPerEncounter"/> ghosts can share a
    /// boundary. Boundaries that hold lethal ghosts are kept a chunk apart, so each overtake stands alone.
    ///
    /// ChunkValidator guarantees the player can reach standing in the boundary band of every chunk. An overtake
    /// at the start of chunk c happens inside chunks c-1 and c, and with no Continue the player's x is fixed by
    /// the tick, so every ghost's position over that stretch is known in advance. The planner checks, with the
    /// same memoised search as the validator, that from every standing entry into chunk c-1 the player can reach
    /// standing in the band at the end of chunk c without touching a hazard or any ghost met there. A ghost with
    /// no such boundary near its grave is not lethal this run: it becomes a faint echo instead (GhostManager).
    /// </summary>
    internal sealed class EncounterPlanner
    {
        public const int MaxPerEncounter = 2;

        /// <summary>How many boundaries before its grave a ghost may be met and still be seen near it.</summary>
        public const int MaxChunksBeforeGrave = 2;

        const int SlackTicks = 120;

        readonly Course _course;
        readonly SimConfig _cfg;
        readonly int _playerSpeed, _num, _den;
        readonly Dictionary<int, List<Track>> _placed = new Dictionary<int, List<Track>>();
        readonly List<Track> _trial = new List<Track>(MaxPerEncounter);

        public EncounterPlanner(Course course, in SimConfig cfg, int playerSpeed, int clockNum, int clockDen)
        {
            _course = course;
            _cfg = cfg;
            _playerSpeed = playerSpeed;
            _num = clockNum;
            _den = clockDen;
        }

        /// <summary>
        /// Claims the latest boundary at or before the ghost's grave (no more than MaxChunksBeforeGrave back, never
        /// before FirstEncounterChunk) where overtaking it, together with any ghost already met there, is always possible.
        /// </summary>
        public bool TryPlace(GhostRecord rec, int ghostSpeed, int deathX, out int chunk)
        {
            int target = _course.ChunkIndexAt(deathX);
            for (int c = target; c >= _cfg.FirstEncounterChunk && c >= target - MaxChunksBeforeGrave; c--)
            {
                if (!Free(c)) continue;
                var track = new Track(rec, ghostSpeed, HeadStartFor(_course.ChunkStartX(c), ghostSpeed, _playerSpeed, _num, _den));
                _trial.Clear();
                if (_placed.TryGetValue(c, out List<Track> there)) _trial.AddRange(there);
                _trial.Add(track);
                if (!OvertakePossible(c, _trial)) continue;
                if (there == null) _placed[c] = there = new List<Track>(MaxPerEncounter);
                there.Add(track);
                chunk = c;
                return true;
            }
            chunk = -1;
            return false;
        }

        int CountAt(int c) => _placed.TryGetValue(c, out List<Track> there) ? there.Count : 0;

        bool Free(int c) => CountAt(c) < MaxPerEncounter && CountAt(c - 1) == 0 && CountAt(c + 1) == 0;

        public static int HeadStartFor(int meetX, int ghostSpeed, int playerSpeed, int clockNum, int clockDen)
        {
            int meetTick = CeilDiv(meetX, playerSpeed);
            return System.Math.Max(0, CeilDiv(meetX, ghostSpeed) - meetTick * clockNum / clockDen);
        }

        bool OvertakePossible(int c, List<Track> tracks)
        {
            int band = ChunkValidator.BoundaryBand(_cfg);
            int entryFrom = _course.ChunkStartX(c - 1) - band;
            int exitFrom = _course.ChunkStartX(c + 1) - band;
            int exitTo = exitFrom + 2 * band;
            int lastTick = exitTo / _playerSpeed + SlackTicks;
            int firstTick = CeilDiv(entryFrom, _playerSpeed);
            foreach (Track t in tracks)
                t.Fill(_course, _cfg, t.HeadStart + firstTick * _num / _den - 1, t.HeadStart + (lastTick + 1) * _num / _den);

            // The player's side is proved with the single jump (hops included): conservative, since the real
            // player also has the double jump, and it keeps the search small. Ghosts replay with the real rules.
            SimConfig single = _cfg.WithAirJumps(0);
            var search = new Search(_course, single, _playerSpeed, exitFrom, exitTo, lastTick, tracks, _num, _den);
            var s = RunnerState.AtStart();
            s.X = firstTick * _playerSpeed;
            s.LastSafeX = s.X;
            for (int t = firstTick; s.X < entryFrom + 2 * band; t++)
            {
                if (!search.Clear(s, t)) return false;
                Runner.Step(ref s, false, _course, t, _playerSpeed, single);
                if (!s.Alive || search.TouchesGhost(s, t)) return false;
            }
            return true;
        }

        static int CeilDiv(int a, int b) => (a + b - 1) / b;

        /// <summary>One ghost met at one boundary: its own states over the overtake, States[k] being its state after own tick First + k.</summary>
        sealed class Track
        {
            static readonly RunnerState DeadState = new RunnerState { Alive = false };

            readonly GhostRecord _rec;
            readonly int _speed;
            public readonly int HeadStart;
            public readonly List<RunnerState> States = new List<RunnerState>(256);
            public int First = -1;
            GhostReplay _replay;
            int _next;

            public Track(GhostRecord rec, int speed, int headStart)
            {
                _rec = rec;
                _speed = speed;
                HeadStart = headStart;
                _replay = new GhostReplay { State = RunnerState.AtStart(), Phase = GhostPhase.Replaying, Speed = speed };
            }

            /// <summary>Steps the replay up to own tick <paramref name="ownLast"/>, keeping states from <paramref name="ownFirst"/> on.</summary>
            public void Fill(Course course, in SimConfig cfg, int ownFirst, int ownLast)
            {
                if (First < 0) First = System.Math.Max(0, ownFirst);
                for (; _next <= ownLast && _replay.Phase == GhostPhase.Replaying; _next++)
                {
                    GhostManager.StepGhost(_rec, ref _replay, course, _next, cfg);
                    if (_next >= First) States.Add(_replay.Phase == GhostPhase.Replaying ? _replay.State : DeadState);
                }
            }

            public bool Touches(in Aabb player, int tick, int num, int den, in SimConfig cfg)
            {
                int own = HeadStart + (tick + 1) * num / den - 1 - First;
                if (own < 0 || own >= States.Count) return false;
                RunnerState g = States[own];
                return g.Alive && player.Overlaps(Runner.GhostBox(g, cfg));
            }
        }

        sealed class Search
        {
            readonly Course _course;
            readonly SimConfig _cfg;
            readonly int _speed, _exitFrom, _exitTo, _lastTick, _num, _den;
            readonly List<Track> _ghosts;
            readonly Dictionary<long, bool> _memo = new Dictionary<long, bool>();

            public Search(Course course, in SimConfig cfg, int speed, int exitFrom, int exitTo, int lastTick,
                          List<Track> ghosts, int num, int den)
            {
                _course = course;
                _cfg = cfg;
                _speed = speed;
                _exitFrom = exitFrom;
                _exitTo = exitTo;
                _lastTick = lastTick;
                _ghosts = ghosts;
                _num = num;
                _den = den;
            }

            /// <summary>Same rule as RunSimulation: after the grace period, touching an active ghost kills.</summary>
            public bool TouchesGhost(in RunnerState player, int tick)
            {
                if (tick < _cfg.GhostGraceTicks) return false;
                Aabb box = Runner.PlayerBox(player, _cfg);
                foreach (Track g in _ghosts)
                    if (g.Touches(box, tick, _num, _den, _cfg)) return true;
                return false;
            }

            public bool Clear(RunnerState s, int t, bool held = false)
            {
                if (!s.Alive) return false;
                held = JumpInputs.Canonical(s, held);
                if (s.Grounded && s.X >= _exitFrom) return s.X < _exitTo;
                if (s.X >= _exitTo || t >= _lastTick) return false;

                long key = ((long)t << 47) | ((long)System.Math.Min(s.JumpAge, 7) << 44) | ((long)(s.Y + (1 << 19)) << 24) | ((long)(s.Vy + (1 << 11)) << 12)
                           | ((long)s.Coyote << 8) | ((long)s.Buffer << 4) | ((long)(s.AirJumpsLeft & 3) << 2)
                           | (held ? 2L : 0L) | (s.Grounded ? 1L : 0L);
                if (_memo.TryGetValue(key, out bool known)) return known;

                bool ok = false;
                for (int branch = 0; branch < JumpInputs.Count && !ok; branch++)
                {
                    if (!JumpInputs.Applies(branch, s, held)) continue;
                    bool tap = JumpInputs.Tap(branch), down = JumpInputs.Held(branch, held);
                    var next = s;
                    Runner.Step(ref next, tap, _course, t, _speed, _cfg, down);
                    if (next.Alive && TouchesGhost(next, t)) continue;
                    ok = Clear(next, t + 1, down);
                }
                _memo[key] = ok;
                return ok;
            }
        }
    }
}
