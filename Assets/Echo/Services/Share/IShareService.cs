namespace Echo.Services.Share
{
    public interface IShareService
    {
        void ShareScreenshot(string caption);

        /// <summary>Shares text only, e.g. a Ghost Challenge message carrying its link.</summary>
        void ShareText(string text);

        /// <summary>Shares a file with text, e.g. a highlights reel clip (GIF) with its challenge link.</summary>
        void ShareFile(string path, string mimeType, string text);
    }
}
