using System.Collections.Generic;
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

        /// <summary>The values under several keys (missing ones left out).</summary>
        Task<Dictionary<string, string>> GetManyAsync(string bucket, IReadOnlyCollection<string> keys);

        /// <summary>Every key and value in a bucket (a closed day's records, to rank them once).</summary>
        Task<Dictionary<string, string>> GetAllAsync(string bucket);

        /// <summary>A value with its write lock (null for a new key), for <see cref="TrySetAsync"/>.</summary>
        Task<(string Json, string WriteLock)> GetLockedAsync(string bucket, string key);

        /// <summary>Writes only if nobody else has since the lock was read; false on a conflict (read again and retry).</summary>
        Task<bool> TrySetAsync(string bucket, string key, string json, string writeLock);
    }
}
