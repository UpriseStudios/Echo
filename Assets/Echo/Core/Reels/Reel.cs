using System.Collections.Generic;
using Echo.Core.Ghosts;
using Echo.Core.Rules;

namespace Echo.Core.Reels
{
    /// <summary>
    /// A saved run in the highlights reel (a user revision). Like a ghost it is inputs, never positions: the run's
    /// own record, the ghosts as they stood at its start, and the ticks of everything done between ticks (Shield,
    /// Phase, Continue). <see cref="ReelPlayer"/> plays it back exactly on the same course.
    /// </summary>
    public sealed class Reel
    {
        /// <summary>The content version it was played on; a different one would replay on a different course.</summary>
        public string ContentVersion = "";
        public uint Seed;
        public int SpeedPct = 100;
        public int RunIndex;
        public RunOptions Options;

        /// <summary>The ghosts at the start of the run, oldest first.</summary>
        public List<GhostRecord> Ghosts = new List<GhostRecord>();

        /// <summary>The run itself: taps, releases, revives, death tick and skin.</summary>
        public GhostRecord Run = new GhostRecord();

        public int ShieldTick = -1;
        public int PhaseTick = -1;
        public int ContinueTick = -1;
        public bool ExtraLife;

        // What the reel card shows, from the run as it ended.
        public int Score;
        public int Distance;
        public int NearMisses;
        public int GhostsAtStart;
        public bool NewBest;

        /// <summary>When it was saved, as Game formats it (shown on the card only).</summary>
        public string SavedOn = "";

        public bool PlayableOn(string contentVersion) => ContentVersion == contentVersion;
    }
}
