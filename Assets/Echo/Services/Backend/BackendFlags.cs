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

        /// <summary>The error Cloud Code answers with when the endpoint's switch is off (it checks them too).</summary>
        public const string Disabled = "disabled";

        public readonly bool DailyEnabled;
        public readonly bool ServerChallengesEnabled;

        public BackendFlags(bool dailyEnabled, bool serverChallengesEnabled)
        {
            DailyEnabled = dailyEnabled;
            ServerChallengesEnabled = serverChallengesEnabled;
        }

        public static BackendFlags Off => default;
    }
}
