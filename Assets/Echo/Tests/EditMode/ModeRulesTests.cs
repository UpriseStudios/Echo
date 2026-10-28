using Echo.Core.Modes;
using NUnit.Framework;

namespace Echo.Tests.EditMode
{
    /// <summary>The mode rules Core shares with Cloud Code (modes doc): switched on, inside the dates, on its days.</summary>
    public class ModeRulesTests
    {
        const long Day = ModeRules.Day;
        // 2027-01-03 00:00 UTC, a Sunday.
        const long Sunday = 1798934400;

        [Test]
        public void Weekdays_CountFromThursdayTheFirst()
        {
            Assert.AreEqual(4, ModeRules.Weekday(0), "1 January 1970");
            Assert.AreEqual(0, ModeRules.Weekday(Sunday));
            Assert.AreEqual(1, ModeRules.Weekday(Sunday + Day));
            Assert.AreEqual(6, ModeRules.Weekday(Sunday - 1));
        }

        [Test]
        public void AWindow_IsOpenWhenOn_FromItsStartUpToItsEnd()
        {
            var w = new ModeWindow { Id = ModeIds.NaughtyNice, Enabled = true, Start = 1000, End = 2000, ShowUntil = 3000 };
            Assert.IsFalse(ModeRules.IsOpen(w, 999));
            Assert.IsTrue(ModeRules.IsOpen(w, 1000));
            Assert.IsFalse(ModeRules.IsOpen(w, 2000));
            Assert.IsTrue(ModeRules.IsShown(w, 2500), "showing its result");
            Assert.IsFalse(ModeRules.IsShown(w, 3000));
            Assert.AreEqual(500, ModeRules.SecondsLeft(w, 1500));
            Assert.AreEqual(100, ModeRules.SecondsLeft(w, 900), "opens in");
            Assert.AreEqual(-1, ModeRules.SecondsLeft(w, 2500), "won't open again");
            w.Enabled = false;
            Assert.IsFalse(ModeRules.IsOpen(w, 1500));
            Assert.IsFalse(ModeRules.IsShown(w, 1500));
        }

        [Test]
        public void AWeekdayWindow_OpensOnItsDays()
        {
            var sundays = new ModeWindow { Id = ModeIds.OneLife, Enabled = true, Start = Sunday - 7 * Day, End = Sunday + 30 * Day, Weekdays = 1 };
            Assert.IsTrue(ModeRules.IsOpen(sundays, Sunday + 3600));
            Assert.AreEqual(Day - 3600, ModeRules.SecondsLeft(sundays, Sunday + 3600), "until the end of Sunday");
            Assert.IsFalse(ModeRules.IsOpen(sundays, Sunday + Day));
            Assert.AreEqual(6 * Day, ModeRules.SecondsLeft(sundays, Sunday + Day), "until next Sunday");
        }

        [Test]
        public void Pick_PrefersAnOpenWindow_ThenOneShowing_ThenTheNext()
        {
            var christmas = new ModeWindow { Id = ModeIds.OneLife, Enabled = true, Start = 100, End = 200 };
            var later = new ModeWindow { Id = ModeIds.OneLife, Enabled = true, Start = 500, End = 600 };
            var off = new ModeWindow { Id = ModeIds.OneLife, Enabled = false, Start = 0, End = 1000 };
            var windows = new[] { off, later, christmas };
            Assert.AreSame(christmas, ModeRules.Pick(windows, ModeIds.OneLife, 150));
            Assert.AreSame(later, ModeRules.Pick(windows, ModeIds.OneLife, 300));
            Assert.IsNull(ModeRules.Pick(windows, ModeIds.OneLife, 700));
            Assert.IsNull(ModeRules.Pick(windows, ModeIds.Ranked, 150));
        }
    }
}
