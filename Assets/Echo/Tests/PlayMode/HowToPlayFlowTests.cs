using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Sim;
using Echo.Game.Flow;
using Echo.Game.Persistence;
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
    /// How to play (a user revision): on first launch the clips start by themselves, captions come and go, a tap moves to
    /// the next clip, Skip leaves for good, and Settings › How to play shows them again and returns to Settings.
    /// </summary>
    public class HowToPlayFlowTests
    {
        const float Dt = 1f / 60f;
        string _savePath;

        [TearDown]
        public void TearDown()
        {
            EchoGame.AutoHowToPlay = false;
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

        static IEnumerator UntilClips(EchoGame game)
        {
            for (int i = 0; i < 600 && game.HowToClipIndex < 0; i++)
            {
                game.TickFrame(Dt, false);
                yield return null;
            }
            Assert.GreaterOrEqual(game.HowToClipIndex, 0, "the clips were built");
        }

        [UnityTest]
        public IEnumerator FirstLaunch_PlaysTheClips_TapMovesOn_SkipLeaves_SettingsShowsThemAgain()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath = Path.Combine(Path.GetTempPath(), "echo_howto_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Best = 321,
                Seed = 9,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
            }));
            EchoGame.SavePathOverride = _savePath;
            EchoGame.AutoHowToPlay = true;

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            for (int i = 0; i < 300 && !game.HowToPlaying; i++) yield return null;
            Assert.IsTrue(game.HowToPlaying, "first launch: the clips start by themselves");
            yield return UntilClips(game);
            Assert.AreEqual(9, game.HowToClips.Count);
            Assert.AreEqual(GameState.Replay, game.State);
            StringAssert.StartsWith("1/4", Find("HowToPlay/Topic").GetComponent<TMP_Text>().text);

            // The first clip plays its captions, then a tap moves on to the next.
            bool captioned = false;
            for (int i = 0; i < 400 && game.HowToClipIndex == 0; i++)
            {
                game.TickFrame(Dt, false);
                captioned |= !string.IsNullOrEmpty(game.HowToCaption);
                if (i % 40 == 0) yield return null;
            }
            Assert.IsTrue(captioned, "captions showed over the first clip");
            int at = game.HowToClipIndex;
            game.TickFrame(Dt, true);
            Assert.AreEqual(at + 1, game.HowToClipIndex, "a tap goes to the next clip");

            // Skip: back on the title, seen for good, and the save's best untouched.
            Click("HowToPlay/Skip");
            yield return null;
            Assert.IsFalse(game.HowToPlaying);
            Assert.AreEqual(GameState.Title, game.State);
            Assert.IsTrue(game.CurrentSave.Tutorial.SeenHowToPlay);
            Assert.AreEqual(321, game.Progress.Best);

            // Settings › How to play: the clips again, and back to Settings at the end.
            Click("Title/Settings");
            Click("Settings/HowToPlay");
            yield return UntilClips(game);
            for (int i = 0; i < 20 && game.HowToPlaying; i++) game.TickFrame(Dt, true);
            yield return null;
            Assert.IsFalse(game.HowToPlaying, "tapped through every clip");
            Assert.IsTrue(Showing("Settings/Close"), "back in Settings");
        }
    }
}
