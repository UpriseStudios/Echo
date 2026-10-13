using System.Collections.Generic;

namespace Echo.Services.Analytics
{
    public interface IAnalytics
    {
        void Log(string evt, IReadOnlyDictionary<string, object> props = null);
    }
}
