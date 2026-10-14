using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Echo.Core.Courses;
using Echo.Core.Daily;
using Echo.Core.Ghosts;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Tests.EditMode;
using EchoCloud;
using NUnit.Framework;

namespace Echo.Server.Tests
{
    /// <summary>The Daily Haunt on the server: the day's seed, leaders, submissions, the grace period and tampering.</summary>
    public class DailyServiceTests
    {
        static readonly GameContent Content = GameContent.Shared;
        const string Salt = "test-salt";

        sealed class MemoryDailyStore : IDailyStore
        {
            public readonly Dictionary<string, string> Items = new Dictionary<string, string>();
            public Dictionary<string, int> Today = new Dictionary<string, int>();
            public Dictionary<string, int> Yesterday = new Dictionary<string, int>();

            public Task<string> GetPlayerAsync(string p, string k) => Task.FromResult(Items.TryGetValue(p + "/" + k, out string v) ? v : null);
            public Task SetPlayerAsync(string p, string k, string json) { Items[p + "/" + k] = json; return Task.CompletedTask; }

            public Task SubmitAsync(string playerId, int distance)
            {
                var board = Today;
                board[playerId] = Math.Max(distance, board.TryGetValue(playerId, out int had) ? had : 0);
                return Task.CompletedTask;
            }

            static List<LeaderboardRow> Rows(Dictionary<string, int> board) =>
                board.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select((kv, i) => new LeaderboardRow { Rank = i + 1, PlayerId = kv.Key, Name = kv.Key.ToUpperInvariant() + "#1", Distance = kv.Value }).ToList();

            public Task<List<LeaderboardRow>> TopAsync(int count) => Task.FromResult(Rows(Today).Take(count).ToList());
            public Task<LeaderboardRow> PlayerAsync(string id) => Task.FromResult(Rows(Today).FirstOrDefault(r => r.PlayerId == id));
            public Task<LeaderboardRow> YesterdaysWinnerAsync() => Task.FromResult(Rows(Yesterday).FirstOrDefault());
            public Task<LeaderboardRow> YesterdaysPlaceAsync(string id) => Task.FromResult(Rows(Yesterday).FirstOrDefault(r => r.PlayerId == id));

            public bool HasAdminKey = true;

            public Task DeleteAllPlayerItemsAsync(string p)
            {
                foreach (string k in Items.Keys.Where(k => k.StartsWith(p + "/", StringComparison.Ordinal)).ToList()) Items.Remove(k);
                return Task.CompletedTask;
            }

            public Task<string> DeleteScoreAsync(string p)
            {
                if (HasAdminKey) Today.Remove(p);
                return Task.FromResult(HasAdminKey ? null : "no-admin-key (missing)");
            }

            // The leaderboard resets at 00:00 UTC with an archive.
            public void Midnight()
            {
                Yesterday = Today;
                Today = new Dictionary<string, int>();
            }
        }

        MemoryDailyStore _store;
        MemoryDayStore _days;
        DateTime _now;
        DailyService _daily;

        [SetUp]
        public void SetUp()
        {
            _store = new MemoryDailyStore();
            _now = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
            _days = new MemoryDayStore();
            _daily = new DailyService(Content, _store, _days, () => _now, Salt);
        }

        // A player opens the Daily Haunt and plays one run against the ghosts they were given, as the game does.
        async Task<(GetDailyResponse daily, Reel reel)> Play(string player, uint style)
        {
            GetDailyResponse d = await _daily.GetDailyAsync(player);
            Assert.IsTrue(d.Ok);
            Assert.IsTrue(ReelPacket.TryDecode(d.GhostsPacket, out Reel container));
            Course course = Content.CourseFor((uint)d.Seed);
            RunSimulation run = DailyRules.Start(course, DailyRules.Ghosts(container.Ghosts.Where(g => g.Leader), container.Ghosts.Where(g => !g.Leader)),
                d.NextRunIndex, Content.Config);
            ParityRuns.Played played = ParityRuns.Drive(run, course, style, Content.Config, Content.ContentVersion);
            if (!run.IsDead) return await Play(player, style + 1);
            return (d, played.Reel);
        }

        async Task<SubmitDailyResponse> PlayAndSubmit(string player, uint style)
        {
            var (d, reel) = await Play(player, style);
            _now = _now.AddSeconds(3);
            return await _daily.SubmitAsync(player, d.Date, d.LeaderVersion, ReelPacket.Encode(reel));
        }

        [Test]
        public void TheSeed_IsTheSameAllDay_DifferentEachDay_AndNeedsTheSalt()
        {
            Assert.AreEqual(DailyService.SeedFor("2026-10-14", Salt), DailyService.SeedFor("2026-10-14", Salt));
            Assert.AreNotEqual(DailyService.SeedFor("2026-10-14", Salt), DailyService.SeedFor("2026-10-15", Salt));
            Assert.AreNotEqual(DailyService.SeedFor("2026-10-14", Salt), DailyService.SeedFor("2026-10-14", "another"));
        }

        [Test]
        public async Task TheFirstRun_SetsABest_AndTheNextPlayerMeetsItsGhost()
        {
            SubmitDailyResponse first = await PlayAndSubmit("alice", 1);
            Assert.IsTrue(first.Ok, first.Error);
            Assert.IsTrue(first.NewBest);
            Assert.AreEqual(1, first.Rank);
            Assert.IsFalse(first.Corrected);

            GetDailyResponse bob = await _daily.GetDailyAsync("bob");
            Assert.IsTrue(ReelPacket.TryDecode(bob.GhostsPacket, out Reel ghosts));
            Assert.AreEqual(1, ghosts.Ghosts.Count(g => g.Leader), "alice is a leader ghost for bob");
            Assert.AreEqual("ALICE#1", ghosts.Ghosts.First(g => g.Leader).Name);
            Assert.AreEqual(first.Distance, bob.Board[0].Distance);

            GetDailyResponse alice = await _daily.GetDailyAsync("alice");
            Assert.IsTrue(ReelPacket.TryDecode(alice.GhostsPacket, out Reel hers));
            Assert.AreEqual(0, hers.Ghosts.Count(g => g.Leader), "never your own leader ghost");
            Assert.IsTrue(alice.Board[0].IsYou);
            Assert.AreEqual(first.Distance, alice.BestDistance);
        }

        [Test]
        public async Task FailedAttempts_BecomeYourDailyGhosts_AtMostTwenty_AndABestClearsThem()
        {
            SubmitDailyResponse best = await PlayAndSubmit("p", 7);
            int bestDistance = best.Distance;
            int kept = 0;
            for (uint style = 100; style < 400 && kept < 24; style++)
            {
                SubmitDailyResponse r = await PlayAndSubmit("p", style);
                Assert.IsTrue(r.Ok, r.Error);
                if (r.NewBest) { bestDistance = r.Distance; kept = 0; continue; }
                kept++;
            }
            GetDailyResponse d = await _daily.GetDailyAsync("p");
            Assert.IsTrue(ReelPacket.TryDecode(d.GhostsPacket, out Reel ghosts));
            Assert.AreEqual(Math.Min(kept, DailyRules.OwnGhostCap), ghosts.Ghosts.Count(g => !g.Leader));
            Assert.AreEqual(bestDistance, d.BestDistance);
        }

        static string Day(string yyyyMMdd) => DailyService.DayPrefix + yyyyMMdd;
        string Today() => Day(DailyService.DateKey(_now).Replace("-", ""));

        // Another player joins the top of today's board with the given player's best run, so the leader set changes.
        void Overtake(string from, string player, int distance)
        {
            _days.Buckets[Today()][player] = _days.Buckets[Today()][from];
            _store.Today[player] = distance;
        }

        [Test]
        public void TheLeaderVersion_IsAHashOfTheDayAndEveryEntry()
        {
            var set = new List<(string, string, int, int, string)> { ("p1", "ALICE#1", 1, 500, "AAA"), ("p2", "BOB#2", 2, 400, "BBB") };
            string v = DailyService.VersionOf("2026-10-14", set);
            Assert.AreEqual(v, DailyService.VersionOf("2026-10-14", set.ToList()), "the same contents, the same version");
            Assert.AreEqual(64, v.Length);
            Assert.AreNotEqual(v, DailyService.VersionOf("2026-10-15", set), "another day");
            Assert.AreNotEqual(v, DailyService.VersionOf("2026-10-14", set.Take(1).ToList()), "fewer leaders");
            Assert.AreNotEqual(v, DailyService.VersionOf("2026-10-14", new[] { set[1], set[0] }), "another order");
            foreach (var changed in new[]
                     {
                         ("p9", "ALICE#1", 1, 500, "AAA"), ("p1", "ALICE#9", 1, 500, "AAA"), ("p1", "ALICE#1", 3, 500, "AAA"),
                         ("p1", "ALICE#1", 1, 501, "AAA"), ("p1", "ALICE#1", 1, 500, "AAB"),
                     })
                Assert.AreNotEqual(v, DailyService.VersionOf("2026-10-14", new[] { changed, set[1] }), changed.ToString());
            // Length prefixes: moving characters between neighbouring fields still changes the version.
            Assert.AreNotEqual(DailyService.VersionOf("d", new[] { ("ab", "c", 1, 1, "x") }), DailyService.VersionOf("d", new[] { ("a", "bc", 1, 1, "x") }));
        }

        [Test]
        public async Task TheLeaderSet_IsKeptFiveMinutes_ThenRebuilt_AndOnlyANewSetGetsANewVersion()
        {
            await PlayAndSubmit("alice", 41);
            GetDailyResponse a = await _daily.GetDailyAsync("x");
            _now = _now.AddMinutes(6);
            Assert.AreEqual(a.LeaderVersion, (await _daily.GetDailyAsync("x")).LeaderVersion, "rebuilt, unchanged: the same version");

            Overtake("alice", "bob", 100_000);
            _now = _now.AddMinutes(4);
            Assert.AreEqual(a.LeaderVersion, (await _daily.GetDailyAsync("x")).LeaderVersion, "kept for 5 minutes");
            _now = _now.AddMinutes(2);
            string b = (await _daily.GetDailyAsync("x")).LeaderVersion;
            Assert.AreNotEqual(a.LeaderVersion, b, "bob leads now: a new set, a new version");
            Assert.AreNotEqual("", b);
        }

        [Test]
        public async Task ARunStartedAgainstAnOlderLeaderSet_StillReplays_UntilItFallsOutOfTheCache()
        {
            await PlayAndSubmit("alice", 2);
            var (d, reel) = await Play("p", 3);
            for (int i = 0; i < 5; i++)
            {
                Overtake("alice", "o" + i, 100_000 + i);
                _now = _now.AddMinutes(6);
                await _daily.GetDailyAsync("someone");
            }
            Assert.IsTrue((await _daily.SubmitAsync("p", d.Date, d.LeaderVersion, ReelPacket.Encode(reel))).Ok, "within the hour");
            var (d2, reel2) = await Play("q", 4);
            for (int i = 0; i < DailyService.LeaderVersionsKept + 1; i++)
            {
                Overtake("alice", "n" + i, 200_000 + i);
                _now = _now.AddMinutes(6);
                await _daily.GetDailyAsync("someone");
            }
            Assert.AreEqual("leader-version", (await _daily.SubmitAsync("q", d2.Date, d2.LeaderVersion, ReelPacket.Encode(reel2))).Error);
        }

        [Test]
        public async Task Tampering_IsRefusedOrCorrected()
        {
            await PlayAndSubmit("leader", 11);
            var (d, reel) = await Play("cheat", 12);
            string Packet(Action<Reel> change)
            {
                Assert.IsTrue(ReelPacket.TryDecode(ReelPacket.Encode(reel), out Reel copy));
                change(copy);
                return ReelPacket.Encode(copy);
            }
            async Task<SubmitDailyResponse> Submit(string packet, string date = null, string version = null)
            {
                _now = _now.AddSeconds(3);
                return await _daily.SubmitAsync("cheat", date ?? d.Date, version ?? d.LeaderVersion, packet);
            }

            SubmitDailyResponse faked = await Submit(Packet(r => r.Distance += 5000));
            Assert.IsTrue(faked.Ok);
            Assert.IsTrue(faked.Corrected);
            Assert.AreEqual(reel.Distance, faked.Distance, "the server's own distance, never the claim");

            Assert.AreEqual("ghosts", (await Submit(Packet(r => r.Ghosts.RemoveAll(g => g.Leader)))).Error, "an easier crowd");
            Assert.AreEqual("power-ups", (await Submit(Packet(r => r.ShieldTick = 30))).Error);
            Assert.AreEqual("options", (await Submit(Packet(r => r.Options = new RunOptions { Daily = true, Twist = Twist.Mirror }))).Error);
            Assert.AreEqual("seed", (await Submit(Packet(r => r.Seed ^= 77))).Error);
            Assert.AreEqual("wrong-day", (await Submit(ReelPacket.Encode(reel), date: "2026-10-01")).Error, "an old date");
            Assert.AreEqual("malformed", (await Submit("x" + new string('A', ReelPacket.MaxChars))).Error, "oversized");
            Assert.AreEqual("leader-version", (await Submit(ReelPacket.Encode(reel), version: "999")).Error);

            Assert.IsTrue(ReelPacket.TryDecode(ReelPacket.Encode(reel), out Reel edited));
            edited.Run.TapTicks = edited.Run.TapTicks.Take(edited.Run.TapTicks.Length / 2).ToArray();
            edited.Run.ReleaseTicks = new int[0];
            SubmitDailyResponse retapped = await Submit(ReelPacket.Encode(edited));
            Assert.IsTrue(retapped.Ok || retapped.Error == "unfinished", "edited taps are replayed as sent: no faked result");

            _now = _now.AddSeconds(1);
            Assert.AreEqual("rate-limit", (await _daily.SubmitAsync("cheat", d.Date, d.LeaderVersion, ReelPacket.Encode(reel))).Error);
        }

        [Test]
        public async Task JustAfterMidnight_YesterdaysRunsAreKeptForFiveMinutes_ButTheBoardHasClosed()
        {
            _now = new DateTime(2026, 10, 14, 23, 59, 0, DateTimeKind.Utc);
            var (d, reel) = await Play("late", 21);
            string packet = ReelPacket.Encode(reel);
            _store.Midnight();
            _now = new DateTime(2026, 10, 15, 0, 3, 0, DateTimeKind.Utc);
            SubmitDailyResponse r = await _daily.SubmitAsync("late", d.Date, d.LeaderVersion, packet);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.IsTrue(r.BoardClosed);
            Assert.IsTrue(r.NewBest, "yesterday's best, kept with yesterday's ghosts");
            Assert.AreEqual(r.Distance, r.BestDistance);
            Assert.AreEqual(0, r.Rank, "not ranked");
            Assert.IsFalse(_store.Yesterday.ContainsKey("late"), "UGS archives are read-only");
            Assert.IsFalse(_store.Today.ContainsKey("late"), "and it never lands on today's board");
            Assert.IsTrue(_days.Has(Day("20261014"), "late"), "kept as that day's run");

            var (d2, reel2) = await Play("later", 22);   // today's course now
            Assert.AreNotEqual(d.Seed, d2.Seed);
            _now = new DateTime(2026, 10, 15, 0, 6, 0, DateTimeKind.Utc);
            Assert.AreEqual("wrong-day", (await _daily.SubmitAsync("late", d.Date, d.LeaderVersion, packet)).Error, "past the grace period");

            GetDailyResponse next = await _daily.GetDailyAsync("late");
            Assert.AreEqual(0, next.YesterdayRank);
            Assert.AreEqual("", next.YesterdayWinner);
        }

        [Test]
        public async Task DeleteMyData_RemovesTheirRuns_TheirScore_AndTheirLeaderGhost()
        {
            Assert.IsTrue((await PlayAndSubmit("alice", 51)).Ok);
            _store.Items["alice/challenge_index"] = "{}";
            GetDailyResponse before = await _daily.GetDailyAsync("bob");
            Assert.IsTrue(ReelPacket.TryDecode(before.GhostsPacket, out Reel had));
            Assert.AreEqual(1, had.Ghosts.Count(g => g.Leader));

            DeleteMyDataResponse r = await _daily.ForgetPlayerAsync("alice");
            Assert.IsTrue(r.Ok);
            Assert.IsTrue(r.LiveScoreDeleted);
            Assert.IsFalse(_store.Items.Keys.Any(k => k.StartsWith("alice/")), "her private items");
            Assert.IsFalse(_days.Has(Day("20261014"), "alice"), "her day");
            Assert.IsFalse(_store.Today.ContainsKey("alice"));
            GetDailyResponse after = await _daily.GetDailyAsync("bob");
            Assert.IsTrue(ReelPacket.TryDecode(after.GhostsPacket, out Reel now));
            Assert.AreEqual(0, now.Ghosts.Count(g => g.Leader), "her ghost no longer haunts anyone");
            Assert.IsFalse(_days.Buckets[Day("20261014")]["leaders"].Contains("alice"), "no cached version holds her run");
        }

        [Test]
        public async Task WithoutTheAdminKey_TheScoreStays_ButHerRunAndGhostGo()
        {
            _store.HasAdminKey = false;
            Assert.IsTrue((await PlayAndSubmit("alice", 52)).Ok);
            DeleteMyDataResponse r = await _daily.ForgetPlayerAsync("alice");
            Assert.IsTrue(r.Ok);
            Assert.IsFalse(r.LiveScoreDeleted);
            StringAssert.StartsWith("no-admin-key", r.LiveScoreNote);
            GetDailyResponse bob = await _daily.GetDailyAsync("bob");
            Assert.IsTrue(ReelPacket.TryDecode(bob.GhostsPacket, out Reel ghosts));
            Assert.AreEqual(0, ghosts.Ghosts.Count(g => g.Leader));
        }

        [Test]
        public async Task ADay_IsDeletedWhole_SevenDaysOn_EvenForPlayersWhoNeverComeBack()
        {
            Assert.IsTrue((await PlayAndSubmit("p", 53)).Ok);
            Assert.IsTrue((await PlayAndSubmit("gone", 54)).Ok);   // never plays again
            Assert.IsTrue(_days.Has(Day("20261014"), "gone"));
            Assert.IsTrue(_days.Has(Day("20261014"), "leaders"));

            _now = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
            await _daily.GetDailyAsync("someone");
            Assert.IsTrue(_days.Buckets.ContainsKey(Day("20261014")), "7 days on: kept");
            _now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
            await _daily.GetDailyAsync("someone");
            Assert.IsFalse(_days.Buckets.ContainsKey(Day("20261014")), "8 days on: the whole day is gone, leaders and all");
            Assert.IsTrue(_store.Items.ContainsKey("p/daily_meta"), "not a day");

            // A quiet month later the sweep still catches up in one call.
            _now = new DateTime(2026, 11, 30, 12, 0, 0, DateTimeKind.Utc);
            await _daily.GetDailyAsync("someone");
            Assert.IsFalse(_days.Buckets.Keys.Any(b => b.StartsWith(DailyService.DayPrefix, StringComparison.Ordinal) && string.CompareOrdinal(b, Day("20261122")) <= 0));
        }

        // The player's own daily ghosts as the server hands them out (run indexes).
        async Task<List<int>> OwnGhosts(string player)
        {
            GetDailyResponse d = await _daily.GetDailyAsync(player);
            Assert.IsTrue(ReelPacket.TryDecode(d.GhostsPacket, out Reel ghosts));
            return ghosts.Ghosts.Where(g => !g.Leader).Select(g => g.RunIndex).ToList();
        }

        // A best, then failed attempts until the player has at least `count` ghosts of their own.
        async Task Haunted(string player, int count)
        {
            Assert.IsTrue((await PlayAndSubmit(player, 7)).Ok);
            for (uint style = 100; style < 400 && (await OwnGhosts(player)).Count < count; style++)
                Assert.IsTrue((await PlayAndSubmit(player, style)).Ok);
            Assert.GreaterOrEqual((await OwnGhosts(player)).Count, count);
        }

        [Test]
        public async Task Banish_RemovesOneOfYourOwnGhosts_ThreeADay_NeverALeader()
        {
            await PlayAndSubmit("leader", 61);
            await Haunted("p", 4);
            GetDailyResponse d = await _daily.GetDailyAsync("p");
            Assert.AreEqual(DailyRules.BanishesPerDay, d.BanishesLeft);
            Assert.IsTrue(ReelPacket.TryDecode(d.GhostsPacket, out Reel given));
            GhostRecord leader = given.Ghosts.FirstOrDefault(g => g.Leader);
            Assert.IsNotNull(leader, "a leader ghost is in the run");
            Assert.AreEqual("not-yours", (await _daily.BanishAsync("p", d.Date, leader.RunIndex)).Error, "leaders can't be banished");
            Assert.AreEqual("not-yours", (await _daily.BanishAsync("p", d.Date, 999_999)).Error);

            List<int> own = await OwnGhosts("p");
            for (int i = 0; i < DailyRules.BanishesPerDay; i++)
            {
                BanishDailyGhostResponse r = await _daily.BanishAsync("p", d.Date, own[i]);
                Assert.IsTrue(r.Ok, r.Error);
                Assert.AreEqual(DailyRules.BanishesPerDay - 1 - i, r.BanishesLeft);
                CollectionAssert.DoesNotContain(await OwnGhosts("p"), own[i]);
            }
            Assert.AreEqual("none-left", (await _daily.BanishAsync("p", d.Date, own[3])).Error);
            Assert.AreEqual(0, (await _daily.GetDailyAsync("p")).BanishesLeft);
            Assert.AreEqual("wrong-day", (await _daily.BanishAsync("q", "2026-10-01", 1)).Error);

            // The next run meets the ghosts that are left, and the server checks it against the same.
            _now = _now.AddSeconds(3);
            Assert.IsTrue((await PlayAndSubmit("p", 450)).Ok);

            // A new day: three more.
            _now = _now.AddDays(1);
            Assert.AreEqual(DailyRules.BanishesPerDay, (await _daily.GetDailyAsync("p")).BanishesLeft);
        }

        [Test]
        public async Task ARunOnTheBoard_CanBeWatched_WholeWithItsGhosts_AndReplaysToItsDistance()
        {
            await PlayAndSubmit("alice", 71);
            SubmitDailyResponse bob = await PlayAndSubmit("bob", 72);   // bob met alice's ghost
            Assert.IsTrue(bob.Ok, bob.Error);
            GetDailyResponse d = await _daily.GetDailyAsync("x");
            for (int rank = 1; rank <= d.Board.Count; rank++)
            {
                GetDailyRunResponse r = await _daily.GetRunAsync(rank);
                Assert.IsTrue(r.Ok, r.Error);
                Assert.AreEqual(d.Board[rank - 1].Name, r.Name);
                Assert.IsTrue(ReelPacket.TryDecode(r.Packet, out Reel reel));
                ReplayResult replay = RunReplay.Replay(reel, Content.CourseFor(reel.Seed), Content.Config);
                Assert.AreEqual(r.Distance, replay.Distance, $"rank {rank} replays exactly");
                Assert.AreEqual(r.Distance, reel.Distance, "the server's own distance, not the claim");
            }
            GetDailyRunResponse bobs = await _daily.GetRunAsync(d.Board.First(e => e.Name == "BOB#1").Rank);
            Assert.IsTrue(ReelPacket.TryDecode(bobs.Packet, out Reel bobReel));
            Assert.IsTrue(bobReel.Ghosts.Any(g => g.Leader && g.Name == "ALICE#1"), "with the leader ghost bob faced");
            Assert.AreEqual("not-found", (await _daily.GetRunAsync(d.Board.Count + 1)).Error);
            Assert.AreEqual("not-found", (await _daily.GetRunAsync(0)).Error);
        }

        [Test]
        public async Task TheNextLeaderToBeat_IsTheNearestAhead()
        {
            await PlayAndSubmit("a", 31);
            await PlayAndSubmit("b", 32);
            _now = _now.AddMinutes(6);
            var (d, reel) = await Play("c", 33);
            _now = _now.AddSeconds(3);
            SubmitDailyResponse r = await _daily.SubmitAsync("c", d.Date, d.LeaderVersion, ReelPacket.Encode(reel));
            Assert.IsTrue(r.Ok, r.Error);
            Assert.IsTrue(ReelPacket.TryDecode(d.GhostsPacket, out Reel ghosts));
            var leaderDistances = d.Board.Where(e => !e.IsYou).Select(e => e.Distance).ToList();
            Assert.AreEqual(DailyRules.Passed(leaderDistances, r.Distance), r.LeadersPassed);
            int next = DailyRules.NextToBeat(leaderDistances, r.Distance);
            Assert.AreEqual(next < 0 ? 0 : next, r.NextLeaderDistance);
        }
    }
}
