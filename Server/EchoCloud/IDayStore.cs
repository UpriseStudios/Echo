using System.Threading.Tasks;

namespace EchoCloud
{
    /// <summary>
    /// Data kept by day (privacy policy retention): one private Cloud Save game-data item per day, such as
    /// echo_day_20261014 holding every player's daily record and the day's leader cache, so a whole day can be deleted
    /// at once when it is old enough, whether or not its players ever come back. Tests use an in-memory store.
    /// </summary>
    public interface IDayStore
    {
        /// <summary>The value under a key in a day's item, or null.</summary>
        Task<string> GetAsync(string bucket, string key);

        Task SetAsync(string bucket, string key, string json);

        Task DeleteAsync(string bucket, string key);

        /// <summary>Deletes a whole day: every key in the item. Nothing there is fine.</summary>
        Task DeleteBucketAsync(string bucket);
    }
}
