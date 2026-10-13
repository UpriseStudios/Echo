using UnityEngine;

namespace Echo.Game.Cosmetics
{
    /// <summary>All cosmetics (tech spec 16): characters, runner colours, trails and palettes. Built by EchoSceneBuilder.</summary>
    [CreateAssetMenu(menuName = "Echo/Cosmetic Catalog", fileName = "CosmeticCatalog")]
    public sealed class CosmeticCatalog : ScriptableObject
    {
        public CosmeticItem[] Items = new CosmeticItem[0];

        public CosmeticItem Find(string id)
        {
            foreach (var item in Items)
                if (item.Id == id) return item;
            return null;
        }

        /// <summary>The equipped item if it exists and has the right type, otherwise the first free item of that type.</summary>
        public CosmeticItem Resolve(string id, CosmeticType type)
        {
            var item = Find(id);
            if (item != null && item.Type == type) return item;
            foreach (var candidate in Items)
                if (candidate.Type == type && candidate.UnlockScore == 0 && candidate.EarnedByScore) return candidate;
            return null;
        }
    }
}
