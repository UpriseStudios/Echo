namespace Echo.Services.Backend
{
    /// <summary>SubmitGraveyardRun's answer (mirrors Server/EchoCloud/GraveyardSubmitResponse).</summary>
    public sealed class GraveyardSubmitResult
    {
        public bool Ok;

        /// <summary>"offline", "closed", "wrong-day", "ghosts", "run-index", or a rule broken.</summary>
        public string Error = "";
        public int Distance;
        public bool NewBest;
        public int BestToday, WeekTotal, DaysPlayed, DailyRank, WeeklyRank;
        public bool BoardClosed;

        public static GraveyardSubmitResult Failed(string error) => new GraveyardSubmitResult { Error = error };
    }
}
