using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Achievements;
using Echo.Core.Courses;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Echo.Game.UI;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Echo.Tests.PlayMode
{
    /// <summary>
    /// ELEMENTS AWAKENED (a user revision): with every achievement earned, the Elementals are owned and the reward
    /// moment plays over the title, holding taps until it is skipped and closed, and plays only once.
    /// </summary>
    public class ElementsAwakenedTests
    {
        static readonly string[] Elementals = { "char_ember", "char_frost", "char_storm", "char_tide" };
        string _savePath;

        [TearDown]
        public void TearDown()
        {
            EchoGame.SavePathOverride = null;
            foreach (string f in new[] { _savePath, _savePath + ".tmp", _savePath + ".bak" })
                if (f != null && File.Exists(f)) File.Delete(f);
        }

        static IEnumerator WaitFor(System.Func<bool> condition, float seconds, string what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail($"timed out waiting for {what}");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator EveryAchievement_PlaysTheRewardOnce_ThenTheRunCanStart()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath = Path.Combine(Path.GetTempPath(), "echo_awakened_" + System.Guid.NewGuid().ToString("N") + ".json");
            var save = new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Seed = 9,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true },
                Achievements = AchievementCatalog.All.Select(a => a.Id).ToList(),
            };
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(save));
            EchoGame.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            yield return WaitFor(() => game.State == GameState.Title, 3f, "title");
            var view = Object.FindAnyObjectByType<ElementsAwakenedView>(FindObjectsInactive.Include);
            Assert.IsNotNull(view);
            yield return WaitFor(() => view.IsOpen, 2f, "ELEMENTS AWAKENED");
            foreach (string id in Elementals)
                Assert.Contains(id, game.CurrentSave.Cosmetics.Owned, "owned for good");

            game.InjectTap();
            yield return null;
            Assert.AreEqual(GameState.Title, game.State, "a tap behind the moment must not start a run");

            // A tap mid-sequence skips to the full picture; the next closes it.
            var tap = view.GetComponent<Button>();
            tap.onClick.Invoke();
            Assert.IsTrue(view.IsOpen);
            var figures = view.GetComponentsInChildren<Image>().Where(i => i.name == "Sprite").ToArray();
            Assert.AreEqual(4, figures.Length);
            Assert.IsTrue(figures.All(f => f.sprite != null && f.color.a > 0.99f), "all four shown");
            tap.onClick.Invoke();
            Assert.IsFalse(view.IsOpen);

            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsFalse(view.IsOpen, "it plays once");
            game.InjectTap();
            yield return WaitFor(() => game.State == GameState.Running, 2f, "the run");
        }
    }
}
