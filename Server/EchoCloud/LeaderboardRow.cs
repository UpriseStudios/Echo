namespace EchoCloud
{
    /// <summary>One place on the daily leaderboard: rank from 1, the player's auto-generated name and their distance.</summary>
    public sealed class LeaderboardRow
    {
        public int Rank { get; set; }
        public string PlayerId { get; set; } = "";
        public string Name { get; set; } = "";
        public int Distance { get; set; }
    }
}
