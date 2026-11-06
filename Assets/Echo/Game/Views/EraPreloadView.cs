using Echo.Game.Seasons;
using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// World Time Run doc, art pass phase 1: the next era is preloaded 240 m into each one. Every era's art is already in
    /// memory with the scene, but a texture reaches the GPU, and a shader variant (the Far Future's lit sprites) is built,
    /// only the first time something draws it, which can cost a frame at the boundary. So the next era's sprites are drawn
    /// here, invisible and tiny, behind everything, until the boundary. A fixed pool: nothing is created mid-run.
    /// </summary>
    public sealed class EraPreloadView : MonoBehaviour
    {
        [SerializeField] Material _material;
        [SerializeField] Material _litMaterial;

        public const int PoolSize = 32;
        public const int SortingOrder = -31;

        readonly SpriteRenderer[] _pool = new SpriteRenderer[PoolSize];
        int _used;

        /// <summary>The theme being warmed, or null.</summary>
        public SeasonTheme Warming { get; private set; }

        /// <summary>Sprites being drawn to warm the next era (tests).</summary>
        public int Warmed => _used;

        void Awake()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Warm" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = _material;
                sr.sortingOrder = SortingOrder;
                sr.color = new Color(1f, 1f, 1f, 0f);
                sr.enabled = false;
                go.transform.localScale = new Vector3(0.01f, 0.01f, 1f);
                _pool[i] = sr;
            }
        }

        /// <summary>Starts drawing <paramref name="theme"/>'s art (null stops); the backdrop with the lit material in high definition.</summary>
        public void Warm(SeasonTheme theme)
        {
            if (theme == Warming) return;
            Warming = theme;
            _used = 0;
            if (theme != null)
            {
                bool lit = theme.HighDefinition && _litMaterial != null;
                Add(theme.FarLayer, lit);
                Add(theme.NearLayer, lit);
                foreach (ExtraLayer l in theme.ExtraLayers)
                {
                    Add(l != null ? l.Sprite : null, lit);
                    if (l != null) foreach (Sprite f in l.Frames) Add(f, lit);
                }
                foreach (Sprite f in theme.GapWheel) Add(f, false);
                Add(theme.FlyerFlap, false);
                Add(theme.FloorTop, false);
                Add(theme.Ceiling, false);
                Add(theme.Floor, false);
                Add(theme.Spike, false);
                Add(theme.Block, false);
                Add(theme.Gap, false);
                Add(theme.Grave, false);
                Add(theme.Coin, false);
                Add(theme.NemesisAccessory, false);
                Add(theme.Flyer, false);
                foreach (Sprite s in theme.EffectSprites) Add(s, false);
            }
            for (int i = _used; i < PoolSize; i++) _pool[i].enabled = false;
        }

        void Add(Sprite sprite, bool lit)
        {
            if (sprite == null || _used >= PoolSize) return;
            SpriteRenderer sr = _pool[_used++];
            sr.sprite = sprite;
            sr.sharedMaterial = lit ? _litMaterial : _material;
            sr.enabled = true;
        }

        /// <summary>Keeps the warming sprites inside the view (a renderer outside it isn't drawn).</summary>
        public void Render(float centreX, float centreY)
        {
            if (_used > 0) transform.position = new Vector3(centreX, centreY, 0f);
        }
    }
}
