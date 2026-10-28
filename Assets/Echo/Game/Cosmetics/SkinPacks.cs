using Echo.Services.Iap;

namespace Echo.Game.Cosmetics
{
    /// <summary>
    /// The paid skin packs as the store shows them (user revision: sold at launch, £1.99 each): four characters
    /// per pack, and the Season of Echo bundle that unlocks all eight.
    /// </summary>
    public static class SkinPacks
    {
        public const string SeasonName = "Season of Echo";

        public static readonly (string Name, string ProductId)[] All =
        {
            ("Undead", ProductIds.PackUndead),
            ("Dungeon", ProductIds.PackDungeon),
            ("Critters", ProductIds.PackCritters),
            ("Space", ProductIds.PackSpace),
            ("Retro", ProductIds.PackRetro),
            ("Royal", ProductIds.PackRoyal),
            ("Monsters", ProductIds.PackMonsters),
            ("Oddballs", ProductIds.PackOddballs),
        };

        /// <summary>Seasons doc: the Christmas Pack, on sale during Christmas and New Year, and never in the bundle.</summary>
        public static readonly (string Name, string ProductId) Christmas = ("Christmas", ProductIds.PackChristmas);
    }
}
