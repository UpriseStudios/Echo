using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Echo.Core.Modes;
using EchoCloud;
using NUnit.Framework;

namespace Echo.Server.Tests
{
    /// <summary>
    /// Naughty vs Nice (modes doc): balanced joining, contributions as daily-best improvements (so a team's total is its
    /// members' daily bests), locked writes surviving a conflict, the result, rewards granted once, badges on the boards,
    /// and deletion keeping the anonymous totals.
    /// </summary>
    public class TeamServiceTests
    {
        static long At(string utc) => DateTimeOffset.Parse(utc + "Z").ToUnixTimeSeconds();

        static readonly List<ModeWindow> Schedule = new List<ModeWindow>
        {
            new ModeWindow { Id = ModeIds.NaughtyNice, Enabled = true, Start = At("2026-12-01T00:00:00"), End = At("2026-12-25T00:00:00"),
                ShowUntil = At("2027-01-01T00:00:00") },
        };

        MemoryDayStore _days;
        MemoryDailyStore _players;

        [SetUp]
        public void SetUp()
        {
            _days = new MemoryDayStore();
            _players = new MemoryDailyStore();
        }

        TeamService On(string utc, bool forced = false, List<ModeWindow> windows = null) =>
            new TeamService(_days, _players, windows ?? Schedule, At(utc), forced);

        [Test]
        public void Joining_IsPermanent_AndBalancedPast55Percent()
        {
            var dec = On("2026-12-02T10:00:00");
            Assert.AreEqual("nice", dec.JoinAsync("a", "nice").Result.Team);
            Assert.AreEqual("nice", dec.JoinAsync("a", "naughty").Result.Team, "the choice is permanent");
            Assert.AreEqual("bad-team", dec.JoinAsync("b", "elf").Result.Error);
            TeamEventResponse second = dec.JoinAsync("b", "nice").Result;
            Assert.AreEqual("naughty", second.Team, "Nice already has 1 of 1 (over 55%): the elves assign the smaller side");
            Assert.IsTrue(second.Assigned);
            Assert.AreEqual("nice", dec.JoinAsync("c", "nice").Result.Team, "1 of 2 is not over 55%");
            Assert.AreEqual("naughty", dec.JoinAsync("d", "naughty").Result.Team);
            TeamEventResponse fifth = dec.JoinAsync("e", "nice").Result;
            Assert.AreEqual("nice", fifth.Team, "2 of 4");
            Assert.IsFalse(fifth.Assigned);
            Assert.AreEqual((3, 2), (fifth.NiceMembers, fifth.NaughtyMembers));
            TeamEventResponse sixth = dec.JoinAsync("f", "nice").Result;
            Assert.AreEqual("naughty", sixth.Team, "3 of 5 is 60%");

            Assert.AreEqual("closed", On("2026-11-30T23:00:00").JoinAsync("g", "nice").Result.Error);
            Assert.AreEqual("closed", On("2026-12-25T00:00:00").JoinAsync("g", "nice").Result.Error, "no joining once it's over");
        }

        [Test]
        public void Contributions_AreDailyBestImprovements_CountedByDay()
        {
            var day1 = On("2026-12-02T10:00:00");
            day1.JoinAsync("a", "nice").Wait();
            day1.JoinAsync("b", "naughty").Wait();
            Assert.AreEqual("", day1.ContributeAsync("nobody", "NOBODY#1", 500).Result, "not on a side");
            Assert.AreEqual("nice", day1.ContributeAsync("a", "A#1", 400).Result);
            Assert.AreEqual("nice", day1.ContributeAsync("a", "A#1", 412).Result, "a better run that day adds only its improvement");
            day1.ContributeAsync("b", "B#1", 600).Wait();

            var day2 = On("2026-12-03T09:00:00");
            day2.ContributeAsync("a", "A#1", 300).Wait();
            TeamEventResponse a = day2.GetAsync("a").Result;
            Assert.AreEqual(812 + 300, a.Nice);
            Assert.AreEqual(600, a.Naughty);
            Assert.AreEqual(300, a.NiceToday);
            Assert.AreEqual(0, a.NaughtyToday);
            Assert.AreEqual((1112L, 2), (a.YourMetres, a.YourDays));

            Assert.AreEqual("", On("2026-12-25T00:00:01").ContributeAsync("a", "A#1", 999).Result, "nothing counts after the event");
        }

        [Test]
        public void ATallyWrite_ThatLosesARace_IsRedoneOnTheNewValue()
        {
            var dec = On("2026-12-02T10:00:00");
            dec.JoinAsync("a", "nice").Wait();
            _days.ConflictsToInject = 2;
            dec.ContributeAsync("a", "A#1", 250).Wait();
            Assert.AreEqual(250, dec.GetAsync("a").Result.Nice);
        }

        [Test]
        public void TheResult_RewardsTheWinners_Once_AndBadgesFiveDayPlayers()
        {
            for (int d = 0; d < 5; d++)
            {
                var day = On($"2026-12-{10 + d:00}T10:00:00");
                if (d == 0)
                {
                    day.JoinAsync("nice1", "nice").Wait();
                    day.JoinAsync("naughty1", "naughty").Wait();
                    day.JoinAsync("idle", "nice").Wait();
                }
                day.ContributeAsync("nice1", "NICE1#1", 300).Wait();
                if (d == 0) day.ContributeAsync("naughty1", "NAUGHTY1#1", 1000).Wait();
            }

            var christmas = On("2026-12-25T09:00:00");
            TeamEventResponse nice = christmas.GetAsync("nice1").Result;
            Assert.IsTrue(nice.Ended);
            Assert.AreEqual("nice", nice.Winner, "1,500 to 1,000");
            Assert.AreEqual(("NICE1#1", 1500L), (nice.TopNice, nice.TopNiceMetres));
            Assert.AreEqual(("NAUGHTY1#1", 1000L), (nice.TopNaughty, nice.TopNaughtyMetres));
            CollectionAssert.AreEquivalent(new[] { TeamService.HaloId, "team_nice_2026" }, nice.Rewards);
            Assert.IsEmpty(christmas.GetAsync("naughty1").Result.Rewards, "the losing side, one day: nothing");
            Assert.IsEmpty(christmas.GetAsync("idle").Result.Rewards, "on the winning side but never contributed");

            ProfileResponse profile = christmas.ProfileAsync("nice1").Result;
            CollectionAssert.AreEqual(new[] { TeamService.HaloId }, profile.Cosmetics);
            Assert.AreEqual("team_nice_2026", profile.Badges.Single().Id);
            christmas.GetAsync("nice1").Wait();
            Assert.AreEqual(1, christmas.ProfileAsync("nice1").Result.Badges.Count, "granted once");

            var boards = TeamService.BadgesAsync(_days, new[] { "nice1", "naughty1" }, At("2026-12-26T00:00:00")).Result;
            CollectionAssert.AreEqual(new[] { "team_nice_2026" }, boards["nice1"]);
            Assert.IsFalse(boards.ContainsKey("naughty1"));
            Assert.IsEmpty(TeamService.BadgesAsync(_days, new[] { "nice1" }, At("2027-03-01T00:00:00")).Result, "badges expire");
            Assert.IsEmpty(christmas.ProfileAsync("nice1").Result.Badges.Where(b => b.Until < At("2027-01-01T00:00:00")));
        }

        [Test]
        public void ADraw_RewardsBothSides()
        {
            var dec = On("2026-12-10T10:00:00");
            dec.JoinAsync("x", "nice").Wait();
            dec.JoinAsync("y", "naughty").Wait();
            dec.ContributeAsync("x", "X", 500).Wait();
            dec.ContributeAsync("y", "Y", 500).Wait();
            var after = On("2026-12-26T10:00:00");
            Assert.AreEqual("draw", after.GetAsync("x").Result.Winner);
            CollectionAssert.Contains(after.GetAsync("x").Result.Rewards, TeamService.HaloId);
            CollectionAssert.Contains(after.GetAsync("y").Result.Rewards, TeamService.CoalTrailId);
        }

        [Test]
        public void Deleting_RemovesTheRecord_ButTheTotalsKeepTheMetres()
        {
            var dec = On("2026-12-10T10:00:00");
            dec.JoinAsync("a", "nice").Wait();
            dec.ContributeAsync("a", "A#1", 700).Wait();
            TeamService.ForgetPlayerAsync(_days, Schedule, "a").Wait();
            TeamEventResponse after = dec.GetAsync("a").Result;
            Assert.AreEqual("", after.Team);
            Assert.AreEqual(700, after.Nice, "anonymous sums stay (a user decision)");
        }

        [Test]
        public void TeamData_GoesAMonthAfterTheResult_AndDevelopmentCanForceTheEvent()
        {
            On("2026-12-10T10:00:00").JoinAsync("a", "nice").Wait();
            string bucket = TeamService.Bucket("naughty_nice_2026");
            TeamService.SweepAsync(_days, Schedule, At("2027-01-15T00:00:00")).Wait();
            Assert.IsTrue(_days.Has(bucket, "a"));
            TeamService.SweepAsync(_days, Schedule, At("2027-02-02T00:00:00")).Wait();
            Assert.IsFalse(_days.Has(bucket, "a"));

            var off = new List<ModeWindow> { new ModeWindow { Id = ModeIds.NaughtyNice, Enabled = false, Start = Schedule[0].Start, End = Schedule[0].End, ShowUntil = Schedule[0].ShowUntil } };
            Assert.AreEqual("closed", On("2026-12-10T10:00:00", windows: off).JoinAsync("b", "nice").Result.Error);
            Assert.IsTrue(On("2026-12-10T10:00:00", forced: true, windows: off).JoinAsync("b", "nice").Result.Ok, "forced: the schedule's window, on");
            Assert.IsTrue(On("2026-12-26T10:00:00", forced: true, windows: off).GetAsync("b").Result.Ended, "and a later test date shows the result");
            Assert.IsTrue(On("2026-06-01T10:00:00", forced: true, windows: new List<ModeWindow>()).JoinAsync("c", "nice").Result.Ok, "no window at all: one round today");
        }
    }
}
