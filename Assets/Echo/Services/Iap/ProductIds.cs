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

        public static readonly string[] Packs =
        {
            PackUndead, PackDungeon, PackCritters, PackSpace, PackRetro, PackRoyal, PackMonsters, PackOddballs,
        };

        public static readonly string[] All =
        {
            PackUndead, PackDungeon, PackCritters, PackSpace, PackRetro, PackRoyal, PackMonsters, PackOddballs,
            SeasonOfEcho, PackChristmas,
        };
    }
}
