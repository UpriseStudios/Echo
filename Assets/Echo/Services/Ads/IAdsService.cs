using System;

namespace Echo.Services.Ads
{
    /// <summary>Rewarded ads only (tech spec 15). Never shown during a run.</summary>
    public interface IAdsService
    {
        bool IsRewardedReady(string placement);
        void ShowRewarded(string placement, Action<bool> onComplete);

        /// <summary>One line for the development status readout, e.g. "LevelPlay ready: continue, banish".</summary>
        string Status { get; }
    }
}
