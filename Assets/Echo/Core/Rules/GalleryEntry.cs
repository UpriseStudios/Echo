using Echo.Core.Ghosts;

namespace Echo.Core.Rules
{
    /// <summary>A defeated Nemesis in the Rogues' Gallery (value-adds doc), with its run for a replay.</summary>
    public sealed class GalleryEntry
    {
        public string Name = "";
        public string SkinId = "";
        public int Kills;
        /// <summary>When it was defeated, as text from the game's clock (Core has no clock).</summary>
        public string DefeatedOn = "";
        public string Epitaph = "";
        /// <summary>The course it ran on and the content it was recorded against, for its replay.</summary>
        public uint Seed;
        public string ContentVersion = "";
        public GhostRecord Record;
    }
}
