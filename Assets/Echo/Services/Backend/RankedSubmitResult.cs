namespace Echo.Services.Backend
{
    /// <summary>SubmitDuel's answer (mirrors Server/EchoCloud/RankedSubmitResponse).</summary>
    public sealed class RankedSubmitResult
    {
        public bool Ok;

        /// <summary>"token" (used, not yours, or forfeited), "offline", or a rule broken.</summary>
        public string Error = "";
        public bool Ranked;
        public int Distance, OpponentDistance;

        /// <summary>"won", "lost" or "draw".</summary>
        public string Outcome = "";
        public int RatingBefore, RatingAfter, Change, Rank, DuelsLeftToday;
        public string Tier = "";
        public bool TierUp;

        public static RankedSubmitResult Failed(string error) => new RankedSubmitResult { Error = error };
    }
}
