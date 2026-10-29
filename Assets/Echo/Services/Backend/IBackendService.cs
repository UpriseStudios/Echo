using System.Threading.Tasks;

namespace Echo.Services.Backend
{
    /// <summary>
    /// Echo's server (version 2.0): Unity Authentication, Cloud Code and Remote Config, behind one interface so the
    /// game never sees an SDK (CLAUDE.md rule 4). The player is signed in anonymously, lazily, the first time a
    /// server feature is used, which is always after the consent choice. The client sends inputs only; the server
    /// replays them (Daily Haunt doc: the server is authoritative).
    /// </summary>
    public interface IBackendService
    {
        bool SignedIn { get; }

        /// <summary>What the backend is doing, for the development status readout.</summary>
        string Status { get; }

        /// <summary>Signs in anonymously if not yet signed in. False when offline or the service fails.</summary>
        Task<bool> SignInAsync();

        /// <summary>The Remote Config switches (signs in first). All off when they cannot be fetched.</summary>
        Task<BackendFlags> GetFlagsAsync();

        /// <summary>Development: the server's own replay of a run packet (Core.Net.ReelPacket).</summary>
        Task<ReplayCheckResult> ReplayCheckAsync(string packet);

        /// <summary>Stores a challenge (a run with its ghosts, as a ReelPacket) and returns its link.</summary>
        Task<CreateChallengeResult> CreateChallengeAsync(string packet, string name);

        /// <summary>Fetches a challenge someone sent, by the owner and id in its link.</summary>
        Task<GetChallengeResult> GetChallengeAsync(string ownerId, string challengeId);

        /// <summary>Today's Daily Haunt. <paramref name="dateOverride"/> (yyyy-MM-dd) is honoured in development only.</summary>
        Task<DailyInfo> GetDailyAsync(string dateOverride);

        /// <summary>Sends a finished daily run (its inputs, as a ReelPacket) for the server to replay and rank.</summary>
        Task<DailySubmitResult> SubmitDailyRunAsync(string date, string leaderVersion, string packet, string dateOverride, bool forceModes = false);

        /// <summary>Banishes one of the player's own daily ghosts (after a rewarded ad), at most 3 a day.</summary>
        Task<DailyBanishResult> BanishDailyGhostAsync(string date, int runIndex, string dateOverride);

        /// <summary>The run at a rank on today's board, to watch (after a rewarded ad, unless it's your own).</summary>
        Task<DailyRunResult> GetDailyRunAsync(int rank, string dateOverride);

        /// <summary>
        /// Modes doc: the competitive modes on show and the server's clock. Development builds can force every mode open
        /// and set the date (the server honours both in development only).
        /// </summary>
        Task<ModesResult> GetModesAsync(string dateOverride, bool forceAll);

        /// <summary>Naughty vs Nice: the tally, your side, and once it's over the result and your rewards.</summary>
        Task<TeamEventResult> GetTeamEventAsync(string dateOverride, bool forceModes);

        /// <summary>Naughty vs Nice: pick a side ("naughty" or "nice"); permanent for the event, balanced past 55%.</summary>
        Task<TeamEventResult> JoinTeamAsync(string team, string dateOverride, bool forceModes);

        /// <summary>Server-granted rewards (cosmetics and badges) and the current side, for Stats' profile.</summary>
        Task<ProfileResult> GetProfileAsync(string dateOverride, bool forceModes);

        /// <summary>The player's generated online name (as on the boards), or null when not signed in.</summary>
        Task<string> GetPlayerNameAsync();

        /// <summary>
        /// Privacy policy: deletes the player's Daily Haunt runs, leaderboard entry and sent challenges on the server,
        /// then their anonymous account (the next online use starts afresh). Ok with nothing to do if never online.
        /// </summary>
        Task<DeleteOnlineDataResult> DeleteOnlineDataAsync();
    }
}
