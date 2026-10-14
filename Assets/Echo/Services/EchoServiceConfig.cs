using UnityEngine;

namespace Echo.Services
{
    /// <summary>
    /// Keys and ids for the real SDKs (tech spec 15). Ships with placeholders; until every id for the
    /// current platform is filled in, the game keeps using the mock services.
    /// </summary>
    [CreateAssetMenu(menuName = "Echo/Service Config", fileName = "EchoServiceConfig")]
    public sealed class EchoServiceConfig : ScriptableObject
    {
        public const string Placeholder = "REPLACE_ME";

        /// <summary>The asset lives in a Resources folder under this name (Settings/Resources/EchoServiceConfig).</summary>
        public const string ResourceName = "EchoServiceConfig";

        /// <summary>The config from Resources: the fallback when a scene's reference to it is missing.</summary>
        public static EchoServiceConfig Load() => Resources.Load<EchoServiceConfig>(ResourceName);

        [Header("LevelPlay")]
        public string LevelPlayAppKeyAndroid = Placeholder;
        public string LevelPlayAppKeyIos = Placeholder;
        public string BanishAdUnitAndroid = Placeholder;
        public string BanishAdUnitIos = Placeholder;
        public string ContinueAdUnitAndroid = Placeholder;
        public string ContinueAdUnitIos = Placeholder;
        public string UnlockAdUnitAndroid = Placeholder;
        public string UnlockAdUnitIos = Placeholder;
        [Tooltip("Rewarded ad unit for Banish 10; while it is REPLACE_ME the banish ad unit is used.")]
        public string BanishTenAdUnitAndroid = Placeholder;
        public string BanishTenAdUnitIos = Placeholder;
        [Tooltip("Rewarded ad unit for power-ups; while it is REPLACE_ME the unlock ad unit is used.")]
        public string PowerUpAdUnitAndroid = Placeholder;
        public string PowerUpAdUnitIos = Placeholder;
        [Tooltip("Rewarded ad unit for banishing your own Daily Haunt ghost; while it is REPLACE_ME the banish ad unit is used.")]
        public string DailyBanishAdUnitAndroid = Placeholder;
        public string DailyBanishAdUnitIos = Placeholder;
        [Tooltip("Rewarded ad unit for watching a run on the Daily Haunt board; while it is REPLACE_ME the unlock ad unit is used.")]
        public string WatchRunAdUnitAndroid = Placeholder;
        public string WatchRunAdUnitIos = Placeholder;

        [Header("Stores")]
        [Tooltip("Store pages, added to Ghost Challenge messages for friends who don't have Echo yet.")]
        public string StoreUrlAndroid = Placeholder;
        public string StoreUrlIos = Placeholder;

        [Header("Unity Gaming Services")]
        [Tooltip("UGS environment for development builds, the editor and development-server builds. Never production.")]
        public string UgsEnvironmentDevelopment = "development";
        [Tooltip("UGS environment for release builds.")]
        public string UgsEnvironmentRelease = "production";

        [Header("Development")]
        [Tooltip("Use the real SDKs in the editor too (normally mocks run in the editor).")]
        public bool RealServicesInEditor;

        public string LevelPlayAppKey => IsIos ? LevelPlayAppKeyIos : LevelPlayAppKeyAndroid;
        public string UgsEnvironment => Debug.isDebugBuild || DevelopmentServerBuild ? UgsEnvironmentDevelopment : UgsEnvironmentRelease;

        /// <summary>
        /// A release build made with Echo › Build › Android App Bundle (development server): store-ready (Play's testing
        /// tracks take it) but it talks to the development environment, so server changes can be tested on phones before
        /// production has them. Never for a production release; the title shows DEV SERVER.
        /// </summary>
#if ECHO_DEV_SERVER
        public static readonly bool DevelopmentServerBuild = true;
#else
        public static readonly bool DevelopmentServerBuild = false;
#endif

        /// <summary>A player build talking to development (not the editor): the title says so.</summary>
        public bool ShowsDevServerTag => !Application.isEditor && UgsEnvironment != UgsEnvironmentRelease;
        public string BanishAdUnit => IsIos ? BanishAdUnitIos : BanishAdUnitAndroid;
        public string BanishTenAdUnit => IsSet(IsIos ? BanishTenAdUnitIos : BanishTenAdUnitAndroid) ? (IsIos ? BanishTenAdUnitIos : BanishTenAdUnitAndroid) : BanishAdUnit;
        public string ContinueAdUnit => IsIos ? ContinueAdUnitIos : ContinueAdUnitAndroid;
        public string UnlockAdUnit => IsIos ? UnlockAdUnitIos : UnlockAdUnitAndroid;
        public string PowerUpAdUnit => IsSet(IsIos ? PowerUpAdUnitIos : PowerUpAdUnitAndroid) ? (IsIos ? PowerUpAdUnitIos : PowerUpAdUnitAndroid) : UnlockAdUnit;
        public string DailyBanishAdUnit => IsSet(IsIos ? DailyBanishAdUnitIos : DailyBanishAdUnitAndroid) ? (IsIos ? DailyBanishAdUnitIos : DailyBanishAdUnitAndroid) : BanishAdUnit;
        public string WatchRunAdUnit => IsSet(IsIos ? WatchRunAdUnitIos : WatchRunAdUnitAndroid) ? (IsIos ? WatchRunAdUnitIos : WatchRunAdUnitAndroid) : UnlockAdUnit;
        public string StoreUrl => IsIos ? StoreUrlIos : StoreUrlAndroid;

        public bool AdsConfigured => IsSet(LevelPlayAppKey) && IsSet(BanishAdUnit) && IsSet(ContinueAdUnit) && IsSet(UnlockAdUnit);

        static bool IsIos => Application.platform == RuntimePlatform.IPhonePlayer;

        public static bool IsSet(string value) => !string.IsNullOrWhiteSpace(value) && value != Placeholder;
    }
}
