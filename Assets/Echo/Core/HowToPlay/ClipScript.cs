using System;
using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Core.Sim;

namespace Echo.Core.HowToPlay
{
    /// <summary>
    /// Writes one how-to-play run move by move. Nothing is hand-timed: each move's tap is found by playing the run with
    /// the game's own simulation (through ReelPlayer, exactly as the clip will play) and keeping a tick where the move
    /// is the one shown (a ground jump, or an air jump for the second tap) and the runner gets past what it was for.
    /// Of the ticks that work, the middle one is taken, so jumps look natural rather than early or late.
    /// </summary>
    sealed class ClipScript
    {
        const int Window = 150;          // ticks searched for a move, from where the last one cleared
        const int HeldTicks = 30;        // a full jump keeps the finger down past its apex
        const int TickLimit = 3600;

        static readonly int[] SecondTapDelays = { 14, 18, 22, 26, 30 };

        readonly Course _course;
        readonly SimConfig _cfg;
        readonly List<int> _taps = new List<int>();
        readonly List<int> _releases = new List<int>();
        int _from;

        public readonly Reel Reel;
        public readonly HowToPlayClip Clip;

        public ClipScript(int topic, string topicName, Course course, in SimConfig cfg, string skinId, IEnumerable<GhostRecord> ghosts, int runIndex)
        {
            _course = course;
            _cfg = cfg;
            Reel = new Reel
            {
                Seed = course.Seed,
                SpeedPct = 100,
                RunIndex = runIndex,
                Run = new GhostRecord { RunIndex = runIndex, SkinId = skinId ?? "" },
            };
            if (ghosts != null)
                foreach (GhostRecord g in ghosts) Reel.Ghosts.Add(g.Clone());
            Clip = new HowToPlayClip { Topic = topic, TopicName = topicName, Course = course, Reel = Reel };
        }

        /// <summary>The run so far: from the start until <paramref name="stop"/>, its end, or the tick limit.</summary>
        public RunSimulation Play(Func<RunSimulation, bool> stop, Action<RunSimulation> everyTick = null)
        {
            Reel.Run.TapTicks = Sorted(_taps);
            Reel.Run.ReleaseTicks = Sorted(_releases);
            var player = new ReelPlayer(Reel, _course, _cfg);
            while (!player.Finished && player.Run.Tick < TickLimit && (stop == null || !stop(player.Run)))
            {
                player.Step();
                everyTick?.Invoke(player.Run);
            }
            return player.Run;
        }

        /// <summary>The first tick at which <paramref name="when"/> holds, or -1.</summary>
        public int TickWhen(Func<RunSimulation, bool> when)
        {
            int found = -1;
            Play(run => found >= 0, run =>
            {
                if (found < 0 && when(run)) found = run.Tick;
            });
            return found;
        }

        /// <summary>
        /// Adds a move whose tap gets the runner to <paramref name="cleared"/> alive; returns the tap tick (for the second
        /// tap of a double jump, see <paramref name="secondTap"/>), or -1 when no tick works.
        /// </summary>
        public int Move(HowToMove move, Func<RunSimulation, bool> cleared, out int secondTap)
        {
            secondTap = -1;
            var works = new List<(int tap, int second)>();
            for (int t = _from; t < _from + Window; t++)
            {
                foreach (int delay in move == HowToMove.DoubleJump ? SecondTapDelays : new[] { 0 })
                {
                    int second = move == HowToMove.DoubleJump ? t + delay : -1;
                    Add(move, t, second);
                    bool ok = Works(t, second, cleared);
                    Remove(move);
                    if (!ok) continue;
                    works.Add((t, second));
                    break;
                }
            }
            if (works.Count == 0) return -1;
            (int tap, int secondChosen) = works[works.Count / 2];
            Add(move, tap, secondChosen);
            secondTap = secondChosen;
            _from = TickWhen(cleared);
            return tap;
        }

        /// <summary>Carries on from where <paramref name="when"/> first holds (after a death and a 1-UP, say).</summary>
        public void From(int tick) => _from = Math.Max(_from, tick);

        public void Beat(int from, int to, string text, bool slowMo = false) =>
            Clip.Beats.Add(new HowToPlayBeat { From = Math.Max(0, from), To = to, Text = text, SlowMo = slowMo });

        // The tap is a ground jump (the second an air jump), and the runner reaches the goal alive.
        bool Works(int tap, int second, Func<RunSimulation, bool> cleared)
        {
            bool shown = true;
            bool reached = false;
            RunSimulation run = Play(r => reached || !shown, r =>
            {
                if (r.Tick == tap + 1) shown &= !r.Player.Grounded && r.Player.Vy > 0 && r.Player.AirJumpsLeft == _cfg.AirJumps;
                if (second >= 0 && r.Tick == second + 1) shown &= r.Player.Vy > 0 && r.Player.AirJumpsLeft == _cfg.AirJumps - 1;
                if (shown && !r.IsDead && cleared(r)) reached = true;
            });
            return shown && reached && !run.IsDead;
        }

        void Add(HowToMove move, int tap, int second)
        {
            _taps.Add(tap);
            switch (move)
            {
                case HowToMove.Hop:
                    _releases.Add(tap + 1);
                    break;
                case HowToMove.Jump:
                    _releases.Add(tap + HeldTicks);
                    break;
                case HowToMove.DoubleJump:
                    _releases.Add(second - 1);
                    _taps.Add(second);
                    _releases.Add(second + HeldTicks);
                    break;
            }
        }

        void Remove(HowToMove move)
        {
            int n = move == HowToMove.DoubleJump ? 2 : 1;
            _taps.RemoveRange(_taps.Count - n, n);
            _releases.RemoveRange(_releases.Count - n, n);
        }

        static int[] Sorted(List<int> ticks)
        {
            int[] a = ticks.ToArray();
            Array.Sort(a);
            return a;
        }
    }
}
