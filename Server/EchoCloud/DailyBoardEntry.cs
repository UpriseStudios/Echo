namespace EchoCloud
{
    /// <summary>A line of the daily lobby's top 10 (no player ids leave the server).</summary>
    public sealed class DailyBoardEntry
    {
        public int Rank { get; set; }
        public string Name { get; set; } = "";
        public int Distance { get; set; }
        public bool IsYou { get; set; }
    }
}
