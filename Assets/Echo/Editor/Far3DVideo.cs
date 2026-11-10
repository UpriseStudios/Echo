using System.IO;
using Echo.Game.Views;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Echo.Editor
{
    /// <summary>
    /// World Time Run doc (art pass phase 6): the reduced-effects version of the Far Far Future is a pre-rendered loop of
    /// the same 3D scene. This renders it from the built scene: the camera travels one Period of the city in LoopSeconds
    /// while the traffic (which repeats with that period) moves, so the last frame leads straight back into the first.
    /// 540 x 960 at 30 fps, H.264, to Content/Video/farfar_loop.mp4; run Echo/Build Scene afterwards to wire it. It needs a
    /// graphics device (not -nographics). Generated, like the art, and committed.
    /// </summary>
    public static class Far3DVideo
    {
        const int Width = 540, Height = 960, Fps = 30;

        /// <summary>Stills of the 3D camera at a few places along its path, to TestResults/Far3D (for judging the scene).</summary>
        [MenuItem("Echo/Preview Far Far Future")]
        public static void Preview()
        {
            EditorSceneManager.OpenScene("Assets/Echo/Scenes/Echo.unity");
            var scene = Object.FindAnyObjectByType<Far3DScene>(FindObjectsInactive.Include);
            var view = Object.FindAnyObjectByType<Far3DView>(FindObjectsInactive.Include);
            Camera cam = new SerializedObject(view).FindProperty("_camera").objectReferenceValue as Camera;
            scene.gameObject.SetActive(true);
            scene.Build();
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var frame = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            cam.targetTexture = rt;
            Directory.CreateDirectory("TestResults/Far3D");
            for (int k = 0; k < 3; k++)
            {
                Far3DScene.CameraAt(k * Far3DScene.Period / 3f, out Vector3 at, out Quaternion look);
                cam.transform.SetPositionAndRotation(at, look);
                scene.Follow(at, k * Far3DScene.Period / 3f);
                scene.Animate(k * 1.7f, k * Far3DScene.Period / 3f);
                cam.Render();
                RenderTexture.active = rt;
                frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                frame.Apply();
                File.WriteAllBytes($"TestResults/Far3D/still_{k}.png", frame.EncodeToPNG());
            }
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(frame);
            rt.Release();
            EditorSceneManager.OpenScene("Assets/Echo/Scenes/Echo.unity");
        }

        [MenuItem("Echo/Generate Far Far Future Loop")]
        public static void Generate()
        {
            EditorSceneManager.OpenScene("Assets/Echo/Scenes/Echo.unity");
            var scene = Object.FindAnyObjectByType<Far3DScene>(FindObjectsInactive.Include);
            var view = Object.FindAnyObjectByType<Far3DView>(FindObjectsInactive.Include);
            Camera cam = new SerializedObject(view).FindProperty("_camera").objectReferenceValue as Camera;
            scene.gameObject.SetActive(true);
            scene.Build();

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var frame = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            cam.targetTexture = rt;
            string dir = Path.GetDirectoryName(EchoSceneBuilder.Far3DLoopPath);
            Directory.CreateDirectory(dir);
            if (File.Exists(EchoSceneBuilder.Far3DLoopPath)) File.Delete(EchoSceneBuilder.Far3DLoopPath);
            var attributes = new VideoTrackAttributes
            {
                frameRate = new MediaRational(Fps),
                width = Width,
                height = Height,
                includeAlpha = false,
                bitRateMode = VideoBitrateMode.Low,
            };
            int frames = Mathf.RoundToInt(Far3DScene.LoopSeconds * Fps);
            using (var encoder = new MediaEncoder(EchoSceneBuilder.Far3DLoopPath, attributes))
                for (int f = 0; f < frames; f++)
                {
                    float t = f / (float)Fps;
                    Far3DScene.CameraAt(t * Far3DScene.Period / Far3DScene.LoopSeconds, out Vector3 at, out Quaternion look);
                    cam.transform.SetPositionAndRotation(at, look);
                    scene.Follow(at, t * Far3DScene.Period / Far3DScene.LoopSeconds);
                    scene.Animate(t, t * Far3DScene.Period / Far3DScene.LoopSeconds);
                    cam.Render();
                    RenderTexture.active = rt;
                    frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    frame.Apply();
                    encoder.AddFrame(frame);
                }
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(frame);
            rt.Release();
            AssetDatabase.ImportAsset(EchoSceneBuilder.Far3DLoopPath);
            // The scene isn't saved: the city was only built to film it.
            EditorSceneManager.OpenScene("Assets/Echo/Scenes/Echo.unity");
            Debug.Log($"[Far3DVideo] {frames} frames to {EchoSceneBuilder.Far3DLoopPath}");
        }
    }
}
