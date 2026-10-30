using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Echo.Game.Seasons;
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
    /// Worlds (a user revision) in the built scene: a store world bought and worn becomes the backdrop of every run; a
    /// season with its own look shows over it while Settings › Seasons is on, and not when it's off; a season without a
    /// backdrop (Valentine's) keeps the world behind its own touches; the mode reward can't be bought.
    /// </summary>
    public class WorldsFlowTests
    {
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
            Button[] buttons = Find("Skins").GetComponentsInChildren<Button>();
            Button b = buttons.FirstOrDefault(x => x.GetComponentInChildren<TMP_Text>() is TMP_Text t && t.text.StartsWith(startsWith));
            Assert.IsNotNull(b, $"no {startsWith} in Skins");
            return b;
        }

        IEnumerator Load()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath = Path.Combine(Path.GetTempPath(), "echo_worlds_" + System.Guid.NewGuid().ToString("N") + ".json");
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

        [UnityTest, Timeout(120000)]
        public IEnumerator AWorld_IsBoughtAndWorn_AndSeasonsShowOverItWhileOn()
        {
            yield return Load();
            var game = Object.FindAnyObjectByType<EchoGame>();
            game.ForceSeason(EchoGame.NoSeason);
            Assert.IsNull(game.ActiveBackdrop, "the normal world to start");

            Click("Title/Skins");
            yield return null;
            Click("Skins/Tabs/Worlds");
            yield return null;
            StringAssert.Contains("best value", SkinsButton("Worlds Bundle").GetComponentInChildren<TMP_Text>().text, "the bundle leads the tab");
            Assert.IsNotNull(SkinsButton("Christmas Town").transform.Find("Preview/Far").GetComponent<Image>().sprite, "a picture of the world");
            Button graveyard = SkinsButton("Haunted Graveyard");
            Assert.IsFalse(graveyard.interactable, "a mode reward is never sold");
            StringAssert.Contains("top 10", graveyard.GetComponentInChildren<TMP_Text>().text);
            StringAssert.Contains("£0.99", SkinsButton("Christmas Town").GetComponentInChildren<TMP_Text>().text);
            StringAssert.Contains("£2.99", SkinsButton("Worlds Bundle").GetComponentInChildren<TMP_Text>().text);
            foreach (string world in new[] { "Halloween Night", "Neon Night", "Sunken Ruins", "Autumn Woods", "Beach", "Desert Dunes", "Volcanic Waste", "Toy Town" })
                StringAssert.Contains("£0.99", SkinsButton(world).GetComponentInChildren<TMP_Text>().text, world + " is in the store");

            SkinsButton("Christmas Town").onClick.Invoke();   // buy
            yield return null;
            CollectionAssert.Contains(game.CurrentSave.Cosmetics.Owned, "world_christmas");
            SkinsButton("Christmas Town").onClick.Invoke();   // wear
            yield return null;
            Assert.AreEqual("world_christmas", game.CurrentSave.Cosmetics.Equipped.World);
            CollectionAssert.Contains(((Echo.Services.Mocks.MockAnalytics)game.Services.Analytics).Events, "world_equipped world=world_christmas bought=True");
            Click("Skins/Close");
            yield return null;
            Assert.AreEqual(SeasonIds.Christmas, game.ActiveBackdrop.Id, "worn out of season");
            Assert.IsNull(game.ActiveSeason, "the backdrop only: no Santa hats or snowman graves");

            // A season with its own look shows over it while Seasons is on...
            game.ForceSeason(SeasonIds.Halloween);
            Assert.AreEqual(SeasonIds.Halloween, game.ActiveBackdrop.Id);
            // ...and a season without one keeps the world behind its own touches.
            game.ForceSeason(SeasonIds.Valentines);
            Assert.AreEqual(SeasonIds.Christmas, game.ActiveBackdrop.Id);
            Assert.AreEqual(SeasonIds.Valentines, game.ActiveSeason.Id);

            // Seasons off: your world always.
            game.ForceSeason(SeasonIds.Halloween);
            Click("Title/Settings");
            yield return null;
            StringAssert.Contains("Seasons", Find("Settings/SeasonalLooks").GetComponentInChildren<TMP_Text>().text);
            Click("Settings/SeasonalLooks");
            Assert.IsFalse(game.CurrentSave.Settings.SeasonalLooks);
            Click("Settings/Close");
            yield return null;
            Assert.AreEqual(SeasonIds.Christmas, game.ActiveBackdrop.Id);
            Assert.IsNull(game.ActiveSeason);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheGravestoneYouChoose_WinsOverTheSeasons()
        {
            yield return Load();
            var game = Object.FindAnyObjectByType<EchoGame>();
            var graves = Object.FindAnyObjectByType<Echo.Game.Views.GraveViews>();
            game.ForceSeason(SeasonIds.Christmas);
            Sprite snowman = graves.Current;
            game.CurrentSave.Cosmetics.Owned.Add("grave_heart");

            Click("Title/Skins");
            yield return null;
            Click("Skins/Tabs/Extras");
            yield return null;
            SkinsButton("Heart").onClick.Invoke();
            yield return null;
            Assert.AreNotEqual(snowman, graves.Current, "a user revision: the chosen stone wins over the season's");
            SkinsButton("Stone").onClick.Invoke();
            yield return null;
            Assert.AreEqual(snowman, graves.Current, "the plain stone gives way to the season's");
            Click("Skins/Close");
        }
    }
}
