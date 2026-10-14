using System.Collections.Generic;
using System.Threading.Tasks;

namespace EchoCloud
{
    /// <summary>
    /// What the Daily Haunt keeps per player (Daily Haunt doc storage table): a little private Cloud Save data (the
    /// rate limit) and the `daily` leaderboard; days themselves live in IDayStore (desc, keep best, reset at
    /// 00:00 UTC with an archive). Only the server reads or writes any of it. Tests use an in-memory store.
    /// </summary>
    public interface IDailyStore
    {
        Task<string> GetPlayerAsync(string playerId, string key);
        Task SetPlayerAsync(string playerId, string key, string json);

        /// <summary>
        /// Records a distance on today's board; the leaderboard keeps each player's best. (Archived boards are
        /// read-only in UGS, so grace-period runs never come here.)
        /// </summary>
        Task SubmitAsync(string playerId, int distance);

        /// <summary>Today's top <paramref name="count"/>, best first.</summary>
        Task<List<LeaderboardRow>> TopAsync(int count);

        /// <summary>The player's place today, or null before their first run.</summary>
        Task<LeaderboardRow> PlayerAsync(string playerId);

        /// <summary>Yesterday's winner from the archived leaderboard, or null.</summary>
        Task<LeaderboardRow> YesterdaysWinnerAsync();

        /// <summary>Where the player finished yesterday, or null if they didn't play.</summary>
        Task<LeaderboardRow> YesterdaysPlaceAsync(string playerId);


        /// <summary>Deletion (privacy policy): every private item the player has (daily meta, challenge index).</summary>
        Task DeleteAllPlayerItemsAsync(string playerId);

        /// <summary>
        /// Removes the player's score from today's live board. Null when it went (or there was none), otherwise why not:
        /// "no-admin-key", or the admin API's HTTP status (a key without the Leaderboards admin role gets 403).
        /// </summary>
        Task<string> DeleteScoreAsync(string playerId);
    }
}
