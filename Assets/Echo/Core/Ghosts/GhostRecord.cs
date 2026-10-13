using System;

namespace Echo.Core.Ghosts
{
    /// <summary>
    /// One finished run, replayed from its inputs only (tech spec 7): never from positions. DeathX is kept only
    /// to place its gravestone without a replay (skins addendum).
    /// </summary>
    public sealed class GhostRecord
    {
        public int RunIndex;
        public int[] TapTicks = Array.Empty<int>();
        public int[] ReviveTicks = Array.Empty<int>();
        /// <summary>Ticks the finger lifted (variable jump). A tap holds until the next release.</summary>
        public int[] ReleaseTicks = Array.Empty<int>();
        public int DeathTick;
        public int SpeedPct = 100;
        public int Kills;
        public int LastKillRun;   // run index of the player's run it most recently killed

        // Skins addendum: what the run wore and how it ended. Cosmetic and descriptive only; the replay never
        // reads them.
        public string SkinId = "";

        /// <summary>Where the run ended, in sub-units, from the sim at death: its gravestone. -1 when unknown (v1 saves).</summary>
        public int DeathX = -1;
        public Rules.DeathCause Cause;

        /// <summary>Run index of the ghost that ended the run, 0 if a hazard or fall did.</summary>
        public int KillerRunIndex;

        /// <summary>A copy whose kills can change without touching this one. The tick arrays are never changed, so they are shared.</summary>
        public GhostRecord Clone() => (GhostRecord)MemberwiseClone();
    }
}
