namespace EchoCloud
{
    /// <summary>SubmitGraveyardRun: the replayed distance, the day's best, the week's sum and both ranks.</summary>
    public sealed class GraveyardSubmitResponse
    {
        public bool Ok { get; set; }

        /// <summary>"closed", "wrong-day", "malformed", "content-version", "options", "seed", "ghosts", "run-index", "unfinished".</summary>
        public string Error { get; set; } = "";
        public int Distance { get; set; }
        public bool NewBest { get; set; }
        public int BestToday { get; set; }
        public int WeekTotal { get; set; }
        public int DailyRank { get; set; }
        public int WeeklyRank { get; set; }

        /// <summary>It came in just after midnight on yesterday's ghosts: kept and counted, not ranked.</summary>
        public bool BoardClosed { get; set; }
    }
}
