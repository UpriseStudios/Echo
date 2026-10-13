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
            public Task<string> GetGameAsync(string k) => Task.FromResult(Items.TryGetValue("game/" + k, out string v) ? v : null);
            public Task SetGameAsync(string k, string json) { Items["game/" + k] = json; return Task.CompletedTask; }

            public Task SubmitAsync(string playerId, int distance, bool yesterday)
            {
                var board = yesterday ? Yesterday : Today;
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

            // The leaderboard resets at 00:00 UTC with an archive.
            public void Midnight()
            {
                Yesterday = Today;
                Today = new Dictionary<string, int>();
            }
        }

        MemoryDailyStore _store;
        DateTime _now;
        DailyService _daily;

        [SetUp]
        public void SetUp()
        {
            _store = new MemoryDailyStore();
            _now = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
            _daily = new DailyService(Content, _store, () => _now, Salt);
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

        [Test]
        public async Task TheLeaderSet_IsKeptFiveMinutes_ThenRebuiltAsANewVersion()
        {
            GetDailyResponse a = await _daily.GetDailyAsync("x");
            _now = _now.AddMinutes(4);
            Assert.AreEqual(a.LeaderVersion, (await _daily.GetDailyAsync("x")).LeaderVersion);
            _now = _now.AddMinutes(2);
            Assert.AreEqual(a.LeaderVersion + 1, (await _daily.GetDailyAsync("x")).LeaderVersion);
        }

        [Test]
        public async Task ARunStartedAgainstAnOlderLeaderSet_StillReplays_UntilItFallsOutOfTheCache()
        {
            var (d, reel) = await Play("p", 3);
            for (int i = 0; i < 5; i++)
            {
                _now = _now.AddMinutes(6);
                await _daily.GetDailyAsync("someone");
            }
            Assert.IsTrue((await _daily.SubmitAsync("p", d.Date, d.LeaderVersion, ReelPacket.Encode(reel))).Ok, "within the hour");
            var (d2, reel2) = await Play("q", 4);
            for (int i = 0; i < DailyService.LeaderVersionsKept + 1; i++)
            {
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
            async Task<SubmitDailyResponse> Submit(string packet, string date = null, int? version = null)
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
            Assert.AreEqual("leader-version", (await Submit(ReelPacket.Encode(reel), version: 999)).Error);

            Assert.IsTrue(ReelPacket.TryDecode(ReelPacket.Encode(reel), out Reel edited));
            edited.Run.TapTicks = edited.Run.TapTicks.Take(edited.Run.TapTicks.Length / 2).ToArray();
            edited.Run.ReleaseTicks = new int[0];
            SubmitDailyResponse retapped = await Submit(ReelPacket.Encode(edited));
            Assert.IsTrue(retapped.Ok || retapped.Error == "unfinished", "edited taps are replayed as sent: no faked result");

            _now = _now.AddSeconds(1);
            Assert.AreEqual("rate-limit", (await _daily.SubmitAsync("cheat", d.Date, d.LeaderVersion, ReelPacket.Encode(reel))).Error);
        }

        [Test]
        public async Task JustAfterMidnight_YesterdaysRunsCountForFiveMinutes_OnYesterdaysBoard()
        {
            _now = new DateTime(2026, 10, 14, 23, 59, 0, DateTimeKind.Utc);
            var (d, reel) = await Play("late", 21);
            string packet = ReelPacket.Encode(reel);
            _store.Midnight();
            _now = new DateTime(2026, 10, 15, 0, 3, 0, DateTimeKind.Utc);
            SubmitDailyResponse r = await _daily.SubmitAsync("late", d.Date, d.LeaderVersion, packet);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.IsTrue(_store.Yesterday.ContainsKey("late"), "on yesterday's board");
            Assert.IsFalse(_store.Today.ContainsKey("late"));

            var (d2, reel2) = await Play("later", 22);   // today's course now
            Assert.AreNotEqual(d.Seed, d2.Seed);
            _now = new DateTime(2026, 10, 15, 0, 6, 0, DateTimeKind.Utc);
            Assert.AreEqual("wrong-day", (await _daily.SubmitAsync("late", d.Date, d.LeaderVersion, packet)).Error, "past the grace period");

            GetDailyResponse next = await _daily.GetDailyAsync("late");
            Assert.AreEqual(1, next.YesterdayRank);
            Assert.AreEqual("LATE#1", next.YesterdayWinner);
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
