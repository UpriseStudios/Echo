using System.Threading.Tasks;
#if UNITY_IOS
using System;
using Unity.Advertisement.IosSupport;
using UnityEngine;
#endif

namespace Echo.Services.Ads
{
    /// <summary>
    /// iOS App Tracking Transparency (tech spec 19). Asked only on iOS, only once, and only when the player
    /// has agreed to personalised ads. Elsewhere it completes immediately. The iOS support assembly exists
    /// only in iOS builds, hence the compile guard.
    /// </summary>
    public static class TrackingAuthorization
    {
#if UNITY_IOS
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        public static async Task RequestIfNeededAsync(bool personalisedAdsConsent)
        {
            if (Application.platform != RuntimePlatform.IPhonePlayer || !personalisedAdsConsent) return;
            if (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() != ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED) return;

            ATTrackingStatusBinding.RequestAuthorizationTracking();
            // The binding has no callback; poll until the player answers.
            var started = DateTime.UtcNow;
            while (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED
                   && DateTime.UtcNow - started < Timeout)
                await Task.Delay(250);
        }
#else
        public static Task RequestIfNeededAsync(bool personalisedAdsConsent) => Task.CompletedTask;
#endif
    }
}
