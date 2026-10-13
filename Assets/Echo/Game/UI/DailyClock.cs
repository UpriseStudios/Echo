namespace Echo.Game.UI
{
    /// <summary>
    /// Daily Haunt (a user addition): in the last 5 minutes before the board closes at 00:00 UTC, the lobby and the
    /// HUD say "Board closes in m:ss".
    /// </summary>
    public static class DailyClock
    {
        public const int ClosingSeconds = 300;
        const string Prefix = "Board closes in ";

        /// <summary>The seconds to show, or 0 outside the closing window.</summary>
        public static int Closing(int secondsLeft) => secondsLeft > 0 && secondsLeft <= ClosingSeconds ? secondsLeft : 0;

        public static string Line(int seconds) => $"{Prefix}{seconds / 60}:{seconds % 60:00}";

        /// <summary>Line(seconds) into a 20-character buffer, without allocating (the window is under 10 minutes).</summary>
        public static void Write(int seconds, char[] buffer)
        {
            for (int i = 0; i < Prefix.Length; i++) buffer[i] = Prefix[i];
            int m = seconds / 60 % 10, s = seconds % 60;
            buffer[Prefix.Length] = (char)('0' + m);
            buffer[Prefix.Length + 1] = ':';
            buffer[Prefix.Length + 2] = (char)('0' + s / 10);
            buffer[Prefix.Length + 3] = (char)('0' + s % 10);
        }
    }
}
