using System.Collections.Generic;
using System.Text;
using Echo.Services.Analytics;
using UnityEngine;

namespace Echo.Services.Mocks
{
    public sealed class MockAnalytics : IAnalytics
    {
        /// <summary>Every event logged, with its parameters as "key=value" text (tests).</summary>
        public readonly List<string> Events = new List<string>();

        public void Log(string evt, IReadOnlyDictionary<string, object> props = null)
        {
            var sb = new StringBuilder("[MockAnalytics] ").Append(evt);
            if (props != null)
                foreach (var kv in props) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            Events.Add(sb.ToString().Substring("[MockAnalytics] ".Length));
            Debug.Log(sb.ToString());
        }
    }
}
