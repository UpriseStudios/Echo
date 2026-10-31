using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Echo.Game.Seasons;
using Echo.Game.Views;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Echo.Tests.PlayMode
{
    /// <summary>
    /// Easter and April Fools (seasons doc) in the built scene: Easter hides two eggs in a main-game run that a hop
    /// collects without changing the run; April Fools dresses everyone as the Banana while runs still record the player's
    /// own skin.
    /// </summary>
    public class SeasonEventsFlowTests
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

        IEnumerator Load()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath = Path.Combine(Path.GetTempPath(), "echo_events_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Best = 50,
                Seed = 31,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true, SeenHowToPlay = true },
            }));
            EchoGame.SavePathOverride = _savePath;
            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Easter_HidesEggsInAMainRun_ThatAHopReaches()
        {
            yield return Load();
            var game = Object.FindAnyObjectByType<EchoGame>();
            game.ForceSeason(SeasonIds.Easter);
            game.ScriptedTaps = t => false;
            game.InjectTap();
            game.TickFrame(Dt, false);
            yield return null;
            Assert.AreEqual(GameState.Running, game.State);
            Vector2 first = game.EggHunt.EggAt(0), second = game.EggHunt.EggAt(1);
            Assert.Greater(first.x, 0f, "two eggs are hidden");
            Assert.Greater(second.x, first.x);
            Assert.Greater(first.y, 0.8f, "above the standing runner (0.8 tall), so it takes a hop");
            Assert.Less(first.y, 1.6f, "and within a hop");
            game.ForceSeason(null);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AprilFools_EveryoneIsABanana_ButRunsKeepTheirSkin()
        {
            yield return Load();
            var game = Object.FindAnyObjectByType<EchoGame>();
            game.ForceSeason(SeasonIds.AprilFools);
            Assert.AreEqual("char_banana", game.ActiveSeason.ForceCharacter);
            game.ScriptedTaps = t => false;
            game.InjectTap();
            game.TickFrame(Dt, false);
            yield return null;
            Assert.AreEqual(GameState.Running, game.State);
            Assert.AreEqual(game.CurrentSave.Cosmetics.Equipped.Character, game.Run.SkinId, "the run records the player's own skin");
            var runner = GameObject.Find("World/Player").GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "Runner");
            StringAssert.Contains("banana", runner.sprite.name, "but looks like the Banana today");
            game.ForceSeason(null);
        }
    }
}
