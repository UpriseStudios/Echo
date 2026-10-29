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
    /// One Life (modes doc): one attempt per player per day, a single-use token, the run replayed against exactly the
    /// leader ghosts it was given, a run closed mid-way counting where it stopped, its own seed, and rewards once the day
    /// has closed (Christmas Day gravestones, Sunday badges).
    /// </summary>
    public class OneLifeServiceTests
    {
        static readonly GameContent Content = GameContent.Shared;
        const string Salt = "test-salt";

        static long At(string utc) => DateTimeOffset.Parse(utc + "Z").ToUnixTimeSeconds();

        static readonly List<ModeWindow> Schedule = new List<ModeWindow>
        {
            new ModeWindow { Id = ModeIds.OneLife, Enabled = true, Start = At("2026-12-25T00:00:00"), End = At("2026-12-26T00:00:00") },
            new ModeWindow { Id = ModeIds.OneLife, Enabled = true, Start = At("2027-01-01T00:00:00"), End = At("2028-01-01T00:00:00"), Weekdays = 1 },
        };

        MemoryDayStore _days;
        MemoryDailyStore _board;

        [SetUp]
        public void SetUp()
        {
            _days = new MemoryDayStore();
            _board = new MemoryDailyStore();
        }

        OneLifeService On(string utc) => new OneLifeService(Content, _board, _days, Schedule, At(utc), false, Salt);

        // A run played the way the game plays one: today's course and the leader ghosts it was given.
        Reel Play(OneLifeStartResponse start, uint player, bool abandonEarly = false)
        {
            Assert.IsTrue(start.Ok, start.Error);
            Assert.IsTrue(ReelPacket.TryDecode(start.GhostsPacket, out Reel given));
            Course course = Content.CourseFor(start.Seed);
            RunSimulation sim = OneLifeRules.Start(course, given.Ghosts, Content.Config);
            ParityRuns.Played played = ParityRuns.Drive(sim, course, player, Content.Config, Content.ContentVersion);
            Assert.IsNotNull(played.Reel, "the look-ahead player finished a run");
            if (abandonEarly)
            {
                played.Reel.Abandoned = true;
                played.Reel.Run.DeathTick = Math.Min(120, played.Reel.Run.DeathTick);
            }
            return played.Reel;
        }

        [Test]
        public void OneAttemptADay_OnlyOnItsDays()
        {
            Assert.AreEqual("closed", On("2026-12-24T12:00:00").StartAsync("a").Result.Error);
            Assert.AreEqual("closed", On("2027-01-04T12:00:00").StartAsync("a").Result.Error, "a Monday");
            OneLifeStartResponse start = On("2026-12-25T09:00:00").StartAsync("a").Result;
            Assert.IsTrue(start.Ok);
            Assert.AreEqual("used", On("2026-12-25T09:05:00").StartAsync("a").Result.Error, "the attempt is used the moment the run starts");
            Assert.IsTrue(On("2027-01-03T12:00:00").StartAsync("a").Result.Ok, "a Sunday: a new day, a new attempt");
            Assert.AreNotEqual(start.Seed, On("2027-01-03T12:00:00").StartAsync("b").Result.Seed, "its own course each day");
            Assert.AreNotEqual(DailyService.SeedFor("2026-12-25", Salt), start.Seed, "and not the Daily Haunt's");
        }

        [Test]
        public void TheToken_IsNeeded_AndSingleUse()
        {
            OneLifeService day = On("2026-12-25T09:00:00");
            OneLifeStartResponse start = day.StartAsync("a").Result;
            string packet = ReelPacket.Encode(Play(start, 7));
            Assert.AreEqual("token", day.SubmitAsync("a", start.Date, "guess", packet).Result.Error);
            Assert.AreEqual("token", day.SubmitAsync("b", start.Date, start.Token, packet).Result.Error, "someone else's token");
            OneLifeSubmitResponse first = day.SubmitAsync("a", start.Date, start.Token, packet).Result;
            Assert.IsTrue(first.Ok, first.Error);
            Assert.Greater(first.Distance, 0);
            Assert.AreEqual(1, first.Rank);
            Assert.AreEqual("token", day.SubmitAsync("a", start.Date, start.Token, packet).Result.Error, "used up");
        }

        [Test]
        public void TheRun_FacesTodaysLeaders_AndKeepsTheRules()
        {
            OneLifeService day = On("2026-12-25T09:00:00");
            OneLifeStartResponse a = day.StartAsync("a").Result;
            Assert.IsTrue(day.SubmitAsync("a", a.Date, a.Token, ReelPacket.Encode(Play(a, 11))).Result.Ok);

            OneLifeStartResponse b = day.StartAsync("b").Result;
            Assert.IsTrue(ReelPacket.TryDecode(b.GhostsPacket, out Reel given));
            Assert.AreEqual(1, given.Ghosts.Count, "a's run is today's leader ghost");
            Assert.IsTrue(given.Ghosts[0].Leader);
            Reel run = Play(b, 13);

            Reel easier = Clone(run);
            easier.Ghosts.Clear();
            Assert.AreEqual("ghosts", day.SubmitAsync("b", b.Date, b.Token, ReelPacket.Encode(easier)).Result.Error);
            Reel lives = Clone(run);
            lives.Options = new RunOptions { Daily = true };
            Assert.AreEqual("options", day.SubmitAsync("b", b.Date, b.Token, ReelPacket.Encode(lives)).Result.Error);
            Reel cheat = Clone(run);
            cheat.ContinueTick = 50;
            Assert.AreEqual("power-ups", day.SubmitAsync("b", b.Date, b.Token, ReelPacket.Encode(cheat)).Result.Error);
            Assert.IsTrue(day.SubmitAsync("b", b.Date, b.Token, ReelPacket.Encode(run)).Result.Ok, "the honest run still counts");
        }

        static Reel Clone(Reel r)
        {
            Assert.IsTrue(ReelPacket.TryDecode(ReelPacket.Encode(r), out Reel copy));
            return copy;
        }

        [Test]
        public void ARunClosedMidway_CountsWhereItStopped_AndAfterItsDayIsKeptButNotRanked()
        {
            OneLifeService day = On("2026-12-25T23:58:00");
            OneLifeStartResponse start = day.StartAsync("a").Result;
            Reel abandoned = Play(start, 5, abandonEarly: true);
            OneLifeSubmitResponse late = On("2026-12-26T08:00:00").SubmitAsync("a", start.Date, start.Token, ReelPacket.Encode(abandoned)).Result;
            Assert.IsTrue(late.Ok, late.Error);
            Assert.IsTrue(late.Abandoned);
            Assert.IsTrue(late.BoardClosed, "the day's board had reset");
            Assert.AreEqual(0, late.Rank);
            Assert.IsEmpty(_board.Today);
            Assert.That(late.Distance, Is.InRange(5, 20), "two seconds in, about 13 m");
        }

        [Test]
        public void ChristmasDay_GivesFinishersItsGravestone_TheTopThreeGold_OnceTheDayCloses()
        {
            OneLifeService christmas = On("2026-12-25T10:00:00");
            var players = new[] { "p1", "p2", "p3", "p4" };
            foreach (string p in players)
            {
                OneLifeStartResponse s = christmas.StartAsync(p).Result;
                Assert.IsTrue(christmas.SubmitAsync(p, s.Date, s.Token, ReelPacket.Encode(Play(s, (uint)(31 + Array.IndexOf(players, p) * 97)))).Result.Ok);
            }
            Assert.IsEmpty(christmas.GetAsync("p1").Result.Rewards, "not until the day has closed");

            var after = On("2026-12-26T10:00:00");
            var rewards = players.ToDictionary(p => p, p => after.GetAsync(p).Result.Rewards);
            Assert.IsTrue(rewards.Values.All(r => r.Contains("grave_xmasday_2026")), "everyone who finished");
            Assert.AreEqual(3, rewards.Values.Count(r => r.Contains("grave_xmasday_2026_gold")), "the day's top 3");
            Assert.IsEmpty(after.GetAsync("p1").Result.Rewards, "granted once");
            CollectionAssert.Contains(RewardStore.ReadAsync(_board, "p1").Result.Cosmetics, "grave_xmasday_2026");
        }

        [Test]
        public void ASunday_BadgesTheTop100_ForAWeek()
        {
            OneLifeService sunday = On("2027-01-03T10:00:00");
            OneLifeStartResponse s = sunday.StartAsync("a").Result;
            Assert.IsTrue(sunday.SubmitAsync("a", s.Date, s.Token, ReelPacket.Encode(Play(s, 77))).Result.Ok);
            CollectionAssert.AreEqual(new[] { "onelife_20270103" }, On("2027-01-04T09:00:00").GetAsync("a").Result.Rewards);
            Assert.IsNotEmpty(TeamService.BadgesAsync(_days, new[] { "a" }, At("2027-01-09T00:00:00")).Result);
            Assert.IsEmpty(TeamService.BadgesAsync(_days, new[] { "a" }, At("2027-01-12T00:00:00")).Result, "for that week");
        }

        [Test]
        public void Deleting_RemovesTheRecords_AndTheScore()
        {
            OneLifeService day = On("2026-12-25T10:00:00");
            OneLifeStartResponse s = day.StartAsync("a").Result;
            day.SubmitAsync("a", s.Date, s.Token, ReelPacket.Encode(Play(s, 3))).Wait();
            OneLifeService.ForgetPlayerAsync(_days, _board, "a", new DateTime(2026, 12, 25)).Wait();
            Assert.IsFalse(_days.Has(OneLifeService.Bucket("2026-12-25"), "a"));
            Assert.IsEmpty(_board.Today);
        }
    }
}
