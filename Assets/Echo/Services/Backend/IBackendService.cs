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

        /// <summary>One Life (modes doc): today's board and your run, plus rewards from days since closed.</summary>
        Task<OneLifeInfo> GetOneLifeAsync(string dateOverride, bool forceModes);

        /// <summary>Uses today's one attempt and returns its course, leader ghosts and single-use token.</summary>
        Task<OneLifeStart> StartOneLifeAsync(string dateOverride, bool forceModes);

        Task<OneLifeSubmitResult> SubmitOneLifeAsync(string date, string token, string packet, string dateOverride, bool forceModes);

        /// <summary>
        /// The Community Graveyard (modes doc): the week's course, today's ghosts and their version, both boards, your
        /// standing, last week's summary and rewards from weeks since closed.
        /// </summary>
        Task<GraveyardInfo> GetGraveyardAsync(string dateOverride, bool forceModes);

        /// <summary>Sends a finished Graveyard run, with the ghost set version it was played against.</summary>
        Task<GraveyardSubmitResult> SubmitGraveyardRunAsync(string date, string setVersion, string packet, string dateOverride, bool forceModes);

        /// <summary>
        /// A user revision: a run on the Graveyard's board to watch, whole (today's, or for the week's board that player's
        /// best single run of the week). After a rewarded ad, unless it's your own.
        /// </summary>
        Task<DailyRunResult> GetGraveyardRunAsync(int rank, bool weekly, string dateOverride, bool forceModes);

        /// <summary>A user revision: a run on today's One Life board to watch (after a rewarded ad, unless it's your own).</summary>
        Task<DailyRunResult> GetOneLifeRunAsync(int rank, string dateOverride, bool forceModes);

        /// <summary>
        /// Ghost Duels (modes doc; "Ranked" in code): your rating, tier, duels left today, the season, recent results, the
        /// board, the "while you were away" note (shown once) and season rewards.
        /// </summary>
        Task<RankedProfileInfo> GetDuelProfileAsync(string dateOverride, bool forceModes);

        /// <summary>Starts a duel (ranked, or unrated practice): a single-use token, the arena and the opponent's run.</summary>
        Task<RankedStart> StartDuelAsync(bool ranked, string dateOverride, bool forceModes);

        Task<RankedSubmitResult> SubmitDuelAsync(string token, string packet, string dateOverride, bool forceModes);

        /// <summary>The player's generated online name (as on the boards), or null when not signed in.</summary>
        Task<string> GetPlayerNameAsync();

        /// <summary>
        /// Privacy policy: deletes the player's Daily Haunt runs, leaderboard entry and sent challenges on the server,
        /// then their anonymous account (the next online use starts afresh). Ok with nothing to do if never online.
        /// </summary>
        Task<DeleteOnlineDataResult> DeleteOnlineDataAsync();
    }
}
