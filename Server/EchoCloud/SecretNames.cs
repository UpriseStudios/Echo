namespace EchoCloud
{
    /// <summary>
    /// Secret Manager names (a user choice): production reads its own `_PROD` secrets, every other environment the plain
    /// names. There is no fallback, so production can never pick up development's salt or key.
    /// </summary>
    public static class SecretNames
    {
        public const string DailySeedSalt = "DAILY_SEED_SALT";
        public const string LeaderboardsAdminKey = "LEADERBOARDS_ADMIN_KEY";
        const string Production = "production";

        public static string For(string environmentName, string name) =>
            environmentName == Production ? name + "_PROD" : name;
    }
}
