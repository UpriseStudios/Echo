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
    }
}
