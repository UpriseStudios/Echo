namespace EchoCloud
{
    /// <summary>A run on today's board to watch: the whole reel (its taps and the ghosts it faced), replayed exactly.</summary>
    public sealed class GetDailyRunResponse
    {
        public bool Ok { get; set; }

        /// <summary>"not-found" (no one at that rank), "not-available" (kept before runs were stored whole), "disabled".</summary>
        public string Error { get; set; } = "";
        public string Packet { get; set; } = "";
        public string Name { get; set; } = "";
        public int Rank { get; set; }
        public int Distance { get; set; }
    }
}
