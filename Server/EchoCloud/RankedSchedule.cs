using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace EchoCloud
{
    /// <summary>
    /// Reads the Remote Config key "ranked" (modes doc, Ghost Duels): { "seasons": [ { "n", "start", "end", "arenas": [10
    /// seeds] } ] }, so seasons and their arenas are set without an update. A season that doesn't read whole (dates, a
    /// number, exactly 10 arenas) is left out, which closes it rather than opening a broken one.
    /// </summary>
    public static class RankedSchedule
    {
        public const string Key = "ranked";

        public static List<RankedSeason> Parse(string json)
        {
            var seasons = new List<RankedSeason>();
            if (string.IsNullOrWhiteSpace(json)) return seasons;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("seasons", out JsonElement list) || list.ValueKind != JsonValueKind.Array) return seasons;
                foreach (JsonElement s in list.EnumerateArray())
                {
                    if (!s.TryGetProperty("n", out JsonElement n) || n.ValueKind != JsonValueKind.Number || !n.TryGetInt32(out int number) || number <= 0)
                        continue;
                    if (!Time(s, "start", out long start) || !Time(s, "end", out long end) || end <= start) continue;
                    if (!s.TryGetProperty("arenas", out JsonElement arenas) || arenas.ValueKind != JsonValueKind.Array) continue;
                    var seeds = new List<uint>();
                    foreach (JsonElement a in arenas.EnumerateArray())
                        if (a.ValueKind == JsonValueKind.Number && a.TryGetUInt32(out uint seed) && seed != 0) seeds.Add(seed);
                    if (seeds.Count != Echo.Core.Modes.RankedRules.ArenasPerSeason) continue;
                    seasons.Add(new RankedSeason { Number = number, Start = start, End = end, Arenas = seeds });
                }
            }
            catch (JsonException)
            {
                seasons.Clear();
            }
            return seasons.OrderBy(s => s.Number).ToList();
        }

        /// <summary>The season running now, or null.</summary>
        public static RankedSeason Current(IReadOnlyList<RankedSeason> seasons, long now) => seasons.FirstOrDefault(s => s.Holds(now));

        /// <summary>The latest season that has ended by now, or null.</summary>
        public static RankedSeason LastEnded(IReadOnlyList<RankedSeason> seasons, long now) => seasons.Where(s => s.End <= now).OrderBy(s => s.Number).LastOrDefault();

        static bool Time(JsonElement m, string name, out long seconds)
        {
            seconds = 0;
            if (!m.TryGetProperty(name, out JsonElement e) || e.ValueKind != JsonValueKind.String
                || !DateTimeOffset.TryParse(e.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset at))
                return false;
            seconds = at.ToUnixTimeSeconds();
            return true;
        }
    }
}
