namespace EchoCloud
{
    /// <summary>
    /// Ghost Town's Daily Haunt card (a user decision: the hub shows the community live): how many have run today,
    /// today's leader, and the player's own place.
    /// </summary>
    public sealed class DailyPulse
    {
        public int Runners { get; set; }
        public string LeaderName { get; set; } = "";
        public int LeaderDistance { get; set; }
        /// <summary>The player's rank today; 0 before their first run.</summary>
        public int Rank { get; set; }
        public int BestDistance { get; set; }
        public long SecondsLeft { get; set; }
    }
}
