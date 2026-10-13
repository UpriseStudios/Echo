using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Challenges;
using Echo.Core.Courses;
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
    /// Fair challenges (balance addendum, version 2.0) in the built scene, through the mock backend: a run is sent
    /// with its ghosts as a server link, the link opens the same haunted course, attempts are counted, and with
    /// server challenges switched off the game falls back to a Lite link.
    /// </summary>
    public class FairChallengeFlowTests
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

        static bool CardOpen => Find("Challenge") != null && Find("Challenge").activeInHierarchy;
        static string CardText(string part) => Find("Challenge/" + part).GetComponent<TMP_Text>().text;

        static IEnumerator ToDeathScreen(EchoGame game)
        {
            for (int i = 0; i < 20000 && game.State != GameState.DeathScreen; i++)
            {
                game.TickFrame(Dt, false);
                if (i % 50 == 0) yield return null;
            }
            Assert.AreEqual(GameState.DeathScreen, game.State);
        }

        [UnityTest]
        public IEnumerator ARunIsSentWithItsGhosts_AndRacedOnTheSameHauntedCourse()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath = Path.Combine(Path.GetTempPath(), "echo_fair_" + System.Guid.NewGuid().ToString("N") + ".json");
            var save = new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Best = 100_000,
                Seed = 4242,
                NextRunIndex = 4,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true },
                Settings = new SettingsSaveData { PlayerName = "Jamie" },
            };
            for (int i = 1; i <= 3; i++)
                save.Ghosts.Add(new GhostSaveData { RunIndex = i, DeathTick = 100 + 40 * i, SpeedPct = 100, Kills = i == 2 ? 3 : 0, LastKillRun = i });
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(save));
            EchoGame.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
            var backend = (MockBackendService)game.Services.Backend;
            backend.Flags = new BackendFlags(false, true);

            // Play and die, then send the run: it goes to the server with its three ghosts.
            game.ScriptedTaps = t => false;
            game.InjectTap();
            game.TickFrame(Dt, false);
            yield return ToDeathScreen(game);
            int sentScore = game.Run.Score;
            Click("DeathScreen/Challenge");
            StringAssert.StartsWith(ChallengeLinks.WebPrefix + MockBackendService.PlayerId + "/", game.LastChallengeLink);
            var share = (MockShareService)game.Services.Share;
            StringAssert.Contains(game.LastChallengeLink, share.LastText);
            StringAssert.Contains("past 4 ghosts", share.LastText, "three ghosts and the run itself");
            Click("DeathScreen/Menu");
            Assert.AreEqual(GameState.Title, game.State);
            int ghostsBefore = game.CurrentSave.Ghosts.Count;

            // The link opens the same haunted course: their run, their ghosts, raced on score.
            game.OpenChallengeLink("race me " + game.LastChallengeLink);
            yield return null;
            Assert.IsTrue(CardOpen);
            Assert.AreEqual("GHOST CHALLENGE", CardText("Title"));
            StringAssert.Contains($"{sentScore:N0}", CardText("Body"));
            Click("Challenge/Primary");
            Assert.AreEqual(GameState.Running, game.State);
            Assert.IsTrue(game.Run.Options.Challenge);
            Assert.AreEqual(4242u, game.Run.Course.Seed);
            Assert.AreEqual(4, game.Run.GhostsAtStart, "their three ghosts and their run");
            yield return ToDeathScreen(game);
            StringAssert.Contains("attempt 1", Find("DeathScreen/Message").GetComponent<TMP_Text>().text);
            Assert.IsFalse(Find("DeathScreen/Continue").activeInHierarchy, "no Continue in a challenge");
            Assert.AreEqual(ghostsBefore, game.CurrentSave.Ghosts.Count, "a friend's attempts never add ghosts");

            // Tap to retry: the same challenge, attempt 2.
            yield return new WaitForSecondsRealtime(0.4f);
            game.InjectTap();
            game.TickFrame(Dt, false);
            Assert.AreEqual(GameState.Running, game.State);
            yield return ToDeathScreen(game);
            StringAssert.Contains("attempt 2", Find("DeathScreen/Message").GetComponent<TMP_Text>().text);
            Click("DeathScreen/Menu");

            // Server challenges switched off: sending falls back to a Lite link.
            backend.Flags = new BackendFlags(false, false);
            game.ScriptedTaps = t => false;
            game.InjectTap();
            game.TickFrame(Dt, false);
            yield return ToDeathScreen(game);
            Click("DeathScreen/Challenge");
            StringAssert.StartsWith(ChallengeCode.LinkPrefix, game.LastChallengeLink);

            // An unknown server link says so.
            Click("DeathScreen/Menu");
            game.OpenChallengeLink(ChallengeLinks.Link("someone", "nothere"));
            yield return null;
            Assert.IsTrue(CardOpen);
            Assert.AreEqual("CHALLENGES ARE RESTING", CardText("Title"), "switched off, so not even fetched");
            Click("Challenge/Primary");
            backend.Flags = new BackendFlags(false, true);
            game.OpenChallengeLink(ChallengeLinks.Link("someone", "nothere"));
            yield return null;
            Assert.AreEqual("CHALLENGE NOT AVAILABLE", CardText("Title"));
        }
    }
}
