using System;
using System.IO;
using Echo.Game.Seasons;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Echo.Tests.EditMode
{
    /// <summary>
    /// The season schedule (seasons doc): UK local dates, one season at a time with the more specific one winning,
    /// dormant seasons switched off, and the built-in copy the same as Remote Config's (Server/Config/echo.rc).
    /// </summary>
    public class SeasonScheduleTests
    {
        static string BuiltIn => File.ReadAllText(Path.Combine(Application.dataPath, "Echo", "Content", "Seasons", "seasons.json"));

        static SeasonSchedule Parse(string json)
        {
            Assert.IsTrue(SeasonSchedule.TryParse(json, out SeasonSchedule s), json);
            return s;
        }

        static string On(SeasonSchedule s, int y, int m, int d) => s.ActiveOn(new DateTime(y, m, d))?.Id;

        [Test]
        public void TheBuiltInSchedule_MatchesRemoteConfig()
        {
            SeasonSchedule builtIn = Parse(BuiltIn);
            var rc = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "..", "Server", "Config", "echo.rc")));
            SeasonSchedule remote = Parse(rc["entries"]["seasons"].ToString());
            Assert.AreEqual(JsonConvert.SerializeObject(builtIn), JsonConvert.SerializeObject(remote), "echo.rc's seasons must match Content/Seasons/seasons.json");
            Assert.AreEqual("JSON", (string)rc["types"]["seasons"]);
            Assert.AreEqual(7, builtIn.Seasons.Count);
        }

        [Test]
        public void TheBuiltInSchedule_Has21sSeasonsOn_AndTheRestDormant()
        {
            SeasonSchedule s = Parse(BuiltIn);
            Assert.IsNull(On(s, 2026, 10, 28), "Halloween ships dormant in 2.1");
            Assert.IsNull(On(s, 2026, 11, 5), "so does Bonfire Night");
            Assert.IsNull(On(s, 2026, 11, 30));
            Assert.AreEqual(SeasonIds.Christmas, On(s, 2026, 12, 1));
            Assert.AreEqual(SeasonIds.Christmas, On(s, 2026, 12, 30));
            Assert.AreEqual(SeasonIds.NewYear, On(s, 2026, 12, 31), "New Year is inside Christmas and more specific");
            Assert.AreEqual(SeasonIds.NewYear, On(s, 2027, 1, 1));
            Assert.IsNull(On(s, 2027, 1, 2));
            Assert.IsNull(On(s, 2027, 2, 12));
            Assert.AreEqual(SeasonIds.Valentines, On(s, 2027, 2, 13));
            Assert.AreEqual(SeasonIds.Valentines, On(s, 2027, 2, 15));
            Assert.IsNull(On(s, 2027, 2, 16));
            Assert.IsNull(On(s, 2027, 3, 27), "Easter comes in 2.2");
            Assert.IsNull(On(s, 2027, 4, 1), "so does April Fools");
        }

        [Test]
        public void Windows_RunOverNewYear_AndAbsoluteDatesHoldOneYearOnly()
        {
            SeasonSchedule s = Parse(@"{ ""seasons"": [
                { ""id"": ""halloween"", ""start"": ""10-24"", ""end"": ""11-02"" },
                { ""id"": ""winter"", ""start"": ""12-20"", ""end"": ""01-10"" },
                { ""id"": ""easter"", ""start"": ""2027-03-26"", ""end"": ""2027-03-29"" } ] }");
            Assert.AreEqual("halloween", On(s, 2026, 10, 28));
            Assert.AreEqual("halloween", On(s, 2027, 11, 2), "every year");
            Assert.IsNull(On(s, 2026, 11, 3));
            Assert.AreEqual("winter", On(s, 2026, 12, 25));
            Assert.AreEqual("winter", On(s, 2027, 1, 10));
            Assert.IsNull(On(s, 2027, 1, 11));
            Assert.AreEqual("easter", On(s, 2027, 3, 27));
            Assert.IsNull(On(s, 2028, 3, 27), "that year only");
        }

        [Test]
        public void Overlaps_GoToTheShorterSeason_AndSwitchedOffNeverShows()
        {
            SeasonSchedule s = Parse(@"{ ""seasons"": [
                { ""id"": ""long"", ""start"": ""12-01"", ""end"": ""12-31"" },
                { ""id"": ""short"", ""start"": ""12-24"", ""end"": ""12-26"" },
                { ""id"": ""off"", ""start"": ""12-25"", ""end"": ""12-25"", ""enabled"": false } ] }");
            Assert.AreEqual("long", On(s, 2026, 12, 23));
            Assert.AreEqual("short", On(s, 2026, 12, 25));
            Assert.AreEqual("long", On(s, 2026, 12, 27));
        }

        [Test]
        public void BrokenSchedules_AreRefused_AndBrokenWindowsDropped()
        {
            Assert.IsFalse(SeasonSchedule.TryParse(null, out _));
            Assert.IsFalse(SeasonSchedule.TryParse("", out _));
            Assert.IsFalse(SeasonSchedule.TryParse("{ not json", out _));
            Assert.IsFalse(SeasonSchedule.TryParse("{}", out _), "no seasons list");
            SeasonSchedule s = Parse(@"{ ""seasons"": [
                { ""id"": ""ok"", ""start"": ""12-01"", ""end"": ""12-02"" },
                { ""id"": """", ""start"": ""12-01"", ""end"": ""12-02"" },
                { ""id"": ""bad-date"", ""start"": ""13-01"", ""end"": ""12-02"" },
                { ""id"": ""mixed"", ""start"": ""12-01"", ""end"": ""2026-12-02"" },
                null ] }");
            Assert.AreEqual(1, s.Seasons.Count);
            Assert.AreEqual("ok", s.Seasons[0].Id);
        }

        [Test]
        public void UkDates_FollowBritishSummerTime()
        {
            // 2026: BST from 01:00 UTC on 29 March to 01:00 UTC on 25 October.
            Assert.AreEqual(new DateTime(2026, 7, 1), UkCalendar.Date(new DateTime(2026, 6, 30, 23, 30, 0, DateTimeKind.Utc)), "23:30 UTC is 00:30 BST");
            Assert.AreEqual(new DateTime(2026, 12, 31), UkCalendar.Date(new DateTime(2026, 12, 31, 23, 30, 0, DateTimeKind.Utc)), "GMT in winter");
            Assert.IsTrue(UkCalendar.IsSummerTime(new DateTime(2026, 10, 25, 0, 59, 0, DateTimeKind.Utc)));
            Assert.IsFalse(UkCalendar.IsSummerTime(new DateTime(2026, 10, 25, 1, 0, 0, DateTimeKind.Utc)));
            Assert.IsFalse(UkCalendar.IsSummerTime(new DateTime(2026, 3, 29, 0, 59, 0, DateTimeKind.Utc)));
            Assert.IsTrue(UkCalendar.IsSummerTime(new DateTime(2026, 3, 29, 1, 0, 0, DateTimeKind.Utc)));
            Assert.AreEqual(new DateTime(2026, 10, 24), UkCalendar.Date(new DateTime(2026, 10, 23, 23, 30, 0, DateTimeKind.Utc)), "Halloween starts at UK midnight");
        }
    }
}
