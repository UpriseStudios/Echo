using System.Collections.Generic;
using System.Text;
using Echo.Services.Analytics;
using UnityEngine;

namespace Echo.Services.Mocks
{
    public sealed class MockAnalytics : IAnalytics
    {
        public void Log(string evt, IReadOnlyDictionary<string, object> props = null)
        {
            var sb = new StringBuilder("[MockAnalytics] ").Append(evt);
            if (props != null)
                foreach (var kv in props) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            Debug.Log(sb.ToString());
        }
    }
}
