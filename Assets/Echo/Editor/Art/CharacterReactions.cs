using Echo.Game.Cosmetics;

namespace Echo.Editor.Art
{
    /// <summary>
    /// The curated characters that play with the scenery (a user revision), in one place. Everyone else has none.
    /// </summary>
    public static class CharacterReactions
    {
        public static CharacterReaction For(string characterId) => characterId switch
        {
            "char_astronaut" or "char_cosmic" => CharacterReaction.Stars,
            "char_alien" => CharacterReaction.Ufo,
            "char_robot" or "char_android" or "char_marine" => CharacterReaction.Headlight,
            "char_werewolf" => CharacterReaction.FullMoon,
            "char_vampire" => CharacterReaction.Bats,
            "char_pumpkin" => CharacterReaction.Lantern,
            "char_wizard" => CharacterReaction.Fireflies,
            "char_lich" or "char_banshee" or "char_reaper" or "char_skeleton" => CharacterReaction.Wisps,
            "char_knight" or "char_princess" or "char_king" or "char_queen" or "char_guard" or "char_paladin" => CharacterReaction.RoyalBanners,
            "char_golden" => CharacterReaction.GoldSparkle,
            "char_frog" => CharacterReaction.Rain,
            "char_bog" => CharacterReaction.Fog,
            "char_ninja" => CharacterReaction.Petals,
            "char_ember" => CharacterReaction.Embers,
            "char_frost" => CharacterReaction.FrostBreath,
            "char_storm" => CharacterReaction.DistantStorm,
            "char_tide" => CharacterReaction.TideShimmer,
            _ => CharacterReaction.None,
        };
    }
}
