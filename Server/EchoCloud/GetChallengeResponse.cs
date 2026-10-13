namespace EchoCloud
{
    /// <summary>GetChallenge's answer: the stored run and ghosts as a ReelPacket, the sender's name and target.</summary>
    public sealed class GetChallengeResponse
    {
        public bool Ok { get; set; }

        /// <summary>"not-found", "expired" or "malformed".</summary>
        public string Error { get; set; } = "";
        public string Packet { get; set; } = "";
        public string Name { get; set; } = "";
        public int Score { get; set; }
        public int Distance { get; set; }
    }
}
