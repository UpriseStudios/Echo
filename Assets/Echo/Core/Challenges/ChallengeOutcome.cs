namespace Echo.Core.Challenges
{
    /// <summary>How a challenge run went: "You outlasted Jamie by 142 m" or "Jamie's Ghost got you".</summary>
    public struct ChallengeOutcome
    {
        public bool Won;

        /// <summary>The difference between the two distances, in metres.</summary>
        public int Margin;

        /// <summary>The challenge ghost itself ended the run.</summary>
        public bool KilledByGhost;
        public int Distance;
        public int TheirDistance;

        /// <summary>Balance addendum: a full challenge is raced on score (both faced the same ghosts, so the
        /// multiplier matches); a Lite one on distance. Margin is in points or metres to match.</summary>
        public bool ByScore;
        public int Score;
        public int TheirScore;
    }
}
