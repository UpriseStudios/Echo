using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Echo.Core.Challenges;
using Echo.Core.Daily;
using Echo.Core.Ghosts;
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

        // ---- the Daily Haunt, kept in memory the way the server keeps it (same DailyRules) ----

        /// <summary>The mock's "today", yyyy-MM-dd; tests set it. A date override replaces it.</summary>
        public string Today = "2026-10-14";

        /// <summary>Test hook: today's leaders (made with DailyRules.AsLeader) and their distances.</summary>
        public readonly List<(GhostRecord ghost, int distance)> Leaders = new List<(GhostRecord, int)>();

        /// <summary>Test hook: what the content version is (the game's own); refuse nothing else.</summary>
        public string ContentVersion = "";
        public int Submissions { get; private set; }

        readonly Dictionary<string, (List<GhostRecord> own, int best, int next)> _days = new Dictionary<string, (List<GhostRecord>, int, int)>();

        static long SeedFor(string date)
        {
            uint h = 2166136261u;
            foreach (char ch in "mock:" + date) h = (h ^ ch) * 16777619u;
            return h == 0 ? 1 : h;
        }

        (List<GhostRecord> own, int best, int next) Day(string date) =>
            _days.TryGetValue(date, out var d) ? d : (new List<GhostRecord>(), 0, 1);

        public Task<DailyInfo> GetDailyAsync(string dateOverride)
        {
            if (Offline) return Task.FromResult(DailyInfo.Failed("offline"));
            string date = string.IsNullOrEmpty(dateOverride) ? Today : dateOverride;
            var day = Day(date);
            var ghosts = new List<GhostRecord>();
            foreach (var l in Leaders) ghosts.Add(l.ghost);
            ghosts.AddRange(day.own);
            ghosts.Sort((a, b) => a.RunIndex.CompareTo(b.RunIndex));
            var info = new DailyInfo
            {
                Ok = true, Date = date, Day = 100, Seed = SeedFor(date), ContentVersion = ContentVersion, SecondsLeft = 3600,
                LeaderVersion = 1, BestDistance = day.best, NextRunIndex = day.next,
                GhostsPacket = ReelPacket.Encode(new Reel { ContentVersion = ContentVersion, Seed = (uint)SeedFor(date), SpeedPct = DailyRules.SpeedPct, RunIndex = 1, Run = new GhostRecord(), Ghosts = ghosts }),
            };
            int rank = 1;
            foreach (var l in Leaders) info.Board.Add(new DailyBoardRow { Rank = rank++, Name = l.ghost.Name, Distance = l.distance });
            if (day.best > 0) info.Board.Add(new DailyBoardRow { Rank = rank, Name = "You", Distance = day.best, IsYou = true });
            info.Rank = day.best > 0 ? rank : 0;
            return Task.FromResult(info);
        }

        public Task<DailySubmitResult> SubmitDailyRunAsync(string date, int leaderVersion, string packet, string dateOverride)
        {
            if (Offline) return Task.FromResult(DailySubmitResult.Failed("offline"));
            Submissions++;
            if (!ReelPacket.TryDecode(packet, out Reel reel)) return Task.FromResult(DailySubmitResult.Failed("malformed"));
            string broken = DailyRules.Check(reel);
            if (broken != null) return Task.FromResult(DailySubmitResult.Failed(broken));
            ReplayCheckResult replay = LocalReplay?.Invoke(packet);
            int distance = replay != null && replay.Accepted ? replay.Distance : reel.Distance;
            var day = Day(date);
            GhostRecord run = reel.Run.Clone();
            run.RunIndex = reel.RunIndex;
            bool newBest = distance > day.best;
            if (newBest)
            {
                day.best = distance;
                day.own.Clear();
            }
            else DailyRules.AddOwnGhost(day.own, run);
            day.next = Math.Max(day.next, reel.RunIndex + 1);
            _days[date] = day;
            var distances = new List<int>();
            foreach (var l in Leaders) distances.Add(l.distance);
            int next = DailyRules.NextToBeat(distances, distance);
            var result = new DailySubmitResult
            {
                Ok = true, Distance = distance, BestDistance = day.best, NewBest = newBest, NextRunIndex = day.next,
                Rank = 1 + Leaders.FindAll(l => l.distance >= day.best).Count, LeadersPassed = DailyRules.Passed(distances, distance),
                NextLeaderDistance = next < 0 ? 0 : next, Corrected = distance != reel.Distance,
            };
            if (next >= 0) result.NextLeaderName = Leaders.Find(l => l.distance == next).ghost.Name;
            return Task.FromResult(result);
        }
    }
}
