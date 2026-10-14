using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Echo.Core.Courses;
using Echo.Core.Daily;
using Echo.Core.Ghosts;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Tests.EditMode;

namespace Echo.Server.Tools
{
    /// <summary>
    /// Phase 5 tamper tests, against the deployed module in development as an anonymous player: edited taps, a faked
    /// distance or score, an old date, an oversized payload and the other ways a client could lie, for the Daily Haunt
    /// and challenges, plus direct writes that Access Control must refuse. Every check prints PASS or FAIL.
    /// </summary>
    public static class TamperProbe
    {
        const string Module = "EchoCloud";
        static int _failed;

        public static async Task<int> Run(HttpClient http, string projectId, string token, GameContent content)
        {
            _failed = 0;
            await Daily(http, projectId, token, content);
            await Challenges(http, projectId, token, content);
            await DirectWrites(http, projectId, token);
            Console.WriteLine(_failed == 0 ? "\nEvery tamper attempt was refused or corrected." : $"\n{_failed} CHECK(S) FAILED.");
            return _failed == 0 ? 0 : 1;
        }

        static void Check(string what, bool ok, string detail)
        {
            Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {what}: {detail}");
            if (!ok) _failed++;
        }

        // ---- the Daily Haunt ----

        static async Task Daily(HttpClient http, string projectId, string token, GameContent content)
        {
            Console.WriteLine("Daily Haunt (SubmitDailyRun)");
            async Task<(JsonElement daily, Reel reel)> Play(uint style)
            {
                JsonElement d = await Program.Call(http, projectId, token, "GetDaily", new { });
                ReelPacket.TryDecode(d.GetProperty("ghostsPacket").GetString(), out Reel ghosts);
                Course course = content.CourseFor((uint)d.GetProperty("seed").GetInt64());
                for (uint s = style; ; s++)
                {
                    var run = DailyRules.Start(course, DailyRules.Ghosts(ghosts.Ghosts.Where(g => g.Leader), ghosts.Ghosts.Where(g => !g.Leader)),
                        d.GetProperty("nextRunIndex").GetInt32(), content.Config);
                    ParityRuns.Played p = ParityRuns.Drive(run, course, s, content.Config, content.ContentVersion);
                    if (p.Reel != null && p.Reel.Run.TapTicks.Length >= 4) return (d, p.Reel);
                }
            }
            async Task<JsonElement> Submit(JsonElement d, string packet, string date = null, string version = null)
            {
                await Task.Delay(2200);   // past the 2 s rate limit, so each check meets the rule it is aimed at
                return await Program.Call(http, projectId, token, "SubmitDailyRun", new
                {
                    date = date ?? d.GetProperty("date").GetString(), leaderVersion = version ?? d.GetProperty("leaderVersion").GetString(), packet,
                });
            }
            string Error(JsonElement r) => r.GetProperty("ok").GetBoolean() ? "accepted" : r.GetProperty("error").GetString();

            var (day, reel) = await Play(500);
            string today = day.GetProperty("date").GetString();

            // Refusals first: a refused run changes nothing, so the same day and ghosts serve them all.
            JsonElement old = await Submit(day, ReelPacket.Encode(reel), date: "2026-10-01");
            Check("old date", Error(old) == "wrong-day", Error(old));
            JsonElement tomorrow = await Submit(day, ReelPacket.Encode(reel), date: DateTime.Parse(today).AddDays(1).ToString("yyyy-MM-dd"));
            Check("tomorrow's date", Error(tomorrow) == "wrong-day", Error(tomorrow));
            await Task.Delay(2200);
            (bool refused, string why) oversized = await Oversized(http, projectId, token, "SubmitDailyRun",
                new { date = today, leaderVersion = day.GetProperty("leaderVersion").GetString(), packet = "x" + new string('A', ReelPacket.MaxChars) });
            Check("oversized payload", oversized.refused, oversized.why);
            // Other ghosts than the ones it was given: none at all, or one extra when it was given none.
            JsonElement crowd = await Submit(day, Edit(reel, r =>
            {
                if (r.Ghosts.Count > 0) r.Ghosts.Clear();
                else r.Ghosts.Add(new GhostRecord { RunIndex = 999, DeathTick = 60, TapTicks = new int[0], ReleaseTicks = new int[0] });
            }));
            Check($"a different crowd ({reel.Ghosts.Count} ghosts given)", Error(crowd) == "ghosts", Error(crowd));
            JsonElement shield = await Submit(day, Edit(reel, r => r.ShieldTick = 30));
            Check("a power-up in a daily run", Error(shield) == "power-ups", Error(shield));
            JsonElement seed = await Submit(day, Edit(reel, r => r.Seed ^= 77));
            Check("another course's seed", Error(seed) == "seed", Error(seed));
            JsonElement version = await Submit(day, ReelPacket.Encode(reel), version: "0000");
            Check("an unknown leader set", Error(version) == "leader-version", Error(version));
            JsonElement garbage = await Submit(day, "not a packet");
            Check("garbage", Error(garbage) == "malformed", Error(garbage));

            // Edited taps are replayed as sent: the server's distance is what those taps really do.
            Reel edited = Clone(reel);
            edited.Run.TapTicks = edited.Run.TapTicks.Take(edited.Run.TapTicks.Length / 2).ToArray();
            edited.Run.ReleaseTicks = edited.Run.ReleaseTicks.Where(t => t <= edited.Run.TapTicks.Last()).ToArray();
            ReplayResult truth = RunReplay.Replay(edited, content.CourseFor(edited.Seed), content.Config);
            edited.Distance = reel.Distance;   // and still claim the original distance
            JsonElement retapped = await Submit(day, ReelPacket.Encode(edited));
            Check("edited taps", truth.Finished
                    ? retapped.GetProperty("ok").GetBoolean() && retapped.GetProperty("distance").GetInt32() == truth.Distance
                    : Error(retapped) == "unfinished",
                $"{Error(retapped)}, server {retapped.GetProperty("distance")} m, the taps really make {truth.Distance} m, claimed {edited.Distance} m");

            // A faked distance on a fresh run (the edited one was accepted, so the day's ghosts have moved on).
            var (day2, reel2) = await Play(600);
            int real = reel2.Distance;
            JsonElement faked = await Submit(day2, Edit(reel2, r => r.Distance += 5000));
            Check("faked distance", faked.GetProperty("ok").GetBoolean() && faked.GetProperty("corrected").GetBoolean() && faked.GetProperty("distance").GetInt32() == real,
                $"claimed {real + 5000} m, server {faked.GetProperty("distance")} m");

            // The limit counts from the start of the previous submission, so two quick ones back to back.
            await Submit(day2, "not a packet");
            JsonElement fast = await Program.Call(http, projectId, token, "SubmitDailyRun",
                new { date = today, leaderVersion = day2.GetProperty("leaderVersion").GetString(), packet = ReelPacket.Encode(reel2) });
            Check("two submissions inside 2 s", Error(fast) == "rate-limit", Error(fast));
        }

        // ---- challenges ----

        static async Task Challenges(HttpClient http, string projectId, string token, GameContent content)
        {
            Console.WriteLine("Challenges (CreateChallenge)");
            uint seed = (uint)DateTime.UtcNow.Ticks;
            Course course = content.CourseFor(seed % 100_000 + 1);
            GhostSet ghosts = ParityRuns.Ghosts(course, 3, seed, content.Config);
            Reel reel = null;
            foreach (uint s in ParityRuns.Seeds(80, seed))
                if ((reel = ParityRuns.Play(course, ghosts, s, content.Config, content.ContentVersion).Reel) != null && reel.Run.TapTicks.Length >= 4) break;

            JsonElement faked = await Program.Call(http, projectId, token, "CreateChallenge", new { packet = Edit(reel, r => r.Score += 50_000), name = "Probe" });
            Check("faked score", faked.GetProperty("ok").GetBoolean() && faked.GetProperty("corrected").GetBoolean() && faked.GetProperty("score").GetInt32() == reel.Score,
                $"claimed {reel.Score + 50_000}, stored {faked.GetProperty("score")}");

            Reel edited = Clone(reel);
            edited.Run.TapTicks = edited.Run.TapTicks.Take(edited.Run.TapTicks.Length / 2).ToArray();
            edited.Run.ReleaseTicks = edited.Run.ReleaseTicks.Where(t => t <= edited.Run.TapTicks.Last()).ToArray();
            ReplayResult truth = RunReplay.Replay(edited, content.CourseFor(edited.Seed), content.Config);
            JsonElement retapped = await Program.Call(http, projectId, token, "CreateChallenge", new { packet = ReelPacket.Encode(edited), name = "Probe" });
            Check("edited taps", truth.Finished
                    ? retapped.GetProperty("ok").GetBoolean() && retapped.GetProperty("score").GetInt32() == truth.Score
                    : retapped.GetProperty("error").GetString() == "unfinished",
                $"stored {retapped.GetProperty("score")}, the taps really score {truth.Score}, claimed {edited.Score}");

            JsonElement oldContent = await Program.Call(http, projectId, token, "CreateChallenge", new { packet = Edit(reel, r => r.ContentVersion = "00000000"), name = "Probe" });
            Check("another content version", oldContent.GetProperty("error").GetString() == "content-version", oldContent.GetProperty("error").GetString());
            (bool refused, string why) oversized = await Oversized(http, projectId, token, "CreateChallenge",
                new { packet = "x" + new string('A', ReelPacket.MaxChars), name = "Probe" });
            Check("oversized payload", oversized.refused, oversized.why);
            JsonElement rude = await Program.Call(http, projectId, token, "CreateChallenge", new { packet = ReelPacket.Encode(reel), name = "Sh1thead" });
            JsonElement got = await Program.Call(http, projectId, token, "GetChallenge",
                new { ownerId = rude.GetProperty("ownerId").GetString(), challengeId = rude.GetProperty("challengeId").GetString() });
            Check("a blocked name", got.GetProperty("name").GetString() == Echo.Core.Challenges.ChallengeNames.Default, got.GetProperty("name").GetString());
        }

        // ---- straight to the services, around the module ----

        static async Task DirectWrites(HttpClient http, string projectId, string token)
        {
            Console.WriteLine("Direct writes (Access Control)");
            string player = PlayerId(token);
            async Task<int> Send(HttpMethod method, string url, string body)
            {
                var request = new HttpRequestMessage(method, url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                request.Headers.Add("Authorization", "Bearer " + token);
                return (int)(await http.SendAsync(request)).StatusCode;
            }
            int score = await Send(HttpMethod.Post,
                $"https://leaderboards.services.api.unity.com/v1/projects/{projectId}/leaderboards/daily/scores/players/{player}", "{\"score\":99999}");
            Check("a leaderboard score", score == 403, score.ToString());
            int save = await Send(HttpMethod.Post,
                $"https://cloud-save.services.api.unity.com/v1/data/projects/{projectId}/players/{player}/items", "{\"key\":\"daily_20261013\",\"value\":\"{}\"}");
            Check("a Cloud Save item", save == 403, save.ToString());
            int name = await Send(HttpMethod.Post, $"https://social.services.api.unity.com/v1/names/{player}", "{\"name\":\"Cheater\"}");
            Check("a player name", name == 403, name.ToString());
        }

        // The module refuses it ("malformed"), or Cloud Code refuses the request before it reaches the module.
        static async Task<(bool, string)> Oversized(HttpClient http, string projectId, string token, string function, object parameters)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://cloud-code.services.api.unity.com/v1/projects/{projectId}/modules/{Module}/{function}")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { @params = parameters }), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Authorization", "Bearer " + token);
            HttpResponseMessage response = await http.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500, $"HTTP {(int)response.StatusCode} from Cloud Code");
            JsonElement output = JsonDocument.Parse(body).RootElement.GetProperty("output");
            string error = output.GetProperty("error").GetString();
            return (error == "malformed", error);
        }

        static string Edit(Reel reel, Action<Reel> change)
        {
            Reel copy = Clone(reel);
            change(copy);
            return ReelPacket.Encode(copy);
        }

        static Reel Clone(Reel reel)
        {
            ReelPacket.TryDecode(ReelPacket.Encode(reel), out Reel copy);
            return copy;
        }

        static string PlayerId(string token)
        {
            string payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            return JsonDocument.Parse(Convert.FromBase64String(payload)).RootElement.GetProperty("sub").GetString();
        }
    }
}
