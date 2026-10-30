namespace EchoCloud
{
    /// <summary>StartDuel: a single-use token, the arena, and the opponent's run, name, skin and tier (never its distance).</summary>
    public sealed class RankedStartResponse : IFailable
    {
        public bool Ok { get; set; }

        /// <summary>"closed", "limit" (10 ranked duels today), "no-opponent".</summary>
        public string Error { get; set; } = "";
        public bool Ranked { get; set; }
        public string Token { get; set; } = "";
        public int Season { get; set; }

        /// <summary>1 to 10.</summary>
        public int Arena { get; set; }
        public uint Seed { get; set; }
        public string ContentVersion { get; set; } = "";

        /// <summary>The opponent's run as a ReelPacket (its Run).</summary>
        public string OpponentPacket { get; set; } = "";
        public string OpponentName { get; set; } = "";
        public string OpponentSkin { get; set; } = "";
        public string OpponentTier { get; set; } = "";
        public int OpponentRating { get; set; }
        public int DuelsLeftToday { get; set; }
    }
}
