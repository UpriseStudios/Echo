using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Modes;
using Echo.Core.Rules;
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
    /// Ranked Ghost Duels (modes doc) in the built scene, through the mock backend: the lobby (rating, tier, duels left),
    /// a ranked duel asked first then shown by name and tier, the rival racing beside you without hurting you, the result
    /// and back to the lobby; practice moves no rating; the "while you were away" note shows once.
    /// </summary>
    public class RankedFlowTests
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

        static string Card() => string.Join("\n", Find("Challenge").GetComponentsInChildren<TMP_Text>().Select(t => t.text));

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        IEnumerator Load()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath = Path.Combine(Path.GetTempPath(), "echo_ranked_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Best = 777,
                Seed = 31,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true, SeenHowToPlay = true },
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
            backend.RankedOpen = true;
            backend.Modes.Add(new ModeState { Id = ModeIds.Ranked, Open = true, SecondsLeft = 400000 });
            backend.DuelRival = new GhostRecord { RunIndex = 1, DeathTick = 900, TapTicks = new int[0], SpeedPct = 100, SkinId = "char_knight" };
            return backend;
        }

        IEnumerator ToTheLobby()
        {
            Click("Title/Modes");
            yield return Frames(2);
            StringAssert.StartsWith("Ranked Ghost Duels", Text("Modes/Card2"));
            Click("Modes/Card2");
            yield return Frames(3);
            Assert.AreEqual("Ranked Ghost Duels", Find("DailyLobby/Heading").GetComponent<TMP_Text>().text);
        }

        IEnumerator RunToDeath(EchoGame game)
        {
            float deadline = Time.realtimeSinceStartup + 60f;
            for (int i = 0; game.State != GameState.DeathScreen && Time.realtimeSinceStartup < deadline; i++)
            {
                game.TickFrame(Dt, false);
                if (game.Run.IsDead || i % 50 == 0) yield return null;
            }
            Assert.AreEqual(GameState.DeathScreen, game.State, "the run ended");
            yield return Frames(3);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator ARankedDuel_FromTheLobby_ToTheResult()
        {
            yield return Load();
            var game = Object.FindAnyObjectByType<EchoGame>();
            MockBackendService backend = Open(game);
            backend.AwayWins = 3;
            backend.AwayChange = 24;
            game.ScriptedTaps = t => false;
            yield return ToTheLobby();
            yield return Frames(2);

            // The note from while you were away, in the lobby (no card to dismiss).
            Assert.IsFalse(Showing("Challenge"), "nothing to dismiss");
            StringAssert.Contains("beat 3 challengers", Find("DailyLobby/Yesterday").GetComponent<TMP_Text>().text);
            StringAssert.Contains("Rating 1,000 · SILVER", Find("DailyLobby/You").GetComponent<TMP_Text>().text);
            StringAssert.Contains("10 ranked duels left today", Find("DailyLobby/Status").GetComponent<TMP_Text>().text);
            StringAssert.Contains("Practice", Text("DailyLobby/BoardToggle"));

            // A user revision: one press starts a ranked duel (the lobby says it counts), and the opponent is named as it
            // starts, by name and tier, never their distance.
            StringAssert.Contains("counts once it starts", Find("DailyLobby/Status").GetComponent<TMP_Text>().text);
            Click("DailyLobby/Play");
            yield return Frames(3);
            StringAssert.Contains("RivalGhost#2024", game.DuelIntro);
            StringAssert.Contains("SILVER", game.DuelIntro);
            StringAssert.DoesNotContain(" m", game.DuelIntro, "never their distance");

            Assert.AreEqual(GameState.Running, game.State);
            Assert.IsTrue(game.Run.Options.Ranked);
            Assert.AreEqual(1, game.Run.GhostsAtStart, "the rival alone");
            Assert.AreEqual("char_knight", game.Run.StartGhosts[0].SkinId, "in their skin");
            yield return RunToDeath(game);
            Assert.AreNotEqual(DeathCause.Ghost, game.Run.Cause, "the rival can't kill");

            Assert.IsNotNull(game.LastDuelResult);
            Assert.IsTrue(game.LastDuelResult.Ok, game.LastDuelResult.Error);
            string banner = game.LastDuelResult.Outcome == "won" ? "VICTORY" : game.LastDuelResult.Outcome == "draw" ? "DRAW" : "DEFEAT";
            StringAssert.Contains("Rating 1,000", Find("DeathScreen/Message").GetComponent<TMP_Text>().text);
            Assert.AreNotEqual(1000, backend.Rating, $"the rating moved ({banner})");
            Assert.AreEqual("", game.CurrentSave.Ranked.Pending, "nothing left waiting");

            // Tap: back to the lobby, where each duel starts.
            yield return new WaitForSecondsRealtime(1f);
            game.InjectTap();
            game.TickFrame(Dt, false);
            yield return Frames(4);
            Assert.AreNotEqual(GameState.Running, game.State);
            Assert.AreEqual("Ranked Ghost Duels", Find("DailyLobby/Heading").GetComponent<TMP_Text>().text);
            StringAssert.DoesNotContain("while you were away", Find("DailyLobby/Yesterday").GetComponent<TMP_Text>().text.ToLowerInvariant(), "the away note showed once");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Practice_MovesNoRating()
        {
            yield return Load();
            var game = Object.FindAnyObjectByType<EchoGame>();
            MockBackendService backend = Open(game);
            game.ScriptedTaps = t => false;
            yield return ToTheLobby();
            Click("DailyLobby/BoardToggle");
            yield return Frames(3);
            StringAssert.Contains("PRACTICE", game.DuelIntro);
            Assert.IsTrue(game.Run.Options.Ranked);
            yield return RunToDeath(game);
            Assert.IsFalse(game.LastDuelResult.Ranked);
            Assert.AreEqual(1000, backend.Rating);
            Assert.AreEqual(0, backend.RankedToday, "practice doesn't use a ranked duel");
            StringAssert.Contains("no rating change", Find("DeathScreen/Message").GetComponent<TMP_Text>().text);
        }
    }
}
