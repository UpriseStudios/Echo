using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace EchoCloud
{
    /// <summary>Challenges in a player's private Cloud Save data, written and read with the module's service token.</summary>
    public sealed class CloudSaveChallengeStore : IChallengeStore
    {
        readonly IGameApiClient _api;
        readonly IExecutionContext _context;

        public CloudSaveChallengeStore(IGameApiClient api, IExecutionContext context)
        {
            _api = api;
            _context = context;
        }

        public async Task<string> GetAsync(string playerId, string key)
        {
            var response = await _api.CloudSaveData.GetPrivateItemsAsync(_context, _context.ServiceToken, _context.ProjectId, playerId,
                new List<string> { key });
            foreach (Item item in response.Data.Results)
                if (item.Key == key) return item.Value?.ToString();
            return null;
        }

        public Task SetAsync(string playerId, string key, string json) =>
            _api.CloudSaveData.SetPrivateItemAsync(_context, _context.ServiceToken, _context.ProjectId, playerId, new SetItemBody(key, json));

        public Task DeleteAsync(string playerId, string key) =>
            _api.CloudSaveData.DeletePrivateItemAsync(_context, _context.ServiceToken, _context.ProjectId, playerId, key);
    }
}
