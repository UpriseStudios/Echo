using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace EchoCloud
{
    /// <summary>
    /// The Remote Config switches (2.0 brief rule 6), read on the server too so a switch turned off closes the
    /// endpoints, not just the game's buttons. Same keys and defaults as the client (BackendFlags): a missing key, or
    /// Remote Config not answering, is off.
    /// </summary>
    public sealed class FeatureSwitches
    {
        public const string DailyKey = "daily_enabled";
        public const string ServerChallengesKey = "server_challenges_enabled";
        public const string Disabled = "disabled";

        public bool Daily { get; }
        public bool ServerChallenges { get; }

        public FeatureSwitches(bool daily, bool serverChallenges)
        {
            Daily = daily;
            ServerChallenges = serverChallenges;
        }

        public static FeatureSwitches From(IDictionary<string, object> settings) =>
            new FeatureSwitches(On(settings, DailyKey), On(settings, ServerChallengesKey));

        // Values arrive as bools or JSON tokens depending on the serializer; both print as true/True.
        static bool On(IDictionary<string, object> settings, string key) =>
            settings != null && settings.TryGetValue(key, out object value) && value != null
            && bool.TryParse(value.ToString(), out bool on) && on;

        public static async Task<FeatureSwitches> ReadAsync(IExecutionContext context, IGameApiClient api)
        {
            try
            {
                var response = await api.RemoteConfigSettings.AssignSettingsGetAsync(context, context.AccessToken, context.ProjectId,
                    context.EnvironmentId, "settings", new List<string> { DailyKey, ServerChallengesKey });
                return From(response.Data.Configs?.Settings);
            }
            catch (System.Exception)
            {
                return new FeatureSwitches(false, false);
            }
        }
    }
}
