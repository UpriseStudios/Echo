using System.Collections;
using System.Collections.Generic;
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
    /// Balance addendum in the built scene: Continue on any run, and at a full graveyard the banish button becomes
    /// Banish 10, which commits the run and leaves 20 ghosts.
    /// </summary>
    public class BanishTenFlowTests
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
        public IEnumerator AFullGraveyard_OffersContinueAndBanishTen_WhichLeavesTwenty()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            var ghosts = new List<GhostSaveData>();
            for (int i = 1; i <= 29; i++)
                ghosts.Add(new GhostSaveData { RunIndex = i, DeathTick = 120 + i, SpeedPct = 100, Kills = i % 4, LastKillRun = i, SkinId = "char_hooded" });
            _savePath = Path.Combine(Path.GetTempPath(), "echo_banish10_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Seed = 11,
                Best = 1_000_000,   // out of reach: every run is added as a ghost, and Continue has no score condition now
                NextRunIndex = 30,
                Ghosts = ghosts,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true },
            }));
            EchoGame.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;

            game.ScriptedTaps = t => false;
            game.InjectTap();
            for (int i = 0; i < 20000 && game.State != GameState.DeathScreen; i++)
            {
                game.TickFrame(Dt, false);
                if (i % 50 == 0) yield return null;
            }
            Assert.AreEqual(GameState.DeathScreen, game.State);

            Assert.IsTrue(Showing("DeathScreen/Continue"), "Continue on every run, however short");
            Assert.IsTrue(Showing("DeathScreen/Banish"));
            StringAssert.StartsWith("Banish 10 ghosts", Find("DeathScreen/Banish").GetComponentInChildren<TMPro.TMP_Text>().text);

            Find("DeathScreen/Banish").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.AreEqual(20, game.CurrentSave.Ghosts.Count, "committed to 30, then 10 banished");
            Assert.IsTrue(game.CurrentSave.BanishTenUsed);
            Assert.IsFalse(Showing("DeathScreen/Continue"), "the run is committed, so Continue has gone");
            Assert.AreEqual(GameState.DeathScreen, game.State, "the death screen stays; tap to retry");
        }
    }
}
