using System.Collections.Generic;
using System.IO;
using Echo.Game.Bootstrap;
using Echo.Game.Persistence;
using Echo.Services;
using Echo.Services.Ads;
using Echo.Services.Iap;
using Echo.Services.Mocks;
using NUnit.Framework;
using UnityEngine;

namespace Echo.Tests.EditMode
{
    public class ServicesTests
    {
        [Test]
        public void Consent_IsUndecidedUntilAnswered_AndSurvivesASave()
        {
            var data = new SaveData();
            Assert.IsFalse(data.Consent.Decided);

            string path = Path.Combine(Path.GetTempPath(), "echo_consent_" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                data.Consent.Version = ConsentSaveData.CurrentVersion;
                data.Consent.Analytics = true;
                var saves = new SaveSystem(path);
                saves.Save(data);
                SaveData loaded = saves.Load();
                Assert.IsTrue(loaded.Consent.Decided);
                Assert.IsTrue(loaded.Consent.Analytics);
                Assert.IsFalse(loaded.Consent.PersonalisedAds);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void MockIap_RaisesEntitlement_OnPurchaseAndRestore()
        {
            var services = EchoServices.CreateMocks();
            var entitled = new List<string>();
            services.Entitled += entitled.Add;

            bool? result = null;
            services.Iap.Purchase(ProductIds.PackRoyal, ok => result = ok);
            Assert.IsTrue(result);
            Assert.IsTrue(services.Iap.Owns(ProductIds.PackRoyal));
            CollectionAssert.AreEqual(new[] { ProductIds.PackRoyal }, entitled);

            services.Iap.Restore(_ => { });
            CollectionAssert.AreEqual(new[] { ProductIds.PackRoyal, ProductIds.PackRoyal }, entitled);
        }

        [Test]
        public void AdoptingRealServices_KeepsMocksForMissingOnes_AndRewiresEntitlements()
        {
            var services = EchoServices.CreateMocks();
            var entitled = new List<string>();
            services.Entitled += entitled.Add;

            var newIap = new MockIapService();
            services.Adopt(new RealServices { Iap = newIap });
            Assert.AreSame(newIap, services.Iap);
            Assert.IsTrue(services.UsingMockAds, "no real ads were provided");

            newIap.Purchase(ProductIds.SeasonOfEcho, _ => { });
            CollectionAssert.AreEqual(new[] { ProductIds.SeasonOfEcho }, entitled);
        }

        [Test]
        public void DevToggle_MakesMockRewardedAdsFail()
        {
            var services = EchoServices.CreateMocks();
            Assert.IsTrue(services.ToggleMockAdFailure());
            bool? rewarded = null;
            services.Ads.ShowRewarded(AdPlacements.Continue, r => rewarded = r);
            Assert.IsFalse(rewarded);
            Assert.IsFalse(services.ToggleMockAdFailure());
        }

        [Test]
        public void ServiceConfig_WithPlaceholders_IsNotConfigured_AndRealServicesStayOffInTheEditor()
        {
            var config = ScriptableObject.CreateInstance<EchoServiceConfig>();
            Assert.IsFalse(config.AdsConfigured);
            Assert.IsFalse(RealServices.Enabled(config));
            config.LevelPlayAppKeyAndroid = "abc";
            config.BanishAdUnitAndroid = "b";
            config.ContinueAdUnitAndroid = "c";
            Assert.IsFalse(config.AdsConfigured, "every placement needs its ad unit");
            config.UnlockAdUnitAndroid = "u";
            Assert.IsTrue(config.AdsConfigured);
            Object.DestroyImmediate(config);
        }
    }
}
