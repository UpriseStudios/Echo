namespace Echo.Game.Cosmetics
{
    /// <summary>
    /// How a character plays with the scenery (a user revision on the Themed Biomes: "owning a matching pack adds a
    /// small flourish"). Cosmetic only, the player's character only, drawn by AmbientView.
    /// </summary>
    public enum CharacterReaction
    {
        None,
        /// <summary>Stars come out as night falls, with the odd shooting star; a star trail in Space.</summary>
        Stars,
        /// <summary>A little UFO drifts across the night sky.</summary>
        Ufo,
        /// <summary>A headlight beam lights the way at night.</summary>
        Headlight,
        /// <summary>A huge full moon rises at nightfall, with one howl.</summary>
        FullMoon,
        /// <summary>Bats flutter across the night sky.</summary>
        Bats,
        /// <summary>A warm lantern glow at night.</summary>
        Lantern,
        /// <summary>Fireflies drift around the runner from dusk.</summary>
        Fireflies,
        /// <summary>Soul wisps rise from the Graveyard's distant tombstones.</summary>
        Wisps,
        /// <summary>Royal banners fly from the Castle's towers, and its torches are lit at night.</summary>
        RoyalBanners,
        /// <summary>A trail of gold sparkles, always.</summary>
        GoldSparkle,
        /// <summary>Rain over the Graveyard.</summary>
        Rain,
        /// <summary>Fog rolls along the ground in the Graveyard and through the night.</summary>
        Fog,
        /// <summary>Blossom petals drift past in the Castle.</summary>
        Petals,
        /// <summary>Ember: embers from your steps; warm Graveyard fog, blazing Castle torches, an orange horizon at night.</summary>
        Embers,
        /// <summary>Frost: breath and ground frost; frosted headstones, iced Castle edges and icicles, crisp stars at night.</summary>
        FrostBreath,
        /// <summary>Storm: distant lightning; drizzle in the Graveyard, bolts on the Castle towers, electric flickers at night.</summary>
        DistantStorm,
        /// <summary>Tide: drips from you; puddle shimmer at the headstones, a moat glint by the Castle, ripples of light at night.</summary>
        TideShimmer,
    }
}
