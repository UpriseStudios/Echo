using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Daily;
using Echo.Core.Ghosts;
using Echo.Core.Modes;
using Echo.Core.Net;
using Echo.Core.Reels;
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
    /// One Life (modes doc) in the built scene, through the mock backend: from the Modes screen to its lobby, the warning,
    /// the one run against today's leader, no 1-UPs, its result, no second run; and a run cut off by putting the game away
    /// kept, abandoned where it stopped, ready to send.
    /// </summary>
    public class OneLifeFlowTests
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

        static bool Showing(string path)
        {
            var go = Find(path);
            return go != null && go.activeInHierarchy;
        }

        static void Click(string path)
        {
            Assert.IsTrue(Showing(path), path + " is showing");
            Find(path).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        static string Text(string path) => Find(path).GetComponentInChildren<TMP_Text>().text;

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        IEnumerator Load()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath ??= Path.Combine(Path.GetTempPath(), "echo_onelife_" + System.Guid.NewGuid().ToString("N") + ".json");
            if (!File.Exists(_savePath))
                File.WriteAllText(_savePath, JsonConvert.SerializeObject(new SaveData
                {
                    ContentVersion = ContentVersion.Compute(library, cfg),
                    Best = 777,
                    Seed = 31,
                    Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                    Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true, SeenHowToPlay = true },
                    PowerUps = new PowerUpsSaveData { ExtraLife = 2 },
                }));
            EchoGame.SavePathOverride = _savePath;
            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
        }

        static MockBackendService Open(EchoGame game)
        {
            var backend = (MockBackendService)game.Services.Backend;
            backend.Flags = new BackendFlags(true, true);
            backend.OneLifeOpen = true;
            backend.Modes.Add(new ModeState { Id = ModeIds.OneLife, Open = true, SecondsLeft = 5000 });
            backend.OneLifeLeaders.Add((DailyRules.AsLeader(new GhostRecord { RunIndex = 1, DeathTick = 900, TapTicks = new int[0] }, 1, "BraveGhost#1111"), 640));
            return backend;
        }

        IEnumerator ToTheOneRun(EchoGame game)
        {
            Click("Title/Modes");
            yield return Frames(2);
            StringAssert.StartsWith("One Life", Text("Modes/Card2"));
            Click("Modes/Card2");
            yield return Frames(3);
            Assert.AreEqual("One Life", Find("DailyLobby/Heading").GetComponent<TMP_Text>().text);
            StringAssert.Contains("BraveGhost#1111", Text("DailyLobby/Row0"));
            Assert.IsFalse(Showing("DailyLobby/WatchHint"), "One Life's runs aren't for watching");
            StringAssert.Contains("Start your one run", Text("DailyLobby/Play"));
            Click("DailyLobby/Play");
            yield return null;
            StringAssert.Contains("No retries", Find("Challenge").GetComponentsInChildren<TMP_Text>().Select(t => t.text).Aggregate((a, b) => a + b));
            Click("Challenge/Primary");
            yield return Frames(2);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheOneRun_CountsOnce()
        {
            yield return Load();
            var game = Object.FindAnyObjectByType<EchoGame>();
            MockBackendService backend = Open(game);
            game.ScriptedTaps = t => false;
            yield return ToTheOneRun(game);

            Assert.AreEqual(GameState.Running, game.State);
            Assert.IsTrue(game.Run.Options.OneLife && game.Run.Options.Daily);
            Assert.IsFalse(game.Run.ExtraLifeHeld, "no Extra 1-UP");
            Assert.AreEqual(1, game.Run.GhostsAtStart, "today's leader");
            for (int i = 0; i < 20000 && game.State != GameState.DeathScreen; i++)
            {
                game.TickFrame(Dt, false);
                if (i % 50 == 0) yield return null;
            }
            yield return Frames(3);
            Assert.IsTrue(backend.OneLifeFinished, "sent");
            Assert.IsNotNull(game.LastOneLifeResult);
            StringAssert.Contains("Your one run today", Find("DeathScreen/Message").GetComponent<TMP_Text>().text);
            Assert.AreEqual("", game.CurrentSave.OneLife.Pending, "nothing left waiting");

            // Tap: no second run, back to the lobby.
            yield return new WaitForSecondsRealtime(1f);
            game.InjectTap();
            game.TickFrame(Dt, false);
            yield return Frames(3);
            Assert.AreNotEqual(GameState.Running, game.State);
            Assert.IsTrue(Showing("DailyLobby"));
            Assert.IsFalse(Showing("DailyLobby/Play"), "today's run is had");
            StringAssert.Contains("Your run today", Find("DailyLobby/You").GetComponent<TMP_Text>().text);
            Assert.AreEqual(1, backend.OneLifeSubmissions);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator PuttingTheGameAway_KeepsTheRunWhereItStopped()
        {
            yield return Load();
            var game = Object.FindAnyObjectByType<EchoGame>();
            Open(game);
            game.ScriptedTaps = t => t % 40 == 5;
            yield return ToTheOneRun(game);
            for (int i = 0; i < 90; i++) game.TickFrame(Dt, false);
            Assert.AreEqual(GameState.Running, game.State);
            int tick = game.Run.Tick;

            game.gameObject.SendMessage("OnApplicationPause", true);
            string pending = game.CurrentSave.OneLife.Pending;
            Assert.IsNotEmpty(pending, "kept in case the game doesn't come back");
            Assert.IsTrue(ReelPacket.TryDecode(pending, out Reel reel));
            Assert.IsTrue(reel.Abandoned);
            Assert.AreEqual(tick, reel.Run.DeathTick, "it ends where the game was put away");
            Assert.IsNull(OneLifeRules.Check(reel), "and keeps the rules, so the server will take it");
            Assert.IsNotEmpty(game.CurrentSave.OneLife.Token);
        }
    }
}
