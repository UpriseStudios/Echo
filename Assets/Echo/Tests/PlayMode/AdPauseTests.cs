using System.Collections;
using System.IO;
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
    /// Device testing: a real rewarded ad takes the app into the background, and the pause committed the pending run,
    /// so Continue played its ad and then did nothing. The mock holds its ad open to reproduce that.
    /// </summary>
    public class AdPauseTests
    {
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
        public IEnumerator TheAdsOwnPause_DoesNotCommitTheRun_SoContinueContinues()
        {
            _savePath = Path.Combine(Path.GetTempPath(), "echo_adpause_" + System.Guid.NewGuid().ToString("N") + ".json");
            var save = new SaveData
            {
                Best = 1, // low, so even a short run can be continued
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true },
            };
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(save));
            EchoGame.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            yield return WaitFor(() => game.State == GameState.Title, 3f, "title");
            var ads = (MockAdsService)game.Services.Ads;
            ads.HoldUntilCompleted = true;

            game.InjectTap();
            yield return WaitFor(() => game.State == GameState.DeathScreen, 10f, "the first death");
            var cont = GameObject.Find("Canvas/SafeArea/DeathScreen/Continue");
            Assert.IsNotNull(cont, "Continue is offered");
            cont.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

            // The ad is on screen: the app goes to the background and comes back.
            game.SendMessage("OnApplicationPause", true);
            game.SendMessage("OnApplicationPause", false);
            Assert.IsFalse(game.Run.Committed, "the ad's pause must not commit the run");
            ads.CompleteShowing(true);
            Assert.AreEqual(GameState.Running, game.State, "the run continues after the ad");
            Assert.IsTrue(game.Run.ContinueUsed);

            // Leaving the app for real from a death screen still commits the run, as before.
            yield return WaitFor(() => game.State == GameState.DeathScreen, 15f, "the second death");
            game.SendMessage("OnApplicationPause", true);
            Assert.IsTrue(game.Run.Committed);
        }
    }
}
