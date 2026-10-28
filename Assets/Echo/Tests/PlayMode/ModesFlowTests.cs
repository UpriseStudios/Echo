using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Modes;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Echo.Services.Backend;
using Echo.Services.Mocks;
using Newtonsoft.Json;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Echo.Tests.PlayMode
{
    /// <summary>
    /// The Modes screen (modes doc, the mode framework) in the built scene: the main game and the Daily Haunt always,
    /// live modes as the server reports them with their countdowns, offline saying so, and the dev override.
    /// </summary>
    public class ModesFlowTests
    {
        const float Dt = 1f / 60f;
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

        static string Text(string path) => Find(path).GetComponentInChildren<TMP_Text>().text;

        [UnityTest]
        public IEnumerator TheModesScreen_ListsTheGame_TheDaily_AndLiveModes()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath = Path.Combine(Path.GetTempPath(), "echo_modes_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Best = 321,
                Seed = 31,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true, SeenHowToPlay = true },
            }));
            EchoGame.SavePathOverride = _savePath;
            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
            var backend = (MockBackendService)game.Services.Backend;
            backend.Flags = new BackendFlags(true, true);
            backend.Modes.Add(new ModeState { Id = ModeIds.NaughtyNice, Open = true, SecondsLeft = 3 * 86400 + 4 * 3600 });

            Click("Title/Modes");
            yield return null;
            Assert.AreEqual(GameState.Menu, game.State);
            StringAssert.StartsWith("Echo", Text("Modes/Card0"));
            StringAssert.Contains("best 321", Text("Modes/Card0"));
            StringAssert.StartsWith("Daily Haunt", Text("Modes/Card1"));
            StringAssert.Contains("●", Text("Modes/Card1"), "not played today");
            StringAssert.StartsWith("Naughty vs Nice", Text("Modes/Card2"));
            StringAssert.Contains("Ends in 3d 4h", Text("Modes/Card2"));
            Assert.IsFalse(Showing("Modes/Card3"));

            // The Daily Haunt card opens its lobby; Back returns to the title.
            Click("Modes/Card1");
            yield return null;
            Assert.IsTrue(Showing("DailyLobby"));
            Click("DailyLobby/Back");
            Assert.AreEqual(GameState.Title, game.State);

            // Offline: the game and the Daily Haunt still show; events say they need a connection.
            backend.Offline = true;
            Click("Title/Modes");
            yield return null;
            StringAssert.Contains("need a connection", Find("Modes/Status").GetComponent<TMP_Text>().text);
            Assert.IsTrue(Showing("Modes/Card1"));
            Assert.IsFalse(Showing("Modes/Card2"));
            Click("Modes/Back");
            backend.Offline = false;

            // Development: every mode forced open, Ranked Ghost Duels named as ranked.
            game.ForceModes = true;
            Click("Title/Modes");
            yield return null;
            Assert.AreEqual(ModeIds.All.Length, game.LastModes.Modes.Count);
            Assert.IsTrue(Enumerable.Range(2, 4).Any(i => Text($"Modes/Card{i}").StartsWith("Ranked Ghost Duels")));

            // The Echo card starts a run.
            Click("Modes/Card0");
            game.TickFrame(Dt, false);
            Assert.AreEqual(GameState.Running, game.State);
            Assert.IsFalse(Showing("Modes"));
        }
    }
}
