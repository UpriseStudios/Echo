namespace Echo.Services.Backend
{
    /// <summary>StartOneLife's answer (mirrors Server/EchoCloud/OneLifeStartResponse): the attempt is used now.</summary>
    public sealed class OneLifeStart
    {
        public bool Ok;

        /// <summary>"closed", "used", "offline".</summary>
        public string Error = "";
        public string Date = "";
        public string Token = "";
        public uint Seed;
        public string ContentVersion = "";
        public string GhostsPacket = "";

        public static OneLifeStart Failed(string error) => new OneLifeStart { Error = error };
    }
}
