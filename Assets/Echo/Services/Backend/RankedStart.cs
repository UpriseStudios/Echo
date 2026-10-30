namespace Echo.Services.Backend
{
    /// <summary>StartDuel's answer (mirrors Server/EchoCloud/RankedStartResponse): the token, the arena and the opponent.</summary>
    public sealed class RankedStart
    {
        public bool Ok;

        /// <summary>"closed", "limit" (10 ranked today), "no-opponent" or "offline".</summary>
        public string Error = "";
        public bool Ranked;
        public string Token = "";
        public int Season, Arena;
        public long Seed;
        public string ContentVersion = "";

        /// <summary>The opponent's run as a ReelPacket (its Run).</summary>
        public string OpponentPacket = "";
        public string OpponentName = "", OpponentSkin = "", OpponentTier = "";
        public int OpponentRating, DuelsLeftToday;

        public static RankedStart Failed(string error) => new RankedStart { Error = error };
    }
}
