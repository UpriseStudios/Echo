namespace Echo.Services.Iap
{
    /// <summary>
    /// Non-consumable products (tech spec 15). Cosmetic only. The skin packs (user revision: sold at launch) are
    /// four characters each at £1.99; the Season of Echo bundle (£4.99) unlocks all eight packs. The original
    /// Supporter, Neon and Mono packs are no longer sold: they unlock by score or rewarded ads (a user revision).
    /// </summary>
    public static class ProductIds
    {
        public const string PackUndead = "echo.pack.undead";
        public const string PackDungeon = "echo.pack.dungeon";
        public const string PackCritters = "echo.pack.critters";
        public const string PackSpace = "echo.pack.space";
        public const string PackRetro = "echo.pack.retro";
        public const string PackRoyal = "echo.pack.royal";
        public const string PackMonsters = "echo.pack.monsters";
        public const string PackOddballs = "echo.pack.oddballs";
        public const string SeasonOfEcho = "echo.pack.season1";

        /// <summary>Seasons doc (version 2.1): four Christmas characters at £1.99, sold each December, not in the bundle.</summary>
        public const string PackChristmas = "echo.pack.christmas";

        /// <summary>
        /// A user revision: worlds, £0.99 each, and the Worlds Bundle (£2.99) with every world sold in the store. A
        /// world won in a mode is never sold, and a world sold is never won.
        /// </summary>
        public const string WorldChristmas = "echo.world.christmas";
        public const string WorldHalloween = "echo.world.halloween";
        public const string WorldNeon = "echo.world.neon";
        public const string WorldSunken = "echo.world.sunken";
        public const string WorldAutumn = "echo.world.autumn";
        public const string WorldBeach = "echo.world.beach";
        public const string WorldDesert = "echo.world.desert";
        public const string WorldVolcano = "echo.world.volcano";
        public const string WorldToyTown = "echo.world.toytown";
        public const string WorldsBundle = "echo.worlds.bundle";

        /// <summary>World Time Run doc, phase 4: the top price point (£1.99), on its own, never in the Worlds Bundle.</summary>
        public const string WorldTimeRun = "echo.world.timerun";

        public static readonly string[] Worlds =
            { WorldChristmas, WorldHalloween, WorldNeon, WorldSunken, WorldAutumn, WorldBeach, WorldDesert, WorldVolcano, WorldToyTown };

        public static readonly string[] Packs =
        {
            PackUndead, PackDungeon, PackCritters, PackSpace, PackRetro, PackRoyal, PackMonsters, PackOddballs,
        };

        public static readonly string[] All =
        {
            PackUndead, PackDungeon, PackCritters, PackSpace, PackRetro, PackRoyal, PackMonsters, PackOddballs,
            SeasonOfEcho, PackChristmas, WorldChristmas, WorldHalloween, WorldNeon, WorldSunken, WorldAutumn,
            WorldBeach, WorldDesert, WorldVolcano, WorldToyTown, WorldsBundle, WorldTimeRun,
        };
    }
}
