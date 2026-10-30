namespace EchoCloud
{
    /// <summary>SubmitDuel: the replayed distances, the result and the rating it moved.</summary>
    public sealed class RankedSubmitResponse : IFailable
    {
        public bool Ok { get; set; }

        /// <summary>"closed", "token" (used, not yours, or forfeited), "malformed", "content-version", "options", "seed", "ghosts", "unfinished".</summary>
        public string Error { get; set; } = "";
        public bool Ranked { get; set; }
        public int Distance { get; set; }
        public int OpponentDistance { get; set; }

        /// <summary>"won", "lost" or "draw".</summary>
        public string Outcome { get; set; } = "";
        public int RatingBefore { get; set; }
        public int RatingAfter { get; set; }
        public int Change { get; set; }
        public string Tier { get; set; } = "";
        public bool TierUp { get; set; }
        public int Rank { get; set; }
        public int DuelsLeftToday { get; set; }
    }
}
