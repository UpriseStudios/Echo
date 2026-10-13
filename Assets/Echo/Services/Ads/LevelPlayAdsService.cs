using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Echo.Services.Ads
{
    /// <summary>
    /// Unity LevelPlay, rewarded placements only (tech spec 15). Consent is passed before Init. No interstitials
    /// or banners exist here.
    /// One rewarded ad object per ad unit, shared by every placement that uses that unit (device testing: power-ups
    /// fall back to the unlock ad unit, and two objects on one unit left the second never loading). Each is reloaded
    /// after it is shown, and a failed load (often "no fill" for a new app) is retried after a pause.
    /// </summary>
    public sealed class LevelPlayAdsService : IAdsService
    {
        static readonly int[] RetrySeconds = { 10, 30, 60 };

        readonly Dictionary<string, string> _unitByPlacement;
        readonly Dictionary<string, LevelPlayRewardedAd> _adByUnit = new Dictionary<string, LevelPlayRewardedAd>();
        readonly Dictionary<string, string> _loadState = new Dictionary<string, string>();
        readonly Dictionary<string, int> _failures = new Dictionary<string, int>();
        string _initState = "initialising";
        Action<bool> _pending;
        bool _rewarded, _closed;
        LevelPlayRewardedAd _showing;
        bool _initialized;

        public LevelPlayAdsService(EchoServiceConfig config, bool personalisedAdsConsent)
        {
            _unitByPlacement = new Dictionary<string, string>
            {
                { AdPlacements.BanishGhost, config.BanishAdUnit },
                { AdPlacements.BanishTen, config.BanishTenAdUnit },
                { AdPlacements.Continue, config.ContinueAdUnit },
                { AdPlacements.UnlockCharacter, config.UnlockAdUnit },
                { AdPlacements.PowerUp, config.PowerUpAdUnit },
            };

            // GDPR: consent must reach the SDK before initialisation; without it networks serve non-personalised ads.
            LevelPlay.SetConsent(personalisedAdsConsent);
            LevelPlay.OnInitSuccess += _ => OnInitialized();
            LevelPlay.OnInitFailed += e =>
            {
                _initState = $"init failed: {e.ErrorCode} {e.ErrorMessage}";
                Debug.LogWarning("[Ads] LevelPlay " + _initState);
            };
            LevelPlay.Init(config.LevelPlayAppKey);
        }

        public void SetConsent(bool personalisedAds) => LevelPlay.SetConsent(personalisedAds);

        public string Status
        {
            get
            {
                var s = new System.Text.StringBuilder("LevelPlay " + _initState);
                foreach (var kv in _unitByPlacement)
                {
                    string state = IsRewardedReady(kv.Key) ? "ready" : _loadState.TryGetValue(kv.Value, out string st) ? st : "not loaded";
                    s.Append($"; {kv.Key}: {state}");
                }
                return s.ToString();
            }
        }

        void OnInitialized()
        {
            _initialized = true;
            _initState = "initialised";
            foreach (string unit in _unitByPlacement.Values)
            {
                if (_adByUnit.ContainsKey(unit)) continue;
                var ad = new LevelPlayRewardedAd(unit);
                ad.OnAdLoaded += _ =>
                {
                    _loadState[unit] = "loaded";
                    _failures[unit] = 0;
                };
                // On Android the reward can arrive just after the close, so whichever comes second completes the show,
                // and a close with no reward waits a moment for one (device testing: rewards were being lost).
                ad.OnAdRewarded += (_, __) =>
                {
                    _rewarded = true;
                    if (_closed) Finish(ad);
                };
                ad.OnAdClosed += _ =>
                {
                    _closed = true;
                    if (_rewarded) Finish(ad);
                    else FinishSoon(ad);
                };
                ad.OnAdDisplayFailed += (_, error) =>
                {
                    Debug.LogWarning($"[Ads] display failed: {error.ErrorMessage}");
                    _rewarded = false;
                    Finish(ad);
                };
                ad.OnAdLoadFailed += error =>
                {
                    _loadState[unit] = $"load failed: {error.ErrorCode} {error.ErrorMessage}";
                    Debug.LogWarning($"[Ads] {unit} {_loadState[unit]}");
                    RetryLater(unit, ad);
                };
                _adByUnit[unit] = ad;
                Load(unit, ad);
            }
        }

        void Load(string unit, LevelPlayRewardedAd ad)
        {
            _loadState[unit] = "loading";
            ad.LoadAd();
        }

        async void RetryLater(string unit, LevelPlayRewardedAd ad)
        {
            int failures = _failures.TryGetValue(unit, out int f) ? f : 0;
            _failures[unit] = failures + 1;
            await Task.Delay(TimeSpan.FromSeconds(RetrySeconds[Math.Min(failures, RetrySeconds.Length - 1)]));
            if (!ad.IsAdReady()) Load(unit, ad);
        }

        public bool IsRewardedReady(string placement) =>
            _initialized && _unitByPlacement.TryGetValue(placement, out string unit)
            && _adByUnit.TryGetValue(unit, out var ad) && ad.IsAdReady();

        public void ShowRewarded(string placement, Action<bool> onComplete)
        {
            if (_pending != null || !IsRewardedReady(placement))
            {
                onComplete?.Invoke(false);
                return;
            }
            _pending = onComplete;
            _rewarded = false;
            _closed = false;
            _showing = _adByUnit[_unitByPlacement[placement]];
            _showing.ShowAd(placement);
        }

        async void FinishSoon(LevelPlayRewardedAd ad)
        {
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            if (_showing == ad && _pending != null) Finish(ad);
        }

        void Finish(LevelPlayRewardedAd ad)
        {
            if (_pending == null || _showing != ad) return; // already completed
            _showing = null;
            var callback = _pending;
            bool rewarded = _rewarded;
            _pending = null;
            _rewarded = false;
            ad.LoadAd();
            callback?.Invoke(rewarded);
        }
    }
}
