using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// World Time Run doc (art pass phase 6): in the Far Far Future the gameplay sprites (runner, ghosts, course, graves)
    /// take the lit sprite shader, so the 2D key and rim lights matching the 3D city fall on them. Swapping a view's
    /// sprites between the unlit and lit materials happens only when the era changes, and allocates nothing.
    /// </summary>
    public static class SpriteLighting
    {
        /// <summary>Every sprite under <paramref name="root"/> on <paramref name="from"/> moves to <paramref name="to"/>.</summary>
        public static void Swap(Transform root, Material from, Material to)
        {
            if (root == null || from == null || to == null || from == to) return;
            if (root.TryGetComponent(out SpriteRenderer sr) && sr.sharedMaterial == from) sr.sharedMaterial = to;
            for (int i = 0; i < root.childCount; i++) Swap(root.GetChild(i), from, to);
        }
    }
}
