using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Echo.Core.Challenges;
using Echo.Core.Daily;
using Echo.Core.Ghosts;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Core.Rules;
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

        /// <summary>The Daily Haunt switched off on the server after the game read the flags: its endpoints answer "disabled".</summary>
        public bool ServerDailyOff;

        /// <summary>Seconds until the mock's day ends (and its board closes).</summary>
        public int SecondsLeft = 3600;

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

        /// <summary>Badges beside leaders' names on the board, by name (tests).</summary>
        public readonly Dictionary<string, List<string>> BoardBadges = new Dictionary<string, List<string>>();

        /// <summary>While set and not finished, today's course is still "loading" (the lobby's loading runner).</summary>
        public Task DailyHold;

        public Task<DailyInfo> GetDailyAsync(string dateOverride) => DailyHold == null ? Daily(dateOverride) : HeldDaily(dateOverride);

        async Task<DailyInfo> HeldDaily(string dateOverride)
        {
            await DailyHold;
            return await Daily(dateOverride);
        }

        Task<DailyInfo> Daily(string dateOverride)
        {
            if (Offline) return Task.FromResult(DailyInfo.Failed("offline"));
            if (ServerDailyOff) return Task.FromResult(DailyInfo.Failed(BackendFlags.Disabled));
            string date = string.IsNullOrEmpty(dateOverride) ? Today : dateOverride;
            var day = Day(date);
            var ghosts = new List<GhostRecord>();
            foreach (var l in Leaders) ghosts.Add(l.ghost);
            ghosts.AddRange(day.own);
            ghosts.Sort((a, b) => a.RunIndex.CompareTo(b.RunIndex));
            var info = new DailyInfo
            {
                Ok = true, Date = date, Day = 100, Seed = SeedFor(date), ContentVersion = ContentVersion, SecondsLeft = SecondsLeft,
                LeaderVersion = "mock-leaders", BestDistance = day.best, NextRunIndex = day.next, BanishesLeft = BanishesLeft(date),
                GhostsPacket = ReelPacket.Encode(new Reel { ContentVersion = ContentVersion, Seed = (uint)SeedFor(date), SpeedPct = DailyRules.SpeedPct, RunIndex = 1, Run = new GhostRecord(), Ghosts = ghosts }),
            };
            int rank = 1;
            foreach (var l in Leaders)
                info.Board.Add(new DailyBoardRow
                {
                    Rank = rank++, Name = l.ghost.Name, Distance = l.distance,
                    Badges = BoardBadges.TryGetValue(l.ghost.Name, out List<string> badges) ? badges : new List<string>(),
                });
            if (day.best > 0) info.Board.Add(new DailyBoardRow { Rank = rank, Name = "You", Distance = day.best, IsYou = true });
            info.Rank = day.best > 0 ? rank : 0;
            return Task.FromResult(info);
        }

        readonly Dictionary<string, int> _banishes = new Dictionary<string, int>();
        readonly Dictionary<string, string> _bestReels = new Dictionary<string, string>();

        /// <summary>Banishes and runs watched that reached the "server".</summary>
        public int Banishes, RunsFetched;

        int BanishesLeft(string date) => DailyRules.BanishesPerDay - (_banishes.TryGetValue(date, out int used) ? used : 0);

        public Task<DailyBanishResult> BanishDailyGhostAsync(string date, int runIndex, string dateOverride)
        {
            if (Offline) return Task.FromResult(DailyBanishResult.Failed("offline"));
            if (ServerDailyOff) return Task.FromResult(DailyBanishResult.Failed(BackendFlags.Disabled));
            if (BanishesLeft(date) <= 0) return Task.FromResult(new DailyBanishResult { Error = "none-left" });
            var day = Day(date);
            if (!DailyRules.BanishOwn(day.own, runIndex)) return Task.FromResult(new DailyBanishResult { Error = "not-yours", BanishesLeft = BanishesLeft(date) });
            _days[date] = day;
            _banishes[date] = DailyRules.BanishesPerDay - BanishesLeft(date) + 1;
            Banishes++;
            return Task.FromResult(new DailyBanishResult { Ok = true, BanishesLeft = BanishesLeft(date) });
        }

        /// <summary>The modes on show (tests set them); forcing shows every mode open.</summary>
        public readonly List<ModeState> Modes = new List<ModeState>();

        public Task<ModesResult> GetModesAsync(string dateOverride, bool forceAll)
        {
            if (Offline) return Task.FromResult(ModesResult.Failed("offline"));
            var result = new ModesResult { Ok = true, Now = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            if (forceAll)
                foreach (string id in Echo.Core.Modes.ModeIds.All) result.Modes.Add(new ModeState { Id = id, Open = true, SecondsLeft = 3600 });
            else
                result.Modes.AddRange(Modes);
            return Task.FromResult(result);
        }

        public Task<DailyRunResult> GetDailyRunAsync(int rank, string dateOverride)
        {
            if (Offline) return Task.FromResult(DailyRunResult.Failed("offline"));
            if (ServerDailyOff) return Task.FromResult(DailyRunResult.Failed(BackendFlags.Disabled));
            string date = string.IsNullOrEmpty(dateOverride) ? Today : dateOverride;
            RunsFetched++;
            if (rank >= 1 && rank <= Leaders.Count)
            {
                var (ghost, distance) = Leaders[rank - 1];
                GhostRecord run = ghost.Clone();
                run.Leader = false;
                var reel = new Reel
                {
                    ContentVersion = ContentVersion, Seed = (uint)SeedFor(date), SpeedPct = DailyRules.SpeedPct, RunIndex = 1,
                    Options = new RunOptions { Daily = true }, Run = run, Distance = distance,
                };
                return Task.FromResult(new DailyRunResult { Ok = true, Packet = ReelPacket.Encode(reel), Name = ghost.Name, Rank = rank, Distance = distance });
            }
            if (rank == Leaders.Count + 1 && _bestReels.TryGetValue(date, out string mine))
                return Task.FromResult(new DailyRunResult { Ok = true, Packet = mine, Name = "You", Rank = rank, Distance = Day(date).best });
            return Task.FromResult(DailyRunResult.Failed("not-found"));
        }

        /// <summary>How many times Delete online data reached the "server".</summary>
        public int Deletions;

        public Task<DeleteOnlineDataResult> DeleteOnlineDataAsync()
        {
            if (Offline) return Task.FromResult(DeleteOnlineDataResult.Failed("offline"));
            Deletions++;
            _days.Clear();
            return Task.FromResult(new DeleteOnlineDataResult { Ok = true, LiveScoreDeleted = true });
        }

        // ---- Naughty vs Nice (modes doc): the server's rules in miniature ----

        /// <summary>The team event: open for joining and contributions, or ended and showing its result.</summary>
        public bool TeamOpen, TeamEnded;
        public string Team = "";
        public bool TeamAssigned;
        /// <summary>Joining Nice puts you on Naughty instead (the balance rule).</summary>
        public bool AssignNaughty;
        public long Naughty, Nice, YourMetres;
        public int YourDays;
        public readonly List<string> ProfileCosmetics = new List<string>();
        public readonly List<BadgeInfo> ProfileBadges = new List<BadgeInfo>();

        TeamEventResult TeamResult()
        {
            var r = new TeamEventResult
            {
                Ok = true, Event = "naughty_nice_2026", Open = TeamOpen, Ended = TeamEnded, SecondsLeft = 86400, Team = Team, Assigned = TeamAssigned,
                Naughty = Naughty, Nice = Nice, NaughtyToday = Naughty, NiceToday = Nice, NaughtyMembers = 1, NiceMembers = 1,
                YourMetres = YourMetres, YourDays = YourDays,
            };
            if (!TeamEnded) return r;
            r.Winner = Nice > Naughty ? "nice" : Naughty > Nice ? "naughty" : "draw";
            r.TopNice = "NICE#1";
            r.TopNiceMetres = Nice;
            r.TopNaughty = "NAUGHTY#1";
            r.TopNaughtyMetres = Naughty;
            if (Team != "" && YourDays >= 1 && (r.Winner == Team || r.Winner == "draw"))
            {
                string reward = Team == "nice" ? "acc_halo" : "trail_coal";
                r.Rewards.Add(reward);
                if (!ProfileCosmetics.Contains(reward)) ProfileCosmetics.Add(reward);
            }
            return r;
        }

        public Task<TeamEventResult> GetTeamEventAsync(string dateOverride, bool forceModes)
        {
            if (Offline) return Task.FromResult(TeamEventResult.Failed("offline"));
            SignedIn = true;   // like the real service, a call signs in
            if (!TeamOpen && !TeamEnded && !forceModes) return Task.FromResult(TeamEventResult.Failed("closed"));
            if (forceModes && !TeamEnded) TeamOpen = true;
            return Task.FromResult(TeamResult());
        }

        public Task<TeamEventResult> JoinTeamAsync(string team, string dateOverride, bool forceModes)
        {
            if (Offline) return Task.FromResult(TeamEventResult.Failed("offline"));
            SignedIn = true;
            if (!TeamOpen && !forceModes) return Task.FromResult(TeamEventResult.Failed("closed"));
            if (Team == "")
            {
                Team = AssignNaughty && team == "nice" ? "naughty" : team;
                TeamAssigned = Team != team;
            }
            return Task.FromResult(TeamResult());
        }

        // ---- One Life (modes doc): the server's rules in miniature ----

        public bool OneLifeOpen;
        public string OneLifeToken = "";
        public bool OneLifeUsed, OneLifeFinished;
        public int OneLifeDistance;
        public int OneLifeSubmissions;
        public readonly List<string> OneLifeRewards = new List<string>();
        public readonly List<(GhostRecord ghost, int distance)> OneLifeLeaders = new List<(GhostRecord, int)>();

        public Task<OneLifeInfo> GetOneLifeAsync(string dateOverride, bool forceModes)
        {
            if (Offline) return Task.FromResult(OneLifeInfo.Failed("offline"));
            SignedIn = true;
            var r = new OneLifeInfo { Ok = true, Open = OneLifeOpen || forceModes, Date = Today, SecondsLeft = SecondsLeft, Played = OneLifeUsed,
                Finished = OneLifeFinished, Distance = OneLifeDistance, Rank = OneLifeFinished ? 1 : 0 };
            int rank = 1;
            foreach (var l in OneLifeLeaders) r.Board.Add(new DailyBoardRow { Rank = rank++, Name = l.ghost.Name, Distance = l.distance });
            r.Rewards.AddRange(OneLifeRewards);
            OneLifeRewards.Clear();
            if (!r.Open) r.Error = "closed";
            return Task.FromResult(r);
        }

        public Task<OneLifeStart> StartOneLifeAsync(string dateOverride, bool forceModes)
        {
            if (Offline) return Task.FromResult(OneLifeStart.Failed("offline"));
            if (!OneLifeOpen && !forceModes) return Task.FromResult(OneLifeStart.Failed("closed"));
            if (OneLifeUsed) return Task.FromResult(OneLifeStart.Failed("used"));
            OneLifeUsed = true;
            OneLifeToken = "token-" + Today;
            var ghosts = new List<GhostRecord>();
            foreach (var l in OneLifeLeaders) ghosts.Add(l.ghost);
            ghosts.Sort((a, b) => a.RunIndex.CompareTo(b.RunIndex));
            uint seed = (uint)SeedFor("onelife-" + Today);
            return Task.FromResult(new OneLifeStart
            {
                Ok = true, Date = Today, Token = OneLifeToken, Seed = seed, ContentVersion = ContentVersion,
                GhostsPacket = ReelPacket.Encode(new Reel { ContentVersion = ContentVersion, Seed = seed, SpeedPct = DailyRules.SpeedPct, RunIndex = 1, Run = new GhostRecord(), Ghosts = ghosts }),
            });
        }

        public Task<OneLifeSubmitResult> SubmitOneLifeAsync(string date, string token, string packet, string dateOverride, bool forceModes)
        {
            if (Offline) return Task.FromResult(OneLifeSubmitResult.Failed("offline"));
            OneLifeSubmissions++;
            if (OneLifeFinished || token != OneLifeToken) return Task.FromResult(OneLifeSubmitResult.Failed("token"));
            if (!ReelPacket.TryDecode(packet, out Reel reel)) return Task.FromResult(OneLifeSubmitResult.Failed("malformed"));
            string broken = Echo.Core.Modes.OneLifeRules.Check(reel);
            if (broken != null) return Task.FromResult(OneLifeSubmitResult.Failed(broken));
            ReplayCheckResult replay = LocalReplay?.Invoke(packet);
            OneLifeFinished = true;
            OneLifeDistance = replay != null && replay.Accepted ? replay.Distance : reel.Distance;
            return Task.FromResult(new OneLifeSubmitResult { Ok = true, Distance = OneLifeDistance, Rank = 1, Abandoned = reel.Abandoned, BoardClosed = date != Today });
        }

        public Task<string> GetPlayerNameAsync() => Task.FromResult(SignedIn ? "MockRunner#1234" : null);

        public Task<ProfileResult> GetProfileAsync(string dateOverride, bool forceModes)
        {
            if (Offline) return Task.FromResult(ProfileResult.Failed("offline"));
            var r = new ProfileResult { Ok = true, Team = Team };
            r.Cosmetics.AddRange(ProfileCosmetics);
            r.Badges.AddRange(ProfileBadges);
            return Task.FromResult(r);
        }

        public Task<DailySubmitResult> SubmitDailyRunAsync(string date, string leaderVersion, string packet, string dateOverride, bool forceModes = false)
        {
            if (Offline) return Task.FromResult(DailySubmitResult.Failed("offline"));
            if (ServerDailyOff) return Task.FromResult(DailySubmitResult.Failed(BackendFlags.Disabled));
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
            int improvement = newBest ? distance - day.best : 0;
            if (newBest)
            {
                day.best = distance;
                day.own.Clear();
                reel.Distance = distance;
                _bestReels[date] = ReelPacket.Encode(reel);
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
            if (improvement > 0 && TeamOpen && Team != "")
            {
                if (Team == "nice") Nice += improvement;
                else Naughty += improvement;
                YourMetres += improvement;
                YourDays = Math.Max(YourDays, 1);
                result.Team = Team;
                result.TeamContribution = improvement;
            }
            return Task.FromResult(result);
        }
    }
}
