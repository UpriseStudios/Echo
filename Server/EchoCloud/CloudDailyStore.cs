using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;
using Unity.Services.Leaderboards.Model;

namespace EchoCloud
{
    /// <summary>The Daily Haunt's store on UGS: private Cloud Save data and the `daily` leaderboard, with the service token.</summary>
    public sealed class CloudDailyStore : IDailyStore
    {
        public const string LeaderboardId = "daily";
        public const string GameDataId = "echo_daily";

        readonly IGameApiClient _api;
        readonly IExecutionContext _context;
        readonly Guid _project;

        public CloudDailyStore(IGameApiClient api, IExecutionContext context)
        {
            _api = api;
            _context = context;
            _project = Guid.Parse(context.ProjectId);
        }

        public async Task<string> GetPlayerAsync(string playerId, string key)
        {
            var response = await _api.CloudSaveData.GetPrivateItemsAsync(_context, _context.ServiceToken, _context.ProjectId, playerId, new List<string> { key });
            foreach (Item item in response.Data.Results)
                if (item.Key == key) return item.Value?.ToString();
            return null;
        }

        public Task SetPlayerAsync(string playerId, string key, string json) =>
            _api.CloudSaveData.SetPrivateItemAsync(_context, _context.ServiceToken, _context.ProjectId, playerId, new SetItemBody(key, json));

        public async Task<string> GetGameAsync(string key)
        {
            var response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(_context, _context.ServiceToken, _context.ProjectId, GameDataId, new List<string> { key });
            foreach (Item item in response.Data.Results)
                if (item.Key == key) return item.Value?.ToString();
            return null;
        }

        public Task SetGameAsync(string key, string json) =>
            _api.CloudSaveData.SetPrivateCustomItemAsync(_context, _context.ServiceToken, _context.ProjectId, GameDataId, new SetItemBody(key, json));

        public async Task SubmitAsync(string playerId, int distance, bool yesterday)
        {
            string version = yesterday ? await LastArchivedVersion() : null;
            await _api.Leaderboards.AddLeaderboardPlayerScoreAsync(_context, _context.ServiceToken, _project, LeaderboardId, playerId,
                new AddLeaderboardScore(distance, null, version));
        }

        public async Task<List<LeaderboardRow>> TopAsync(int count)
        {
            var page = await _api.Leaderboards.GetLeaderboardScoresAsync(_context, _context.ServiceToken, _project, LeaderboardId, false, 0, count);
            var rows = new List<LeaderboardRow>();
            foreach (LeaderboardEntry e in page.Data.Results) rows.Add(Row(e.Rank, e.PlayerId, e.PlayerName, e.Score));
            return rows;
        }

        public async Task<LeaderboardRow> PlayerAsync(string playerId)
        {
            try
            {
                var entry = await _api.Leaderboards.GetLeaderboardPlayerScoreAsync(_context, _context.ServiceToken, _project, LeaderboardId, playerId, false);
                return Row(entry.Data.Rank, entry.Data.PlayerId, entry.Data.PlayerName, entry.Data.Score);
            }
            catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<LeaderboardRow> YesterdaysWinnerAsync()
        {
            string version = await LastArchivedVersion();
            if (version == null) return null;
            var page = await _api.Leaderboards.GetLeaderboardVersionScoresAsync(_context, _context.ServiceToken, _project, LeaderboardId, version, false, 0, 1);
            if (page.Data.Results.Count == 0) return null;
            LeaderboardEntry e = page.Data.Results[0];
            return Row(e.Rank, e.PlayerId, e.PlayerName, e.Score);
        }

        public async Task<LeaderboardRow> YesterdaysPlaceAsync(string playerId)
        {
            string version = await LastArchivedVersion();
            if (version == null) return null;
            try
            {
                var entry = await _api.Leaderboards.GetLeaderboardVersionPlayerScoreAsync(_context, _context.ServiceToken, _project, LeaderboardId, version, playerId, false);
                return Row(entry.Data.Rank, entry.Data.PlayerId, entry.Data.PlayerName, entry.Data.Score);
            }
            catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        async Task<string> LastArchivedVersion()
        {
            var versions = await _api.Leaderboards.GetLeaderboardVersionsAsync(_context, _context.ServiceToken, _project, LeaderboardId, 1);
            return versions.Data.Results.Count > 0 ? versions.Data.Results[0].Id : null;
        }

        // UGS ranks count from 0; the game's count from 1.
        static LeaderboardRow Row(int rank, string playerId, string name, double score) =>
            new LeaderboardRow { Rank = rank + 1, PlayerId = playerId, Name = name ?? "", Distance = (int)score };
    }
}
