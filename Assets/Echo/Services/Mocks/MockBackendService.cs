using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Echo.Core.Challenges;
using Echo.Core.Net;
using Echo.Core.Reels;
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

        public const string PlayerId = "mockplayer";

        // Challenges kept in memory, checked and scored the way the server does it (via LocalReplay).
        readonly Dictionary<string, (string packet, string name, int score, int distance)> _challenges =
            new Dictionary<string, (string, string, int, int)>();

        public Task<CreateChallengeResult> CreateChallengeAsync(string packet, string name)
        {
            if (Offline) return Task.FromResult(CreateChallengeResult.Failed("offline"));
            if (!ReelPacket.TryDecode(packet, out Reel reel)) return Task.FromResult(CreateChallengeResult.Failed("malformed"));
            if (reel.Ghosts.Count > Challenge.MaxGhosts) return Task.FromResult(CreateChallengeResult.Failed("too-many-ghosts"));
            ReplayCheckResult replay = LocalReplay?.Invoke(packet);
            if (replay != null && !replay.Accepted) return Task.FromResult(CreateChallengeResult.Failed(replay.Error));
            int score = replay?.Score ?? reel.Score, distance = replay?.Distance ?? reel.Distance;
            reel.Score = score;
            reel.Distance = distance;
            string id = "mock" + (_challenges.Count + 1);
            _challenges[id] = (ReelPacket.Encode(reel), name, score, distance);
            return Task.FromResult(new CreateChallengeResult
            {
                Ok = true, OwnerId = PlayerId, ChallengeId = id, Link = ChallengeLinks.Link(PlayerId, id), Score = score, Distance = distance,
            });
        }

        public Task<GetChallengeResult> GetChallengeAsync(string ownerId, string challengeId)
        {
            if (Offline) return Task.FromResult(GetChallengeResult.Failed("offline"));
            if (ownerId != PlayerId || !_challenges.TryGetValue(challengeId, out var c)) return Task.FromResult(GetChallengeResult.Failed("not-found"));
            return Task.FromResult(new GetChallengeResult { Ok = true, Packet = c.packet, Name = c.name, Score = c.score, Distance = c.distance });
        }
    }
}
