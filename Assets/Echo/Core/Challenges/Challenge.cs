using Echo.Core.Ghosts;

namespace Echo.Core.Challenges
{
    /// <summary>
    /// Ghost Challenges (value-adds doc): one run, sent to a friend, who plays the same course against it as a
    /// single named ghost. Everything the friend's game needs travels in the challenge itself (ChallengeCode):
    /// the course seed, the content version, the run's speed, the skin it wore and its inputs.
    /// </summary>
    public sealed class Challenge
    {
        public const int MaxNameLength = 12;

        /// <summary>Who sent it, e.g. "Jamie" for "Jamie's Ghost".</summary>
        public string Name = "";
        public uint Seed;
        public string ContentVersion = "";
        public int SpeedPct = 100;

        /// <summary>The metres the run reached: the distance to beat.</summary>
        public int Distance;
        public GhostRecord Record = new GhostRecord();

        /// <summary>"Jamie's Ghost", or "A friend's ghost" with no name.</summary>
        public string GhostName => string.IsNullOrEmpty(Name) ? "A friend's ghost" : Name + "'s Ghost";

        /// <summary>A fresh copy of the run to replay, so kills from one attempt never carry into the next.</summary>
        public GhostRecord FreshRecord() => new GhostRecord
        {
            RunIndex = Record.RunIndex,
            TapTicks = (int[])Record.TapTicks.Clone(),
            ReleaseTicks = (int[])Record.ReleaseTicks.Clone(),
            ReviveTicks = (int[])Record.ReviveTicks.Clone(),
            DeathTick = Record.DeathTick,
            DeathX = Record.DeathX,
            Cause = Record.Cause,
            SpeedPct = SpeedPct,
            SkinId = Record.SkinId,
        };
    }
}
