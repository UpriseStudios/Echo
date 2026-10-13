using System;
using UnityEngine;

namespace Echo.Game.Cosmetics
{
    /// <summary>
    /// One cosmetic (tech spec 16). Most unlock by play and a product id is only a shortcut; characters are
    /// the exception (a revision chosen by the user): they are <see cref="PurchaseOnly"/>, or unlocked by
    /// watching rewarded ads (<see cref="AdsToUnlock"/>).
    /// </summary>
    [Serializable]
    public sealed class CosmeticItem
    {
        public string Id;
        public string DisplayName;
        public CosmeticType Type;

        [Tooltip("Runner colour (tints a tintable character), or ghost outline and trail colour.")]
        public Color Color = Color.white;

        [Tooltip("Unused since characters carry their own frames; kept so older catalogs still load.")]
        public Sprite Sprite;

        [Tooltip("Palette only: background colour at 0 m, 500 m and 1000 m.")]
        public Color[] PaletteStops = new Color[0];

        [Tooltip("Best score that unlocks this by play. Ignored when PurchaseOnly.")]
        public int UnlockScore;

        [Tooltip("Optional IAP product that also unlocks it.")]
        public string ProductId = "";

        [Tooltip("A second product that unlocks it too, e.g. the Character Pack.")]
        public string BundleProductId = "";

        [Tooltip("Only a purchase unlocks it (characters).")]
        public bool PurchaseOnly;

        [Tooltip("Unlocked by watching this many rewarded ads in total (0 = not an ad unlock). A user revision.")]
        public int AdsToUnlock;

        [Tooltip("The Elementals: permanent once every achievement is earned, or on trial for a few runs after ads.")]
        public bool AllAchievements;

        [Tooltip("Colour packs (a user revision): the ad-unlocked pack this item belongs to, e.g. \"Neon\"; empty for none.")]
        public string AdPack = "";

        [Tooltip("Earned when this lifetime stat reaches UnlockTarget (skins addendum). Never bought.")]
        public CosmeticUnlockStat UnlockStat;
        public int UnlockTarget;

        [Tooltip("How to unlock it, shown while locked, e.g. \"Die 250 times\".")]
        public string UnlockText = "";

        /// <summary>Unlocked by best score (tech spec 16), as opposed to a purchase or ads.</summary>
        /// <summary>Colour packs keep their score milestone and add ads as a second way in (a user revision).</summary>
        public bool EarnedByScore => !PurchaseOnly && !AllAchievements && UnlockStat == CosmeticUnlockStat.None && (AdsToUnlock == 0 || UnlockScore > 0);

        [Header("Character")]
        public Sprite[] RunFrames = new Sprite[0];
        public Sprite JumpFrame;
        public Sprite[] GhostFrames = new Sprite[0];
        public Sprite GhostJumpFrame;

        [Tooltip("Greyscale art that the equipped runner colour tints.")]
        public bool Tintable;

        [Tooltip("Scarf, cape, tail or hair trailing behind; width 0 for none.")]
        public Color ScarfColor = Color.white;
        public float ScarfWidth;

        [Tooltip("Where the scarf attaches, in units from the feet (sprite pivot).")]
        public Vector2 ScarfAnchor;

        [Tooltip("How this character plays with the scenery (biomes and night). Cosmetic only.")]
        public CharacterReaction Reaction;

        [Tooltip("Top of the head (or hat) in units from the feet, where the Nemesis wears its horns.")]
        public float HeadTop = 0.85f;

        public bool UnlockedBy(string productId) =>
            !string.IsNullOrEmpty(productId) && (ProductId == productId || BundleProductId == productId);
    }
}
