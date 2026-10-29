using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EchoCloud;

namespace Echo.Server.Tests
{
    /// <summary>IDayStore in memory: bucket -> key -> value.</summary>
    public sealed class MemoryDayStore : IDayStore
    {
        public readonly Dictionary<string, Dictionary<string, string>> Buckets = new Dictionary<string, Dictionary<string, string>>();

        public Task<string> GetAsync(string bucket, string key) =>
            Task.FromResult(Buckets.TryGetValue(bucket, out var b) && b.TryGetValue(key, out string v) ? v : null);

        public Task SetAsync(string bucket, string key, string json)
        {
            if (!Buckets.TryGetValue(bucket, out var b)) Buckets[bucket] = b = new Dictionary<string, string>();
            b[key] = json;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string bucket, string key)
        {
            if (Buckets.TryGetValue(bucket, out var b)) b.Remove(key);
            return Task.CompletedTask;
        }

        public Task DeleteBucketAsync(string bucket)
        {
            Buckets.Remove(bucket);
            return Task.CompletedTask;
        }

        readonly Dictionary<string, int> _versions = new Dictionary<string, int>();

        /// <summary>A write slipped in by "someone else" just before the next locked write (tests the retry).</summary>
        public int ConflictsToInject;

        public Task<Dictionary<string, string>> GetManyAsync(string bucket, IReadOnlyCollection<string> keys)
        {
            var values = new Dictionary<string, string>();
            if (Buckets.TryGetValue(bucket, out var b))
                foreach (string k in keys)
                    if (b.TryGetValue(k, out string v)) values[k] = v;
            return Task.FromResult(values);
        }

        public Task<(string Json, string WriteLock)> GetLockedAsync(string bucket, string key)
        {
            string json = Buckets.TryGetValue(bucket, out var b) && b.TryGetValue(key, out string v) ? v : null;
            return Task.FromResult((json, json == null ? null : Version(bucket, key).ToString()));
        }

        public Task<bool> TrySetAsync(string bucket, string key, string json, string writeLock)
        {
            if (ConflictsToInject > 0)
            {
                ConflictsToInject--;
                _versions[bucket + "/" + key] = Version(bucket, key) + 1;
            }
            if (writeLock != null && writeLock != Version(bucket, key).ToString()) return Task.FromResult(false);
            _versions[bucket + "/" + key] = Version(bucket, key) + 1;
            return SetAsync(bucket, key, json).ContinueWith(_ => true);
        }

        int Version(string bucket, string key) => _versions.TryGetValue(bucket + "/" + key, out int v) ? v : 0;

        public bool Has(string bucket, string key) => Buckets.TryGetValue(bucket, out var b) && b.ContainsKey(key);

        public IEnumerable<string> KeysIn(string prefix) =>
            Buckets.Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal)).SelectMany(kv => kv.Value.Keys.Select(k => kv.Key + "/" + k));
    }
}
