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
    /// The Community Graveyard (modes doc): one course a week, Monday clean, each day's ghosts chosen from the deaths
    /// before it (the same for everyone and however often it's built), a run checked against the set it played (yesterday's
    /// too, just after midnight), the week board as the sum of daily bests, and rewards once the week has closed.
    /// </summary>
    public class GraveyardServiceTests
    {
        static readonly GameContent Content = GameContent.Shared;
        const string Salt = "test-salt";

        static long At(string utc) => DateTimeOffset.Parse(utc + "Z").ToUnixTimeSeconds();

        // 18 January 2027 is a Monday.
        static readonly List<ModeWindow> Schedule = new List<ModeWindow>
        {
            new ModeWindow { Id = ModeIds.Graveyard, Enabled = true, Start = At("2027-01-18T00:00:00"), End = At("2028-01-17T00:00:00") },
        };

        MemoryDayStore _days;
        MemoryDailyStore _dayBoard, _weekBoard;

        [SetUp]
        public void SetUp()
        {
            _days = new MemoryDayStore();
            _dayBoard = new MemoryDailyStore();
            _weekBoard = new MemoryDailyStore();
        }

        GraveyardService On(string utc) => new GraveyardService(Content, _dayBoard, _weekBoard, _days, Schedule, At(utc), false, Salt);

        // A run played the way the game plays one: the week's course and the day's ghosts.
        static Reel Play(GraveyardResponse lobby, uint player)
        {
            Assert.IsTrue(lobby.Ok, lobby.Error);
            Assert.IsTrue(ReelPacket.TryDecode(lobby.GhostsPacket, out Reel given));
            Course course = Content.CourseFor(lobby.Seed);
            RunSimulation sim = GraveyardRules.Start(course, given.Ghosts, lobby.NextRunIndex, Content.Config);
            ParityRuns.Played played = ParityRuns.Drive(sim, course, player, Content.Config, Content.ContentVersion);
            Assert.IsNotNull(played.Reel, "the look-ahead player finished a run");
            return played.Reel;
        }

        GraveyardSubmitResponse Run(string utc, string player, uint seed)
        {
            GraveyardService day = On(utc);
            GraveyardResponse lobby = day.GetAsync(player).Result;
            GraveyardSubmitResponse r = day.SubmitAsync(player, lobby.Date, lobby.SetVersion, ReelPacket.Encode(Play(lobby, seed))).Result;
            Assert.IsTrue(r.Ok, r.Error);
            return r;
        }

        static int Ghosts(GraveyardResponse lobby) => ReelPacket.TryDecode(lobby.GhostsPacket, out Reel r) ? r.Ghosts.Count : -1;

        [Test]
        public void Closed_OutsideItsWindow()
        {
            Assert.AreEqual("closed", On("2027-01-17T12:00:00").GetAsync("a").Result.Error);
            Assert.AreEqual("closed", On("2027-01-17T12:00:00").SubmitAsync("a", "2027-01-17", "", "").Result.Error);
        }

        [Test]
        public void OneCourseAWeek_MondayStartsClean()
        {
            GraveyardResponse monday = On("2027-01-18T09:00:00").GetAsync("a").Result;
            Assert.AreEqual(0, Ghosts(monday), "no deaths before Monday");
            Assert.AreEqual(0, monday.Size);
            Assert.AreEqual(0, monday.Day);
            Assert.AreEqual(6, monday.DaysLeft);
            Assert.AreEqual(monday.Seed, On("2027-01-24T23:00:00").GetAsync("a").Result.Seed, "the same course all week");
            Assert.AreNotEqual(monday.Seed, On("2027-01-25T09:00:00").GetAsync("a").Result.Seed, "a new course next Monday");
            Assert.AreNotEqual(DailyService.SeedFor("2027-01-18", Salt), monday.Seed, "and not the Daily Haunt's");
            Assert.AreEqual(0, Ghosts(On("2027-01-25T09:00:00").GetAsync("a").Result), "next Monday starts clean again");
        }

        [Test]
        public void TodaysDeaths_FeedTomorrowsGhosts_TheSameForEveryone()
        {
            for (uint p = 1; p <= 5; p++) Run("2027-01-18T10:00:00", "p" + p, p * 7919);
            GraveyardResponse sameDay = On("2027-01-18T20:00:00").GetAsync("a").Result;
            Assert.AreEqual(0, Ghosts(sameDay), "today's deaths wait for tomorrow");
            Assert.AreEqual(5, sameDay.Size, "but the graveyard's size counts them");

            GraveyardResponse a = On("2027-01-19T00:00:10").GetAsync("a").Result;
            GraveyardResponse b = On("2027-01-19T15:00:00").GetAsync("b").Result;
            int sections = _days.Buckets[GraveyardService.SectionPrefix + "20270118"].Keys.Count(k => k.StartsWith("s_", StringComparison.Ordinal));
            Assert.That(Ghosts(a), Is.InRange(Math.Min(5, sections), Math.Min(5, sections * GraveyardRules.PerSection)), "up to 3 from each section");
            Assert.AreEqual(a.SetVersion, b.SetVersion, "two players on the same day");
            Assert.AreEqual(a.GhostsPacket, b.GhostsPacket);

            // Built again from nothing (the trigger and a player racing), the same set.
            _days.Buckets[GraveyardService.WeekPrefix + "20270118"].Remove("set_20270119");
            GraveyardService.DaySet rebuilt = On("2027-01-19T15:00:00").SetForAsync(new DateTime(2027, 1, 19)).Result;
            Assert.AreEqual(a.SetVersion, rebuilt.Version);
            Assert.AreEqual(a.GhostsPacket, rebuilt.Packet);

            Assert.IsTrue(ReelPacket.TryDecode(a.GhostsPacket, out Reel set));
            Assert.IsTrue(set.Ghosts.All(g => g.Leader && g.Name.StartsWith("This spot has claimed", StringComparison.Ordinal)));
        }

        [Test]
        public void Choose_IsDeterministic_AndSpreadsAlongTheCourse()
        {
            var sections = Enumerable.Range(0, 20).Select(i => new SectionTally
            {
                Index = i, Deaths = (i * 7) % 5 + 1, Runs = Enumerable.Range(0, 6).Select(k => $"d/{i}_{k}").ToList(),
            }).ToList();
            var first = GraveyardRules.Choose(sections);
            sections.Reverse();
            Assert.AreEqual(first, GraveyardRules.Choose(sections), "input order doesn't matter");
            Assert.AreEqual(GraveyardRules.MaxGhosts, first.Count);
            Assert.IsTrue(first.GroupBy(c => c.Run.Split('_')[0]).All(g => g.Count() <= GraveyardRules.PerSection));
            Assert.IsTrue(first.Zip(first.Skip(1), (x, y) => x.Deaths >= y.Deaths).All(ok => ok), "deadliest first");

            var skipping = GraveyardRules.Choose(sections, run => !run.EndsWith("_0", StringComparison.Ordinal));
            Assert.IsFalse(skipping.Any(c => c.Run.EndsWith("_0", StringComparison.Ordinal)), "deleted runs are skipped");
            Assert.AreEqual(GraveyardRules.MaxGhosts, skipping.Count, "and the section's other candidates fill in");
        }

        [Test]
        public void ARun_IsCheckedAgainstTheSetItPlayed_YesterdaysJustAfterMidnight()
        {
            Run("2027-01-18T10:00:00", "p1", 11);
            Run("2027-01-18T10:00:00", "p2", 23);
            GraveyardService tuesday = On("2027-01-19T23:58:00");
            GraveyardResponse lobby = tuesday.GetAsync("a").Result;
            string packet = ReelPacket.Encode(Play(lobby, 41));

            Assert.AreEqual("ghosts", tuesday.SubmitAsync("a", lobby.Date, "0000", packet).Result.Error, "a version that wasn't given");
            GraveyardSubmitResponse late = On("2027-01-20T00:03:00").SubmitAsync("a", lobby.Date, lobby.SetVersion, packet).Result;
            Assert.IsTrue(late.Ok, late.Error);
            Assert.IsTrue(late.BoardClosed, "kept, not ranked");
            Assert.AreEqual(0, late.DailyRank);
            Assert.AreEqual("wrong-day", On("2027-01-20T00:10:00").SubmitAsync("a", lobby.Date, lobby.SetVersion, packet).Result.Error);
            Assert.AreEqual("run-index", On("2027-01-20T00:04:00").SubmitAsync("a", lobby.Date, lobby.SetVersion, packet).Result.Error, "sent twice");
        }

        [Test]
        public void TamperedRuns_AreRefused()
        {
            GraveyardService day = On("2027-01-18T10:00:00");
            GraveyardResponse lobby = day.GetAsync("a").Result;
            Reel reel = Play(lobby, 5);
            Assert.AreEqual("malformed", day.SubmitAsync("a", lobby.Date, lobby.SetVersion, "junk").Result.Error);
            Assert.AreEqual("wrong-day", day.SubmitAsync("a", "2027-01-15", lobby.SetVersion, ReelPacket.Encode(reel)).Result.Error);
            reel.Seed ^= 1;
            Assert.AreEqual("seed", day.SubmitAsync("a", lobby.Date, lobby.SetVersion, ReelPacket.Encode(reel)).Result.Error);
            reel.Seed ^= 1;
            reel.Options.Graveyard = false;
            Assert.AreEqual("options", day.SubmitAsync("a", lobby.Date, lobby.SetVersion, ReelPacket.Encode(reel)).Result.Error);
        }

        [Test]
        public void TheWeekBoard_IsTheSumOfDailyBests()
        {
            GraveyardSubmitResponse mon1 = Run("2027-01-18T10:00:00", "a", 3);
            GraveyardSubmitResponse mon2 = Run("2027-01-18T11:00:00", "a", 9);
            int monday = Math.Max(mon1.Distance, mon2.Distance);
            Assert.AreEqual(monday, mon2.BestToday);
            _dayBoard.Midnight();
            GraveyardSubmitResponse tue = Run("2027-01-19T10:00:00", "a", 17);
            Assert.AreEqual(monday + tue.Distance, tue.WeekTotal);
            Assert.AreEqual(monday + tue.Distance, _weekBoard.Today["a"]);
            Assert.AreEqual(tue.Distance, _dayBoard.Today["a"]);
            Assert.AreEqual(1, tue.WeeklyRank);
        }

        [Test]
        public void ClosedWeeks_PayBadgesToTheTop10_AndCountFullWeeksForTheSkin()
        {
            for (int d = 0; d < 7; d++) Run($"2027-01-{18 + d}T10:00:00", "a", (uint)(d + 1));
            Run("2027-01-18T12:00:00", "b", 77);
            List<string> a = On("2027-01-25T09:00:00").GetAsync("a").Result.Rewards;
            CollectionAssert.AreEqual(new[] { "gravekeeper_20270118" }, a);
            Assert.IsEmpty(On("2027-01-25T10:00:00").GetAsync("a").Result.Rewards, "settled once");
            Assert.IsTrue(On("2027-01-25T09:00:00").GetAsync("b").Result.Rewards.Contains("gravekeeper_20270118"), "top 10 of 2");

            // Three more full weeks: the fourth unlocks the Graveyard skin.
            for (int w = 1; w <= 3; w++)
                for (int d = 0; d < 7; d++)
                    Run(new DateTime(2027, 1, 18).AddDays(7 * w + d).ToString("yyyy-MM-dd") + "T10:00:00", "a", (uint)(w * 10 + d + 1));
            List<string> fourth = On("2027-02-15T09:00:00").GetAsync("a").Result.Rewards;
            Assert.Contains(GraveyardService.SkinId, fourth);
        }

        [Test]
        public void DeletingYourData_RemovesYourRunsAndScores_AndTheSetsSkipThem()
        {
            Run("2027-01-18T10:00:00", "a", 3);
            Run("2027-01-18T10:00:00", "b", 5);
            GraveyardService.ForgetPlayerAsync(_days, _dayBoard, _weekBoard, "a", new DateTime(2027, 1, 18)).Wait();
            Assert.IsFalse(_dayBoard.Today.ContainsKey("a"));
            Assert.IsFalse(_weekBoard.Today.ContainsKey("a"));
            Assert.IsFalse(_days.Buckets[GraveyardService.RunPrefix + "20270118"].Keys.Any(k => k.StartsWith("a_", StringComparison.Ordinal)));
            GraveyardResponse tuesday = On("2027-01-19T09:00:00").GetAsync("c").Result;
            Assert.AreEqual(1, Ghosts(tuesday), "only b's run is left to choose");
            Assert.AreEqual(2, tuesday.Size, "the tally stays as a count");
        }
    }
}
