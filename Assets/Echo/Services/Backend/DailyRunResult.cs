namespace Echo.Services.Backend
{
    /// <summary>A run on today's board to watch (mirrors Server/EchoCloud/GetDailyRunResponse): a ReelPacket, ghosts and all.</summary>
    public sealed class DailyRunResult
    {
        public bool Ok;

        /// <summary>"offline", "not-found", "not-available" or "disabled".</summary>
        public string Error = "";
        public string Packet = "";
        public string Name = "";
        public int Rank;
        public int Distance;

        public static DailyRunResult Failed(string error) => new DailyRunResult { Error = error };
    }
}
