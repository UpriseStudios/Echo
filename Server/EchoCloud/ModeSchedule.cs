using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Echo.Core.Modes;

namespace EchoCloud
{
    /// <summary>
    /// Reads the Remote Config key "modes" (modes doc): each mode's switch and UTC start and end, optionally a date its
    /// card shows until (show_until) and the weekdays it runs on. Anything that doesn't read is left out, so a broken
    /// value closes modes rather than opening them.
    /// </summary>
    public static class ModeSchedule
    {
        public const string Key = "modes";

        static readonly string[] Days = { "sun", "mon", "tue", "wed", "thu", "fri", "sat" };

        public static List<ModeWindow> Parse(string json)
        {
            var windows = new List<ModeWindow>();
            if (string.IsNullOrWhiteSpace(json)) return windows;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("modes", out JsonElement modes)
                    || modes.ValueKind != JsonValueKind.Array)
                    return windows;
                foreach (JsonElement m in modes.EnumerateArray())
                {
                    if (m.ValueKind != JsonValueKind.Object || !Text(m, "id", out string id) || !Time(m, "start", out long start)
                        || !Time(m, "end", out long end) || end <= start)
                        continue;
                    var w = new ModeWindow
                    {
                        Id = id, Start = start, End = end,
                        Enabled = m.TryGetProperty("enabled", out JsonElement on) && on.ValueKind == JsonValueKind.True,
                    };
                    if (Time(m, "show_until", out long until)) w.ShowUntil = until;
                    if (m.TryGetProperty("weekdays", out JsonElement days) && days.ValueKind == JsonValueKind.Array)
                        foreach (JsonElement d in days.EnumerateArray())
                        {
                            int i = d.ValueKind == JsonValueKind.String ? Array.IndexOf(Days, d.GetString().ToLowerInvariant()) : -1;
                            if (i >= 0) w.Weekdays |= 1 << i;
                        }
                    windows.Add(w);
                }
            }
            catch (JsonException)
            {
                windows.Clear();
            }
            return windows;
        }

        /// <summary>Now, or in development a test date: "yyyy-MM-dd" (noon UTC that day) or a full UTC time.</summary>
        public static long Now(string dateOverride) =>
            dateOverride != null && DateTimeOffset.TryParse(dateOverride.Length == 10 ? dateOverride + "T12:00:00Z" : dateOverride,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset at)
                ? at.ToUnixTimeSeconds()
                : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        static bool Text(JsonElement m, string name, out string value)
        {
            value = m.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
            return !string.IsNullOrEmpty(value);
        }

        static bool Time(JsonElement m, string name, out long seconds)
        {
            seconds = 0;
            if (!Text(m, name, out string text)
                || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset at))
                return false;
            seconds = at.ToUnixTimeSeconds();
            return true;
        }
    }
}
