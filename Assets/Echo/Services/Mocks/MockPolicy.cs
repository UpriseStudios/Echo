using UnityEngine;

namespace Echo.Services.Mocks
{
    /// <summary>
    /// Mocks stand in for the SDKs while developing. On a release build on a phone they must never give anything
    /// away (device testing: with the real store not running, every pack unlocked for free), so there they act
    /// as an unavailable service: no rewarded ad is ready, no price shows and every purchase fails.
    /// </summary>
    public static class MockPolicy
    {
        public static bool Grants => Application.isEditor || Debug.isDebugBuild;
    }
}
