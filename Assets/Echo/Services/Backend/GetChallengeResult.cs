namespace Echo.Services.Backend
{
    /// <summary>The server's answer to GetChallenge (mirrors Server/EchoCloud/GetChallengeResponse).</summary>
    public sealed class GetChallengeResult
    {
        public bool Ok;

        /// <summary>"not-found", "expired", "malformed" or "offline".</summary>
        public string Error = "";

        /// <summary>The challenger's run and ghosts as a Core.Net.ReelPacket.</summary>
        public string Packet = "";
        public string Name = "";
        public int Score;
        public int Distance;

        public static GetChallengeResult Failed(string error) => new GetChallengeResult { Error = error };
    }
}
