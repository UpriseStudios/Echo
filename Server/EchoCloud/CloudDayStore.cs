using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace EchoCloud
{
    /// <summary>Days as private Cloud Save custom items (game data only the server can read or write).</summary>
    public sealed class CloudDayStore : IDayStore
    {
        readonly IGameApiClient _api;
        readonly IExecutionContext _context;

        public CloudDayStore(IGameApiClient api, IExecutionContext context)
        {
            _api = api;
            _context = context;
        }

        public async Task<string> GetAsync(string bucket, string key)
        {
            try
            {
                var response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(_context, _context.ServiceToken, _context.ProjectId, bucket,
                    new List<string> { key });
                foreach (Item item in response.Data.Results)
                    if (item.Key == key) return item.Value?.ToString();
                return null;
            }
            catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public Task SetAsync(string bucket, string key, string json) =>
            _api.CloudSaveData.SetPrivateCustomItemAsync(_context, _context.ServiceToken, _context.ProjectId, bucket, new SetItemBody(key, json));

        public async Task DeleteAsync(string bucket, string key)
        {
            try
            {
                await _api.CloudSaveData.DeletePrivateCustomItemAsync(_context, _context.ServiceToken, _context.ProjectId, bucket, key);
            }
            catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }

        public async Task<Dictionary<string, string>> GetManyAsync(string bucket, IReadOnlyCollection<string> keys)
        {
            var values = new Dictionary<string, string>();
            var all = new List<string>(keys);
            // Cloud Save reads at most 20 keys a call.
            for (int i = 0; i < all.Count; i += 20)
            {
                try
                {
                    var response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(_context, _context.ServiceToken, _context.ProjectId, bucket,
                        all.GetRange(i, System.Math.Min(20, all.Count - i)));
                    foreach (Item item in response.Data.Results) values[item.Key] = item.Value?.ToString();
                }
                catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
                {
                }
            }
            return values;
        }

        public async Task<Dictionary<string, string>> GetAllAsync(string bucket)
        {
            var values = new Dictionary<string, string>();
            string after = null;
            try
            {
                while (true)
                {
                    var response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(_context, _context.ServiceToken, _context.ProjectId, bucket,
                        null, after);
                    var page = response.Data.Results;
                    foreach (Item item in page) values[item.Key] = item.Value?.ToString();
                    if (page.Count == 0 || page.Count < 20) break;
                    after = page[page.Count - 1].Key;
                }
            }
            catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
            {
            }
            return values;
        }

        public async Task<(string Json, string WriteLock)> GetLockedAsync(string bucket, string key)
        {
            try
            {
                var response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(_context, _context.ServiceToken, _context.ProjectId, bucket,
                    new List<string> { key });
                foreach (Item item in response.Data.Results)
                    if (item.Key == key) return (item.Value?.ToString(), item.WriteLock);
            }
            catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
            {
            }
            return (null, null);
        }

        public async Task<bool> TrySetAsync(string bucket, string key, string json, string writeLock)
        {
            try
            {
                await _api.CloudSaveData.SetPrivateCustomItemAsync(_context, _context.ServiceToken, _context.ProjectId, bucket,
                    new SetItemBody(key, json, writeLock));
                return true;
            }
            catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.Conflict)
            {
                return false;
            }
        }

        public async Task DeleteBucketAsync(string bucket)
        {
            try
            {
                await _api.CloudSaveData.DeletePrivateCustomItemsAsync(_context, _context.ServiceToken, _context.ProjectId, bucket);
            }
            catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }
    }
}
