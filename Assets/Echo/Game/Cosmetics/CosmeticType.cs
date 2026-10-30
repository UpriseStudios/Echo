namespace Echo.Game.Cosmetics
{
    public enum CosmeticType
    {
        Runner,
        Trail,
        Palette,
        Character,
        /// <summary>Seasons doc (version 2.1): the gravestone your runs leave, e.g. the Heart from It's Complicated.</summary>
        Grave,
        /// <summary>Modes doc: worn on the runner's head (and by its ghosts), e.g. the Halo from Naughty vs Nice.</summary>
        Accessory,
        /// <summary>
        /// A user revision: a world, the backdrop of every run (sky, scenery layers, drifting effects, a flyer in the
        /// sky). Sold in the store or granted by a mode, never both.
        /// </summary>
        World,
    }
}
