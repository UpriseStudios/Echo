using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace Echo.Services.Backend
{
    /// <summary>
    /// The real backend: anonymous Unity Authentication, the EchoCloud Cloud Code module and Remote Config.
    /// Created once Unity Services are initialised (after consent); nothing signs in until a feature asks.
    /// </summary>
    public sealed class UgsBackendService : IBackendService
    {
        public const string Module = "EchoCloud";

        // Remote Config wants attribute objects; Echo targets no user or app attributes.
        struct UserAttributes { }
        struct AppAttributes { }

        string _status = "not signed in (signs in on first use)";

        public bool SignedIn => AuthenticationService.Instance.IsSignedIn;
        public string Status => _status;

        public async Task<bool> SignInAsync()
        {
            if (SignedIn) return true;
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                _status = "signed in as " + AuthenticationService.Instance.PlayerId;
                return true;
            }
            catch (Exception e)
            {
                _status = "sign-in failed: " + e.Message;
                Debug.LogWarning("[Backend] " + _status);
                return false;
            }
        }

        public async Task<BackendFlags> GetFlagsAsync()
        {
            if (!await SignInAsync()) return BackendFlags.Off;
            try
            {
                RuntimeConfig config = await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());
                string seasons = config.GetJson(BackendFlags.SeasonsKey, "");
                return new BackendFlags(config.GetBool(BackendFlags.DailyKey, false), config.GetBool(BackendFlags.ServerChallengesKey, false),
                    string.IsNullOrEmpty(seasons) ? null : seasons);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Backend] Remote Config: " + e.Message);
                return BackendFlags.Off;
            }
        }

        public async Task<ReplayCheckResult> ReplayCheckAsync(string packet)
        {
            if (!await SignInAsync()) return ReplayCheckResult.Failed("offline");
            try
            {
                return await CloudCodeService.Instance.CallModuleEndpointAsync<ReplayCheckResult>(Module, "ReplayCheck",
                    new Dictionary<string, object> { { "packet", packet } });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Backend] ReplayCheck: " + e.Message);
                return ReplayCheckResult.Failed(e.Message);
            }
        }

        public async Task<CreateChallengeResult> CreateChallengeAsync(string packet, string name)
        {
            if (!await SignInAsync()) return CreateChallengeResult.Failed("offline");
            try
            {
                return await CloudCodeService.Instance.CallModuleEndpointAsync<CreateChallengeResult>(Module, "CreateChallenge",
                    new Dictionary<string, object> { { "packet", packet }, { "name", name } });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Backend] CreateChallenge: " + e.Message);
                return CreateChallengeResult.Failed("offline");
            }
        }

        public async Task<GetChallengeResult> GetChallengeAsync(string ownerId, string challengeId)
        {
            if (!await SignInAsync()) return GetChallengeResult.Failed("offline");
            try
            {
                return await CloudCodeService.Instance.CallModuleEndpointAsync<GetChallengeResult>(Module, "GetChallenge",
                    new Dictionary<string, object> { { "ownerId", ownerId }, { "challengeId", challengeId } });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Backend] GetChallenge: " + e.Message);
                return GetChallengeResult.Failed("offline");
            }
        }

        public async Task<DailyInfo> GetDailyAsync(string dateOverride)
        {
            if (!await SignInAsync()) return DailyInfo.Failed("offline");
            try
            {
                var args = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(dateOverride)) args["dateOverride"] = dateOverride;
                return await CloudCodeService.Instance.CallModuleEndpointAsync<DailyInfo>(Module, "GetDaily", args);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Backend] GetDaily: " + e.Message);
                return DailyInfo.Failed("offline");
            }
        }

        public async Task<DailyBanishResult> BanishDailyGhostAsync(string date, int runIndex, string dateOverride)
        {
            if (!await SignInAsync()) return DailyBanishResult.Failed("offline");
            try
            {
                var args = new Dictionary<string, object> { { "date", date }, { "runIndex", runIndex } };
                if (!string.IsNullOrEmpty(dateOverride)) args["dateOverride"] = dateOverride;
                return await CloudCodeService.Instance.CallModuleEndpointAsync<DailyBanishResult>(Module, "BanishDailyGhost", args);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Backend] BanishDailyGhost: " + e.Message);
                return DailyBanishResult.Failed("offline");
            }
        }

        public async Task<DailyRunResult> GetDailyRunAsync(int rank, string dateOverride)
        {
            if (!await SignInAsync()) return DailyRunResult.Failed("offline");
            try
            {
                var args = new Dictionary<string, object> { { "rank", rank } };
                if (!string.IsNullOrEmpty(dateOverride)) args["dateOverride"] = dateOverride;
                return await CloudCodeService.Instance.CallModuleEndpointAsync<DailyRunResult>(Module, "GetDailyRun", args);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Backend] GetDailyRun: " + e.Message);
                return DailyRunResult.Failed("offline");
            }
        }

        public Task<TeamEventResult> GetTeamEventAsync(string dateOverride, bool forceModes) =>
            Call("GetTeamEvent", ModeArgs(dateOverride, forceModes), TeamEventResult.Failed);

        public Task<TeamEventResult> JoinTeamAsync(string team, string dateOverride, bool forceModes)
        {
            var args = ModeArgs(dateOverride, forceModes);
            args["team"] = team;
            return Call("JoinTeam", args, TeamEventResult.Failed);
        }

        public Task<ProfileResult> GetProfileAsync(string dateOverride, bool forceModes) =>
            Call("GetProfile", ModeArgs(dateOverride, forceModes), ProfileResult.Failed);

        public async Task<string> GetPlayerNameAsync()
        {
            if (!SignedIn) return null;
            try
            {
                return await AuthenticationService.Instance.GetPlayerNameAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Backend] player name: " + e.Message);
                return null;
            }
        }

        static Dictionary<string, object> ModeArgs(string dateOverride, bool forceModes)
        {
            var args = new Dictionary<string, object> { { "forceModes", forceModes } };
            if (!string.IsNullOrEmpty(dateOverride)) args["dateOverride"] = dateOverride;
            return args;
        }

        async Task<T> Call<T>(string function, Dictionary<string, object> args, Func<string, T> failed)
        {
            if (!await SignInAsync()) return failed("offline");
            try
            {
                return await CloudCodeService.Instance.CallModuleEndpointAsync<T>(Module, function, args);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Backend] {function}: {e.Message}");
                return failed("offline");
            }
        }

        public async Task<ModesResult> GetModesAsync(string dateOverride, bool forceAll)
        {
            if (!await SignInAsync()) return ModesResult.Failed("offline");
            try
            {
                var args = new Dictionary<string, object> { { "forceAll", forceAll } };
                if (!string.IsNullOrEmpty(dateOverride)) args["dateOverride"] = dateOverride;
                return await CloudCodeService.Instance.CallModuleEndpointAsync<ModesResult>(Module, "GetModes", args);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Backend] GetModes: " + e.Message);
                return ModesResult.Failed("offline");
            }
        }

        public async Task<DeleteOnlineDataResult> DeleteOnlineDataAsync()
        {
            // Never signed in on this device: there is no account and nothing online to delete.
            if (!SignedIn && !AuthenticationService.Instance.SessionTokenExists) return new DeleteOnlineDataResult { Ok = true };
            if (!await SignInAsync()) return DeleteOnlineDataResult.Failed("offline");
            try
            {
                DeleteOnlineDataResult r = await CloudCodeService.Instance.CallModuleEndpointAsync<DeleteOnlineDataResult>(Module, "DeleteMyData",
                    new Dictionary<string, object>());
                if (!r.Ok) return r;
                await AuthenticationService.Instance.DeleteAccountAsync();
                if (AuthenticationService.Instance.IsSignedIn) AuthenticationService.Instance.SignOut(true);
                AuthenticationService.Instance.ClearSessionToken();
                _status = "online data deleted; signs in afresh on next use";
                return r;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Backend] DeleteMyData: " + e.Message);
                return DeleteOnlineDataResult.Failed("offline");
            }
        }

        public async Task<DailySubmitResult> SubmitDailyRunAsync(string date, string leaderVersion, string packet, string dateOverride, bool forceModes = false)
        {
            if (!await SignInAsync()) return DailySubmitResult.Failed("offline");
            try
            {
                var args = new Dictionary<string, object> { { "date", date }, { "leaderVersion", leaderVersion }, { "packet", packet }, { "forceModes", forceModes } };
                if (!string.IsNullOrEmpty(dateOverride)) args["dateOverride"] = dateOverride;
                return await CloudCodeService.Instance.CallModuleEndpointAsync<DailySubmitResult>(Module, "SubmitDailyRun", args);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Backend] SubmitDailyRun: " + e.Message);
                return DailySubmitResult.Failed("offline");
            }
        }
    }
}
