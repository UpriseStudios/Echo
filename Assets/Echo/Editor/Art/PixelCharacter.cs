using System.Collections.Generic;
using UnityEngine;

namespace Echo.Editor.Art
{
    /// <summary>
    /// One playable character as data for <see cref="PixelRig"/>: body colours plus text-grid pictures for the
    /// head, chest and optional back item. Cosmetic only: every character shares the same hitbox.
    /// </summary>
    public sealed class PixelCharacter
    {
        public string Id;
        public string Name;

        /// <summary>Greyscale art that the colour skins tint (the hooded runner). Others keep their colours.</summary>
        public bool Tintable;

        /// <summary>The Elementals: permanent once every achievement is earned, or on trial after 5 ads (a user revision).</summary>
        public bool AllAchievements;

        /// <summary>The paid skin pack that sells it (PixelPacks), or empty for the free, ad and earned cast.</summary>
        public string ProductId = "";

        /// <summary>Unlocked by watching this many rewarded ads (0 = not an ad unlock).</summary>
        public int AdsToUnlock;

        /// <summary>Earned when a lifetime stat reaches a target (skins addendum); None for free or ad skins.</summary>
        public Echo.Game.Cosmetics.CosmeticUnlockStat UnlockStat;
        public int UnlockTarget;
        public string UnlockText = "";

        /// <summary>Applied to every frame after drawing: inverted colours or an RGB glitch.</summary>
        public PixelEffect Effect;

        public Color32 Torso, TorsoShade, Legs, Boots, Sleeve, Hand, Outline;

        /// <summary>Optional robe or dress over the upper legs, down to <see cref="SkirtBottom"/>.</summary>
        public bool Skirt;
        public Color32 SkirtColor, SkirtShade;
        public int SkirtBottom = 3;

        public string[] Head;
        public int HeadX;               // nudge from centred, in pixels
        public string[] Chest;          // 8 x 8 over the torso, optional
        public string[] Back;           // drawn behind everything at BackAt, optional
        public Vector2Int BackAt;
        public Dictionary<char, Color32> Palette = new Dictionary<char, Color32>();

        /// <summary>Scarf, cape, tail or hair, drawn at runtime as a trailing line. Width 0 means none.</summary>
        public Color32 ScarfColor;
        public float ScarfWidth;
        public Vector2Int ScarfAnchor = new Vector2Int(11, 17);
    }
}
