using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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

        readonly IGameApiClient _api;
        readonly IExecutionContext _context;
        readonly Guid _project;
        readonly Func<Task<string>> _adminKey;
        static readonly HttpClient Http = new HttpClient();

        /// <param name="adminKey">
        /// "keyId:secret" of a service account with the Leaderboards admin role, read from Secret Manager only when a
        /// score is deleted (Cloud Code's own token can't delete scores).
        /// </param>
        public CloudDailyStore(IGameApiClient api, IExecutionContext context, Func<Task<string>> adminKey = null)
        {
            _api = api;
            _context = context;
            _project = Guid.Parse(context.ProjectId);
            _adminKey = adminKey;
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

        public Task SubmitAsync(string playerId, int distance) =>
            _api.Leaderboards.AddLeaderboardPlayerScoreAsync(_context, _context.ServiceToken, _project, LeaderboardId, playerId,
                new AddLeaderboardScore(distance));

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

        public async Task DeleteAllPlayerItemsAsync(string playerId)
        {
            try
            {
                await _api.CloudSaveData.DeletePrivateItemsAsync(_context, _context.ServiceToken, _context.ProjectId, playerId);
            }
            catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
            {
                // nothing stored
            }
        }

        // Leaderboards admin API (Delete Player Score), with the service account's key: Basic auth.
        public async Task<string> DeleteScoreAsync(string playerId)
        {
            string key;
            try
            {
                key = _adminKey == null ? null : await _adminKey();
            }
            catch (Exception e)
            {
                // Never the secret itself: only why it couldn't be read (missing, or not visible to this environment).
                string why = e.Message.Length > 120 ? e.Message.Substring(0, 120) : e.Message;
                return $"no-admin-key ({e.GetType().Name}: {why})";
            }
            if (string.IsNullOrEmpty(key)) return "no-admin-key (empty)";
            if (key.IndexOf(':') <= 0) return "admin-key-format (expected keyId:secret)";
            var request = new HttpRequestMessage(HttpMethod.Delete,
                $"https://services.api.unity.com/leaderboards/v1/projects/{_context.ProjectId}/environments/{_context.EnvironmentId}/leaderboards/{LeaderboardId}/scores/players/{playerId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(key)));
            HttpResponseMessage response = await Http.SendAsync(request);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound) return null;   // 404: no score today
            return "HTTP " + (int)response.StatusCode;
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
