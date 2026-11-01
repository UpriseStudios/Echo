using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EchoCloud
{
    /// <summary>
    /// Ghost Town's live numbers (a user decision): GetModes adds the Daily Haunt's pulse and each mode on show's
    /// player count. Every part is read at once and on its own: one that fails is left unknown (-1, or no pulse)
    /// rather than failing the hub.
    /// </summary>
    public static class CommunityPulse
    {
        public static async Task FillAsync(GetModesResponse response, Func<Task<DailyPulse>> daily,
                                           IReadOnlyDictionary<string, Func<Task<int>>> counts)
        {
            Task<DailyPulse> pulse = Safe(daily, null);
            var reads = new List<(ModeStatus Mode, Task<int> Count)>();
            foreach (ModeStatus mode in response.Modes)
                reads.Add((mode, counts.TryGetValue(mode.Id, out Func<Task<int>> count) ? Safe(count, -1) : Task.FromResult(-1)));
            await Task.WhenAll(reads.Select(r => (Task)r.Count).Append(pulse));
            response.Daily = pulse.Result;
            foreach (var (mode, count) in reads) mode.Players = count.Result;
        }

        static async Task<T> Safe<T>(Func<Task<T>> read, T fallback)
        {
            try
            {
                return await read();
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}
