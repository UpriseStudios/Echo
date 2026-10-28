namespace Echo.Services.Backend
{
    /// <summary>
    /// The Remote Config switches (Daily Haunt doc: kill switches for the Daily Haunt and server-stored challenges).
    /// Keys match Server/Config/echo.rc.
    /// </summary>
    public readonly struct BackendFlags
    {
        public const string DailyKey = "daily_enabled";
        public const string ServerChallengesKey = "server_challenges_enabled";

        /// <summary>Seasons doc: the season schedule as JSON (the game keeps a built-in copy for when it can't be read).</summary>
        public const string SeasonsKey = "seasons";

        /// <summary>The error Cloud Code answers with when the endpoint's switch is off (it checks them too).</summary>
        public const string Disabled = "disabled";

        public readonly bool DailyEnabled;
        public readonly bool ServerChallengesEnabled;

        /// <summary>The "seasons" schedule, or null when it wasn't read.</summary>
        public readonly string SeasonsJson;

        public BackendFlags(bool dailyEnabled, bool serverChallengesEnabled, string seasonsJson = null)
        {
            DailyEnabled = dailyEnabled;
            ServerChallengesEnabled = serverChallengesEnabled;
            SeasonsJson = seasonsJson;
        }

        public static BackendFlags Off => default;
    }
}
