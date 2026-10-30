using System;
using System.Collections.Generic;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Modes;
using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Core.Rules;
using Echo.Server;
using Echo.Tests.EditMode;
using EchoCloud;
using NUnit.Framework;

namespace Echo.Server.Tests
{
    /// <summary>
    /// Ghost Duels (modes doc): single-use tokens; opponents near your rating, never your own run nor the same one twice
    /// running, falling back to reference runs; both ratings move (the opponent at half size, with a note); 10 ranked a
    /// day, practice unlimited and unrated; an abandoned ranked duel is a loss; the soft reset between seasons; rewards
    /// by final tier; Delete online data.
    /// </summary>
    public class RankedServiceTests
    {
        static readonly GameContent Content = GameContent.Shared;

        static long At(string utc) => DateTimeOffset.Parse(utc + "Z").ToUnixTimeSeconds();

        static readonly List<uint> Arenas = new List<uint> { 104729, 224737, 350377, 479909, 611953, 746773, 882377, 1020379, 1159523, 1299709 };

        static readonly List<RankedSeason> Seasons = new List<RankedSeason>
        {
            new RankedSeason { Number = 1, Start = At("2027-02-01T00:00:00"), End = At("2027-03-29T00:00:00"), Arenas = Arenas },
            new RankedSeason { Number = 2, Start = At("2027-04-05T00:00:00"), End = At("2027-06-01T00:00:00"), Arenas = Arenas },
        };

        static readonly List<ModeWindow> Windows = new List<ModeWindow>
        {
            new ModeWindow { Id = ModeIds.Ranked, Enabled = true, Start = At("2027-02-01T00:00:00"), End = At("2028-02-01T00:00:00") },
        };

        MemoryDayStore _days;
        MemoryDailyStore _players;
        Dictionary<int, MemoryDailyStore> _boards;

        [SetUp]
        public void SetUp()
        {
            _days = new MemoryDayStore();
            _players = new MemoryDailyStore();
            _boards = new Dictionary<int, MemoryDailyStore>();
        }

        MemoryDailyStore Board(RankedSeason s) =>
            _boards.TryGetValue(s.Number, out MemoryDailyStore b) ? b : _boards[s.Number] = new MemoryDailyStore { KeepLatest = true };

        RankedService On(string utc, IReadOnlyList<RankedReference> references = null) =>
            new RankedService(Content, _days, _players, Windows, Seasons, Board, references ?? RankedReference.Shipped, At(utc), false);

        // A duel played the way the game plays one: the arena, the rival alongside.
        static string Play(RankedStartResponse start, uint player)
        {
            Assert.IsTrue(start.Ok, start.Error);
            Assert.IsTrue(ReelPacket.TryDecode(start.OpponentPacket, out Reel rival));
            Course course = Content.CourseFor(start.Seed);
            RunSimulation sim = RankedRules.Start(course, rival.Run, Content.Config);
            ParityRuns.Played played = ParityRuns.Drive(sim, course, player, Content.Config, Content.ContentVersion);
            Assert.IsNotNull(played.Reel, "the look-ahead player finished a run");
            return ReelPacket.Encode(played.Reel);
        }

        RankedSubmitResponse Duel(string utc, string player, uint seed, bool ranked = true)
        {
            RankedService day = On(utc);
            RankedStartResponse start = day.StartAsync(player, ranked).Result;
            RankedSubmitResponse r = day.SubmitAsync(player, start.Token, Play(start, seed)).Result;
            Assert.IsTrue(r.Ok, r.Error);
            return r;
        }

        RankedService.PlayerSeason Record(int season, string player) =>
            System.Text.Json.JsonSerializer.Deserialize<RankedService.PlayerSeason>(_days.GetAsync(RankedService.SeasonPrefix + season, player).Result);

        [Test]
        public void ReferenceRuns_ShipForEveryArena()
        {
            foreach (uint seed in Arenas)
                Assert.GreaterOrEqual(RankedReference.Shipped.Count(r => r.Seed == seed), 3, $"arena {seed} has reference runs");
            foreach (RankedReference r in RankedReference.Shipped.Take(5))
            {
                Assert.IsTrue(ReelPacket.TryDecode(r.Packet, out Reel reel));
                Assert.AreEqual(r.Distance, RunReplay.Replay(reel, Content.CourseFor(r.Seed), Content.Config).Distance, "replays to its distance");
            }
        }

        [Test]
        public void TheToken_IsSingleUse_AndOnlyYours()
        {
            RankedService day = On("2027-02-10T10:00:00");
            RankedStartResponse start = day.StartAsync("a", true).Result;
            string packet = Play(start, 7);
            Assert.AreEqual("token", day.SubmitAsync("b", start.Token, packet).Result.Error, "someone else's token");
            Assert.AreEqual("token", day.SubmitAsync("a", "guess", packet).Result.Error);
            Assert.IsTrue(day.SubmitAsync("a", start.Token, packet).Result.Ok);
            Assert.AreEqual("token", day.SubmitAsync("a", start.Token, packet).Result.Error, "used up");
        }

        [Test]
        public void ARankedDuel_MovesBothRatings_AndPoolsTheRun()
        {
            RankedSubmitResponse first = Duel("2027-02-10T10:00:00", "a", 3);   // against a reference run: no one else yet
            Assert.AreEqual(RankedRules.StartRating, first.RatingBefore);
            Assert.AreEqual(first.RatingBefore + first.Change, first.RatingAfter);
            Assert.AreNotEqual(0, first.Change, "a new player's duel always moves the rating");
            Assert.AreEqual(first.Distance > first.OpponentDistance ? "won" : first.Distance == first.OpponentDistance ? "draw" : "lost", first.Outcome);

            // b is matched with a's run (pooled, and within 150), and a's rating moves at half size.
            RankedService day = On("2027-02-10T11:00:00");
            RankedStartResponse start = day.StartAsync("b", true).Result;
            Assert.AreNotEqual("Echo Ghost", start.OpponentName, "a player's run before a reference");
            int aBefore = Record(1, "a").Rating;
            RankedSubmitResponse r = day.SubmitAsync("b", start.Token, Play(start, 11)).Result;
            Assert.IsTrue(r.Ok, r.Error);
            RankedService.PlayerSeason a = Record(1, "a");
            int theirs = RankedRules.OpponentChange(aBefore, r.RatingBefore, 1, r.Outcome == "won" ? 0 : r.Outcome == "lost" ? 1000 : 500);
            Assert.AreEqual(aBefore + theirs, a.Rating, "the opponent at half size");
            Assert.AreEqual(r.Outcome == "lost" ? 1 : 0, a.AwayWins, "a's ghost won while a was away");
            Assert.AreEqual(a.Rating, Board(Seasons[0]).Today["a"], "the board follows the opponent too");

            RankedProfileResponse profile = On("2027-02-10T12:00:00").ProfileAsync("a").Result;
            Assert.AreEqual(a.AwayWins, profile.AwayWins);
            Assert.AreEqual(0, On("2027-02-10T12:05:00").ProfileAsync("a").Result.AwayWins, "the note shows once");
        }

        [Test]
        public void Matching_NeverYourOwnRun_NorTheSameOpponentTwiceRunning()
        {
            Duel("2027-02-10T10:00:00", "a", 3);
            Duel("2027-02-10T10:05:00", "b", 5);
            for (int i = 0; i < 4; i++)
            {
                RankedService day = On($"2027-02-10T1{1 + i}:00:00");
                RankedStartResponse start = day.StartAsync("a", true).Result;
                Assert.IsTrue(start.Ok, start.Error);
                string last = Record(1, "a").LastOpponent;
                Assert.IsTrue(day.SubmitAsync("a", start.Token, Play(start, (uint)(20 + i))).Result.Ok);
                RankedService.PlayerSeason a = Record(1, "a");
                Assert.AreNotEqual("p:a", a.LastOpponent, "never your own run");
                Assert.AreNotEqual(last, a.LastOpponent, "never the same opponent twice in a row");
            }
        }

        [Test]
        public void TenRankedADay_PracticeUnlimited_AndPracticeChangesNothing()
        {
            for (int i = 0; i < RankedRules.RankedPerDay; i++)
                Assert.IsTrue(On("2027-02-10T10:00:00").StartAsync("a", true).Result.Ok, $"ranked duel {i + 1}");
            Assert.AreEqual("limit", On("2027-02-10T11:00:00").StartAsync("a", true).Result.Error);
            int rating = Record(1, "a").Rating;
            RankedSubmitResponse practice = Duel("2027-02-10T11:30:00", "a", 9, ranked: false);
            Assert.IsFalse(practice.Ranked);
            Assert.AreEqual(rating, Record(1, "a").Rating, "practice moves no rating");
            Assert.IsTrue(On("2027-02-11T09:00:00").StartAsync("a", true).Result.Ok, "a new day, ten more");
        }

        [Test]
        public void AnAbandonedRankedDuel_IsALoss()
        {
            On("2027-02-10T10:00:00").StartAsync("a", true).Wait();
            On("2027-02-10T10:01:00").StartAsync("a", true).Wait();   // starting again forfeits the first
            RankedService.PlayerSeason a = Record(1, "a");
            Assert.AreEqual(1, a.Duels);
            Assert.Less(a.Rating, RankedRules.StartRating);
            Assert.AreEqual("lost", a.Recent[0].Outcome);

            // Left unsent past the forfeit time: a loss, and the token no longer counts.
            RankedStartResponse open = On("2027-02-10T10:02:00").StartAsync("b", true).Result;
            string packet = Play(open, 5);
            Assert.AreEqual("token", On("2027-02-10T10:30:00").SubmitAsync("b", open.Token, packet).Result.Error);
            Assert.AreEqual(1, Record(1, "b").Duels);
        }

        [Test]
        public void TheNextSeason_StartsHalfwayBackTo1000_AndRewardsGoByFinalTier()
        {
            Duel("2027-02-10T10:00:00", "a", 3);
            RankedService.PlayerSeason a = Record(1, "a");
            a.Rating = 1420;
            _days.SetAsync(RankedService.SeasonPrefix + 1, "a", System.Text.Json.JsonSerializer.Serialize(a)).Wait();
            Board(Seasons[0]).Today["a"] = 1420;
            for (int i = 0; i < 150; i++) Board(Seasons[0]).Today["x" + i] = 2000 + i;   // a is outside the top 100

            RankedProfileResponse after = On("2027-04-01T09:00:00").ProfileAsync("a").Result;
            Assert.AreEqual("closed", after.Error, "between seasons");
            CollectionAssert.AreEquivalent(new[] { "trail_duel_phantom", "duel_phantom_s1" }, after.Rewards, "Phantom by final rating");
            Assert.IsEmpty(On("2027-04-02T09:00:00").ProfileAsync("a").Result.Rewards, "once");

            RankedProfileResponse season2 = On("2027-04-06T09:00:00").ProfileAsync("a").Result;
            Assert.IsTrue(season2.Ok, season2.Error);
            Assert.AreEqual(2, season2.Season);
            Assert.AreEqual(RankedRules.SoftReset(1420), season2.Rating);
        }

        [Test]
        public void ClosedOutsideItsWindow_OrWithoutASeason()
        {
            Assert.AreEqual("closed", On("2027-01-20T10:00:00").StartAsync("a", true).Result.Error);
            Assert.AreEqual("closed", On("2027-04-01T10:00:00").StartAsync("a", true).Result.Error, "between seasons");
        }

        [Test]
        public void DeletingYourData_RemovesYourSeasonsRunsAndBoardEntries()
        {
            Duel("2027-02-10T10:00:00", "a", 3);
            Duel("2027-02-10T10:05:00", "b", 5);
            RankedService.ForgetPlayerAsync(_days, Seasons, Board, "a").Wait();
            Assert.IsNull(_days.GetAsync(RankedService.SeasonPrefix + 1, "a").Result);
            Assert.IsFalse(Board(Seasons[0]).Today.ContainsKey("a"));
            foreach (var kv in _days.GetAllAsync(RankedService.PoolPrefix + 1).Result)
                StringAssert.DoesNotContain("\"Player\":\"a\"", kv.Value);
        }
    }
}
