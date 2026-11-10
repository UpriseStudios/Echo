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
    /// The Infinite City prototype's proof: a real run, played with ordinary jumps and wearing Time Run, recorded for 10
    /// seconds from 3 seconds before 1200 m: the Far Future, the rift (the flash, the shards, the "10,000 AD" title) and the
    /// reveal (the camera rising as the orbital ring climbs into view), then the city. The HUD is in the picture (as the
    /// player sees it; no benchmark overlay). To TestResults/infinite_city_crossing.mp4 (1080 x 1920, 30 fps; time runs at
    /// the video's rate), with a still of the city once the reveal is done (Screenshots/infinite_city.png).
    /// </summary>
    public class InfiniteCityProofTests
    {
        const float Dt = 1f / 60f;
        const uint Seed = 41;
        const int Width = 540, Height = 960, Fps = 30, Seconds = 10, SecondsBefore = 3;
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
        public IEnumerator ARecording_FromTheFarFuture_IntoTheInfiniteCity()
        {
            var cfg = SimConfig.Default;
            _savePath = Path.Combine(Path.GetTempPath(), "echo_city_" + System.Guid.NewGuid().ToString("N") + ".json");
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

            int speed = cfg.SpeedForPct(game.Run.SpeedPct);
            Schedule run = SurvivingSchedule.Build(new Course(Library(), game.Run.Course.Seed), speed, 16000, new Random(7), cfg);
            game.PreviewTimeRun = true;
            game.WeakDeviceOverride = false;
            game.enabled = false;
            game.ScriptedTaps = run.TapAt;
            game.ScriptedHeld = run.HeldAt;
            game.TickFrame(Dt, tapped: true);

            // Metres a tick, to start the recording SecondsBefore ahead of the boundary.
            int t0 = 0, d0 = 0;
            while (game.State == GameState.Running && game.Run.Distance < 1000)
            {
                game.TickFrame(Dt, false);
                if (game.Run.Distance == 900 && t0 == 0) { t0 = game.Run.Tick; d0 = game.Run.Distance; }
                if (game.Run.Tick % 120 == 0) yield return null;
            }
            float perTick = (game.Run.Distance - d0) / (float)(game.Run.Tick - t0);
            int startAt = 1200 - Mathf.RoundToInt(perTick * SecondsBefore * 60);
            while (game.State == GameState.Running && game.Run.Distance < startAt)
            {
                game.TickFrame(Dt, false);
                if (game.Run.Tick % 120 == 0) yield return null;
            }
            Assert.AreEqual(GameState.Running, game.State, $"the run reaches {startAt} m");
            Assert.AreEqual(3, game.EraShown, "recording starts in the Far Future");

            var grab = new FrameGrab(Width, Height);
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "TestResults", "infinite_city_crossing.mp4");
            IVideoClipWriter writer = VideoClipWriters.Create();
            Assert.IsTrue(writer.Begin(path, Width, Height, 2, Fps, 0, 0));
            var far3D = Object.FindAnyObjectByType<Far3DView>(FindObjectsInactive.Include);
            bool sawReveal = false;
            Time.captureFramerate = Fps;   // Time.time moves on 1/30 s a frame: the animations and the reveal at their real speed
            for (int f = 0; f < Seconds * Fps; f++)
            {
                for (int k = 0; k < 60 / Fps; k++) game.TickFrame(Dt, false);
                yield return null;
                if (game.EraShown == 4 && far3D.Reveal < 1f) sawReveal = true;
                writer.AddFrame(grab.Frame());
            }
            Time.captureFramerate = 0;
            Assert.AreEqual(4, game.EraShown, "into the Far Far Future");
            Assert.IsTrue(sawReveal, "the reveal played through the rift");
            Assert.AreEqual(1f, far3D.Reveal, "and finished: the ring in place");
            var finish = writer.FinishAsync();
            while (!finish.IsCompleted) yield return null;
            Assert.IsTrue(finish.Result, "the recording was written");
            grab.Dispose();
            Assert.Greater(new FileInfo(path).Length, 100000, "a real video");

            // The city a moment later, the HUD as the player sees it.
            for (int f = 0; f < 60; f++) game.TickFrame(Dt, false);
            yield return ScreenCaptureTests.Capture("infinite_city");
            game.ScriptedTaps = null;
            game.ScriptedHeld = null;
        }

        /// <summary>
        /// The world as the main camera draws it with its effects, and the UI over it exactly as ScreenCaptureTests composes
        /// it (drawn over black and over white, the difference giving its coverage), as RGBA bytes, top row first.
        /// </summary>
        sealed class FrameGrab : System.IDisposable
        {
            readonly int _w, _h;
            readonly RenderTexture _world, _ui;
            readonly Texture2D _read;
            readonly Camera _uiCam;
            readonly Canvas _canvas;
            readonly RenderMode _mode;
            readonly byte[] _rgba;

            public FrameGrab(int w, int h)
            {
                _w = w;
                _h = h;
                _world = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR);
                _ui = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR);
                _read = new Texture2D(w, h, TextureFormat.RGBAHalf, false, true);
                _rgba = new byte[w * h * 4];
                _canvas = Object.FindAnyObjectByType<Canvas>();
                _mode = _canvas.renderMode;
                var go = new GameObject("ProofUiCamera");
                _uiCam = go.AddComponent<Camera>();
                _uiCam.clearFlags = CameraClearFlags.SolidColor;
                _uiCam.cullingMask = 1 << _canvas.gameObject.layer;
                _uiCam.orthographic = true;
                _uiCam.enabled = false;
                _uiCam.targetTexture = _ui;
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.worldCamera = _uiCam;
                _canvas.planeDistance = 5f;
                var scaler = _canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.enabled = false;
                scaler.enabled = true;
                Canvas.ForceUpdateCanvases();
                foreach (var frame in Object.FindObjectsByType<Echo.Game.UI.DesignFrame>(FindObjectsSortMode.None)) frame.Refit();
            }

            Color[] Read(RenderTexture rt)
            {
                RenderTexture.active = rt;
                _read.ReadPixels(new Rect(0, 0, _w, _h), 0, 0);
                RenderTexture.active = null;
                return _read.GetPixels();
            }

            public byte[] Frame()
            {
                Camera cam = Camera.main;
                RenderTexture before = cam.targetTexture;
                cam.targetTexture = _world;
                Object.FindAnyObjectByType<CameraRig>().Follow(cam.transform.position.x - CameraRig.WidthUnits * 0.25f);
                cam.Render();
                cam.targetTexture = before;
                Color[] world = Read(_world);
                _uiCam.backgroundColor = Color.black;
                _uiCam.Render();
                Color[] overBlack = Read(_ui);
                _uiCam.backgroundColor = Color.white;
                _uiCam.Render();
                Color[] overWhite = Read(_ui);
                for (int y = 0; y < _h; y++)
                    for (int x = 0; x < _w; x++)
                    {
                        int s = (_h - 1 - y) * _w + x;
                        float through = Mathf.Clamp01(overWhite[s].g - overBlack[s].g);
                        Color c = new Color(overBlack[s].r, overBlack[s].g, overBlack[s].b) + world[s] * through;
                        Color g = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b)).gamma;
                        int i = (y * _w + x) * 4;
                        _rgba[i] = (byte)(g.r * 255f + 0.5f);
                        _rgba[i + 1] = (byte)(g.g * 255f + 0.5f);
                        _rgba[i + 2] = (byte)(g.b * 255f + 0.5f);
                        _rgba[i + 3] = 255;
                    }
                return _rgba;
            }

            public void Dispose()
            {
                _canvas.renderMode = _mode;
                _canvas.worldCamera = null;
                Object.Destroy(_uiCam.gameObject);
                _world.Release();
                _ui.Release();
                Object.Destroy(_read);
            }
        }
    }
}
