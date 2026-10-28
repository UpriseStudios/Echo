using System;
using System.Collections;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

namespace Echo.Services.Share
{
    /// <summary>
    /// Shares a screenshot with a caption through NativeShare (tech spec 15, 20: screenshot in v1, clip in v1.1).
    /// The capture waits for the end of the frame, so a small hidden runner hosts the coroutine.
    /// </summary>
    public sealed class NativeShareService : IShareService
    {
        readonly Runner _runner;

        public NativeShareService()
        {
            var go = new GameObject("NativeShareRunner") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }

        public void ShareScreenshot(string caption) => _runner.StartCoroutine(CaptureAndShare(caption));

        public void ShareText(string text) => new NativeShare().SetText(text).SetSubject("Echo").Share();

        public void ShareFile(string path, string mimeType, string text) =>
            new NativeShare().AddFile(path, mimeType).SetText(text).SetSubject("Echo").Share();

        // A user revision: clips kept in the gallery as well as shared. Our own native code (Plugins/Android/EchoGallery.java,
        // Plugins/iOS/EchoGallery.mm), no package; elsewhere (the editor) the file is copied to a Gallery folder.
        public void SaveToGallery(string path, string mimeType, string name, Action<bool> done)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            bool saved = false;
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var gallery = new AndroidJavaClass("com.uprise.echo.EchoGallery");
                saved = gallery.CallStatic<bool>("save", activity, path, mimeType, name);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Share] gallery: " + e.Message);
            }
            done?.Invoke(saved);
#elif UNITY_IOS && !UNITY_EDITOR
            EchoGallery_Save(path, mimeType.StartsWith("video/") ? 1 : 0);
            _runner.StartCoroutine(WaitForPhotos(done));
#else
            bool saved = false;
            try
            {
                string dir = System.IO.Path.Combine(Application.persistentDataPath, "Gallery");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.Copy(path, System.IO.Path.Combine(dir, name), true);
                saved = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Share] gallery: " + e.Message);
            }
            done?.Invoke(saved);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void EchoGallery_Save(string path, int video);
        [DllImport("__Internal")] static extern int EchoGallery_Status();

        static IEnumerator WaitForPhotos(Action<bool> done)
        {
            // The first save waits on the player answering the permission prompt.
            float until = Time.realtimeSinceStartup + 120f;
            while (EchoGallery_Status() == 0 && Time.realtimeSinceStartup < until) yield return null;
            done?.Invoke(EchoGallery_Status() == 1);
        }
#endif

        static IEnumerator CaptureAndShare(string caption)
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            new NativeShare().AddFile(shot, "echo.png").SetText(caption).SetSubject("Echo").Share();
            Object.Destroy(shot);
        }

        sealed class Runner : MonoBehaviour { }
    }
}
