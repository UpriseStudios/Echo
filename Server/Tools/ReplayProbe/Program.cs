using System;
using System.Diagnostics;
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
            int count = args.Length > 0 && int.TryParse(args[0], out int n) ? n : 10;
            string projectId = System.Environment.GetEnvironmentVariable("ECHO_UGS_PROJECT_ID") ?? Ugs("config get project-id");
            using var http = new HttpClient();

            string token = await SignInAnonymously(http, projectId);
            Console.WriteLine($"Signed in as an anonymous test player in {Environment}.");

            GameContent content = GameContent.Shared;
            Console.WriteLine($"Content version {content.ContentVersion}\n");
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

        static async Task<JsonElement> Call(HttpClient http, string projectId, string token, string function, string packet)
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://cloud-code.services.api.unity.com/v1/projects/{projectId}/modules/{Module}/{function}")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { @params = new { packet } }), Encoding.UTF8, "application/json"),
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
