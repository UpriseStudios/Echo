namespace Echo.Services.Backend
{
    /// <summary>
    /// The server's replay of a run (Cloud Code EchoCloud.ReplayCheck; mirrors Server/EchoCloud/ReplayCheckResponse).
    /// Error is set when the server refused the run, or when it could not be reached ("offline").
    /// </summary>
    public sealed class ReplayCheckResult
    {
        public bool Accepted;
        public string Error = "";
        public string ServerContentVersion = "";
        public int Distance;
        public int Score;
        public int DeathTick;
        public bool Finished;
        public int ClaimedDistance;

        public static ReplayCheckResult Failed(string error) => new ReplayCheckResult { Error = error };
    }
}
