using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Challenges;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
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
    /// Ghost Challenges (value-adds doc) end to end in the built scene: a link opens a challenge card, the race is
    /// played on the sender's course against their ghost alone, the outcome is shown, nothing of the player's own
    /// progress changes, and "Send one back" asks for a name once and shares a working link.
    /// </summary>
    public class ChallengeFlowTests
    {
        const uint OwnSeed = 777;
        const uint TheirSeed = 4242;
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

        static void Click(string path)
        {
            var go = GameObject.Find("Canvas/SafeArea/" + path);
            Assert.IsNotNull(go, path);
            go.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        // A path lookup finds inactive objects too, so check it is actually showing.
        static bool CardOpen
        {
            get
            {
                var card = GameObject.Find("Canvas/SafeArea/Challenge");
                return card != null && card.activeInHierarchy;
            }
        }

        static string CardTitle => GameObject.Find("Canvas/SafeArea/Challenge/Title").GetComponent<TMP_Text>().text;

        [UnityTest]
        public IEnumerator ALink_OpensARace_OnTheirCourse_AndSendingOneBack_AsksForANameOnce()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            string version = ContentVersion.Compute(library, cfg);

            _savePath = Path.Combine(Path.GetTempPath(), "echo_challenge_" + System.Guid.NewGuid().ToString("N") + ".json");
            var save = new SaveData
            {
                ContentVersion = version,
                Best = 500,
                Seed = OwnSeed,
                NextRunIndex = 3,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true },
            };
            save.Ghosts.Add(new GhostSaveData { RunIndex = 1, DeathTick = 120, SpeedPct = 100 });
            save.Ghosts.Add(new GhostSaveData { RunIndex = 2, DeathTick = 150, SpeedPct = 100 });
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(save));
            EchoGame.SavePathOverride = _savePath;

            // Their run: it walked into the first tutorial spike, a few metres in.
            var theirs = new Challenge
            {
                Name = "Jamie",
                Seed = TheirSeed,
                ContentVersion = version,
                SpeedPct = 100,
                Distance = 9,
                Record = new GhostRecord { RunIndex = 4, DeathTick = 80, SkinId = "char_wizard" },
            };

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            yield return WaitFor(() => game.State == GameState.Title, 3f, "title");

            // An older version's challenge is refused politely.
            var old = new Challenge { Name = "Old", Seed = 1, ContentVersion = "00000000", Distance = 5 };
            game.OpenChallengeLink("look: " + ChallengeCode.Link(old));
            Assert.IsTrue(CardOpen);
            Assert.AreEqual("OLDER CHALLENGE", CardTitle);
            Click("Challenge/Secondary");
            Assert.IsFalse(CardOpen);

            // A real one: the card, then the race.
            game.OpenChallengeLink(ChallengeCode.Link(theirs));
            Assert.AreEqual("GHOST CHALLENGE", CardTitle);
            game.InjectTap();
            yield return null;
            Assert.AreEqual(GameState.Title, game.State, "a tap behind the card must not start a run");
            Click("Challenge/Primary");
            Assert.AreEqual(GameState.Running, game.State);
            Assert.IsTrue(game.Run.Options.Challenge);
            Assert.AreEqual(TheirSeed, game.Run.Course.Seed, "their course");
            Assert.AreEqual(1, game.Run.Ghosts.Count, "their ghost alone");

            yield return WaitFor(() => game.State == GameState.DeathScreen, 10f, "the challenge to end");
            string message = GameObject.Find("Canvas/SafeArea/DeathScreen/Message").GetComponent<TMP_Text>().text;
            StringAssert.IsMatch("Jamie|outlasted|level", message);
            Assert.AreEqual("Send one back", GameObject.Find("Canvas/SafeArea/DeathScreen/Challenge").GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual(500, game.Progress.Best, "challenges never touch the best");
            Assert.AreEqual(2, game.Progress.Ghosts.Count, "nor the ghosts");

            // Send one back: no name yet, so it asks, then shares a link to the same course.
            Click("DeathScreen/Challenge");
            Assert.AreEqual("YOUR NAME", CardTitle);
            GameObject.Find("Canvas/SafeArea/Challenge/Name").GetComponent<TMP_InputField>().text = "Tester";
            Click("Challenge/Primary");
            var share = (MockShareService)game.Services.Share;
            StringAssert.Contains("Tester's Ghost", share.LastText);
            Assert.IsTrue(ChallengeCode.TryParse(share.LastText, out Challenge back));
            Assert.AreEqual(TheirSeed, back.Seed, "the reply races them on their own course");
            Assert.AreEqual("Tester", back.Name);

            // Tap to retry races the same challenge again; Menu goes back to the player's own course.
            yield return new WaitForSecondsRealtime(0.4f); // past the death screen's tap guard
            game.InjectTap();
            yield return WaitFor(() => game.State == GameState.Running, 2f, "the rematch");
            Assert.IsTrue(game.Run.Options.Challenge);
            yield return WaitFor(() => game.State == GameState.DeathScreen, 10f, "the rematch to end");
            Click("DeathScreen/Menu");
            Assert.AreEqual(GameState.Title, game.State);
            Assert.IsFalse(game.Run.Options.Challenge);
            Assert.AreEqual(OwnSeed, game.Run.Course.Seed);
        }
    }
}
