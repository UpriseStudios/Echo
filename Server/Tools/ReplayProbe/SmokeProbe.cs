using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Echo.Server.Tools
{
    /// <summary>
    /// A deploy check that leaves the public board alone, for development or (deliberately) production: one anonymous
    /// test player checks the switches, the daily seed salt and content version, that the development-only endpoint is
    /// closed in production, that direct writes are refused, and that the Leaderboards admin key works, then deletes its
    /// own data. It submits no runs and sends no challenges.
    /// </summary>
    public static class SmokeProbe
    {
        const string Module = "EchoCloud";
        static int _failed;

        public static async Task<int> Run(HttpClient http, string projectId, string token, string contentVersion, string environment)
        {
            _failed = 0;
            Console.WriteLine($"Smoke check of the {environment} backend\n");

            // The Daily Haunt: its switch, the seed salt (GetDaily fails without it) and the content version.
            (JsonElement? daily, string dailyFault) = await TryCall(http, projectId, token, "GetDaily", new { });
            if (daily == null) Check("daily: GetDaily answers", false, dailyFault + $" ({(environment == "production" ? "DAILY_SEED_SALT_PROD" : "DAILY_SEED_SALT")} missing from Secret Manager?)");
            else if (daily.Value.GetProperty("error").GetString() == "disabled") Console.WriteLine("  INFO  daily: switched off in Remote Config");
            else
            {
                Check("daily: today's course", daily.Value.GetProperty("ok").GetBoolean() && daily.Value.GetProperty("seed").GetInt64() != 0,
                    $"{daily.Value.GetProperty("date").GetString()}, seed set, {daily.Value.GetProperty("board").GetArrayLength()} on the board");
                string serverContent = daily.Value.GetProperty("contentVersion").GetString();
                Check("daily: content version matches this build", serverContent == contentVersion, $"server {serverContent}, here {contentVersion}");
            }

            // Server challenges: the switch, and an unknown link refused cleanly.
            (JsonElement? challenge, string challengeFault) = await TryCall(http, projectId, token, "GetChallenge", new { ownerId = "nobody", challengeId = "nothing" });
            string challengeError = challenge?.GetProperty("error").GetString();
            if (challengeError == "disabled") Console.WriteLine("  INFO  challenges: switched off in Remote Config");
            else Check("challenges: an unknown link is refused", challengeError == "not-found" || challengeError == "malformed", challengeError ?? challengeFault);

            // ReplayCheck is a development tool: production must refuse it.
            (JsonElement? replay, string replayFault) = await TryCall(http, projectId, token, "ReplayCheck", new { packet = "x" });
            string replayError = replay?.GetProperty("error").GetString();
            Check("ReplayCheck is development-only", environment == "production" ? replayError == "development-only" : replayError != "development-only",
                replayError ?? replayFault);

            // Access Control: players can't write scores, Cloud Save or their name directly.
            string player = PlayerId(token);
            async Task<int> Send(HttpMethod method, string url, string body)
            {
                var request = new HttpRequestMessage(method, url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                request.Headers.Add("Authorization", "Bearer " + token);
                return (int)(await http.SendAsync(request)).StatusCode;
            }
            int score = await Send(HttpMethod.Post, $"https://leaderboards.services.api.unity.com/v1/projects/{projectId}/leaderboards/daily/scores/players/{player}", "{\"score\":1}");
            Check("a leaderboard write is refused", score == 403, score.ToString());
            int save = await Send(HttpMethod.Post, $"https://cloud-save.services.api.unity.com/v1/data/projects/{projectId}/players/{player}/items", "{\"key\":\"smoke\",\"value\":\"x\"}");
            Check("a Cloud Save write is refused", save == 403, save.ToString());
            int name = await Send(HttpMethod.Post, $"https://social.services.api.unity.com/v1/names/{player}", "{\"name\":\"Smoke\"}");
            Check("a name change is refused", name == 403, name.ToString());

            // Delete online data (never behind a switch); the note says whether the Leaderboards admin key works.
            (JsonElement? deleted, string deleteFault) = await TryCall(http, projectId, token, "DeleteMyData", new { });
            Check("Delete online data works", deleted?.GetProperty("ok").GetBoolean() == true, deleteFault ?? "ok");
            string note = deleted?.GetProperty("liveScoreNote").GetString() ?? "";
            Check("the Leaderboards admin key works", deleted != null && note == "", note == "" ? (environment == "production" ? "LEADERBOARDS_ADMIN_KEY_PROD" : "LEADERBOARDS_ADMIN_KEY") + " found" : note);

            Console.WriteLine(_failed == 0 ? $"\nThe {environment} backend is ready." : $"\n{_failed} CHECK(S) FAILED.");
            return _failed == 0 ? 0 : 1;
        }

        static void Check(string what, bool ok, string detail)
        {
            Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {what}: {detail}");
            if (!ok) _failed++;
        }

        // The module's answer, or why there wasn't one (a function that threw, or no module deployed).
        static async Task<(JsonElement?, string)> TryCall(HttpClient http, string projectId, string token, string function, object parameters)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://cloud-code.services.api.unity.com/v1/projects/{projectId}/modules/{Module}/{function}")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { @params = parameters }), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Authorization", "Bearer " + token);
            HttpResponseMessage response = await http.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                string message = body.Length > 300 ? body.Substring(0, 300) : body;
                return (null, $"HTTP {(int)response.StatusCode}: {message}");
            }
            return (JsonDocument.Parse(body).RootElement.GetProperty("output").Clone(), null);
        }

        static string PlayerId(string token)
        {
            string payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            return JsonDocument.Parse(Convert.FromBase64String(payload)).RootElement.GetProperty("sub").GetString();
        }
    }
}
