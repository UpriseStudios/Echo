namespace EchoCloud
{
    /// <summary>CreateChallenge's answer: the link to share, and the score the server confirmed by replaying.</summary>
    public sealed class CreateChallengeResponse
    {
        public bool Ok { get; set; }

        /// <summary>"malformed", "content-version", "too-many-ghosts", "unfinished", "duel" or "rate-limit".</summary>
        public string Error { get; set; } = "";
        public string OwnerId { get; set; } = "";
        public string ChallengeId { get; set; } = "";
        public string Link { get; set; } = "";
        public int Score { get; set; }
        public int Distance { get; set; }

        /// <summary>The client claimed a different result; the server's own is stored (Daily Haunt doc anti-cheat).</summary>
        public bool Corrected { get; set; }
    }
}
