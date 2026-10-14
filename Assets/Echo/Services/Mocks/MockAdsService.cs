using System;
using Echo.Services.Ads;
using UnityEngine;

namespace Echo.Services.Mocks
{
    /// <summary>Development stand-in for LevelPlay. Completes immediately; can simulate no-fill and failure.</summary>
    public sealed class MockAdsService : IAdsService
    {
        public bool SimulateNotReady;
        public bool SimulateFailure;

        /// <summary>Tests: hold the ad "on screen" until CompleteShowing, as a real ad does while the app is paused.</summary>
        public bool HoldUntilCompleted;

        /// <summary>Every placement asked for, in order (tests).</summary>
        public readonly System.Collections.Generic.List<string> Shown = new System.Collections.Generic.List<string>();
        Action<bool> _held;

        public bool IsRewardedReady(string placement) => !SimulateNotReady && MockPolicy.Grants;

        public string Status => MockPolicy.Grants ? "mock ads (no real SDK running)" : "no ads (mock refuses on release builds)";

        public void ShowRewarded(string placement, Action<bool> onComplete)
        {
            Shown.Add(placement);
            bool success = !SimulateNotReady && !SimulateFailure && MockPolicy.Grants;
            Debug.Log($"[MockAds] rewarded '{placement}' -> {(success ? "rewarded" : "failed")}");
            if (HoldUntilCompleted)
            {
                _held = ok => onComplete?.Invoke(ok && success);
                return;
            }
            onComplete?.Invoke(success);
        }

        /// <summary>Ends a held ad, rewarded or not.</summary>
        public void CompleteShowing(bool rewarded)
        {
            Action<bool> held = _held;
            _held = null;
            held?.Invoke(rewarded);
        }
    }
}
