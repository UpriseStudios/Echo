using System;
using System.Threading.Tasks;
using Echo.Server;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace EchoCloud
{
    /// <summary>
    /// Echo's Cloud Code endpoints (version 2.0): ReplayCheck (development only, shows a run replaying to the same
    /// distance on the server as on the device) and the friend challenges, CreateChallenge and GetChallenge.
    /// Players only ever send inputs; everything is replayed and written here (Access Control blocks direct writes).
    /// </summary>
    public class EchoModule
    {
        const string Development = "development";

        [CloudCodeFunction("ReplayCheck")]
        public Task<ReplayCheckResponse> ReplayCheck(IExecutionContext context, string packet)
        {
            if (context.EnvironmentName != Development)
                return Task.FromResult(new ReplayCheckResponse { Error = "development-only" });
            return Task.FromResult(new ReplayService(GameContent.Shared).Check(packet));
        }

        [CloudCodeFunction("CreateChallenge")]
        public Task<CreateChallengeResponse> CreateChallenge(IExecutionContext context, IGameApiClient api, string packet, string name) =>
            Challenges(context, api).CreateAsync(context.PlayerId, packet, name);

        [CloudCodeFunction("GetChallenge")]
        public Task<GetChallengeResponse> GetChallenge(IExecutionContext context, IGameApiClient api, string ownerId, string challengeId) =>
            Challenges(context, api).GetAsync(ownerId, challengeId);

        static ChallengeService Challenges(IExecutionContext context, IGameApiClient api) =>
            new ChallengeService(GameContent.Shared, new CloudSaveChallengeStore(api, context), () => DateTime.UtcNow);
    }
}
