using System.Collections.Generic;
using Echo.Core.Ghosts;

namespace Echo.Core.Rules
{
    /// <summary>What a commit did, for the death screen, effects, analytics and achievements.</summary>
    public sealed class CommitResult
    {
        public int Score;
        public int PreviousBest;
        public bool NewBest;
        public bool NemesisDefeated;
        public string DefeatedNemesisName;
        public int GhostsCleared;
        public GhostRecord AddedGhost;
        public List<GhostRecord> Evicted = new List<GhostRecord>();
        public GhostRecord Nemesis;

        // Value-adds: weekly twist bests, the Nemesis Duel, and the defeated Nemesis for the Rogues' Gallery.
        public bool TwistRun;
        public bool NewTwistBest;
        public int PreviousTwistBest;
        public bool Duel;
        public bool DuelWon;
        public GhostRecord DefeatedNemesis;
    }
}
