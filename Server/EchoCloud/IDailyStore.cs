using System.Collections.Generic;
using System.Threading.Tasks;

namespace EchoCloud
{
    /// <summary>
    /// What the Daily Haunt keeps (Daily Haunt doc storage table): each player's day in their private Cloud Save data,
    /// the leader ghost cache in a private game-data item, and the `daily` leaderboard (desc, keep best, reset at
    /// 00:00 UTC with an archive). Only the server reads or writes any of it. Tests use an in-memory store.
    /// </summary>
    public interface IDailyStore
    {
        Task<string> GetPlayerAsync(string playerId, string key);
        Task SetPlayerAsync(string playerId, string key, string json);
        Task<string> GetGameAsync(string key);
        Task SetGameAsync(string key, string json);

        /// <summary>
        /// Records a distance; the leaderboard keeps each player's best. <paramref name="yesterday"/>: a run from just
        /// before midnight, submitted in the grace period, goes on yesterday's archived board.
        /// </summary>
        Task SubmitAsync(string playerId, int distance, bool yesterday);

        /// <summary>Today's top <paramref name="count"/>, best first.</summary>
        Task<List<LeaderboardRow>> TopAsync(int count);

        /// <summary>The player's place today, or null before their first run.</summary>
        Task<LeaderboardRow> PlayerAsync(string playerId);

        /// <summary>Yesterday's winner from the archived leaderboard, or null.</summary>
        Task<LeaderboardRow> YesterdaysWinnerAsync();

        /// <summary>Where the player finished yesterday, or null if they didn't play.</summary>
        Task<LeaderboardRow> YesterdaysPlaceAsync(string playerId);
    }
}
