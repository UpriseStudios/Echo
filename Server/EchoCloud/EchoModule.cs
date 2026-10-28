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

        // Every endpoint below checks its Remote Config switch first (2.0 brief rule 6), so turning one off closes it here
        // as well as in the game.
        [CloudCodeFunction("CreateChallenge")]
        public async Task<CreateChallengeResponse> CreateChallenge(IExecutionContext context, IGameApiClient api, string packet, string name) =>
            !(await FeatureSwitches.ReadAsync(context, api)).ServerChallenges
                ? new CreateChallengeResponse { Error = FeatureSwitches.Disabled }
                : await Challenges(context, api).CreateAsync(context.PlayerId, packet, name);

        [CloudCodeFunction("GetChallenge")]
        public async Task<GetChallengeResponse> GetChallenge(IExecutionContext context, IGameApiClient api, string ownerId, string challengeId) =>
            !(await FeatureSwitches.ReadAsync(context, api)).ServerChallenges
                ? new GetChallengeResponse { Error = FeatureSwitches.Disabled }
                : await Challenges(context, api).GetAsync(ownerId, challengeId);

        [CloudCodeFunction("GetDaily")]
        public async Task<GetDailyResponse> GetDaily(IExecutionContext context, IGameApiClient api, string dateOverride = null) =>
            !(await FeatureSwitches.ReadAsync(context, api)).Daily
                ? new GetDailyResponse { Error = FeatureSwitches.Disabled }
                : await (await Daily(context, api)).GetDailyAsync(context.PlayerId, DevelopmentOnly(context, dateOverride));

        [CloudCodeFunction("SubmitDailyRun")]
        public async Task<SubmitDailyResponse> SubmitDailyRun(IExecutionContext context, IGameApiClient api, string date, string leaderVersion,
            string packet, string dateOverride = null) =>
            !(await FeatureSwitches.ReadAsync(context, api)).Daily
                ? new SubmitDailyResponse { Error = FeatureSwitches.Disabled }
                : await (await Daily(context, api)).SubmitAsync(context.PlayerId, date, leaderVersion, packet, DevelopmentOnly(context, dateOverride));

        [CloudCodeFunction("BanishDailyGhost")]
        public async Task<BanishDailyGhostResponse> BanishDailyGhost(IExecutionContext context, IGameApiClient api, string date, int runIndex,
            string dateOverride = null) =>
            !(await FeatureSwitches.ReadAsync(context, api)).Daily
                ? new BanishDailyGhostResponse { Error = FeatureSwitches.Disabled }
                : await (await Daily(context, api)).BanishAsync(context.PlayerId, date, runIndex, DevelopmentOnly(context, dateOverride));

        [CloudCodeFunction("GetDailyRun")]
        public async Task<GetDailyRunResponse> GetDailyRun(IExecutionContext context, IGameApiClient api, int rank, string dateOverride = null) =>
            !(await FeatureSwitches.ReadAsync(context, api)).Daily
                ? new GetDailyRunResponse { Error = FeatureSwitches.Disabled }
                : await (await Daily(context, api)).GetRunAsync(rank, DevelopmentOnly(context, dateOverride));

        // Modes doc: the modes on show and the server's clock. Development can force every mode open and set the date.
        [CloudCodeFunction("GetModes")]
        public async Task<GetModesResponse> GetModes(IExecutionContext context, IGameApiClient api, string dateOverride = null, bool forceAll = false)
        {
            FeatureSwitches switches = await FeatureSwitches.ReadAsync(context, api);
            return ModeService.Describe(switches.Modes, ModeSchedule.Now(DevelopmentOnly(context, dateOverride)),
                forceAll && context.EnvironmentName == Development);
        }

        // Privacy policy: Settings › Delete online data. Never behind a switch: deleting must always work.
        [CloudCodeFunction("DeleteMyData")]
        public async Task<DeleteMyDataResponse> DeleteMyData(IExecutionContext context, IGameApiClient api)
        {
            await Challenges(context, api).ForgetPlayerAsync(context.PlayerId);   // first: it needs their index
            return await (await Daily(context, api)).ForgetPlayerAsync(context.PlayerId);
        }

        // Daily Haunt doc: the date can be overridden to test midnight, in development only.
        static string DevelopmentOnly(IExecutionContext context, string dateOverride) =>
            context.EnvironmentName == Development ? dateOverride : null;

        // The seed salt lives in Secret Manager (DAILY_SEED_SALT, DAILY_SEED_SALT_PROD in production), never in the
        // client or the repo.
        static async Task<DailyService> Daily(IExecutionContext context, IGameApiClient api)
        {
            var salt = await api.SecretManager.GetSecret(context, SecretNames.For(context.EnvironmentName, SecretNames.DailySeedSalt));
            return new DailyService(GameContent.Shared, new CloudDailyStore(api, context, () => AdminKey(context, api)), new CloudDayStore(api, context),
                () => DateTime.UtcNow, salt.Value);
        }

        // A service account key ("keyId:secret") with the Leaderboards admin role, in Secret Manager as
        // LEADERBOARDS_ADMIN_KEY (LEADERBOARDS_ADMIN_KEY_PROD in production); only deleting a player's score needs it. Missing: scores stay, everything else goes
        // (CloudDailyStore reports why).
        static async Task<string> AdminKey(IExecutionContext context, IGameApiClient api) =>
            (await api.SecretManager.GetSecret(context, SecretNames.For(context.EnvironmentName, SecretNames.LeaderboardsAdminKey))).Value;

        static ChallengeService Challenges(IExecutionContext context, IGameApiClient api) =>
            new ChallengeService(GameContent.Shared, new CloudSaveChallengeStore(api, context), new CloudDayStore(api, context), () => DateTime.UtcNow);
    }
}
