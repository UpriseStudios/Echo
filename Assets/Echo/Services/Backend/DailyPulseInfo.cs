namespace Echo.Services.Backend
{
    /// <summary>Ghost Town's Daily Haunt card (mirrors Server/EchoCloud/DailyPulse): runners today, the leader, your place.</summary>
    public sealed class DailyPulseInfo
    {
        public int Runners;
        public string LeaderName = "";
        public int LeaderDistance;

        /// <summary>Your rank today; 0 before your first run.</summary>
        public int Rank;
        public int BestDistance;
        public long SecondsLeft;
    }
}
