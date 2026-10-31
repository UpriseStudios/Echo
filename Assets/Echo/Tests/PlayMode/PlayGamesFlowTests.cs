using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Echo.Core.Achievements;
using Echo.Core.Courses;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Echo.Services.Mocks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Echo.Tests.PlayMode
{
    /// <summary>
    /// Google Play Games (Play's Level Up guidelines): signs in once services settle, sends what was already earned,
    /// Game Stats and the save after a run; a cloud save that is further along is offered on the title and, taken,
    /// reloads the game on it with this device's kept beside it.
    /// </summary>
    public class PlayGamesFlowTests
    {
        string _savePath;
        MockPlayGamesService _play;

        [SetUp]
        public void SetUp()
        {
            _play = new MockPlayGamesService { Available = true };
            EchoGame.PlayGamesOverride = _play;
        }

        [TearDown]
        public void TearDown()
        {
            EchoGame.PlayGamesOverride = null;
            EchoGame.SavePathOverride = null;
            foreach (string f in new[] { _savePath, _savePath + ".tmp", _savePath + ".bak", _savePath + ".device.bak" })
                if (f != null && File.Exists(f)) File.Delete(f);
        }

        static void Click(string path)
        {
            var go = GameObject.Find("Canvas/SafeArea/" + path);
            Assert.IsNotNull(go, path);
            Assert.IsTrue(go.activeInHierarchy, path + " is showing");
            go.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        SaveData StartingSave(int runs, int best)
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            var save = new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Seed = 5,
                Best = best,
                Stats = new StatsSaveData { TotalRuns = runs, MaxDistance = best, Deaths = 40 },
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true },
                Achievements = new List<string> { AchievementIds.HelloAgain },
                AchievementProgress = new Dictionary<string, int> { [AchievementIds.BadDecisions] = 40 },
            };
            _savePath = Path.Combine(Path.GetTempPath(), "echo_playgames_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(save));
            EchoGame.SavePathOverride = _savePath;
            return save;
        }

        static IEnumerator Title()
        {
            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
            for (int i = 0; i < 5; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator SignsIn_SendsAchievementsStatsAndTheSave()
        {
            StartingSave(runs: 12, best: 300);
            yield return Title();
            var game = Object.FindAnyObjectByType<EchoGame>();

            Assert.IsTrue(_play.SignedIn, "signed in once the services settled");
            Assert.AreEqual(1, _play.Achievements[AchievementIds.HelloAgain], "an earned achievement is unlocked on Play");
            Assert.AreEqual(40, _play.Achievements[AchievementIds.BadDecisions], "a counted one keeps its steps");
            Assert.AreEqual(300, _play.BestDistance, "the progression stat");
            Assert.AreEqual(1, _play.CloudWrites, "an empty cloud gets this device's save");
            Assert.IsFalse(game.CloudSaveOffered);

            // A run into the first obstacle, then back to the menu, which commits it.
            game.enabled = false;
            const float dt = 1f / 60f;
            game.TickFrame(dt, tapped: true);
            for (int f = 0; f < 3000 && game.State != GameState.DeathScreen; f++) { game.TickFrame(dt, false); if (f % 30 == 0) yield return null; }
            Assert.AreEqual(GameState.DeathScreen, game.State);
            Assert.AreEqual(0, _play.Runs.Count, "a main-game run counts once committed (Continue may revive it)");
            Click("DeathScreen/Menu");
            Assert.AreEqual(1, _play.Runs.Count, "Game Stats: one finished run");
            Assert.Greater(_play.Runs[0].Distance, 0, "with its distance");
            Assert.AreEqual(41, _play.Achievements[AchievementIds.BadDecisions], "a death counts towards Bad Decisions");

            var cloud = CloudSave.Unpack(_play.CloudSave);
            Assert.IsNotNull(cloud);
            Assert.AreEqual(12, cloud.Stats.TotalRuns, "within a minute only the first save goes up");
            game.SendMessage("OnApplicationPause", true);
            yield return null;
            Assert.AreEqual(13, CloudSave.Unpack(_play.CloudSave).Stats.TotalRuns, "putting the game away sends it now");
        }

        [UnityTest]
        public IEnumerator CloudSaveAhead_IsOffered_AndTakingItReloadsOnIt()
        {
            StartingSave(runs: 3, best: 50);
            SaveData elsewhere = new SaveData
            {
                ContentVersion = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(_savePath)).ContentVersion,
                Seed = 9,
                Best = 900,
                Stats = new StatsSaveData { TotalRuns = 120, MaxDistance = 900 },
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, SeenHowToPlay = true },
            };
            _play.CloudSave = CloudSave.Pack(elsewhere);

            yield return Title();
            var game = Object.FindAnyObjectByType<EchoGame>();
            Assert.IsTrue(game.CloudSaveOffered, "the cloud has more runs");
            Assert.AreEqual(0, _play.CloudWrites, "nothing overwrites the cloud while it's on offer");
            Click("Challenge/Primary");

            // The scene loads again on the cloud's save.
            for (int i = 0; i < 300 && (game != null || Object.FindAnyObjectByType<EchoGame>() == null); i++) yield return null;
            var restored = Object.FindAnyObjectByType<EchoGame>();
            Assert.IsNotNull(restored);
            while (restored.State != GameState.Title) yield return null;
            Assert.AreEqual(900, restored.Progress.Best, "playing on the restored save");
            Assert.AreEqual(120, restored.Progress.Stats.TotalRuns);
            Assert.IsTrue(File.Exists(_savePath + ".device.bak"), "this device's save is kept beside it");
            Assert.AreEqual(50, JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(_savePath + ".device.bak")).Best);
            Assert.IsFalse(restored.CloudSaveOffered, "the same save isn't offered again");
            Assert.AreSame(_play, restored.Services.PlayGames, "the running services came along");
        }

        [UnityTest]
        public IEnumerator KeepingThisDevice_SendsItsSaveUp()
        {
            StartingSave(runs: 3, best: 50);
            _play.CloudSave = CloudSave.Pack(new SaveData { Best = 10, Stats = new StatsSaveData { TotalRuns = 99 } });
            yield return Title();
            var game = Object.FindAnyObjectByType<EchoGame>();
            Assert.IsTrue(game.CloudSaveOffered);
            Click("Challenge/Secondary");
            Assert.IsFalse(game.CloudSaveOffered);
            yield return null;
            Assert.AreEqual(3, CloudSave.Unpack(_play.CloudSave).Stats.TotalRuns, "this device's save replaces the cloud's");
            Assert.AreEqual(50, game.Progress.Best);
        }

        [UnityTest]
        public IEnumerator Settings_OffersSignIn_ThenTheAchievements()
        {
            _play.SignInWorks = false;
            StartingSave(runs: 3, best: 50);
            yield return Title();
            Assert.IsFalse(_play.SignedIn);
            Click("Title/Settings");
            yield return null;
            var button = GameObject.Find("Canvas/SafeArea/Settings/PlayGames");
            Assert.IsNotNull(button, "shown where Play Games is available");
            Assert.AreEqual("Play Games", button.GetComponentInChildren<TMPro.TMP_Text>().text);
            _play.SignInWorks = true;
            Click("Settings/PlayGames");
            yield return null;
            Assert.IsTrue(_play.SignedIn, "signs in when asked");
            Assert.AreEqual("Achievements", button.GetComponentInChildren<TMPro.TMP_Text>().text);
            Click("Settings/PlayGames");
            Assert.AreEqual(1, _play.AchievementsShown);
        }
    }
}
