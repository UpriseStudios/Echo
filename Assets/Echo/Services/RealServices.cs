using System;
using System.Threading.Tasks;
using Echo.Services.Ads;
using Echo.Services.Analytics;
using Echo.Services.Iap;
using Echo.Services.Share;
using Unity.Services.Core;
using UnityEngine;

namespace Echo.Services
{
    /// <summary>
    /// Creates the real SDK services (tech spec 15, 19) once the player has made their consent choice.
    /// Order: iOS tracking prompt (only with ad consent), Unity Services, analytics, IAP, then LevelPlay.
    /// Each part starts on its own, with a time limit, so one that throws or hangs never keeps the others on
    /// mocks (device testing: the whole set stayed mock). Any part that fails is left null, and the caller keeps
    /// the mock for it. Sharing needs no consent and starts at once (CreateShare).
    /// </summary>
    public sealed class RealServices
    {
        static readonly TimeSpan PromptLimit = TimeSpan.FromSeconds(35);
        static readonly TimeSpan StartLimit = TimeSpan.FromSeconds(20);

        public IAdsService Ads;
        public IIapService Iap;
        public IAnalytics Analytics;

        /// <summary>What started and what did not, for the development status readout.</summary>
        public string Note = "";

        public static bool Enabled(EchoServiceConfig config) =>
            config != null && (Application.isEditor ? config.RealServicesInEditor : Application.isMobilePlatform);

        /// <summary>The native share sheet. Not tracking, so it needs no consent; null where it cannot run.</summary>
        public static IShareService CreateShare(EchoServiceConfig config)
        {
            if (!Enabled(config)) return null;
            try
            {
                return new NativeShareService();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Services] share unavailable: {e.Message}");
                return null;
            }
        }

        public static async Task<RealServices> CreateAsync(EchoServiceConfig config, bool personalisedAds, bool analytics)
        {
            var real = new RealServices();
            if (!Enabled(config)) return real;

            try
            {
                await Limit(TrackingAuthorization.RequestIfNeededAsync(personalisedAds), PromptLimit, "tracking prompt");
            }
            catch (Exception e)
            {
                real.Fail("Tracking prompt", e);
            }

            bool core = false;
            try
            {
                await Limit(UnityServices.InitializeAsync(), StartLimit, "Unity Services");
                core = true;
            }
            catch (Exception e)
            {
                real.Fail("Unity Services (store and analytics need it)", e);
            }
            if (core)
            {
                try
                {
                    real.Analytics = new UgsAnalytics(analytics);
                }
                catch (Exception e)
                {
                    real.Fail("Analytics", e);
                }
                try
                {
                    real.Iap = new UnityIapService();
                }
                catch (Exception e)
                {
                    real.Fail("Store", e);
                }
            }

            if (!config.AdsConfigured)
                real.Note += "Some LevelPlay keys or ad units are still REPLACE_ME: no ads. ";
            else
                try
                {
                    real.Ads = new LevelPlayAdsService(config, personalisedAds);
                }
                catch (Exception e)
                {
                    real.Fail("LevelPlay", e);
                }

            if (real.Note.Length == 0) real.Note = "Real services running.";
            return real;
        }

        void Fail(string what, Exception e)
        {
            Note += $"{what} failed: {e.GetType().Name} {e.Message}. ";
            Debug.LogWarning($"[Services] {what} failed: {e}");
        }

        static async Task Limit(Task task, TimeSpan limit, string what)
        {
            if (await Task.WhenAny(task, Task.Delay(limit)) != task) throw new TimeoutException($"{what} took over {limit.TotalSeconds:0} s");
            await task;
        }
    }
}
