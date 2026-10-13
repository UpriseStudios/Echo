namespace Echo.Services.Backend
{
    /// <summary>The server's answer to CreateChallenge (mirrors Server/EchoCloud/CreateChallengeResponse).</summary>
    public sealed class CreateChallengeResult
    {
        public bool Ok;

        /// <summary>Why it failed: the server's reason, or "offline".</summary>
        public string Error = "";
        public string OwnerId = "";
        public string ChallengeId = "";
        public string Link = "";
        public int Score;
        public int Distance;
        public bool Corrected;

        public static CreateChallengeResult Failed(string error) => new CreateChallengeResult { Error = error };
    }
}
