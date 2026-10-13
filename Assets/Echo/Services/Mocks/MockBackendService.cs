using System;
using System.Threading.Tasks;
using Echo.Services.Backend;

namespace Echo.Services.Mocks
{
    /// <summary>
    /// The backend in the editor and tests: signs in at once, serves the given switches, and "replays" on the
    /// device with the game's own code (set by the game), standing in for the Cloud Code module.
    /// </summary>
    public sealed class MockBackendService : IBackendService
    {
        public bool SignedIn { get; private set; }
        public string Status => "mock backend" + (SignedIn ? " (signed in)" : "");

        /// <summary>The switches tests and development builds want.</summary>
        public BackendFlags Flags = BackendFlags.Off;

        /// <summary>Offline: every call fails as if there were no connection.</summary>
        public bool Offline;

        /// <summary>Replays a packet locally (the game supplies it, since only it holds the course content).</summary>
        public Func<string, ReplayCheckResult> LocalReplay;

        public Task<bool> SignInAsync()
        {
            SignedIn = !Offline;
            return Task.FromResult(SignedIn);
        }

        public Task<BackendFlags> GetFlagsAsync() => Task.FromResult(Offline ? BackendFlags.Off : Flags);

        public Task<ReplayCheckResult> ReplayCheckAsync(string packet)
        {
            if (Offline) return Task.FromResult(ReplayCheckResult.Failed("offline"));
            return Task.FromResult(LocalReplay != null ? LocalReplay(packet) : ReplayCheckResult.Failed("no local replay"));
        }
    }
}
