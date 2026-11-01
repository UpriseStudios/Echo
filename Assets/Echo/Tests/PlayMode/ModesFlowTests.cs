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

        static string Line(string path) => Find(path).GetComponent<TMP_Text>().text;

        [UnityTest]
        public IEnumerator GhostTown_ShowsTheDaily_TheLiveNumbers_AndTheEvents()
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
            backend.Modes.Add(new ModeState { Id = ModeIds.NaughtyNice, Open = true, SecondsLeft = 3 * 86400 + 4 * 3600, Players = 1204 });
            backend.Modes.Add(new ModeState { Id = ModeIds.OneLife, Open = false, SecondsLeft = 2 * 3600, Players = -1 });
            backend.Pulse = new DailyPulseInfo { Runners = 87, LeaderName = "Tess#1234", LeaderDistance = 1204, Rank = 12, BestDistance = 880, SecondsLeft = 5 * 3600 };

            // Ghost Town (a user decision): the title's button, the Daily Haunt's card with the community's live numbers,
            // then the events, live first.
            StringAssert.Contains("Ghost Town", Text("Title/Modes"));
            Click("Title/Modes");
            yield return null;
            Assert.AreEqual(GameState.Menu, game.State);
            Assert.AreEqual("GHOST TOWN", Find("Modes/Heading").GetComponent<TMP_Text>().text);
            StringAssert.Contains("87 runners today", Line("Modes/Hero/Runners"));
            StringAssert.Contains("Tess#1234", Line("Modes/Hero/Leader"));
            StringAssert.Contains("1,204 m", Line("Modes/Hero/Leader"));
            StringAssert.Contains("#12", Line("Modes/Hero/You"));
            StringAssert.Contains("5h", Line("Modes/Hero/Countdown"));
            Assert.IsTrue(Showing("Modes/Hero/Dot"), "not played today");
            Assert.IsTrue(Showing("Modes/Events/LiveHeader"));
            Assert.AreEqual("Naughty vs Nice", Line("Modes/Events/Event0/Name"));
            StringAssert.Contains("1,204 on the teams", Line("Modes/Events/Event0/Players"));
            StringAssert.Contains("Ends in 3d 4h", Line("Modes/Events/Event0/Countdown"));
            Assert.AreEqual("LIVE", Line("Modes/Events/Event0/Chip/Label"));
            Assert.IsTrue(Showing("Modes/Events/SoonHeader"));
            Assert.AreEqual("One Life", Line("Modes/Events/Event1/Name"));
            Assert.AreEqual("SOON", Line("Modes/Events/Event1/Chip/Label"));
            Assert.AreEqual("", Line("Modes/Events/Event1/Players"), "an unknown count shows nothing");
            Assert.IsFalse(Showing("Modes/Events/Event2"));

            // An event that hasn't opened says when it does, and stays here.
            Click("Modes/Events/Event1");
            yield return null;
            StringAssert.Contains("Opens in", Find("Modes/Status").GetComponent<TMP_Text>().text);
            Assert.IsTrue(Showing("Modes"));

            // The Daily Haunt card opens its lobby; Back returns to the title.
            Click("Modes/Hero");
            yield return null;
            Assert.IsTrue(Showing("DailyLobby"));
            Click("DailyLobby/Back");
            Assert.AreEqual(GameState.Title, game.State);

            // Offline: the game and the Daily Haunt still show; events say they need a connection.
            backend.Offline = true;
            Click("Title/Modes");
            yield return null;
            StringAssert.Contains("need a connection", Find("Modes/Status").GetComponent<TMP_Text>().text);
            Assert.IsTrue(Showing("Modes/Hero"));
            Assert.IsFalse(Showing("Modes/Events/Event0"));
            Click("Modes/Back");
            backend.Offline = false;

            // Development: every mode forced open, Ranked Ghost Duels named as ranked.
            game.ForceModes = true;
            Click("Title/Modes");
            yield return null;
            Assert.AreEqual(ModeIds.All.Length, game.LastModes.Modes.Count);
            Assert.IsTrue(Enumerable.Range(0, 4).Any(i => Line($"Modes/Events/Event{i}/Name") == "Ranked Ghost Duels"));
            Click("Modes/Back");
            Assert.AreEqual(GameState.Title, game.State);
        }
    }
}
