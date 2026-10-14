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
