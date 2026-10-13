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
                return new BackendFlags(config.GetBool(BackendFlags.DailyKey, false), config.GetBool(BackendFlags.ServerChallengesKey, false));
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
    }
}
