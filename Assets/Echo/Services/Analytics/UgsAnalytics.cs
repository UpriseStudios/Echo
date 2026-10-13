using System.Collections.Generic;
using Unity.Services.Analytics;

namespace Echo.Services.Analytics
{
    /// <summary>
    /// Unity Analytics backend. Collects nothing until the player consents; revoking stops collection and
    /// requests deletion of what was sent. Requires UnityServices initialised first.
    /// </summary>
    public sealed class UgsAnalytics : IAnalytics
    {
        bool _collecting;

        public UgsAnalytics(bool consent) => SetConsent(consent);

        public void SetConsent(bool consent)
        {
            if (consent == _collecting) return;
            if (consent) AnalyticsService.Instance.StartDataCollection();
            else
            {
                AnalyticsService.Instance.StopDataCollection();
                AnalyticsService.Instance.RequestDataDeletion();
            }
            _collecting = consent;
        }

        public void Log(string evt, IReadOnlyDictionary<string, object> props = null)
        {
            if (!_collecting) return;
            var e = new CustomEvent(evt);
            if (props != null)
                foreach (var kv in props) e.Add(kv.Key, kv.Value);
            AnalyticsService.Instance.RecordEvent(e);
        }

        public void Flush()
        {
            if (_collecting) AnalyticsService.Instance.Flush();
        }
    }
}
