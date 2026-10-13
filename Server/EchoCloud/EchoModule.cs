using System;
using System.Threading.Tasks;
using Echo.Server;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace EchoCloud
{
    /// <summary>
    /// Echo's Cloud Code endpoints (version 2.0): ReplayCheck (development only, shows a run replaying to the same
    /// distance on the server as on the device), the friend challenges (CreateChallenge, GetChallenge) and the
    /// Daily Haunt (GetDaily, SubmitDailyRun).
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

        [CloudCodeFunction("GetDaily")]
        public async Task<GetDailyResponse> GetDaily(IExecutionContext context, IGameApiClient api, string dateOverride = null) =>
            await (await Daily(context, api)).GetDailyAsync(context.PlayerId, DevelopmentOnly(context, dateOverride));

        [CloudCodeFunction("SubmitDailyRun")]
        public async Task<SubmitDailyResponse> SubmitDailyRun(IExecutionContext context, IGameApiClient api, string date, int leaderVersion,
            string packet, string dateOverride = null) =>
            await (await Daily(context, api)).SubmitAsync(context.PlayerId, date, leaderVersion, packet, DevelopmentOnly(context, dateOverride));

        // Daily Haunt doc: the date can be overridden to test midnight, in development only.
        static string DevelopmentOnly(IExecutionContext context, string dateOverride) =>
            context.EnvironmentName == Development ? dateOverride : null;

        // The seed salt lives in Secret Manager (DAILY_SEED_SALT), never in the client or the repo.
        static async Task<DailyService> Daily(IExecutionContext context, IGameApiClient api)
        {
            var salt = await api.SecretManager.GetSecret(context, "DAILY_SEED_SALT");
            return new DailyService(GameContent.Shared, new CloudDailyStore(api, context), () => DateTime.UtcNow, salt.Value);
        }

        static ChallengeService Challenges(IExecutionContext context, IGameApiClient api) =>
            new ChallengeService(GameContent.Shared, new CloudSaveChallengeStore(api, context), () => DateTime.UtcNow);
    }
}
