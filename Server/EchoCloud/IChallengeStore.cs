using System.Threading.Tasks;

namespace EchoCloud
{
    /// <summary>
    /// Where challenges are kept: a player's private Cloud Save data, which only the server can read or write
    /// (Daily Haunt doc storage table). Values are JSON strings. Tests use an in-memory store.
    /// </summary>
    public interface IChallengeStore
    {
        /// <summary>The value under a key, or null when there is none.</summary>
        Task<string> GetAsync(string playerId, string key);

        Task SetAsync(string playerId, string key, string json);

        Task DeleteAsync(string playerId, string key);
    }
}
