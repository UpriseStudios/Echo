using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Achievements;
using Echo.Core.Courses;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Echo.Game.Seasons;
using Echo.Services.Mocks;
using Newtonsoft.Json;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Echo.Tests.PlayMode
{
    /// <summary>
    /// World Time Run doc, phase 4, in the built scene: Time Run in the store with "3 free runs left", worn on trial for
    /// three runs (each logged, each counted in the save, Dino Land deaths counting towards Extinct), then the normal
    /// world until it's bought for £1.99, outside the Worlds Bundle; and its rewards in Skins.
    /// </summary>
    public class TimeRunShopFlowTests
    {
        const float Dt = 1f / 60f;
        string _savePath;

        [TearDown]
        public void TearDown()
        {
            EchoGame.SavePathOverride = null;
            foreach (string f in new[] { _savePath, _savePath + ".tmp", _savePath + ".bak" })
                if (f != null && File.Exists(f)) File.Delete(f);
            _savePath = null;
        }

        static GameObject Find(string path) => GameObject.Find("Canvas/SafeArea/" + path);

        static void Click(string path)
        {
            GameObject go = Find(path);
            Assert.IsTrue(go != null && go.activeInHierarchy, path + " is showing");
            go.GetComponent<Button>().onClick.Invoke();
        }

        static Button SkinsButton(string startsWith)
        {
            Button b = Find("Skins").GetComponentsInChildren<Button>()
                .FirstOrDefault(x => x.GetComponentInChildren<TMP_Text>() is TMP_Text t && t.text.StartsWith(startsWith));
            Assert.IsNotNull(b, $"no {startsWith} in Skins");
            return b;
        }

        static string Label(Button b) => b.GetComponentInChildren<TMP_Text>().text;

        IEnumerator Load()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath = Path.Combine(Path.GetTempPath(), "echo_timerun_shop_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Best = 100,
                Seed = 31,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true, SeenHowToPlay = true },
            }));
            EchoGame.SavePathOverride = _savePath;
            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
        }

        // A run with no taps: it falls at the first obstacle, in Dino Land.
        static IEnumerator RunToDeath(EchoGame game)
        {
            for (int f = 0; f < 60 * 60 && game.State != GameState.DeathScreen; f++)
            {
                game.TickFrame(Dt, false);
                if (f % 30 == 0) yield return null;
            }
            Assert.AreEqual(GameState.DeathScreen, game.State);
            for (int f = 0; f < 30; f++) game.TickFrame(Dt, false);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TimeRun_ThreeFreeRuns_ThenBoughtOnItsOwn()
        {
            yield return Load();
            var game = Object.FindAnyObjectByType<EchoGame>();
            var analytics = (MockAnalytics)game.Services.Analytics;
            game.ForceSeason(EchoGame.NoSeason);

            Click("Title/Skins");
            yield return null;
            Click("Skins/Tabs/Worlds");
            yield return null;
            StringAssert.Contains("All 9 store worlds", Label(SkinsButton("Worlds Bundle")), "Time Run isn't in the bundle");
            Button card = SkinsButton("Time Run");
            StringAssert.Contains("3 free runs left", Label(card));
            Assert.IsTrue(card.interactable);
            card.onClick.Invoke();   // try it
            yield return null;
            Assert.AreEqual("world_timerun", game.CurrentSave.Cosmetics.Equipped.World);
            CollectionAssert.DoesNotContain(game.CurrentSave.Cosmetics.Owned, "world_timerun", "trying isn't buying");
            StringAssert.Contains("£1.99", Label(SkinsButton("Time Run")), "worn on trial, a tap buys it");
            Click("Skins/Close");
            yield return null;
            Assert.AreEqual(SeasonIds.EraDino, game.ActiveBackdrop.Id, "Dino Land at the start");

            // Three free runs, each counted as it starts; every one falls in Dino Land.
            game.enabled = false;
            for (int run = 1; run <= 3; run++)
            {
                game.TickFrame(Dt, tapped: true);
                Assert.AreEqual(GameState.Running, game.State);
                Assert.AreEqual(run, game.CurrentSave.Cosmetics.WorldTrialUsed);
                CollectionAssert.Contains(analytics.Events, $"timerun_trial_started run={run} left={3 - run}");
                yield return RunToDeath(game);
            }
            Assert.AreEqual(3, AchievementRules.ProgressOf(game.Progress, AchievementIds.Extinct), "three deaths in Dino Land");
            Assert.AreEqual(3, game.CurrentSave.AchievementProgress[AchievementIds.Extinct], "saved");

            // The trial is over: the normal world, and the card asks for £1.99.
            Click("DeathScreen/Menu");
            yield return null;
            game.enabled = true;
            Assert.IsNull(game.ActiveBackdrop, "back to the normal world");
            game.TickFrame(Dt, tapped: true);
            Assert.AreEqual(3, game.CurrentSave.Cosmetics.WorldTrialUsed, "no fourth free run");
            yield return RunToDeath(game);
            Click("DeathScreen/Menu");
            yield return null;

            Click("Title/Skins");
            yield return null;
            Click("Skins/Tabs/Worlds");
            yield return null;
            card = SkinsButton("Time Run");
            StringAssert.DoesNotContain("free", Label(card));
            StringAssert.Contains("£1.99", Label(card));
            card.onClick.Invoke();   // buy
            yield return null;
            CollectionAssert.Contains(game.CurrentSave.Cosmetics.Owned, "world_timerun");
            CollectionAssert.Contains(analytics.Events, "timerun_purchased trialRunsUsed=3");
            CollectionAssert.DoesNotContain(game.CurrentSave.Cosmetics.Owned, "world_neon", "only Time Run");

            // Its rewards wait in Skins: the Caveman and the Chrono Android under Time Run.
            Click("Skins/Tabs/Runners");
            yield return null;
            StringAssert.Contains("Time Traveller", Label(SkinsButton("Caveman")));
            StringAssert.Contains("Year 3026", Label(SkinsButton("Chrono Android")));
            Click("Skins/Close");
            yield return null;
            Assert.AreEqual(SeasonIds.EraDino, game.ActiveBackdrop.Id, "worn, and owned now");
        }
    }
}
