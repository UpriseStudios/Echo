using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EchoCloud;

namespace Echo.Server.Tests
{
    /// <summary>IDailyStore in memory: player items and today's and yesterday's boards.</summary>
    internal sealed class MemoryDailyStore : IDailyStore
    {
        public readonly Dictionary<string, string> Items = new Dictionary<string, string>();
        public Dictionary<string, int> Today = new Dictionary<string, int>();
        public Dictionary<string, int> Yesterday = new Dictionary<string, int>();

        public Task<string> GetPlayerAsync(string p, string k) => Task.FromResult(Items.TryGetValue(p + "/" + k, out string v) ? v : null);
        public Task SetPlayerAsync(string p, string k, string json) { Items[p + "/" + k] = json; return Task.CompletedTask; }

        public Task SubmitAsync(string playerId, int distance)
        {
            var board = Today;
            board[playerId] = Math.Max(distance, board.TryGetValue(playerId, out int had) ? had : 0);
            return Task.CompletedTask;
        }

        static List<LeaderboardRow> Rows(Dictionary<string, int> board) =>
            board.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select((kv, i) => new LeaderboardRow { Rank = i + 1, PlayerId = kv.Key, Name = kv.Key.ToUpperInvariant() + "#1", Distance = kv.Value }).ToList();

        public Task<List<LeaderboardRow>> TopAsync(int count) => Task.FromResult(Rows(Today).Take(count).ToList());
        public Task<LeaderboardRow> PlayerAsync(string id) => Task.FromResult(Rows(Today).FirstOrDefault(r => r.PlayerId == id));
        public Task<LeaderboardRow> YesterdaysWinnerAsync() => Task.FromResult(Rows(Yesterday).FirstOrDefault());
        public Task<LeaderboardRow> YesterdaysPlaceAsync(string id) => Task.FromResult(Rows(Yesterday).FirstOrDefault(r => r.PlayerId == id));

        public bool HasAdminKey = true;

        public Task DeleteAllPlayerItemsAsync(string p)
        {
            foreach (string k in Items.Keys.Where(k => k.StartsWith(p + "/", StringComparison.Ordinal)).ToList()) Items.Remove(k);
            return Task.CompletedTask;
        }

        public Task<string> DeleteScoreAsync(string p)
        {
            if (HasAdminKey) Today.Remove(p);
            return Task.FromResult(HasAdminKey ? null : "no-admin-key (missing)");
        }

        // The leaderboard resets at 00:00 UTC with an archive.
        public void Midnight()
        {
            Yesterday = Today;
            Today = new Dictionary<string, int>();
        }
    }
}
