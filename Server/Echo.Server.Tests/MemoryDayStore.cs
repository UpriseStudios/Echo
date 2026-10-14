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

        public bool Has(string bucket, string key) => Buckets.TryGetValue(bucket, out var b) && b.ContainsKey(key);

        public IEnumerable<string> KeysIn(string prefix) =>
            Buckets.Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal)).SelectMany(kv => kv.Value.Keys.Select(k => kv.Key + "/" + k));
    }
}
