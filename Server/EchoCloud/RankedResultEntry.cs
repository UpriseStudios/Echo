namespace EchoCloud
{
    /// <summary>A recent Ghost Duel on the profile: who, how it went, and how far each of you got.</summary>
    public sealed class RankedResultEntry
    {
        public string Opponent { get; set; } = "";

        /// <summary>"won", "lost" or "draw".</summary>
        public string Outcome { get; set; } = "";
        public int Change { get; set; }
        public int Distance { get; set; }
        public int OpponentDistance { get; set; }
    }
}
