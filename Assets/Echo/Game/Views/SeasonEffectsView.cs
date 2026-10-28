using Echo.Game.Seasons;
using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// A season's background effects layer (seasons doc): a fixed pool of sprites placed each frame as a pure function
    /// of time and the view, so nothing allocates. It sorts in front of the scenery and behind the floor, so it never
    /// covers the player, ghosts, graves or obstacles (seasons doc ground rule 3; SeasonReplayTests checks it).
    /// </summary>
    public sealed class SeasonEffectsView : MonoBehaviour
    {
        public const int MaxParticles = 64;

        /// <summary>In front of the background layers (-21 to -18), behind the floor (0) and everything on it.</summary>
        public const int SortingOrder = -17;

        [SerializeField] Material _material;

        SpriteRenderer[] _pool;
        SeasonTheme _theme;
        bool _shown;

        void Awake()
        {
            _pool = new SpriteRenderer[MaxParticles];
            for (int i = 0; i < MaxParticles; i++)
            {
                var go = new GameObject("Particle" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = _material;
                sr.sortingOrder = SortingOrder;
                sr.enabled = false;
                _pool[i] = sr;
            }
        }

        public void SetTheme(SeasonTheme theme)
        {
            _theme = theme != null && theme.Effect != SeasonEffect.None && theme.EffectSprites.Length > 0 ? theme : null;
            if (_theme == null) Hide();
        }

        void Hide()
        {
            if (!_shown || _pool == null) return;
            foreach (SpriteRenderer sr in _pool) sr.enabled = false;
            _shown = false;
        }

        public void Render(float left, float right, float bottom, float height, float time)
        {
            if (_theme == null || _pool == null)
            {
                Hide();
                return;
            }
            _shown = true;
            int count = Mathf.Min(_theme.EffectCount, MaxParticles);
            float width = right - left + 1f;
            for (int i = 0; i < MaxParticles; i++)
            {
                SpriteRenderer sr = _pool[i];
                if (i >= count)
                {
                    sr.enabled = false;
                    continue;
                }
                float u = Hash01(i, 1), v = Hash01(i, 2), w = Hash01(i, 3);
                float fall = _theme.EffectFall * Mathf.Lerp(0.6f, 1.4f, w);
                float sway = _theme.EffectSway * Mathf.Sin(time * Mathf.Lerp(0.8f, 1.8f, v) + u * 6.283f);
                float x = left - 0.5f + Mathf.Repeat(u * width - left * _theme.EffectParallax + sway, width);
                float y = bottom + height + 0.5f - Mathf.Repeat(v * (height + 1f) + time * fall, height + 1f);
                sr.sprite = _theme.EffectSprites[i % _theme.EffectSprites.Length];
                sr.color = _theme.EffectColours.Length > 0 ? _theme.EffectColours[i % _theme.EffectColours.Length] : Color.white;
                sr.transform.localPosition = new Vector3(SimUnits.Pixel(x), SimUnits.Pixel(y), 0f);
                sr.enabled = true;
            }
        }

        // A fixed scatter per particle, the same every frame.
        static float Hash01(int i, int salt)
        {
            uint h = (uint)(i * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}
