using System.Collections;
using UnityEngine;

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
