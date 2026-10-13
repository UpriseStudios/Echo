using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Net;
using Echo.Server;
using Echo.Tests.EditMode;

namespace Echo.Server.Tools
{
    /// <summary>
    /// Sends runs played with the game's own code to the deployed EchoCloud.ReplayCheck in the UGS development
    /// environment and prints the device and server results side by side. Development only, by construction: it
    /// signs in with UnityEnvironment "development" and ReplayCheck refuses every other environment.
    /// </summary>
    public static class Program
    {
        const string Environment = "development";
        const string Module = "EchoCloud";

        public static async Task<int> Main(string[] args)
        {
            bool challengeMode = args.Length > 0 && args[0] == "challenge";
            int count = !challengeMode && args.Length > 0 && int.TryParse(args[0], out int n) ? n : 10;
            string projectId = System.Environment.GetEnvironmentVariable("ECHO_UGS_PROJECT_ID") ?? Ugs("config get project-id");
            using var http = new HttpClient();

            string token = await SignInAnonymously(http, projectId);
            Console.WriteLine($"Signed in as an anonymous test player in {Environment}.");

            GameContent content = GameContent.Shared;
            Console.WriteLine($"Content version {content.ContentVersion}\n");
            if (challengeMode) return await ProbeChallenges(http, projectId, token, content);
            if (args.Length > 0 && args[0] == "daily") return await ProbeDaily(http, projectId, token, content);
            Console.WriteLine("  #   course   ghosts  device m  server m  device score  server score  match");
            int sent = 0, matched = 0;
            foreach (uint seed in ParityRuns.Seeds(count * 4, (uint)DateTime.UtcNow.Ticks))
            {
                Course course = content.CourseFor(seed % 100_000 + 1);
                GhostSet ghosts = ParityRuns.Ghosts(course, (int)(seed % 6), seed, content.Config);
                ParityRuns.Played run = ParityRuns.Play(course, ghosts, seed, content.Config, content.ContentVersion);
                if (run.Reel == null) continue;

                JsonElement output = await Call(http, projectId, token, "ReplayCheck", ReelPacket.Encode(run.Reel));
                if (!output.GetProperty("accepted").GetBoolean())
                {
                    Console.WriteLine($"  refused: {output.GetProperty("error").GetString()} (server content {output.GetProperty("serverContentVersion").GetString()})");
                    return 1;
                }
                int distance = output.GetProperty("distance").GetInt32(), score = output.GetProperty("score").GetInt32();
                bool same = distance == run.Run.Distance && score == run.Run.Score && output.GetProperty("deathTick").GetInt32() == run.Run.DeathTick;
                sent++;
                if (same) matched++;
                Console.WriteLine($"{sent,3}   {run.Reel.Seed,6}   {ghosts.Count,6}  {run.Run.Distance,8}  {distance,8}  {run.Run.Score,12}  {score,12}  {(same ? "yes" : "NO")}");
                if (sent == count) break;
            }
            Console.WriteLine($"\n{matched} of {sent} runs replayed on the server to exactly the device's result.");
            return matched == sent ? 0 : 1;
        }

        static async Task<string> SignInAnonymously(HttpClient http, string projectId)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://player-auth.services.api.unity.com/v1/authentication/anonymous")
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("ProjectId", projectId);
            request.Headers.Add("UnityEnvironment", Environment);
            HttpResponseMessage response = await http.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new Exception($"sign-in failed: {(int)response.StatusCode} {body}");
            return JsonDocument.Parse(body).RootElement.GetProperty("idToken").GetString();
        }

        // CreateChallenge, then GetChallenge: the stored run comes back with its ghosts and the server's own score;
        // a faked score is corrected; an unknown id is refused.
        static async Task<int> ProbeChallenges(HttpClient http, string projectId, string token, GameContent content)
        {
            uint seed = (uint)DateTime.UtcNow.Ticks;
            Course course = content.CourseFor(seed % 100_000 + 1);
            GhostSet ghosts = ParityRuns.Ghosts(course, 4, seed, content.Config);
            Echo.Core.Reels.Reel reel = null;
            foreach (uint s in ParityRuns.Seeds(80, seed))
                if ((reel = ParityRuns.Play(course, ghosts, s, content.Config, content.ContentVersion).Reel) != null) break;
            int realScore = reel.Score;

            JsonElement created = await Call(http, projectId, token, "CreateChallenge", new { packet = ReelPacket.Encode(reel), name = "Probe" });
            Console.WriteLine($"CreateChallenge: ok {created.GetProperty("ok")}, link {created.GetProperty("link").GetString()}, score {created.GetProperty("score")} (device {realScore})");
            string owner = created.GetProperty("ownerId").GetString(), id = created.GetProperty("challengeId").GetString();
            JsonElement got = await Call(http, projectId, token, "GetChallenge", new { ownerId = owner, challengeId = id });
            ReelPacket.TryDecode(got.GetProperty("packet").GetString(), out Echo.Core.Reels.Reel back);
            Console.WriteLine($"GetChallenge: ok {got.GetProperty("ok")}, name {got.GetProperty("name").GetString()}, score {got.GetProperty("score")}, ghosts {back?.Ghosts.Count} (sent {reel.Ghosts.Count})");

            reel.Score = realScore + 50_000;
            JsonElement faked = await Call(http, projectId, token, "CreateChallenge", new { packet = ReelPacket.Encode(reel), name = "Cheat" });
            Console.WriteLine($"Faked score {reel.Score}: stored score {faked.GetProperty("score")}, corrected {faked.GetProperty("corrected")}");
            JsonElement missing = await Call(http, projectId, token, "GetChallenge", new { ownerId = owner, challengeId = "nothere" });
            Console.WriteLine($"Unknown id: ok {missing.GetProperty("ok")}, error {missing.GetProperty("error").GetString()}");

            bool good = created.GetProperty("ok").GetBoolean() && created.GetProperty("score").GetInt32() == realScore
                && got.GetProperty("ok").GetBoolean() && back != null && back.Ghosts.Count == reel.Ghosts.Count
                && faked.GetProperty("score").GetInt32() == realScore && faked.GetProperty("corrected").GetBoolean()
                && !missing.GetProperty("ok").GetBoolean();
            Console.WriteLine(good ? "\nChallenges work on the development server." : "\nSOMETHING DIDN'T MATCH.");
            return good ? 0 : 1;
        }

        // Two anonymous players play today's Daily Haunt: the first sets a best, the second meets them as a leader
        // ghost. A faked distance is corrected, and a date override (development only) gives tomorrow's course.
        static async Task<int> ProbeDaily(HttpClient http, string projectId, string tokenA, GameContent content)
        {
            async Task<(JsonElement daily, Echo.Core.Reels.Reel reel, Echo.Core.Reels.Reel ghosts)> Play(string token, uint style)
            {
                JsonElement d = await Call(http, projectId, token, "GetDaily", new { });
                ReelPacket.TryDecode(d.GetProperty("ghostsPacket").GetString(), out Echo.Core.Reels.Reel ghosts);
                Course course = content.CourseFor((uint)d.GetProperty("seed").GetInt64());
                for (uint s = style; ; s++)
                {
                    var run = Echo.Core.Daily.DailyRules.Start(course,
                        Echo.Core.Daily.DailyRules.Ghosts(ghosts.Ghosts.Where(g => g.Leader), ghosts.Ghosts.Where(g => !g.Leader)),
                        d.GetProperty("nextRunIndex").GetInt32(), content.Config);
                    ParityRuns.Played p = ParityRuns.Drive(run, course, s, content.Config, content.ContentVersion);
                    if (p.Reel != null) return (d, p.Reel, ghosts);
                }
            }
            async Task<JsonElement> Submit(string token, JsonElement d, Echo.Core.Reels.Reel reel) =>
                await Call(http, projectId, token, "SubmitDailyRun", new
                {
                    date = d.GetProperty("date").GetString(), leaderVersion = d.GetProperty("leaderVersion").GetInt32(), packet = ReelPacket.Encode(reel),
                });

            var (dA, reelA, _) = await Play(tokenA, 100);
            Console.WriteLine($"Today {dA.GetProperty("date").GetString()}, seed {dA.GetProperty("seed")}, leader set v{dA.GetProperty("leaderVersion")}, {dA.GetProperty("board").GetArrayLength()} on the board");
            JsonElement rA = await Submit(tokenA, dA, reelA);
            Console.WriteLine($"Player A: ok {rA.GetProperty("ok")} {rA.GetProperty("error").GetString()}, device {reelA.Distance} m, server {rA.GetProperty("distance")} m, new best {rA.GetProperty("newBest")}, rank {rA.GetProperty("rank")}");

            string tokenB = await SignInAnonymously(http, projectId);
            var (dB, reelB, ghostsB) = await Play(tokenB, 200);
            Console.WriteLine($"Player B meets {ghostsB.Ghosts.Count(g => g.Leader)} leader ghosts: {string.Join(", ", ghostsB.Ghosts.Where(g => g.Leader).Select(g => g.Name))}");
            reelB.Distance += 5000;
            JsonElement rB = await Submit(tokenB, dB, reelB);
            Console.WriteLine($"Player B claims {reelB.Distance} m: server {rB.GetProperty("distance")} m, corrected {rB.GetProperty("corrected")}, rank {rB.GetProperty("rank")}, next {rB.GetProperty("nextLeaderName").GetString()} {rB.GetProperty("nextLeaderDistance")} m");

            JsonElement tomorrow = await Call(http, projectId, tokenB, "GetDaily", new { dateOverride = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd") });
            Console.WriteLine($"Date override: {tomorrow.GetProperty("date").GetString()}, seed {tomorrow.GetProperty("seed")}");

            bool good = rA.GetProperty("ok").GetBoolean() && rA.GetProperty("distance").GetInt32() == reelA.Distance
                && ghostsB.Ghosts.Any(g => g.Leader)
                && rB.GetProperty("ok").GetBoolean() && rB.GetProperty("corrected").GetBoolean() && rB.GetProperty("distance").GetInt32() == reelB.Distance - 5000
                && tomorrow.GetProperty("seed").GetInt64() != dA.GetProperty("seed").GetInt64();
            Console.WriteLine(good ? "\nThe Daily Haunt works on the development server." : "\nSOMETHING DIDN'T MATCH.");
            return good ? 0 : 1;
        }

        static Task<JsonElement> Call(HttpClient http, string projectId, string token, string function, string packet) =>
            Call(http, projectId, token, function, new { packet });

        static async Task<JsonElement> Call(HttpClient http, string projectId, string token, string function, object parameters)
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://cloud-code.services.api.unity.com/v1/projects/{projectId}/modules/{Module}/{function}")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { @params = parameters }), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Authorization", "Bearer " + token);
            HttpResponseMessage response = await http.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new Exception($"{function} failed: {(int)response.StatusCode} {body}");
            return JsonDocument.Parse(body).RootElement.GetProperty("output").Clone();
        }

        static string Ugs(string arguments)
        {
            // On Windows the CLI is a .cmd shim, which only the command shell can start.
            bool windows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
            var start = windows
                ? new ProcessStartInfo("cmd.exe", "/c ugs " + arguments)
                : new ProcessStartInfo("ugs", arguments);
            start.RedirectStandardOutput = true;
            start.UseShellExecute = false;
            using Process p = Process.Start(start);
            string output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return output;
        }
    }
}
