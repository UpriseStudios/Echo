namespace Echo.Core.Daily
{
    /// <summary>
    /// The player's Daily Haunt history for the online achievements (version 2.0). Days are whole UTC days numbered
    /// by the server, so Core never reads a clock.
    /// </summary>
    public sealed class DailyProgress
    {
        /// <summary>Different days the Daily Haunt has been played on.</summary>
        public int DaysPlayed;

        /// <summary>The last day played, or -1 before the first.</summary>
        public int LastDay = -1;

        /// <summary>Days played in a row, up to and including LastDay.</summary>
        public int Streak;
    }
}
