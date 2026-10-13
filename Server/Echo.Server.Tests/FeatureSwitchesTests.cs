using System.Collections.Generic;
using System.Text.Json;
using EchoCloud;
using NUnit.Framework;

namespace Echo.Server.Tests
{
    /// <summary>The server's reading of the Remote Config switches: the same keys as the game, and off unless on.</summary>
    public class FeatureSwitchesTests
    {
        [Test]
        public void TheSwitches_AreOn_OnlyWhenRemoteConfigSaysTrue()
        {
            var on = FeatureSwitches.From(new Dictionary<string, object> { { "daily_enabled", true }, { "server_challenges_enabled", true } });
            Assert.IsTrue(on.Daily);
            Assert.IsTrue(on.ServerChallenges);

            var split = FeatureSwitches.From(new Dictionary<string, object> { { "daily_enabled", false }, { "server_challenges_enabled", true } });
            Assert.IsFalse(split.Daily);
            Assert.IsTrue(split.ServerChallenges);

            var json = FeatureSwitches.From(new Dictionary<string, object>
            {
                { "daily_enabled", JsonDocument.Parse("true").RootElement }, { "server_challenges_enabled", JsonDocument.Parse("false").RootElement },
            });
            Assert.IsTrue(json.Daily, "a JSON token");
            Assert.IsFalse(json.ServerChallenges);
        }

        [Test]
        public void AMissingKey_NoSettings_OrNonsense_IsOff()
        {
            Assert.IsFalse(FeatureSwitches.From(null).Daily);
            Assert.IsFalse(FeatureSwitches.From(new Dictionary<string, object>()).ServerChallenges);
            Assert.IsFalse(FeatureSwitches.From(new Dictionary<string, object> { { "daily_enabled", "maybe" } }).Daily);
            Assert.IsFalse(FeatureSwitches.From(new Dictionary<string, object> { { "daily_enabled", null } }).Daily);
        }

        [Test]
        public void TheKeys_MatchTheGamesAndTheDeployedConfig()
        {
            string rc = System.IO.File.ReadAllText(System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Config", "echo.rc"));
            StringAssert.Contains("\"" + FeatureSwitches.DailyKey + "\"", rc);
            StringAssert.Contains("\"" + FeatureSwitches.ServerChallengesKey + "\"", rc);
        }
    }
}
