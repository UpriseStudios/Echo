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

        /// <summary>
        /// A run from yesterday sent in the grace period after midnight: replayed and kept as that day's best and
        /// ghosts, but yesterday's board had closed (UGS archives are read-only), so it isn't ranked.
        /// </summary>
        public bool BoardClosed { get; set; }

        /// <summary>A new daily best on today's board: how much it improved the day (metres), and the player's board name.</summary>
        public int Improvement { get; set; }
        public string PlayerName { get; set; } = "";

        /// <summary>Naughty vs Nice (modes doc): the side the improvement counted for, or "".</summary>
        public string Team { get; set; } = "";
        public int TeamContribution { get; set; }
    }
}
