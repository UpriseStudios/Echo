namespace Echo.Services.Share
{
    public interface IShareService
    {
        void ShareScreenshot(string caption);

        /// <summary>Shares text only, e.g. a Ghost Challenge message carrying its link.</summary>
        void ShareText(string text);

        /// <summary>Shares a file with text, e.g. a highlights reel clip (GIF) with its challenge link.</summary>
        void ShareFile(string path, string mimeType, string text);

        /// <summary>
        /// Keeps a clip in the phone's gallery under <paramref name="name"/> (a user revision); <paramref name="done"/>
        /// says whether it was saved. Android 9 and older, and a refused permission, say false.
        /// </summary>
        void SaveToGallery(string path, string mimeType, string name, System.Action<bool> done);
    }
}
