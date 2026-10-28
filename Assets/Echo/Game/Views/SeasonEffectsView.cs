using Echo.Game.Seasons;
using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// A season's background effects layer (seasons doc): falling sprites (snow, confetti) and fireworks, from fixed
    /// pools placed each frame as pure functions of time and the view, so nothing allocates. Everything sorts in front of
    /// the scenery and behind the floor, so it never covers the player, ghosts, graves or obstacles (seasons doc ground
    /// rule 3; SeasonReplayTests checks it).
    /// </summary>
    public sealed class SeasonEffectsView : MonoBehaviour
    {
        public const int MaxParticles = 64;
        public const int MaxBursts = 6, SparksPerBurst = 16;
        const float BurstSeconds = 2f, ShowBusier = 10f;

        /// <summary>In front of the background layers (-21 to -18), behind the floor (0) and everything on it.</summary>
        public const int SortingOrder = -17;

        [SerializeField] Material _material;

        SpriteRenderer[] _pool, _sparks;
        SeasonTheme _theme;
        bool _falling, _fireworks, _shown;

        void Awake()
        {
            _pool = Pool("Particle", MaxParticles);
            _sparks = Pool("Spark", MaxBursts * SparksPerBurst);
        }

        SpriteRenderer[] Pool(string name, int count)
        {
            var pool = new SpriteRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(name + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = _material;
                sr.sortingOrder = SortingOrder;
                sr.enabled = false;
                pool[i] = sr;
            }
            return pool;
        }

        public void SetTheme(SeasonTheme theme)
        {
            _theme = theme;
            _falling = theme != null && theme.Effect == SeasonEffect.Falling && theme.EffectSprites.Length > 0;
            _fireworks = theme != null && theme.Fireworks && theme.FireworkSpark != null && theme.FireworksPerMinute > 0f;
            if (!_falling && !_fireworks) Hide();
        }

        void Hide()
        {
            if (!_shown || _pool == null) return;
            foreach (SpriteRenderer sr in _pool) sr.enabled = false;
            foreach (SpriteRenderer sr in _sparks) sr.enabled = false;
            _shown = false;
        }

        /// <param name="show">0 to 1: the New Year show at the board reset, when fireworks go off ten times as often.</param>
        public void Render(float left, float right, float bottom, float height, float time, float show = 0f)
        {
            if (_pool == null || (!_falling && !_fireworks))
            {
                Hide();
                return;
            }
            _shown = true;
            Falling(left, right, bottom, height, time);
            Fireworks(left, right, bottom, height, time, show);
        }

        void Falling(float left, float right, float bottom, float height, float time)
        {
            int count = _falling ? Mathf.Min(_theme.EffectCount, MaxParticles) : 0;
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

        // Bursts launch every period (with a little jitter), each a ring of sparks flying out, falling and fading.
        void Fireworks(float left, float right, float bottom, float height, float time, float show)
        {
            int used = 0;
            if (_fireworks)
            {
                float period = 60f / (_theme.FireworksPerMinute * Mathf.Lerp(1f, ShowBusier, Mathf.Clamp01(show)));
                int newest = Mathf.FloorToInt(time / period);
                for (int b = newest; b > newest - MaxBursts && used + SparksPerBurst <= _sparks.Length; b--)
                {
                    float age = time - (b + 0.5f * Hash01(b, 4)) * period;
                    if (age < 0f || age > BurstSeconds) continue;
                    float k = age / BurstSeconds;
                    var centre = new Vector2(left + (right - left) * Mathf.Lerp(0.15f, 0.85f, Hash01(b, 5)),
                                             bottom + height * Mathf.Lerp(0.68f, 0.9f, Hash01(b, 6)));
                    Color colour = _theme.FireworkColours.Length > 0
                        ? _theme.FireworkColours[(int)(Hash01(b, 7) * _theme.FireworkColours.Length) % _theme.FireworkColours.Length]
                        : Color.white;
                    colour.a = 1f - k * k;
                    float speed = Mathf.Lerp(2.2f, 3.2f, Hash01(b, 8));
                    for (int j = 0; j < SparksPerBurst; j++)
                    {
                        float angle = (j + Hash01(b, 9)) * (6.2832f / SparksPerBurst);
                        float reach = speed * age * (1f - 0.45f * k);
                        var at = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach + new Vector2(0f, -0.7f * age * age);
                        SpriteRenderer sr = _sparks[used++];
                        sr.sprite = _theme.FireworkSpark;
                        sr.color = colour;
                        sr.transform.localPosition = new Vector3(SimUnits.Pixel(at.x), SimUnits.Pixel(at.y), 0f);
                        sr.enabled = true;
                    }
                }
            }
            for (int i = used; i < _sparks.Length; i++) _sparks[i].enabled = false;
        }

        // A fixed scatter per particle or burst, the same every frame.
        static float Hash01(int i, int salt)
        {
            uint h = (uint)(i * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}
