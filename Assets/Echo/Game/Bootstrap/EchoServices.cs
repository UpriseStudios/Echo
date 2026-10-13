using System;
using Echo.Services;
using Echo.Services.Ads;
using Echo.Services.Analytics;
using Echo.Services.Iap;
using Echo.Services.Mocks;
using Echo.Services.Share;

namespace Echo.Game.Bootstrap
{
    /// <summary>
    /// The service set the game runs with. Starts as mocks; real SDK services replace them once consent is
    /// known and the keys are configured (tech spec 15). Entitlements are forwarded from whichever IAP is live.
    /// </summary>
    public sealed class EchoServices
    {
        public IAdsService Ads { get; private set; }
        public IIapService Iap { get; private set; }
        public IAnalytics Analytics { get; private set; }
        public IShareService Share { get; private set; }

        public event Action<string> Entitled;

        public bool UsingMockAds => Ads is MockAdsService;

        /// <summary>Why the services are what they are; set by EchoGame as they start.</summary>
        public string StartupNote = "Real services not started yet (they wait for the privacy choice).";

        /// <summary>The development status readout: what is running, and how ads and the store are doing.</summary>
        public string StatusReport => StartupNote + "\nAds: " + Ads.Status + "\nStore: " + Iap.Status;

        public static EchoServices CreateMocks()
        {
            var s = new EchoServices
            {
                Ads = new MockAdsService(),
                Analytics = new MockAnalytics(),
                Share = new MockShareService(),
            };
            s.SetIap(new MockIapService());
            return s;
        }

        public void Adopt(RealServices real)
        {
            if (real.Ads != null) Ads = real.Ads;
            if (real.Analytics != null) Analytics = real.Analytics;
            if (real.Iap != null) SetIap(real.Iap);
        }

        /// <summary>The native share sheet, which needs no consent, so it replaces the mock at launch.</summary>
        public void AdoptShare(IShareService share)
        {
            if (share != null) Share = share;
        }

        /// <summary>Applies a changed privacy choice to services that are already running.</summary>
        public void ApplyConsent(bool personalisedAds, bool analytics)
        {
            if (Ads is LevelPlayAdsService levelPlay) levelPlay.SetConsent(personalisedAds);
            if (Analytics is UgsAnalytics ugs) ugs.SetConsent(analytics);
        }

        /// <summary>Development builds: make the mock ad fail, to test the death screen's failure path (tech spec 15).</summary>
        public bool ToggleMockAdFailure()
        {
            if (!(Ads is MockAdsService mock)) return false;
            mock.SimulateFailure = !mock.SimulateFailure;
            return mock.SimulateFailure;
        }

        void SetIap(IIapService iap)
        {
            if (Iap != null) Iap.Entitled -= OnEntitled;
            Iap = iap;
            Iap.Entitled += OnEntitled;
        }

        void OnEntitled(string productId) => Entitled?.Invoke(productId);
    }
}
