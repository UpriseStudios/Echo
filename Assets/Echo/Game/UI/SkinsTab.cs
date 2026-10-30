namespace Echo.Game.UI
{
    /// <summary>
    /// The store's sections (a user revision: one long list became tabs, organised like a shop, since this is where
    /// players spend money), in the order of the tab bar.
    /// </summary>
    public enum SkinsTab
    {
        /// <summary>The free and earned cast, the Elementals and the seasonal characters.</summary>
        Runners,

        /// <summary>The Season of Echo bundle first, then each skin pack with its four characters.</summary>
        Packs,

        /// <summary>The Worlds Bundle first, then every world with a preview.</summary>
        Worlds,

        /// <summary>Runner colours, trails and palettes.</summary>
        Style,

        /// <summary>Gravestones and accessories.</summary>
        Extras,

        /// <summary>What rewarded ads unlock: power-ups and the colour packs.</summary>
        Free,
    }
}
