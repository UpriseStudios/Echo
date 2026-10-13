using Echo.Core.Rules;

namespace Echo.Core.Reels
{
    /// <summary>What a replay worked out for itself (version 2.0: the server trusts this, never the client's claim).</summary>
    public readonly struct ReplayResult
    {
        public readonly int Distance;
        public readonly int Score;
        public readonly int DeathTick;
        public readonly int NearMisses;
        public readonly DeathCause Cause;

        /// <summary>False when the run had not ended by RunReplay.MaxTicks: not a finished run.</summary>
        public readonly bool Finished;

        public ReplayResult(int distance, int score, int deathTick, int nearMisses, DeathCause cause, bool finished)
        {
            Distance = distance;
            Score = score;
            DeathTick = deathTick;
            NearMisses = nearMisses;
            Cause = cause;
            Finished = finished;
        }
    }
}
