using System.Collections.Generic;
using System.Threading.Tasks;
using Echo.Services.PlayGames;

namespace Echo.Services.Mocks
{
    /// <summary>
    /// Play Games for the editor, tests and devices without it. Not available unless a test says so; then it signs in
    /// at once and keeps everything it's sent, with an in-memory Saved Game.
    /// </summary>
    public sealed class MockPlayGamesService : IPlayGamesService
    {
        public bool Available { get; set; }
        public bool SignedIn { get; private set; }
        public string Status => !Available ? "not available (mock)" : SignedIn ? "signed in (mock)" : "not signed in (mock)";

        /// <summary>Tests: whether sign-in works, the cloud's save, and what was sent.</summary>
        public bool SignInWorks = true;
        public byte[] CloudSave;
        public readonly Dictionary<string, int> Achievements = new Dictionary<string, int>();
        public readonly List<(int Distance, int Score, int GhostsPassed, int NearMisses)> Runs = new List<(int, int, int, int)>();
        public int BestDistance;
        public int CloudWrites;
        public int AchievementsShown;

        public Task<bool> SignInAsync(bool interactive)
        {
            SignedIn = Available && SignInWorks;
            return Task.FromResult(SignedIn);
        }

        public Task<string> GetServerAuthCodeAsync() => Task.FromResult<string>(null);

        public void ReportAchievement(string achievementId, int progress, int target)
        {
            if (!SignedIn) return;
            Achievements.TryGetValue(achievementId, out int had);
            if (progress > had) Achievements[achievementId] = progress;
        }

        public void RecordRun(int distance, int score, int ghostsPassed, int nearMisses)
        {
            if (SignedIn) Runs.Add((distance, score, ghostsPassed, nearMisses));
        }

        public void RecordBestDistance(int metres)
        {
            if (SignedIn && metres > BestDistance) BestDistance = metres;
        }

        public void FlushEvents() { }

        public Task<byte[]> LoadCloudSaveAsync() => Task.FromResult(SignedIn ? CloudSave : null);

        public Task<bool> WriteCloudSaveAsync(byte[] data, string description, int runsPlayed)
        {
            if (!SignedIn) return Task.FromResult(false);
            CloudSave = data;
            CloudWrites++;
            return Task.FromResult(true);
        }

        public void ShowAchievements() => AchievementsShown++;
    }
}
