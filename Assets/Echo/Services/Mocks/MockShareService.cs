using Echo.Services.Share;
using UnityEngine;

namespace Echo.Services.Mocks
{
    public sealed class MockShareService : IShareService
    {
        public void ShareScreenshot(string caption) => Debug.Log($"[MockShare] screenshot: {caption}");

        /// <summary>The last text shared, so tests can read the challenge link back.</summary>
        public string LastText { get; private set; }

        /// <summary>The last file shared (a reel clip), so tests can check it.</summary>
        public string LastFile { get; private set; }
        public string LastMime { get; private set; }

        public void ShareFile(string path, string mimeType, string text)
        {
            LastFile = path;
            LastMime = mimeType;
            LastText = text;
            Debug.Log($"[MockShare] file {path} ({mimeType}): {text}");
        }

        /// <summary>The last clip kept in the "gallery", and whether saving should fail (tests).</summary>
        public string LastSaved { get; private set; }
        public bool SaveFails;

        public void SaveToGallery(string path, string mimeType, string name, System.Action<bool> done)
        {
            if (!SaveFails) LastSaved = name;
            Debug.Log($"[MockShare] gallery {(SaveFails ? "refused" : "saved")} {name} ({mimeType}) from {path}");
            done?.Invoke(!SaveFails);
        }

        public void ShareText(string text)
        {
            LastText = text;
            Debug.Log($"[MockShare] text: {text}");
        }
    }
}
