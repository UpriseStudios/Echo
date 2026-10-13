using System.Collections.Generic;
using Echo.Core.Ghosts;

namespace Echo.Core.Rules
{
    /// <summary>Every defeated Nemesis (value-adds doc), most notorious first, at most 50 kept.</summary>
    public static class RoguesGallery
    {
        public const int Cap = 50;

        public static GalleryEntry Entry(GhostRecord nemesis, uint seed, string contentVersion, string defeatedOn) => new GalleryEntry
        {
            Name = NemesisNames.For(nemesis.RunIndex),
            SkinId = nemesis.SkinId ?? "",
            Kills = nemesis.Kills,
            DefeatedOn = defeatedOn ?? "",
            Epitaph = Epitaphs.For(nemesis),
            Seed = seed,
            ContentVersion = contentVersion ?? "",
            Record = nemesis,
        };

        /// <summary>Adds the entry, keeps the list sorted by kills (ties: the earlier one first), drops the lowest past the cap.</summary>
        public static void Add(List<GalleryEntry> gallery, GalleryEntry entry)
        {
            int i = gallery.Count;
            while (i > 0 && gallery[i - 1].Kills < entry.Kills) i--;
            gallery.Insert(i, entry);
            while (gallery.Count > Cap) gallery.RemoveAt(gallery.Count - 1);
        }
    }
}
