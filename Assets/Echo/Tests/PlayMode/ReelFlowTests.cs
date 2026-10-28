using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Sim;
using Echo.Game.Clips;
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
    /// The highlights reel (a user revision) in the built scene: runs saved from the death screen, a sixth replacing
    /// a chosen one, a reel watched back to the same death, and its clip made and shared as a GIF.
    /// </summary>
    public class ReelFlowTests
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

        static bool Showing(string path)
        {
            var go = Find(path);
            return go != null && go.activeInHierarchy;
        }

        static string Label(string path) => Find(path).GetComponentInChildren<TMPro.TMP_Text>().text;

        static IEnumerator Frames(EchoGame game, System.Func<bool> until, int max, string what)
        {
            for (int i = 0; i < max && !until(); i++)
            {
                game.TickFrame(Dt, false);
                if (i % 20 == 0) yield return null;
            }
            Assert.IsTrue(until(), "timed out waiting for " + what);
        }

        // A run with no taps, so it ends at the first hazard; then the death screen.
        static IEnumerator RunAndDie(EchoGame game, int taps)
        {
            game.ScriptedTaps = t => taps > 0 && t % 97 == 40;
            game.InjectTap();
            game.TickFrame(Dt, false);
            Assert.AreEqual(GameState.Running, game.State);
            yield return Frames(game, () => game.State == GameState.DeathScreen, 20000, "the death screen");
        }

        // Records the chosen stretch in game time, then waits for the file to be written.
        static IEnumerator MakeClip(EchoGame game, string button)
        {
            Click("ClipEditor/" + button);
            Assert.AreEqual(ClipStage.Recording, game.ClipStage);
            float until = Time.realtimeSinceStartup + 120f;
            while ((game.MakingClip || game.LastClipPath == null) && Time.realtimeSinceStartup < until)
            {
                game.TickFrame(Dt, false);
                yield return null;
            }
            Assert.IsNotNull(game.LastClipPath, "a clip was made");
            Assert.AreEqual(ClipStage.Editing, game.ClipStage, "back in the editor to make another");
        }

        [UnityTest]
        public IEnumerator Save_FillTheCupboard_ReplaceOne_Watch_AndClip()
        {
            var cfg = SimConfig.Default;
            var library = ChunkLibrary.FromJson(Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json")
                .Select(File.ReadAllText));
            _savePath = Path.Combine(Path.GetTempPath(), "echo_reels_" + System.Guid.NewGuid().ToString("N") + ".json");
            var save = new SaveData
            {
                ContentVersion = ContentVersion.Compute(library, cfg),
                Seed = 31,
                Best = 100000,  // never a new best, so every run leaves a ghost for the next
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true },
            };
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(save));
            EchoGame.SavePathOverride = _savePath;

            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            yield return Frames(game, () => game.State == GameState.Title, 600, "title");

            // Five runs saved, one after another.
            for (int k = 0; k < 5; k++)
            {
                yield return RunAndDie(game, k);
                Assert.AreEqual("Save reel", Label("DeathScreen/SaveReel"));
                Click("DeathScreen/SaveReel");
                Assert.AreEqual(k + 1, game.CurrentSave.Reels.Count);
                Assert.AreEqual("Saved", Label("DeathScreen/SaveReel"));
                Click("DeathScreen/Menu");
                Assert.AreEqual(GameState.Title, game.State);
            }

            // A sixth: the cupboard is full, so it asks which to replace; a tap behind it does not retry.
            yield return RunAndDie(game, 9);
            int sixth = game.Run.RunIndex;
            Click("DeathScreen/SaveReel");
            Assert.IsTrue(Showing("Reels/Slot2/Replace"));
            Assert.IsFalse(Showing("Reels/Slot2/Watch"));
            game.InjectTap();
            for (int i = 0; i < 30; i++) game.TickFrame(Dt, false);
            Assert.AreEqual(GameState.DeathScreen, game.State, "a tap behind the cupboard must not retry");
            Click("Reels/Slot2/Replace");
            Assert.IsFalse(Showing("Reels"));
            Assert.AreEqual(5, game.CurrentSave.Reels.Count);
            Assert.AreEqual(sixth, game.CurrentSave.Reels[2].RunIndex, "the sixth run took slot 3");
            Click("DeathScreen/Menu");

            // Watch: it plays to the same death and score as the run did.
            Click("Title/Reels");
            Assert.IsTrue(Showing("Reels/Slot2/Watch"));
            Click("Reels/Slot2/Watch");
            Assert.AreEqual(GameState.Replay, game.State);
            Assert.IsTrue(Showing("Replay/Exit"));
            Assert.IsFalse(Showing("Hud/PowerShield"), "no power-up buttons on a replay");
            ReelSaveData saved = game.CurrentSave.Reels[2];
            yield return Frames(game, () => game.ReplayEnded, 30000, "the replay to end");
            Assert.AreEqual(saved.Run.DeathTick, game.CurrentReplay.Run.DeathTick);
            Assert.AreEqual(saved.Score, game.CurrentReplay.Run.Score);
            Assert.IsTrue(Showing("Replay/Again"));

            // The clip editor: it opens on the final moments as a GIF, previews, and makes the GIF.
            Click("Replay/Clip");
            Assert.IsTrue(Showing("ClipEditor"));
            Assert.AreEqual(ClipStage.Editing, game.ClipStage);
            ClipRange range = game.ClipRange;
            Assert.AreEqual(ClipRange.Length(saved.Run.DeathTick), range.End, "it starts on the final moments");
            Assert.LessOrEqual(range.Ticks, ClipRange.GifDefaultTicks);

            Click("ClipEditor/Play");
            Assert.AreEqual(ClipStage.Previewing, game.ClipStage);
            yield return Frames(game, () => game.ClipStage == ClipStage.Editing, 2000, "the preview to finish");

            var share = (MockShareService)game.Services.Share;
            yield return MakeClip(game, "Make");
            byte[] gif = File.ReadAllBytes(game.LastClipPath);
            Assert.AreEqual("GIF89a", System.Text.Encoding.ASCII.GetString(gif, 0, 6));
            // A user revision: a made clip waits for Save to gallery or Share.
            Assert.IsTrue(Showing("ClipEditor/Ready/Save"));
            Click("ClipEditor/Ready/Share");
            Assert.AreEqual(game.LastClipPath, share.LastFile);
            Click("ClipEditor/Ready/Save");
            StringAssert.EndsWith(".gif", share.LastSaved);
            StringAssert.Contains("Saved to your gallery", Find("ClipEditor/Status").GetComponent<TMPro.TMP_Text>().text);
            share.SaveFails = true;
            Click("ClipEditor/Ready/Save");
            StringAssert.Contains("Use Share", Find("ClipEditor/Status").GetComponent<TMPro.TMP_Text>().text);
            share.SaveFails = false;
            StringAssert.Contains("echoghost://challenge/", share.LastText);
            Debug.Log($"[ReelFlowTests] GIF {gif.Length / 1024} KB");

            // A GIF stops at 10 s; a video can run to a minute. Then a 2 s video with sound.
            game.SetClipRange(0, ClipRange.Length(saved.Run.DeathTick) + 10000);
            Assert.LessOrEqual(game.ClipRange.Ticks, ClipRange.GifMaxTicks);
            Click("ClipEditor/Video");
            int end = ClipRange.Length(saved.Run.DeathTick);
            game.SetClipRange(end - 2 * ClipRange.TicksPerSecond, end);
            yield return MakeClip(game, "Make");
            StringAssert.EndsWith(".mp4", game.LastClipPath);
            byte[] mp4 = File.ReadAllBytes(game.LastClipPath);
            Assert.AreEqual("ftyp", System.Text.Encoding.ASCII.GetString(mp4, 4, 4), "an MP4 file");
            Click("ClipEditor/Ready/Share");
            Assert.AreEqual("video/mp4", share.LastMime);
            Debug.Log($"[ReelFlowTests] video {mp4.Length / 1024} KB");

            // Marketing content plan: the Vertical preset, x4 (1152 x 2048) at 60 fps, with a hook line and the end card.
            Click("ClipEditor/Vertical");
            Assert.IsTrue(Showing("ClipEditor/VerticalOptions"), "the hook and end card show for Vertical only");
            Find("ClipEditor/VerticalOptions/Hook").GetComponent<TMPro.TMP_InputField>().text = "every death makes it harder";
            yield return MakeClip(game, "Make");
            StringAssert.EndsWith("echo_clip_vertical.mp4", game.LastClipPath);
            Assert.AreEqual(VideoClipWriters.VerticalScale, game.LastClipScale, "the editor's encoder takes the full size");
            byte[] vertical = File.ReadAllBytes(game.LastClipPath);
            Assert.AreEqual("ftyp", System.Text.Encoding.ASCII.GetString(vertical, 4, 4), "an MP4 file");
            Debug.Log($"[ReelFlowTests] vertical {vertical.Length / 1024} KB for {game.ClipRange.Seconds:0.0} s plus the 1 s end card");
            Click("ClipEditor/Gif");
            Assert.IsFalse(Showing("ClipEditor/VerticalOptions"));

            Click("ClipEditor/Back");
            Assert.IsFalse(Showing("ClipEditor"));

            // Back to the cupboard; the next run is a normal one again.
            Assert.AreEqual(GameState.Menu, game.State);
            Assert.IsTrue(Showing("Reels"));
            Click("Reels/Close");
            Assert.AreEqual(GameState.Title, game.State);
            Assert.AreEqual(5, game.CurrentSave.Reels.Count);

            // Version 2.0: the development server check replays the newest reel (here through the mock backend,
            // which runs the module's code path locally) to the distance the run reached.
            game.OnServerCheck();
            Assert.IsNotNull(game.LastServerCheck);
            Assert.IsTrue(game.LastServerCheck.Accepted, game.LastServerCheck.Error);
            Assert.AreEqual(game.CurrentSave.Reels[4].Distance, game.LastServerCheck.Distance);
        }
    }
}
