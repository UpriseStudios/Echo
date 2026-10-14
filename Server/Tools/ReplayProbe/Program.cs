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
            if (args.Length > 0 && args[0] == "grace") return await ProbeGrace(http, projectId, token, content);
            if (args.Length > 0 && args[0] == "names") return await ProbeNames(http, token);
            if (args.Length > 0 && args[0] == "delete") return await ProbeDelete(http, projectId, token, content);
            if (args.Length > 0 && args[0] == "tamper") return await TamperProbe.Run(http, projectId, token, content);
            if (args.Length > 0 && args[0] == "switches") return await ProbeSwitches(http, projectId, token, args.Length > 1 && args[1] == "off");
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

        internal static async Task<string> SignInAnonymously(HttpClient http, string projectId)
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
                    date = d.GetProperty("date").GetString(), leaderVersion = d.GetProperty("leaderVersion").GetString(), packet = ReelPacket.Encode(reel),
                });

            var (dA, reelA, _) = await Play(tokenA, 100);
            Console.WriteLine($"Today {dA.GetProperty("date").GetString()}, seed {dA.GetProperty("seed")}, leader set {dA.GetProperty("leaderVersion").GetString()[..12]}, {dA.GetProperty("board").GetArrayLength()} on the board");
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

        // The grace window against a real archive: the development board is archived and reset (so an archived
        // version exists), then a fresh player sends today's run as if it were 00:02 tomorrow. The server takes the
        // grace path: the run is replayed and kept, but UGS archives are read-only, so it lands on neither board.
        static async Task<int> ProbeGrace(HttpClient http, string projectId, string token, GameContent content)
        {
            Console.WriteLine("Archiving and resetting the development daily board: " + Ugs($"leaderboards reset daily --archive -e {Environment}"));
            JsonElement d = await Call(http, projectId, token, "GetDaily", new { });
            ReelPacket.TryDecode(d.GetProperty("ghostsPacket").GetString(), out Echo.Core.Reels.Reel ghosts);
            Course course = content.CourseFor((uint)d.GetProperty("seed").GetInt64());
            Echo.Core.Reels.Reel reel = null;
            for (uint s = 300; reel == null; s++)
            {
                var run = Echo.Core.Daily.DailyRules.Start(course,
                    Echo.Core.Daily.DailyRules.Ghosts(ghosts.Ghosts.Where(g => g.Leader), ghosts.Ghosts.Where(g => !g.Leader)),
                    d.GetProperty("nextRunIndex").GetInt32(), content.Config);
                reel = ParityRuns.Drive(run, course, s, content.Config, content.ContentVersion).Reel;
            }
            DateTime midnight = DateTime.UtcNow.Date.AddDays(1);
            string late = midnight.AddMinutes(2).ToString("yyyy-MM-ddTHH:mm:ssZ");
            Console.WriteLine($"Run on {d.GetProperty("date").GetString()}: {reel.Distance} m, sent as if at {late}");
            JsonElement r;
            try
            {
                r = await Call(http, projectId, token, "SubmitDailyRun", new
                {
                    date = d.GetProperty("date").GetString(), leaderVersion = d.GetProperty("leaderVersion"),
                    packet = ReelPacket.Encode(reel), dateOverride = late,
                });
            }
            catch (Exception e)
            {
                Console.WriteLine("The archive refused the write: " + e.Message);
                return 1;
            }
            Console.WriteLine($"Submit: ok {r.GetProperty("ok")} {r.GetProperty("error").GetString()}, server {r.GetProperty("distance")} m, new best {r.GetProperty("newBest")}");

            JsonElement next = await Call(http, projectId, token, "GetDaily", new { dateOverride = midnight.AddMinutes(3).ToString("yyyy-MM-ddTHH:mm:ssZ") });
            int yesterdayRank = next.GetProperty("yesterdayRank").GetInt32();
            bool onToday = next.GetProperty("board").EnumerateArray().Any(e => e.GetProperty("isYou").GetBoolean());
            Console.WriteLine($"Archived board: player's rank {yesterdayRank}, winner {next.GetProperty("yesterdayWinner").GetString()} {next.GetProperty("yesterdayWinnerDistance")} m; on the live board too: {onToday}");
            bool good = r.GetProperty("ok").GetBoolean() && r.GetProperty("boardClosed").GetBoolean() && yesterdayRank == 0 && !onToday;
            Console.WriteLine(good ? "\nThe grace run was accepted and kept with the board closed: it is on neither board." : "\nTHE GRACE RUN WAS NOT HANDLED AS EXPECTED.");
            return good ? 0 : 1;
        }

        // The public daily board shows the UGS player name. The game never sets one, so it stays auto-generated; this
        // checks a player can't rename themselves by calling the Player Names API directly (Access Control denies it).
        static async Task<int> ProbeNames(HttpClient http, string token)
        {
            string payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            string playerId = JsonDocument.Parse(Convert.FromBase64String(payload)).RootElement.GetProperty("sub").GetString();
            async Task<HttpResponseMessage> Send(HttpMethod method, string body)
            {
                var request = new HttpRequestMessage(method, $"https://social.services.api.unity.com/v1/names/{playerId}");
                request.Headers.Add("Authorization", "Bearer " + token);
                if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                return await http.SendAsync(request);
            }
            HttpResponseMessage got = await Send(HttpMethod.Get, null);
            Console.WriteLine($"Player {playerId}: name {(int)got.StatusCode} {await got.Content.ReadAsStringAsync()}");
            HttpResponseMessage put = await Send(HttpMethod.Post, "{\"name\":\"ProbeRename\"}");
            Console.WriteLine($"Rename attempt: {(int)put.StatusCode} {await put.Content.ReadAsStringAsync()}");
            HttpResponseMessage after = await Send(HttpMethod.Get, null);
            Console.WriteLine($"Name now: {(int)after.StatusCode} {await after.Content.ReadAsStringAsync()}");
            bool blocked = (int)put.StatusCode == 403;
            Console.WriteLine(blocked ? "\nPlayers can't rename themselves." : "\nA PLAYER CAN SET THEIR OWN NAME.");
            return blocked ? 0 : 1;
        }

        // The Remote Config switches as Cloud Code sees them: with "off", every Daily Haunt and challenge endpoint
        // should answer "disabled"; otherwise none should.
        static async Task<int> ProbeSwitches(HttpClient http, string projectId, string token, bool expectOff)
        {
            var answers = new (string function, JsonElement output)[]
            {
                ("GetDaily", await Call(http, projectId, token, "GetDaily", new { })),
                ("SubmitDailyRun", await Call(http, projectId, token, "SubmitDailyRun", new { date = "2026-01-01", leaderVersion = "", packet = "x" })),
                ("CreateChallenge", await Call(http, projectId, token, "CreateChallenge", new { packet = "x", name = "Probe" })),
                ("GetChallenge", await Call(http, projectId, token, "GetChallenge", new { ownerId = "x", challengeId = "y" })),
            };
            bool good = true;
            foreach (var (function, output) in answers)
            {
                string error = output.GetProperty("error").GetString();
                Console.WriteLine($"{function}: error '{error}'");
                good &= (error == "disabled") == expectOff;
            }
            Console.WriteLine(good ? $"\nEvery endpoint {(expectOff ? "is closed" : "is open")}, as its switch says." : "\nAN ENDPOINT IGNORED ITS SWITCH.");
            return good ? 0 : 1;
        }

        // Settings › Delete online data: a player plays today and sends a challenge, then deletes; their day, their
        // challenge and (with the admin key in Secret Manager) their live score are gone.
        static async Task<int> ProbeDelete(HttpClient http, string projectId, string token, GameContent content)
        {
            JsonElement d = await Call(http, projectId, token, "GetDaily", new { });
            ReelPacket.TryDecode(d.GetProperty("ghostsPacket").GetString(), out Echo.Core.Reels.Reel ghosts);
            Course course = content.CourseFor((uint)d.GetProperty("seed").GetInt64());
            Echo.Core.Reels.Reel reel = null;
            for (uint s = 700; reel == null; s++)
                reel = ParityRuns.Drive(Echo.Core.Daily.DailyRules.Start(course,
                    Echo.Core.Daily.DailyRules.Ghosts(ghosts.Ghosts.Where(g => g.Leader), ghosts.Ghosts.Where(g => !g.Leader)),
                    d.GetProperty("nextRunIndex").GetInt32(), content.Config), course, s, content.Config, content.ContentVersion).Reel;
            JsonElement sent = await Call(http, projectId, token, "SubmitDailyRun",
                new { date = d.GetProperty("date").GetString(), leaderVersion = d.GetProperty("leaderVersion").GetString(), packet = ReelPacket.Encode(reel) });
            Course other = content.CourseFor(12345);
            Echo.Core.Reels.Reel run = null;
            foreach (uint s in ParityRuns.Seeds(80, 99))
                if ((run = ParityRuns.Play(other, ParityRuns.Ghosts(other, 1, 99, content.Config), s, content.Config, content.ContentVersion).Reel) != null) break;
            JsonElement made = await Call(http, projectId, token, "CreateChallenge", new { packet = ReelPacket.Encode(run), name = "Probe" });
            Console.WriteLine($"Before: daily best {sent.GetProperty("bestDistance")} m, rank {sent.GetProperty("rank")}, challenge {made.GetProperty("challengeId").GetString()}");

            JsonElement deleted = await Call(http, projectId, token, "DeleteMyData", new { });
            Console.WriteLine($"DeleteMyData: ok {deleted.GetProperty("ok")}, live score deleted {deleted.GetProperty("liveScoreDeleted")} {deleted.GetProperty("liveScoreNote").GetString()}");
            JsonElement after = await Call(http, projectId, token, "GetDaily", new { });
            JsonElement challenge = await Call(http, projectId, token, "GetChallenge",
                new { ownerId = made.GetProperty("ownerId").GetString(), challengeId = made.GetProperty("challengeId").GetString() });
            bool onBoard = after.GetProperty("board").EnumerateArray().Any(e => e.GetProperty("isYou").GetBoolean());
            Console.WriteLine($"After: daily best {after.GetProperty("bestDistance")} m, on the board {onBoard}, challenge {challenge.GetProperty("error").GetString()}");
            bool good = deleted.GetProperty("ok").GetBoolean() && after.GetProperty("bestDistance").GetInt32() == 0
                && challenge.GetProperty("error").GetString() == "not-found" && onBoard == !deleted.GetProperty("liveScoreDeleted").GetBoolean();
            Console.WriteLine(good ? "\nThe player's online data was deleted" + (deleted.GetProperty("liveScoreDeleted").GetBoolean() ? ", live score included." : "; the live score stays until LEADERBOARDS_ADMIN_KEY is in Secret Manager.") : "\nDELETION DID NOT DO WHAT IT SAYS.");
            return good ? 0 : 1;
        }

        static Task<JsonElement> Call(HttpClient http, string projectId, string token, string function, string packet) =>
            Call(http, projectId, token, function, new { packet });

        internal static async Task<JsonElement> Call(HttpClient http, string projectId, string token, string function, object parameters)
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

        internal static string Ugs(string arguments)
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
