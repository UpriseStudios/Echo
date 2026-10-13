using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Echo.Tests.PlayMode
{
    /// <summary>Exit (a user revision): the title's Exit asks first; Stay keeps playing, Exit saves and quits.</summary>
    public class ExitTests
    {
        string _savePath;

        [TearDown]
        public void TearDown()
        {
            EchoGame.SavePathOverride = null;
            foreach (string f in new[] { _savePath, _savePath + ".tmp", _savePath + ".bak" })
                if (f != null && File.Exists(f)) File.Delete(f);
        }

        static GameObject Find(string path) => GameObject.Find("Canvas/SafeArea/" + path);

        static void Click(string path)
        {
            var go = Find(path);
            Assert.IsNotNull(go, path);
            Assert.IsTrue(go.activeInHierarchy, path + " is showing");
            go.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        static bool Showing(string path)
        {
            var go = Find(path);
            return go != null && go.activeInHierarchy;
        }

        [UnityTest]
        public IEnumerator Exit_AsksFirst_StayKeepsPlaying_ExitQuits()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath = Path.Combine(Path.GetTempPath(), "echo_exit_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Seed = 5,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true },
            }));
            EchoGame.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;

            Assert.IsTrue(Showing("Title/Exit"), "offered everywhere but iOS");
            Click("Title/Exit");
            Assert.IsTrue(Showing("Challenge"), "it asks first");
            game.InjectTap();
            game.TickFrame(1f / 60f, false);
            Assert.AreEqual(GameState.Title, game.State, "a tap behind the question must not start a run");
            Click("Challenge/Secondary");
            Assert.IsFalse(Showing("Challenge"));
            Assert.IsFalse(game.QuitRequested, "Stay");

            Click("Title/Exit");
            Click("Challenge/Primary");
            Assert.IsTrue(game.QuitRequested, "Exit");
            Assert.IsTrue(File.Exists(_savePath), "saved before quitting");
        }
    }
}
