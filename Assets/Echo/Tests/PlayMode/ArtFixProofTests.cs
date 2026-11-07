using System.Collections;
using System.IO;
using System.Linq;
using Echo.Core.Courses;
using Echo.Core.Sim;
using Echo.Game.Clips;
using Echo.Game.Dev;
using Echo.Game.Flow;
using Echo.Game.Persistence;
using Echo.Game.Views;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Echo.Tests.PlayMode
{
    /// <summary>
    /// Time Run art fix, phase 4 (proof): a recording of a real run, played with ordinary jumps and wearing Time Run,
    /// 5 seconds around each of the later boundaries: Today into the Far Future (900 m) and the Far Future into the Far Far
    /// Future (1200 m), 10 seconds in all, to TestResults/proof_crossing.mp4 (1080 x 1920, 30 fps, the world as the game
    /// draws it with full effects; time runs at the video's rate, so rain, traffic and lights move at their real speed).
    /// A strip of frames goes beside it for reading without a player. The five eras side by side at one point of one run
    /// are SeasonScreenshotTests' proof_five_eras.
    /// </summary>
    public class ArtFixProofTests
    {
        const float Dt = 1f / 60f;
        const uint Seed = 41;
        const int Width = 540, Height = 960, Fps = 30, SecondsEach = 5;
        string _savePath;

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            EchoGame.SavePathOverride = null;
            foreach (string f in new[] { _savePath, _savePath + ".tmp", _savePath + ".bak", _savePath + ".seasons.json" })
                if (f != null && File.Exists(f)) File.Delete(f);
        }

        static ChunkLibrary Library() => ChunkLibrary.FromJson(
            Directory.GetFiles(Path.Combine(Application.dataPath, "Echo", "Content", "Chunks"), "*.json").Select(File.ReadAllText));

        [UnityTest, Timeout(900000)]
        public IEnumerator ARecording_AcrossTodayToTheFarFuture_AndOnToTheFarFarFuture()
        {
            var cfg = SimConfig.Default;
            _savePath = Path.Combine(Path.GetTempPath(), "echo_proof_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_savePath, JsonConvert.SerializeObject(new SaveData
            {
                ContentVersion = ContentVersion.Compute(Library(), cfg),
                Seed = Seed,
                Best = 100000,
                Consent = new ConsentSaveData { Version = ConsentSaveData.CurrentVersion },
                Tutorial = new TutorialSaveData { SeenTapHint = true, SeenGhostHint = true, SeenHopHint = true, UsedDoubleJump = true, SeenHowToPlay = true },
            }));
            EchoGame.SavePathOverride = _savePath;
            yield return SceneManager.LoadSceneAsync("Echo");
            var game = Object.FindAnyObjectByType<EchoGame>();
            while (game.State != GameState.Title) yield return null;

            // A run past 1200 m with ordinary jumps, wearing Time Run (looks only).
            int speed = cfg.SpeedForPct(game.Run.SpeedPct);
            Schedule run = SurvivingSchedule.Build(new Course(Library(), game.Run.Course.Seed), speed, 16000, new Random(7), cfg);
            game.PreviewTimeRun = true;
            game.WeakDeviceOverride = false;
            game.enabled = false;
            game.ScriptedTaps = run.TapAt;
            game.ScriptedHeld = run.HeldAt;
            game.TickFrame(Dt, tapped: true);

            // Metres a tick, to start each stretch half its length before the boundary.
            int t0 = 0, d0 = 0;
            while (game.State == GameState.Running && game.Run.Distance < 700)
            {
                game.TickFrame(Dt, false);
                if (game.Run.Distance == 600 && t0 == 0) { t0 = game.Run.Tick; d0 = game.Run.Distance; }
                if (game.Run.Tick % 120 == 0) yield return null;
            }
            float perTick = (game.Run.Distance - d0) / (float)(game.Run.Tick - t0);
            int lead = Mathf.RoundToInt(perTick * SecondsEach * 60 / 2f);

            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "TestResults", "proof_crossing.mp4");
            IVideoClipWriter writer = VideoClipWriters.Create();
            Assert.IsTrue(writer.Begin(path, Width, Height, 2, Fps, 0, 0));
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.DefaultHDR);
            var read = new Texture2D(Width, Height, TextureFormat.RGBAHalf, false, true);
            var rgba = new byte[Width * Height * 4];
            var strip = new Texture2D(6 * (Width / 2 + 6) - 6, Height / 2, TextureFormat.RGB24, false);
            strip.SetPixels32(Enumerable.Repeat(new Color32(12, 12, 18, 255), strip.width * strip.height).ToArray());
            int stripped = 0, ticksPerFrame = 60 / Fps;
            var cam = Camera.main;
            var rig = Object.FindAnyObjectByType<CameraRig>();
            int[] erasSeen = { -1, -1 };

            for (int b = 0; b < 2; b++)
            {
                int boundary = b == 0 ? 900 : 1200;
                while (game.State == GameState.Running && game.Run.Distance < boundary - lead)
                {
                    game.TickFrame(Dt, false);
                    if (game.Run.Tick % 120 == 0) yield return null;
                }
                Assert.AreEqual(GameState.Running, game.State, $"the run reaches {boundary - lead} m");
                int before = game.EraShown;
                Time.captureFramerate = Fps;   // Time.time moves on 1/30 s a frame: animations at their real speed
                for (int f = 0; f < SecondsEach * Fps; f++)
                {
                    for (int k = 0; k < ticksPerFrame; k++) game.TickFrame(Dt, false);
                    yield return null;
                    Frame(cam, rig, rt, read, rgba);
                    writer.AddFrame(rgba);
                    // Three frames of each stretch for the strip: its start, just after the boundary, its end.
                    if (f == 0 || f == SecondsEach * Fps / 2 + 8 || f == SecondsEach * Fps - 1) Strip(rt, strip, stripped++);
                }
                Time.captureFramerate = 0;
                erasSeen[b] = game.EraShown;
                Assert.AreEqual(before + 1, game.EraShown, $"the stretch crosses into the next era at {boundary} m");
            }
            var finish = writer.FinishAsync();
            while (!finish.IsCompleted) yield return null;
            Assert.IsTrue(finish.Result, "the recording was written");
            Assert.Greater(new FileInfo(path).Length, 100000, "a real video");
            CollectionAssert.AreEqual(new[] { 3, 4 }, erasSeen, "into the Far Future, then the Far Far Future");
            strip.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Application.dataPath), "TestResults", "proof_crossing_frames.png"), strip.EncodeToPNG());
            rt.Release();
            Object.Destroy(read);
            Object.Destroy(strip);
            game.ScriptedTaps = null;
            game.ScriptedHeld = null;
        }

        // The world as the main camera draws it with its effects, in HDR, converted to gamma like the screen.
        static void Frame(Camera cam, CameraRig rig, RenderTexture rt, Texture2D read, byte[] rgba)
        {
            RenderTexture before = cam.targetTexture;
            cam.targetTexture = rt;
            rig.Follow(cam.transform.position.x - CameraRig.WidthUnits * 0.25f);
            cam.Render();
            cam.targetTexture = before;
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            RenderTexture.active = null;
            Color[] px = read.GetPixels();
            // The writer wants the top row first.
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    Color c = px[(Height - 1 - y) * Width + x];
                    Color g = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b)).gamma;
                    int i = (y * Width + x) * 4;
                    rgba[i] = (byte)(g.r * 255f + 0.5f);
                    rgba[i + 1] = (byte)(g.g * 255f + 0.5f);
                    rgba[i + 2] = (byte)(g.b * 255f + 0.5f);
                    rgba[i + 3] = 255;
                }
        }

        static void Strip(RenderTexture rt, Texture2D strip, int slot)
        {
            var half = RenderTexture.GetTemporary(Width / 2, Height / 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(rt, half);
            RenderTexture.active = half;
            strip.ReadPixels(new Rect(0, 0, Width / 2, Height / 2), slot * (Width / 2 + 6), 0);
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(half);
        }
    }
}
