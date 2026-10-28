using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Echo.Core.Modes;
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

        /// <summary>Modes doc: the competitive modes' windows from the "modes" key (none when it's missing or unreadable).</summary>
        public IReadOnlyList<ModeWindow> Modes { get; }

        public FeatureSwitches(bool daily, bool serverChallenges, IReadOnlyList<ModeWindow> modes = null)
        {
            Daily = daily;
            ServerChallenges = serverChallenges;
            Modes = modes ?? new List<ModeWindow>();
        }

        public static FeatureSwitches From(IDictionary<string, object> settings) =>
            new FeatureSwitches(On(settings, DailyKey), On(settings, ServerChallengesKey), ModeSchedule.Parse(Json(settings, ModeSchedule.Key)));

        // A JSON value arrives as a JSON token or as text, depending on the serializer.
        static string Json(IDictionary<string, object> settings, string key)
        {
            if (settings == null || !settings.TryGetValue(key, out object value) || value == null) return null;
            return value switch
            {
                string text => text,
                JsonElement element => element.GetRawText(),
                _ => value.ToString(),
            };
        }

        // Values arrive as bools or JSON tokens depending on the serializer; both print as true/True.
        static bool On(IDictionary<string, object> settings, string key) =>
            settings != null && settings.TryGetValue(key, out object value) && value != null
            && bool.TryParse(value.ToString(), out bool on) && on;

        public static async Task<FeatureSwitches> ReadAsync(IExecutionContext context, IGameApiClient api)
        {
            try
            {
                var response = await api.RemoteConfigSettings.AssignSettingsGetAsync(context, context.AccessToken, context.ProjectId,
                    context.EnvironmentId, "settings", new List<string> { DailyKey, ServerChallengesKey, ModeSchedule.Key });
                return From(response.Data.Configs?.Settings);
            }
            catch (System.Exception)
            {
                return new FeatureSwitches(false, false);
            }
        }
    }
}
