namespace EchoCloud
{
    /// <summary>StartOneLife: today's attempt is used now; the run, its course and its leader ghosts.</summary>
    public sealed class OneLifeStartResponse
    {
        public bool Ok { get; set; }

        /// <summary>"closed", "used" (today's attempt is gone), "content-version".</summary>
        public string Error { get; set; } = "";
        public string Date { get; set; } = "";

        /// <summary>Single use: SubmitOneLife needs it.</summary>
        public string Token { get; set; } = "";
        public uint Seed { get; set; }
        public string ContentVersion { get; set; } = "";

        /// <summary>Today's leader ghosts as a ReelPacket.</summary>
        public string GhostsPacket { get; set; } = "";
    }
}
