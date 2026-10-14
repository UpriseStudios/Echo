using System;
using System.Globalization;
using System.Threading.Tasks;

namespace EchoCloud
{
    /// <summary>
    /// Privacy policy retention: deletes whole days once they are older than the policy allows (daily records after 7
    /// days, challenges after 30). It runs on ordinary traffic and remembers the last day it deleted, so days nobody
    /// visited are still caught up; running twice at once is harmless.
    /// </summary>
    public static class Housekeeping
    {
        public const string Bucket = "echo_housekeeping";

        /// <summary>The first sweep looks this far back; later ones carry on from the last.</summary>
        public const int FirstSweepDays = 7;

        /// <summary>
        /// At most this many days per call (more than the longest retention, so one call always catches up on a
        /// quiet month); deleting a day nobody used is one cheap request.
        /// </summary>
        public const int MaxDaysPerSweep = 40;

        public static string Day(DateTime day) => day.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        /// <summary>Deletes prefix+yyyyMMdd for every day more than keepDays before today that isn't yet deleted.</summary>
        public static async Task SweepAsync(IDayStore days, string prefix, DateTime today, int keepDays)
        {
            DateTime newest = today.Date.AddDays(-(keepDays + 1));
            string marker = await days.GetAsync(Bucket, prefix);
            DateTime from = DateTime.TryParseExact(marker, "yyyyMMdd", CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime done)
                ? done.Date.AddDays(1)
                : newest.AddDays(-FirstSweepDays);
            if (from > newest) return;
            DateTime last = from;
            for (int i = 0; i < MaxDaysPerSweep && last <= newest; i++, last = last.AddDays(1))
                await days.DeleteBucketAsync(prefix + Day(last));
            await days.SetAsync(Bucket, prefix, Day(last.AddDays(-1)));
        }
    }
}
