using Echo.Core.Courses;
using Echo.Core.Rules;
using Echo.Core.Sim;

namespace Echo.Core.Reels
{
    /// <summary>
    /// Plays a reel back one tick at a time, exactly: the recorded taps, the finger lifts (held is false only on a
    /// release tick, which reproduces the original down state), and the Shield, Phase and Continue on their ticks.
    /// A 1-UP needs nothing: the run spends it itself. Steps without allocating.
    /// </summary>
    public sealed class ReelPlayer
    {
        readonly Reel _reel;
        int _tap, _release;

        public ReelPlayer(Reel reel, Course course, in SimConfig cfg)
        {
            _reel = reel;
            Run = ReelRules.Restart(reel, course, cfg);
        }

        public RunSimulation Run { get; }
        public Reel Reel => _reel;

        /// <summary>Dead here, and the original run was continued here: call <see cref="Continue"/>.</summary>
        public bool ContinueDue => Run.IsDead && !Run.ContinueUsed && Run.Tick == _reel.ContinueTick;

        /// <summary>The reel has played to its end.</summary>
        public bool Finished => Run.IsDead && !ContinueDue;

        public void Continue()
        {
            if (ContinueDue) Run.Revive();
        }

        public void Step()
        {
            if (Run.IsDead) return;
            int t = Run.Tick;
            if (t == _reel.ShieldTick) Run.UseShield();
            if (t == _reel.PhaseTick) Run.UsePhase();

            int[] taps = _reel.Run.TapTicks, releases = _reel.Run.ReleaseTicks;
            while (_tap < taps.Length && taps[_tap] < t) _tap++;
            while (_release < releases.Length && releases[_release] < t) _release++;
            bool tap = _tap < taps.Length && taps[_tap] == t;
            bool release = _release < releases.Length && releases[_release] == t;
            Run.Step(tap, !release);
        }
    }
}
