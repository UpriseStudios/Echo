using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Echo.Core.Modes;
using EchoCloud;
using NUnit.Framework;

namespace Echo.Server.Tests
{
    /// <summary>
    /// The mode framework (modes doc): the "modes" key, which modes the Modes screen shows, and the gate every mode
    /// endpoint checks. Only switched on and inside their UTC dates; a broken value closes modes, never opens them.
    /// </summary>
    public class ModeServiceTests
    {
        static long At(string utc) => DateTimeOffset.Parse(utc + "Z").ToUnixTimeSeconds();

        const string Schedule = @"{ ""modes"": [
            { ""id"": ""naughty_nice"", ""enabled"": true, ""start"": ""2026-12-01T00:00:00Z"", ""end"": ""2026-12-25T00:00:00Z"", ""show_until"": ""2027-01-01T00:00:00Z"" },
            { ""id"": ""one_life"", ""enabled"": true, ""start"": ""2026-12-25T00:00:00Z"", ""end"": ""2026-12-26T00:00:00Z"" },
            { ""id"": ""one_life"", ""enabled"": true, ""start"": ""2027-01-01T00:00:00Z"", ""end"": ""2027-12-31T00:00:00Z"", ""weekdays"": [""sun""] },
            { ""id"": ""graveyard"", ""enabled"": false, ""start"": ""2026-01-01T00:00:00Z"", ""end"": ""2030-01-01T00:00:00Z"" } ] }";

        static Dictionary<string, ModeStatus> Shown(string utc, bool force = false) =>
            ModeService.Describe(ModeSchedule.Parse(Schedule), At(utc), force).Modes.ToDictionary(m => m.Id);

        [Test]
        public void AMode_ShowsOnlyWhenOn_AndInsideItsDates()
        {
            Assert.IsEmpty(Shown("2026-11-30T23:59:59"));
            var dec = Shown("2026-12-10T12:00:00");
            Assert.IsTrue(dec[ModeIds.NaughtyNice].Open);
            Assert.AreEqual(At("2026-12-25T00:00:00") - At("2026-12-10T12:00:00"), dec[ModeIds.NaughtyNice].SecondsLeft);
            Assert.IsFalse(dec.ContainsKey(ModeIds.Graveyard), "switched off");

            var christmas = Shown("2026-12-25T09:00:00");
            Assert.IsFalse(christmas[ModeIds.NaughtyNice].Open, "closed, but still showing its result until New Year");
            Assert.IsTrue(christmas[ModeIds.OneLife].Open, "the Christmas Day special");
            Assert.IsFalse(Shown("2027-01-01T00:00:00").ContainsKey(ModeIds.NaughtyNice));
        }

        [Test]
        public void AWeekdayMode_OpensOnItsDays_AndCountsDownToTheNext()
        {
            // 3 January 2027 is a Sunday.
            Assert.AreEqual(0, ModeRules.Weekday(At("2027-01-03T10:00:00")));
            var sunday = Shown("2027-01-03T10:00:00");
            Assert.IsTrue(sunday[ModeIds.OneLife].Open);
            Assert.AreEqual(14 * 3600, sunday[ModeIds.OneLife].SecondsLeft, "closes at the end of Sunday");

            var monday = Shown("2027-01-04T10:00:00");
            Assert.IsFalse(monday[ModeIds.OneLife].Open);
            Assert.AreEqual(At("2027-01-10T00:00:00") - At("2027-01-04T10:00:00"), monday[ModeIds.OneLife].SecondsLeft, "opens next Sunday");
        }

        [Test]
        public void TheGate_FollowsTheSchedule_AndDevelopmentCanForceEveryModeOpen()
        {
            List<ModeWindow> windows = ModeSchedule.Parse(Schedule);
            Assert.IsTrue(ModeService.IsOpen(windows, ModeIds.NaughtyNice, At("2026-12-24T23:59:59"), false));
            Assert.IsFalse(ModeService.IsOpen(windows, ModeIds.NaughtyNice, At("2026-12-25T00:00:00"), false));
            Assert.IsFalse(ModeService.IsOpen(windows, ModeIds.Graveyard, At("2027-06-01T00:00:00"), false));
            Assert.IsFalse(ModeService.IsOpen(windows, ModeIds.Ranked, At("2027-06-01T00:00:00"), false), "not in the schedule at all");
            Assert.IsTrue(ModeService.IsOpen(windows, ModeIds.Ranked, At("2027-06-01T00:00:00"), true));
            var forced = Shown("2026-06-01T00:00:00", force: true);
            CollectionAssert.AreEquivalent(ModeIds.All, forced.Keys);
            Assert.IsTrue(forced.Values.All(m => m.Open));
        }

        [Test]
        public void BrokenValues_CloseModes()
        {
            Assert.IsEmpty(ModeSchedule.Parse(null));
            Assert.IsEmpty(ModeSchedule.Parse("not json"));
            Assert.IsEmpty(ModeSchedule.Parse(@"{ ""modes"": 3 }"));
            List<ModeWindow> some = ModeSchedule.Parse(@"{ ""modes"": [
                { ""id"": ""one_life"", ""enabled"": true, ""start"": ""2026-12-25T00:00:00Z"" },
                { ""id"": ""one_life"", ""enabled"": true, ""start"": ""2026-12-26T00:00:00Z"", ""end"": ""2026-12-25T00:00:00Z"" },
                { ""id"": ""one_life"", ""enabled"": ""yes"", ""start"": ""2026-12-25T00:00:00Z"", ""end"": ""2026-12-26T00:00:00Z"" } ] }");
            Assert.AreEqual(1, some.Count, "no end, and an end before the start, are left out");
            Assert.IsFalse(some[0].Enabled, "only true switches a mode on");
        }

        [Test]
        public void TheSwitchesCarryTheModes_AndTheDeployedConfigShipsThemAllOff()
        {
            var settings = new Dictionary<string, object> { { ModeSchedule.Key, JsonDocument.Parse(Schedule).RootElement } };
            Assert.AreEqual(4, FeatureSwitches.From(settings).Modes.Count, "as a JSON token");
            Assert.AreEqual(4, FeatureSwitches.From(new Dictionary<string, object> { { ModeSchedule.Key, Schedule } }).Modes.Count, "as text");

            string rc = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Config", "echo.rc"));
            JsonElement entries = JsonDocument.Parse(rc).RootElement.GetProperty("entries");
            List<ModeWindow> shipped = ModeSchedule.Parse(entries.GetProperty(ModeSchedule.Key).GetRawText());
            Assert.IsNotEmpty(shipped);
            Assert.IsTrue(shipped.All(w => !w.Enabled), "every mode ships switched off; going live is a Remote Config change");
            CollectionAssert.IsSubsetOf(shipped.Select(w => w.Id).Distinct(), ModeIds.All);
        }
    }
}
