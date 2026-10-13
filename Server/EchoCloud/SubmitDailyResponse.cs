namespace EchoCloud
{
    /// <summary>The server's verdict on a daily run (Daily Haunt doc, SubmitDailyRun): its own distance, always.</summary>
    public sealed class SubmitDailyResponse
    {
        public bool Ok { get; set; }

        /// <summary>
        /// "malformed", "content-version", "options", "power-ups", "speed", "run-index", "wrong-day", "seed",
        /// "leader-version", "ghosts", "unfinished" or "rate-limit".
        /// </summary>
        public string Error { get; set; } = "";
        public int Distance { get; set; }
        public int BestDistance { get; set; }
        public bool NewBest { get; set; }
        public int Rank { get; set; }
        public int PreviousRank { get; set; }
        public int LeadersPassed { get; set; }

        /// <summary>The next leader ahead: "Next: SwiftGhost · 812 m". Empty when every leader is behind.</summary>
        public string NextLeaderName { get; set; } = "";
        public int NextLeaderDistance { get; set; }

        /// <summary>The client claimed a different distance (logged: a desync bug or tampering).</summary>
        public bool Corrected { get; set; }
        public int NextRunIndex { get; set; }
    }
}
