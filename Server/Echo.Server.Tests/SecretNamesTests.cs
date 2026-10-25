using EchoCloud;
using NUnit.Framework;

namespace Echo.Server.Tests
{
    /// <summary>Production reads its own _PROD secrets; development the plain names; nothing falls back.</summary>
    public class SecretNamesTests
    {
        [Test]
        public void Production_UsesTheProdSecrets_EverythingElseThePlainOnes()
        {
            Assert.AreEqual("DAILY_SEED_SALT_PROD", SecretNames.For("production", SecretNames.DailySeedSalt));
            Assert.AreEqual("LEADERBOARDS_ADMIN_KEY_PROD", SecretNames.For("production", SecretNames.LeaderboardsAdminKey));
            Assert.AreEqual("DAILY_SEED_SALT", SecretNames.For("development", SecretNames.DailySeedSalt));
            Assert.AreEqual("LEADERBOARDS_ADMIN_KEY", SecretNames.For("development", SecretNames.LeaderboardsAdminKey));
        }
    }
}
