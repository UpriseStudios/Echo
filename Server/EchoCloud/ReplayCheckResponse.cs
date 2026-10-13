namespace EchoCloud
{
    /// <summary>What ReplayCheck reports: the server's own result for a run, or why it would not replay it.</summary>
    public sealed class ReplayCheckResponse
    {
        public bool Accepted { get; set; }

        /// <summary>Why a run was refused: "malformed", "content-version" or "development-only". Empty when accepted.</summary>
        public string Error { get; set; } = "";

        public string ServerContentVersion { get; set; } = "";
        public int Distance { get; set; }
        public int Score { get; set; }
        public int DeathTick { get; set; }
        public bool Finished { get; set; }

        /// <summary>The distance the client claimed, echoed so a mismatch is plain to see.</summary>
        public int ClaimedDistance { get; set; }
        public bool Matches => Accepted && Finished && Distance == ClaimedDistance;
    }
}
