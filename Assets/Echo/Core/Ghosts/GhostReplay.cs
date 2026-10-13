using Echo.Core.Sim;

namespace Echo.Core.Ghosts
{
    /// <summary>Per-run playback state of one ghost. Views read it; only GhostManager writes it.</summary>
    public struct GhostReplay
    {
        public RunnerState State;

        /// <summary>State one own tick earlier, so views can render smoothly between the ghost's slower ticks.</summary>
        public RunnerState PrevState;

        public GhostPhase Phase;
        public int FadeTicksLeft;
        public int TapCursor;
        public int ReviveCursor;
        public int ReleaseCursor;

        /// <summary>Finger down in the replayed run: set by a tap, cleared by a release.</summary>
        public bool Held;
        public int Speed;
        public bool NearMissed;

        /// <summary>The ghost's next own tick to step (its own run's tick numbering).</summary>
        public int OwnTick;

        /// <summary>Own ticks already played before the player's tick 0.</summary>
        public int HeadStart;

        /// <summary>Player tick at which the player catches this ghost.</summary>
        public int MeetTick;

        /// <summary>No overtake of this ghost was provably possible, so it cannot kill (or be near-missed) this run.</summary>
        public bool Harmless;
    }
}
