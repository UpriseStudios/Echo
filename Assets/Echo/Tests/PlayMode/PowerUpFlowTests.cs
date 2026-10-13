using System.Collections;
using System.IO;
using Echo.Core.Courses;
using Echo.Core.Rules;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using System.Linq;
using Object = UnityEngine.Object;

namespace Echo.Tests.PlayMode
{
    /// <summary>
    /// Power-ups (a user revision) in the built scene: earned with a (mock) rewarded ad on the title, capped at two,
    /// shown as floor buttons during a run, used with a tap that is not a jump, and taken from the save.
    /// </summary>
    public class PowerUpFlowTests
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

        static GameObject Find(string path) => GameObject.Find("Canvas/SafeArea/" + path);

        static void Click(string path)
        {
            var go = Find(path);
            Assert.IsNotNull(go, path);
            go.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        static bool Showing(string path)
        {
            var go = Find(path);
            return go != null && go.activeInHierarchy;
        }

        [UnityTest]
        public IEnumerator EarnOnTheTitle_UseOnTheFloor_OneOfEachPerRun()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath = Path.Combine(Path.GetTempPath(), "echo_powerups_" + System.Guid.NewGuid().ToString("N") + ".json");
            var save = new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Seed = 9,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true },
                PowerUps = new PowerUpsSaveData { ExtraLife = 1 },
            };
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(save));
            EchoGame.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            yield return WaitFor(() => game.State == GameState.Title, 3f, "title");

            // Earn: the title button opens the card; two ads fill the Shield, a third is refused.
            Click("Title/PowerUps");
            Assert.IsTrue(Showing("PowerUps"));
            game.InjectTap();
            yield return null;
            Assert.AreEqual(GameState.Title, game.State, "a tap behind the card must not start a run");
            Click("PowerUps/Watch0");
            Click("PowerUps/Watch0");
            Click("PowerUps/Watch0");
            Click("PowerUps/Watch1");
            Assert.AreEqual(2, SaveCounts().Shield, "held up to two");
            Assert.AreEqual(1, SaveCounts().Phase);
            Click("PowerUps/Close");
            Assert.IsFalse(Showing("PowerUps"));

            // Use: the run starts with the floor buttons for what is held (no Lantern held, so no Lantern button).
            game.InjectTap();
            yield return WaitFor(() => game.State == GameState.Running, 2f, "the run");
            Assert.IsTrue(game.Run.ExtraLifeHeld, "a held Extra 1-UP comes along automatically");
            Assert.IsTrue(Showing("Hud/PowerShield"));
            Assert.IsTrue(Showing("Hud/PowerPhase"));
            Assert.IsFalse(Showing("Hud/PowerLantern"));

            int tick = game.Run.Tick;
            Click("Hud/PowerShield");
            Assert.IsTrue(game.Run.ShieldArmed);
            Assert.IsFalse(Showing("Hud/PowerShield"), "one of each per run");
            Assert.AreEqual(1, SaveCounts().Shield, "taken from the held ones");
            Assert.AreEqual(tick, game.Run.Tick);
            Assert.IsTrue(game.Run.Player.Grounded, "tapping a power-up is not a jump");

            Click("Hud/PowerPhase");
            Assert.IsTrue(game.Run.PhaseActive);
            Assert.AreEqual(0, SaveCounts().Phase);

            PowerUpsSaveData SaveCounts() => game.CurrentSave.PowerUps;
        }
    }
}
