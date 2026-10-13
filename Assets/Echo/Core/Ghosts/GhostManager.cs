using System;
using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Sim;

namespace Echo.Core.Ghosts
{
    /// <summary>
    /// Replays every ghost for one run with the same Runner.Step as the player (tech spec 7). Ghosts never
    /// interact with each other. Allocates only in Begin.
    ///
    /// Gameplay revision: a ghost locked to the player's x can only be avoided by never sharing its height,
    /// which made any ghost that lived past the grace period unbeatable. Each ghost now runs on its own slower
    /// clock with a head start, so the player catches and overtakes it at a chunk boundary (clear floor) just
    /// before its gravestone (EncounterPlanner). Ghosts that have killed the player claim boundaries first (see
    /// EncounterOrder), then newest first. A ghost with no fair boundary near its grave is a harmless echo,
    /// timed to fall onto its grave as the player arrives. The replay itself is unchanged: same taps on the
    /// same own ticks.
    /// </summary>
    public sealed class GhostManager
    {
        public const int FadeTicks = 18;

        /// <summary>A harmless echo dies this far ahead of the player, so its fall onto the grave is seen.</summary>
        public const int EchoLeadX = 3000;

        GhostRecord[] _records = new GhostRecord[32];
        GhostReplay[] _replays = new GhostReplay[32];
        int _clockNum = 1, _clockDen = 1;
        // Player ticks the ghost clocks are held back by, after revives moved the player back (see HoldBack).
        int _delay;

        public int Count { get; private set; }
        public int ClockNum => _clockNum;
        public int ClockDen => _clockDen;

        public GhostRecord RecordAt(int i) => _records[i];
        public ref readonly GhostReplay ReplayAt(int i) => ref _replays[i];

        /// <summary>Ghosts that are replaying or fading, i.e. still on screen.</summary>
        public int VisibleCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Count; i++) if (_replays[i].Phase != GhostPhase.Gone) n++;
                return n;
            }
        }

        /// <summary>A ghost can kill or be near-missed only while replaying and alive (dead between a death and its revive).</summary>
        public bool IsActive(int i) => _replays[i].Phase == GhostPhase.Replaying && _replays[i].State.Alive;

        /// <summary>Active and not made harmless by the encounter planner.</summary>
        public bool IsLethal(int i) => IsActive(i) && !_replays[i].Harmless;

        /// <summary>Ghosts locked to the player (original tech spec 7), for code that has no course or speed.</summary>
        public void Begin(IReadOnlyList<GhostRecord> ghosts, in SimConfig cfg) => Begin(ghosts, cfg, null, 0);

        /// <param name="course">Used to place each overtake at a chunk boundary; null keeps ghosts locked to the player.</param>
        /// <param name="playerSpeed">The player's speed this run, in sub-units per tick.</param>
        public void Begin(IReadOnlyList<GhostRecord> ghosts, in SimConfig cfg, Course course, int playerSpeed)
        {
            if (ghosts.Count > _records.Length)
            {
                Array.Resize(ref _records, ghosts.Count);
                Array.Resize(ref _replays, ghosts.Count);
            }
            bool echoes = course != null && playerSpeed > 0 && cfg.GhostClockNum < cfg.GhostClockDen;
            _clockNum = echoes ? cfg.GhostClockNum : 1;
            _clockDen = echoes ? cfg.GhostClockDen : 1;
            _delay = 0;

            Count = ghosts.Count;
            EncounterPlanner planner = echoes ? new EncounterPlanner(course, cfg, playerSpeed, _clockNum, _clockDen) : null;
            int[] order = EncounterOrder(ghosts);
            for (int n = 0; n < Count; n++)
            {
                int i = order[n];
                GhostRecord rec = ghosts[i];
                int speed = cfg.SpeedForPct(rec.SpeedPct);
                var r = new GhostReplay
                {
                    State = RunnerState.AtStart(),
                    PrevState = RunnerState.AtStart(),
                    Phase = GhostPhase.Replaying,
                    Speed = speed,
                };
                if (echoes)
                {
                    int deathX = rec.DeathX >= 0 ? rec.DeathX : ReplayDeathX(rec, speed, course, cfg);
                    if (planner.TryPlace(rec, speed, deathX, out int chunk))
                    {
                        int meetX = course.ChunkStartX(chunk);
                        r.MeetTick = CeilDiv(meetX, playerSpeed);
                        // At MeetTick the ghost must be on its own tick that reaches meetX.
                        r.HeadStart = EncounterPlanner.HeadStartFor(meetX, speed, playerSpeed, _clockNum, _clockDen);
                    }
                    else
                    {
                        // The echo's last own tick lands as the player comes within EchoLeadX of the grave.
                        r.Harmless = true;
                        r.MeetTick = CeilDiv(Math.Max(0, deathX - EchoLeadX), playerSpeed);
                        r.HeadStart = Math.Max(0, rec.DeathTick - r.MeetTick * _clockNum / _clockDen);
                    }
                }
                _records[i] = rec;
                _replays[i] = r;
                AdvanceTo(rec, ref _replays[i], course, r.HeadStart, cfg);
            }
            for (int i = Count; i < _records.Length; i++) _records[i] = null;
        }

        /// <summary>
        /// Who is met first (gameplay revision): ghosts that have killed the player, the most recent killer first,
        /// then the rest newest first. Without this the run that just died took the first slot every time and
        /// pushed its killer two chunks further out, so a ghost rarely got a second kill and no Nemesis formed.
        /// Indices into <paramref name="ghosts"/> (oldest first); a stable insertion sort, so it is deterministic.
        /// </summary>
        public static int[] EncounterOrder(IReadOnlyList<GhostRecord> ghosts)
        {
            var order = new int[ghosts.Count];
            for (int n = 0; n < order.Length; n++)
            {
                int i = ghosts.Count - 1 - n; // newest first before the sort
                int j = n;
                while (j > 0 && MetBefore(ghosts[i], ghosts[order[j - 1]]))
                {
                    order[j] = order[j - 1];
                    j--;
                }
                order[j] = i;
            }
            return order;
        }

        static bool MetBefore(GhostRecord a, GhostRecord b) => KillRank(a) > KillRank(b);

        // The run index of its latest kill, or 0 for a ghost that has never killed the player.
        static int KillRank(GhostRecord g) => g.Kills > 0 ? g.LastKillRun : 0;

        /// <summary>
        /// A lethal ghost is overtaken at a boundary within one chunk of <paramref name="chunk"/>, so the Copycat twist
        /// keeps its encounters clear of it and each proof stands alone.
        /// </summary>
        public bool LethalMeetNear(int chunk, Course course, int playerSpeed)
        {
            if (_clockNum >= _clockDen) return false;
            for (int i = 0; i < Count; i++)
            {
                if (_replays[i].Harmless || _replays[i].Phase == GhostPhase.Gone) continue;
                int meetChunk = course.ChunkIndexAt(_replays[i].MeetTick * playerSpeed);
                if (Math.Abs(meetChunk - chunk) <= 1) return true;
            }
            return false;
        }

        /// <summary>Own ticks a ghost has played once the player has stepped <paramref name="playerTicks"/> ticks.</summary>
        public int OwnTicksAt(int i, int playerTicks) => _replays[i].HeadStart + Math.Max(0, playerTicks - _delay) * _clockNum / _clockDen;

        /// <summary>
        /// A revive moved the player back by <paramref name="playerTicks"/> ticks of running (device testing: after a
        /// 1-UP, overtakes landed in tunnels). Every ghost's clock is held back by as much, so each ghost is where it
        /// was when the player last stood here and every overtake still happens on the clear floor it was proved on.
        /// Replays only run forwards, so each is rebuilt from its start; a revive is rare, so the cost is fine.
        /// </summary>
        public void HoldBack(int playerTicks, int nowTick, Course course, in SimConfig cfg)
        {
            if (playerTicks <= 0) return;
            _delay += playerTicks;
            for (int i = 0; i < Count; i++)
            {
                GhostReplay old = _replays[i];
                var r = new GhostReplay
                {
                    State = RunnerState.AtStart(),
                    PrevState = RunnerState.AtStart(),
                    Phase = GhostPhase.Replaying,
                    Speed = old.Speed,
                    HeadStart = old.HeadStart,
                    MeetTick = old.MeetTick + playerTicks,
                    Harmless = old.Harmless,
                    NearMissed = old.NearMissed,
                };
                _replays[i] = r;
                AdvanceTo(_records[i], ref _replays[i], course, OwnTicksAt(i, nowTick), cfg);
            }
        }

        public void Step(Course course, int tick, in SimConfig cfg)
        {
            for (int i = 0; i < Count; i++)
                AdvanceTo(_records[i], ref _replays[i], course, OwnTicksAt(i, tick + 1), cfg);
        }

        static void AdvanceTo(GhostRecord rec, ref GhostReplay r, Course course, int ownTicks, in SimConfig cfg)
        {
            while (r.OwnTick < ownTicks)
            {
                r.PrevState = r.State;
                StepGhost(rec, ref r, course, r.OwnTick, cfg);
                r.OwnTick++;
            }
        }

        internal static void StepGhost(GhostRecord rec, ref GhostReplay r, Course course, int tick, in SimConfig cfg)
        {
            if (r.Phase == GhostPhase.Gone) return;
            if (r.Phase == GhostPhase.Fading)
            {
                if (--r.FadeTicksLeft <= 0) r.Phase = GhostPhase.Gone;
                return;
            }

            // tech spec 7: a revive is applied before the tick is stepped, exactly as the player's was.
            if (r.ReviveCursor < rec.ReviveTicks.Length && rec.ReviveTicks[r.ReviveCursor] == tick)
            {
                Runner.Revive(ref r.State, cfg);
                r.ReviveCursor++;
            }

            bool tap = r.TapCursor < rec.TapTicks.Length && rec.TapTicks[r.TapCursor] == tick;
            if (tap) r.TapCursor++;
            // Held as the player had it: a tap presses, a release on the same or a later tick lifts.
            if (tap) r.Held = true;
            if (r.ReleaseCursor < rec.ReleaseTicks.Length && rec.ReleaseTicks[r.ReleaseCursor] == tick)
            {
                r.Held = false;
                r.ReleaseCursor++;
            }

            Runner.Step(ref r.State, tap, course, tick, r.Speed, cfg, r.Held);

            // The ghost's own sim can't see what killed the player (another ghost), so the death tick is forced.
            if (tick >= rec.DeathTick)
            {
                r.State.Alive = false;
                r.Phase = GhostPhase.Fading;
                r.FadeTicksLeft = FadeTicks;
            }
        }

        /// <summary>
        /// Positions ghost <paramref name="i"/> will have over its next own ticks, for its preview trail (tech spec 11).
        /// Steps a copy, so the real ghost is never touched. Stops early at a death. Returns the count written.
        /// </summary>
        public int Preview(int i, Course course, int ownTicks, in SimConfig cfg, int[] xs, int[] ys)
        {
            GhostReplay copy = _replays[i];
            int n = 0;
            for (int k = 0; k < ownTicks && n < xs.Length && n < ys.Length; k++)
            {
                if (copy.Phase != GhostPhase.Replaying) break;
                StepGhost(_records[i], ref copy, course, copy.OwnTick, cfg);
                copy.OwnTick++;
                if (!copy.State.Alive) break;
                xs[n] = copy.State.X;
                ys[n] = copy.State.Y;
                n++;
            }
            return n;
        }

        /// <summary>
        /// Where ghost <paramref name="i"/>'s run ended, in sub-units. Ghosts store no positions (tech spec 7), so
        /// this replays the record from tick 0 on a fresh copy. Views use it for grave markers; it changes nothing.
        /// </summary>
        public int DeathX(int i, Course course, in SimConfig cfg) => ReplayDeathX(_records[i], _replays[i].Speed, course, cfg);

        static int ReplayDeathX(GhostRecord rec, int speed, Course course, in SimConfig cfg)
        {
            var r = new GhostReplay
            {
                State = RunnerState.AtStart(),
                Phase = GhostPhase.Replaying,
                Speed = speed,
            };
            for (int tick = 0; tick <= rec.DeathTick && r.Phase == GhostPhase.Replaying; tick++)
                StepGhost(rec, ref r, course, tick, cfg);
            return r.State.X;
        }

        /// <summary>Index of the first active ghost whose 80% hitbox overlaps the player's box, or -1.</summary>
        public int FirstOverlapping(in Aabb playerBox, in SimConfig cfg)
        {
            for (int i = 0; i < Count; i++)
                if (IsLethal(i) && playerBox.Overlaps(Runner.GhostBox(_replays[i].State, cfg))) return i;
            return -1;
        }

        /// <summary>Marks and counts ghosts passed within NearMissDistance without touching, once per ghost per run.</summary>
        public int MarkNearMisses(in Aabb playerBox, in SimConfig cfg)
        {
            int n = 0;
            for (int i = 0; i < Count; i++)
            {
                if (_replays[i].NearMissed || !IsLethal(i)) continue;
                Aabb ghostBox = Runner.GhostBox(_replays[i].State, cfg);
                if (playerBox.Overlaps(ghostBox) || playerBox.GapTo(ghostBox) > cfg.NearMissDistance) continue;
                _replays[i].NearMissed = true;
                n++;
            }
            return n;
        }

        static int CeilDiv(int a, int b) => (a + b - 1) / b;
    }
}
