using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Ghosts;
using Echo.Game.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echo.Tests.PlayMode
{
    /// <summary>Boots the built scene and plays the die-restart-ghost loop with injected taps.</summary>
    public class GreyBoxLoopTests
    {
        string _savePath;

        [SetUp]
        public void SetUp()
        {
            _savePath = Path.Combine(Path.GetTempPath(), "echo_playmode_" + System.Guid.NewGuid().ToString("N") + ".json");
            EchoGame.SavePathOverride = _savePath;
        }

        [TearDown]
        public void TearDown()
        {
            EchoGame.SavePathOverride = null;
            foreach (string f in new[] { _savePath, _savePath + ".tmp", _savePath + ".bak" })
                if (File.Exists(f)) File.Delete(f);
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
        public IEnumerator Boot_Die_Restart_AndTheLastRunReplaysAsAGhost()
        {
            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            Assert.IsNotNull(game, "EchoGame missing; run Echo/Build Scene");
            yield return AnswerConsent(game);
            yield return WaitFor(() => game.State == GameState.Title, 3f, "Title");

            // Run 1: no taps, dies on the first tutorial spike; any score is a new best, so no ghost yet.
            game.InjectTap();
            yield return WaitFor(() => game.State == GameState.DeathScreen, 5f, "first death");
            int firstScore = game.Run.Score;
            yield return new WaitForSecondsRealtime(0.3f);

            // Run 2: same inputs, same death, equal score: not a new best, so it becomes a ghost.
            float restartAt = Time.realtimeSinceStartup;
            game.InjectTap();
            yield return WaitFor(() => game.State == GameState.Running, 1f, "instant restart");
            Assert.Less(Time.realtimeSinceStartup - restartAt, 1f, "death to running in under 1 s");
            yield return WaitFor(() => game.State == GameState.DeathScreen, 5f, "second death");
            Assert.AreEqual(firstScore, game.Run.Score);
            yield return new WaitForSecondsRealtime(0.3f);

            // Run 3 starts with that ghost. Run 2 fell at the first spike, before any boundary it could be met at,
            // so it comes back as a harmless echo that falls onto its gravestone as the player gets there.
            game.InjectTap();
            yield return WaitFor(() => game.State == GameState.Running, 1f, "third run");
            Assert.AreEqual(1, game.Run.GhostsAtStart);
            Assert.AreEqual(1, game.Progress.Ghosts.Count);
            yield return WaitFor(() => game.Run.Tick > 20, 2f, "ticks");
            Assert.IsTrue(game.Run.Ghosts.ReplayAt(0).Harmless, "fell before any meeting point, so it is a harmless echo");
            yield return WaitFor(() => game.Run.Ghosts.ReplayAt(0).Phase != GhostPhase.Replaying, 3f, "the echo falls on its grave");

            Assert.IsTrue(File.Exists(_savePath), "commit saved the game");
        }

        /// <summary>A fresh save shows the consent screen first (tech spec 19); answer it like a player would.</summary>
        static IEnumerator AnswerConsent(EchoGame game)
        {
            yield return WaitFor(() => game.State == GameState.Consent, 3f, "consent screen");
            Click("Canvas/SafeArea/Consent/EssentialOnly");
            Assert.IsTrue(game.Services.UsingMockAds, "the editor keeps mock ads");
        }

        static void Click(string path)
        {
            var go = GameObject.Find(path);
            Assert.IsNotNull(go, path);
            go.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator TitleMenus_OpenClose_AndSwallowTaps()
        {
            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            yield return AnswerConsent(game);
            yield return WaitFor(() => game.State == GameState.Title, 3f, "Title");

            foreach (string screen in new[] { "Skins", "Stats", "Settings" })
            {
                Click($"Canvas/SafeArea/Title/{screen}");
                Assert.AreEqual(GameState.Menu, game.State, screen);
                yield return null;
                game.InjectTap();
                yield return null;
                Assert.AreEqual(GameState.Menu, game.State, $"a tap on {screen} must not start a run");

                if (screen == "Skins")
                {
                    var content = GameObject.Find("Canvas/SafeArea/Skins/Scroll/Viewport/Content").transform;
                    var buttons = content.GetComponentsInChildren<UnityEngine.UI.Button>();
                    int characters = buttons.Count(b => b.transform.Find("Preview") != null);
                    // Seasons doc: Santa and Rudolph always show (to earn); the Christmas Pack only while on sale (December), so not here.
                    Assert.AreEqual(15 + 32 + 4 + 2, characters, "hooded + 8 base, Pumpkin, 5 earned, 8 packs of 4, the Elementals, Santa and Rudolph");
                    Assert.AreEqual(53 + 1 + 8 + 1 + 1 + 21 + 3, buttons.Length,
                        "characters, the Season bundle, 8 skin packs, the Elementals trial, power-ups, 6 colours + 9 trails + 4 palettes + 2 gravestones, 3 colour packs");
                }

                Click($"Canvas/SafeArea/{screen}/Close");
                Assert.AreEqual(GameState.Title, game.State);
                yield return null;
            }
        }
    }
}
