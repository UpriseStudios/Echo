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
    /// <summary>
    /// Recording mode (marketing content plan): no ad buttons on the title or death screen, the HUD hidden when asked,
    /// and the run itself untouched.
    /// </summary>
    public class RecordingModeTests
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

        [UnityTest]
        public IEnumerator RecordingMode_HidesAdButtonsAndTheHud_AndLeavesTheRunAlone()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath = Path.Combine(Path.GetTempPath(), "echo_recording_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Seed = 11,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true, SeenHowToPlay = true },
            }));
            EchoGame.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
            Assert.IsTrue(Showing("Title/PowerUps"), "normally offered");

            game.SetRecordingMode(true, hideHud: true);
            Assert.IsFalse(Showing("Title/PowerUps"), "no ad buttons on the title");

            // A run with no taps: the HUD stays hidden, the run plays as ever, and the death screen offers no ads.
            game.ScriptedTaps = t => false;
            game.InjectTap();
            game.TickFrame(Dt, false);
            Assert.AreEqual(GameState.Running, game.State);
            Assert.IsFalse(Showing("Hud/Score"), "HUD hidden");
            for (int i = 0; i < 20000 && game.State != GameState.DeathScreen; i++)
            {
                game.TickFrame(Dt, false);
                if (i % 50 == 0) yield return null;
            }
            Assert.AreEqual(GameState.DeathScreen, game.State);
            for (int i = 0; i < 90; i++)
            {
                game.TickFrame(Dt, false);
                yield return null;
            }
            Assert.IsFalse(Showing("DeathScreen/Continue"), "no Continue (an ad) while recording");
            Assert.IsFalse(Showing("DeathScreen/Banish"));
            Assert.IsFalse(Showing("DeathScreen/PowerUps"));

            // Off again: the buttons come back.
            game.SetRecordingMode(false, hideHud: false);
            Assert.IsTrue(Showing("DeathScreen/PowerUps"));
        }
    }
}
