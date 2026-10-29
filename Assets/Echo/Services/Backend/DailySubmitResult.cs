namespace Echo.Services.Backend
{
    /// <summary>The server's verdict on a daily run (mirrors Server/EchoCloud/SubmitDailyResponse).</summary>
    public sealed class DailySubmitResult
    {
        public bool Ok;

        /// <summary>"offline" (queued to retry) or the server's reason for refusing the run.</summary>
        public string Error = "";
        public int Distance;
        public int BestDistance;
        public bool NewBest;
        public int Rank;
        public int PreviousRank;
        public int LeadersPassed;
        public string NextLeaderName = "";
        public int NextLeaderDistance;
        public bool Corrected;
        public int NextRunIndex;

        /// <summary>Yesterday's run sent just after midnight: kept as that day's best and ghosts, but not ranked.</summary>
        public bool BoardClosed;

        /// <summary>Naughty vs Nice: the side this run counted for ("" if none) and the metres it added.</summary>
        public string Team = "";
        public int TeamContribution;

        public static DailySubmitResult Failed(string error) => new DailySubmitResult { Error = error };
    }
}
