using System;
using Echo.Game.Cosmetics;
using Echo.Game.World;
using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// The character flourishes (a user revision on the Themed Biomes): the equipped character's reaction to the
    /// biome and the night, such as stars for the Astronaut, bats for the Vampire or rain for the Frog. Every effect
    /// is a pure function of time and the view, drawn from a fixed pool of sprites, so nothing is allocated per
    /// frame (tech spec 18). Cosmetic only; the sim never sees any of it.
    /// </summary>
    public sealed class AmbientView : MonoBehaviour
    {
        const int PoolSize = 64;
        const int TrailLength = 14;
        const float TrailEvery = 0.04f;

        [SerializeField] Material _material;
        [SerializeField] Sprite _star;
        [SerializeField] Sprite _shootingStar;
        [SerializeField] Sprite _ufo;
        [SerializeField] Sprite _fullMoon;
        [SerializeField] Sprite[] _bat = new Sprite[0];
        [SerializeField] Sprite _glow;
        [SerializeField] Sprite _beam;
        [SerializeField] Sprite _firefly;
        [SerializeField] Sprite _sparkle;
        [SerializeField] Sprite _wisp;
        [SerializeField] Sprite _rain;
        [SerializeField] Sprite _fog;
        [SerializeField] Sprite _petal;
        [Header("Elementals (subtle)")]
        [SerializeField] Sprite _bolt;
        [SerializeField] Sprite _shimmer;
        [SerializeField] Sprite _ember;
        [SerializeField] Sprite _frost;
        [SerializeField] Sprite _frostCap;
        [SerializeField] Sprite _icicles;
        [SerializeField] Sprite _glint;
        [SerializeField] Sprite _torch;
        [Tooltip("Headstones in the Graveyard layer: x in its tile and the ground height there, in pixels.")]
        [SerializeField] Vector2Int[] _graveyardStones = new Vector2Int[0];
        [Tooltip("The Castle layer's tower centres and tops, its gate torches' x and its wall top, in pixels.")]
        [SerializeField] Vector2Int[] _castleTowers = new Vector2Int[0];
        [SerializeField] int[] _gateTorches = new int[0];
        [SerializeField] int _castleWallTop = 52;

        SpriteRenderer[] _pool;
        SpriteRenderer _shield;
        int _used;
        CharacterReaction _reaction;
        readonly Vector2[] _trail = new Vector2[TrailLength];
        int _trailCount, _trailHead;
        float _trailTimer;
        bool _howled;

        /// <summary>The Werewolf's full moon is up: EchoGame plays the howl, once a run.</summary>
        public event Action Howled;

        public CharacterReaction Reaction => _reaction;

        void Awake()
        {
            // The Ghost Shield's bubble (a power-up), drawn over the runner while it is armed.
            var bubble = new GameObject("ShieldBubble");
            bubble.transform.SetParent(transform, false);
            _shield = bubble.AddComponent<SpriteRenderer>();
            _shield.sharedMaterial = _material;
            _shield.sprite = _glow;
            _shield.sortingOrder = 12;
            _shield.enabled = false;

            _pool = new SpriteRenderer[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Ambient" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = _material;
                sr.enabled = false;
                _pool[i] = sr;
            }
        }

        public void SetReaction(CharacterReaction reaction) => _reaction = reaction;

        /// <summary>The Ghost Shield bubble around the runner while it is armed; a gentle pulse.</summary>
        public void RenderShield(bool armed, Vector2 player)
        {
            if (_shield == null) return;
            if (_shield.enabled != armed) _shield.enabled = armed;
            if (!armed) return;
            float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 6f);
            _shield.transform.localPosition = new Vector3(SimUnits.Pixel(player.x), SimUnits.Pixel(player.y + 0.42f), 0f);
            _shield.transform.localScale = new Vector3(1.5f * pulse, 1.5f * pulse, 1f);
            _shield.color = new Color(0.45f, 0.95f, 1f, 0.7f);
        }

        /// <summary>A new run: the trail empties and the moon may howl again.</summary>
        public void ResetRun()
        {
            _trailCount = 0;
            _howled = false;
        }

        /// <param name="player">The runner's feet, in world units.</param>
        /// <param name="graveyardLeft">Where the Graveyard layer's tiles start, and their width (BackgroundView). The
        /// Castle layer is the same size at the same parallax, so its tiles start there too.</param>
        public void Render(in EnvironmentState env, Vector2 player, float left, float right, float bottom, float height,
                           float graveyardLeft, float tileWidth, bool visible)
        {
            if (_pool == null) return;
            _used = 0;
            if (visible)
            {
                float t = Time.time;
                float w = right - left, top = bottom + height;
                switch (_reaction)
                {
                    case CharacterReaction.Stars: Stars(env, t, left, w, bottom, height, player); break;
                    case CharacterReaction.Ufo: Ufo(env, t, left, w, bottom, height); break;
                    case CharacterReaction.Headlight: Headlight(env, player); break;
                    case CharacterReaction.FullMoon: FullMoon(env, left, w, bottom, height); break;
                    case CharacterReaction.Bats: Bats(env, t, right, w, bottom, height); break;
                    case CharacterReaction.Lantern: Lantern(env, t, player); break;
                    case CharacterReaction.Fireflies: Fireflies(env, t, player); break;
                    case CharacterReaction.Wisps: Wisps(env, t, left, right, graveyardLeft, tileWidth); break;
                    case CharacterReaction.GoldSparkle: Trail(player, _sparkle, new Color(1f, 0.85f, 0.35f), 1f, t); break;
                    case CharacterReaction.Rain: Rain(env, t, left, w, top, height); break;
                    case CharacterReaction.Fog: Fog(env, t, left, w); break;
                    case CharacterReaction.Petals: Petals(env, t, right, w, top, height); break;
                    case CharacterReaction.Embers: Ember(env, t, player, left, right, w, graveyardLeft, tileWidth); break;
                    case CharacterReaction.FrostBreath: Frost(env, t, player, left, right, w, bottom, height, graveyardLeft, tileWidth); break;
                    case CharacterReaction.DistantStorm: Storm(env, t, left, right, w, bottom, height, top, graveyardLeft, tileWidth); break;
                    case CharacterReaction.NoseGlow: NoseGlow(env, t, player); break;
                    case CharacterReaction.TideShimmer: Tide(env, t, player, left, right, w, bottom, height, graveyardLeft, tileWidth); break;
                }
            }
            for (int i = _used; i < PoolSize; i++)
                if (_pool[i].enabled) _pool[i].enabled = false;
        }

        // ---- the effects ----

        void Stars(in EnvironmentState env, float t, float left, float w, float bottom, float height, Vector2 player)
        {
            if (env.Night > 0f)
            {
                for (int i = 0; i < 36; i++)
                {
                    float hx = Hash(i, 1), hy = Hash(i, 2);
                    float twinkle = 0.55f + 0.45f * Mathf.Sin(t * (1.5f + hx * 2f) + i);
                    Put(_star, new Vector2(left + hx * w, bottom + height * (0.5f + 0.47f * hy)), new Color(1f, 1f, 1f, env.Night * twinkle), -22);
                }
                // A shooting star every five seconds, streaking down-left for 0.6 s.
                float cycle = Mathf.Floor(t / 5f), p = (t - cycle * 5f) / 0.6f;
                if (p < 1f)
                {
                    var from = new Vector2(left + w * (0.4f + 0.5f * Hash((int)cycle, 3)), bottom + height * 0.92f);
                    Put(_shootingStar, from + new Vector2(-3f, -1f) * p, new Color(1f, 1f, 1f, env.Night * (1f - p)), -21, 1f, true);
                }
            }
            // Out in Space the Astronaut leaves a trail of stars.
            if (env.Space > 0f) Trail(player, _star, new Color(0.8f, 0.92f, 1f), env.Space, t);
        }

        void Ufo(in EnvironmentState env, float t, float left, float w, float bottom, float height)
        {
            float a = Mathf.Max(env.Night, env.Space);
            if (a <= 0f) return;
            float p = t / 14f - Mathf.Floor(t / 14f);
            Put(_ufo, new Vector2(left - 2f + p * (w + 4f), bottom + height * 0.8f + 0.2f * Mathf.Sin(t * 2f)), new Color(1f, 1f, 1f, a), -19);
        }

        void Headlight(in EnvironmentState env, Vector2 player)
        {
            if (env.Night <= 0f) return;
            Put(_beam, player + new Vector2(0.22f, 0.6f), new Color(1f, 1f, 1f, env.Night * 0.85f), 9, 1.4f);
        }

        void FullMoon(in EnvironmentState env, float left, float w, float bottom, float height)
        {
            if (env.Night <= 0f) return;
            // It rises as night falls.
            Put(_fullMoon, new Vector2(left + w * 0.7f, bottom + height * (0.66f + 0.2f * env.Night)), new Color(1f, 1f, 1f, env.Night), -21);
            if (!_howled && env.Night >= 0.6f)
            {
                _howled = true;
                Howled?.Invoke();
            }
        }

        void Bats(in EnvironmentState env, float t, float right, float w, float bottom, float height)
        {
            if (env.Night <= 0f || _bat.Length < 2) return;
            for (int i = 0; i < 6; i++)
            {
                float h = Hash(i, 4), span = w + 2f;
                float p = (t * (1.2f + h) + h * 10f) / span;
                float x = right + 1f - (p - Mathf.Floor(p)) * span;
                float y = bottom + height * (0.5f + 0.32f * Hash(i, 5)) + 0.25f * Mathf.Sin(t * 4f + i);
                Put(_bat[(int)(t * 8f + i) % 2], new Vector2(x, y), new Color(1f, 1f, 1f, env.Night), -17);
            }
        }

        void Lantern(in EnvironmentState env, float t, Vector2 player)
        {
            if (env.Night <= 0f) return;
            float flicker = 0.9f + 0.1f * Mathf.Sin(t * 13f) * Mathf.Sin(t * 7.3f);
            Put(_glow, player + new Vector2(0f, 0.45f), new Color(1f, 0.62f, 0.25f, 0.55f * env.Night * flicker), 9, 2.6f);
        }

        // Seasons doc: Rudolph's nose lights the ground just ahead. Sorted with the floor's edge and graves (1), under
        // the obstacles (2), ghosts and player, so it never makes a hazard easier to see; a touch stronger at night.
        void NoseGlow(in EnvironmentState env, float t, Vector2 player)
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(t * 4f);
            Put(_glow, player + new Vector2(0.9f, 0.05f), new Color(1f, 0.28f, 0.22f, (0.16f + 0.2f * env.Night) * pulse), 1, new Vector2(2.4f, 0.7f));
        }

        void Fireflies(in EnvironmentState env, float t, Vector2 player)
        {
            float a = Mathf.Max(env.Night, Mathf.Clamp01(env.Castle * 0.6f));
            if (a <= 0f) return;
            for (int i = 0; i < 10; i++)
            {
                float h = Hash(i, 6), h2 = Hash(i, 7);
                var at = player + new Vector2(0.2f + 1.6f * Mathf.Sin(t * (0.6f + h) + i * 1.7f), 0.7f + 0.6f * Mathf.Sin(t * (0.9f + h2) + i * 2.3f));
                Put(_firefly, at, new Color(1f, 1f, 1f, a * (0.45f + 0.55f * Mathf.Sin(t * 3f + i))), 11);
            }
        }

        // Soul wisps rise from the Graveyard layer's own headstones, so they line up with the scenery.
        void Wisps(in EnvironmentState env, float t, float left, float right, float graveyardLeft, float tileWidth)
        {
            if (env.Graveyard <= 0f || tileWidth <= 0f) return;
            const float ppu = CameraRig.PixelsPerUnit;
            for (int n = 0; n < 4 && _used < PoolSize; n++)
            {
                float tile = graveyardLeft + n * tileWidth;
                for (int s = 0; s < _graveyardStones.Length; s++)
                {
                    float x = tile + (_graveyardStones[s].x + 3) / ppu;
                    if (x < left - 0.5f || x > right + 0.5f) continue;
                    float h = Hash(s, 8);
                    float p = (t / 3f + h) - Mathf.Floor(t / 3f + h);
                    float y = (_graveyardStones[s].y + 10) / ppu + p * 1.6f;
                    Put(_wisp, new Vector2(x + 0.1f * Mathf.Sin(t * 3f + s), y), new Color(1f, 1f, 1f, env.Graveyard * 0.8f * Mathf.Sin(p * Mathf.PI)), -18);
                }
            }
        }

        void Rain(in EnvironmentState env, float t, float left, float w, float top, float height)
        {
            if (env.Graveyard <= 0f) return;
            for (int i = 0; i < 40; i++)
            {
                float p = t * 1.8f + Hash(i, 9);
                p -= Mathf.Floor(p);
                float x = left + Hash(i, 10) * (w + 1f) - p * 0.6f;
                Put(_rain, new Vector2(x, top - p * height), new Color(1f, 1f, 1f, env.Graveyard * 0.8f), 12);
            }
        }

        void Fog(in EnvironmentState env, float t, float left, float w)
        {
            float a = Mathf.Max(env.Graveyard, env.Night);
            if (a <= 0f) return;
            for (int i = 0; i < 6; i++)
            {
                float p = Hash(i, 11) - t * 0.03f;
                p -= Mathf.Floor(p);
                Put(_fog, new Vector2(left - 1.5f + p * (w + 3f), 0.25f + 0.35f * Hash(i, 12)), new Color(1f, 1f, 1f, a * 0.7f), 8);
            }
        }

        void Petals(in EnvironmentState env, float t, float right, float w, float top, float height)
        {
            float a = env.Castle - env.Space;
            if (a <= 0f) return;
            for (int i = 0; i < 18; i++)
            {
                float px = t * 0.15f + Hash(i, 13), py = t * 0.25f + Hash(i, 14);
                px -= Mathf.Floor(px);
                py -= Mathf.Floor(py);
                Put(_petal, new Vector2(right - px * (w + 1f), top - py * height), new Color(1f, 1f, 1f, a), 12, 1f, Mathf.Sin(t * 5f + i) > 0f);
            }
        }

        // ---- the Elementals (a user revision) ----
        // Each keeps one signature everywhere and answers each world differently: the Graveyard, the Castle and the
        // night sky (night or Space). The parts crossfade with the biome blends. Subtle on purpose: low alpha, few sprites.

        const float Ppu = CameraRig.PixelsPerUnit;

        static float CastleWeight(in EnvironmentState env) => Mathf.Clamp01(env.Castle - env.Space);
        static float SkyWeight(in EnvironmentState env) => Mathf.Max(env.Night, env.Space);

        // Ember: embers rise from your steps everywhere. Warm fog in the Graveyard, the gate torches blazing in the
        // Castle (and the tower windows lit), and a faint orange horizon behind the peaks at night.
        void Ember(in EnvironmentState env, float t, Vector2 player, float left, float right, float w, float nearLeft, float tileWidth)
        {
            Put(_glow, player + new Vector2(0f, 0.05f), new Color(1f, 0.55f, 0.2f, 0.16f), 2, 1.3f);
            for (int i = 0; i < 6; i++)
            {
                float p = t * 0.9f + Hash(i, 15);
                p -= Mathf.Floor(p);
                var at = player + new Vector2(-0.2f - p * 1.2f + 0.15f * Mathf.Sin(t * 3f + i), 0.1f + p * 1.1f);
                Put(_ember, at, new Color(1f, 1f, 1f, 0.7f * (1f - p)), 11);
            }

            float sky = SkyWeight(env);
            if (sky > 0f)
                for (int i = 0; i < 4; i++)
                    Put(_glow, new Vector2(left + w * (0.125f + 0.25f * i), 4.6f), new Color(1f, 0.45f, 0.15f, 0.2f * sky), -22,
                        new Vector2(4.2f, 2.6f));

            if (env.Graveyard > 0f)
                for (int i = 0; i < 5; i++)
                {
                    float p = Hash(i, 21) - t * 0.025f;
                    p -= Mathf.Floor(p);
                    Put(_fog, new Vector2(left - 1.5f + p * (w + 3f), 0.3f + 0.3f * Hash(i, 22)),
                        new Color(1f, 0.62f, 0.38f, 0.38f * env.Graveyard), 8, 1.2f);
                }

            float castle = CastleWeight(env);
            if (castle <= 0f || tileWidth <= 0f) return;
            for (int n = 0; n < 4; n++)
            {
                float tile = nearLeft + n * tileWidth;
                for (int k = 0; k < _gateTorches.Length; k++)
                {
                    float x = tile + _gateTorches[k] / Ppu;
                    if (x < left - 1f || x > right + 1f) continue;
                    float flicker = 0.85f + 0.15f * Mathf.Sin(t * 14f + k * 3f) * Mathf.Sin(t * 6.1f + k);
                    Put(_torch, new Vector2(tile + (_gateTorches[k] - 1) / Ppu, 14f / Ppu), new Color(1f, 1f, 1f, castle), -17);
                    var flame = new Vector2(x + 1f / Ppu, 23f / Ppu);
                    Put(_glow, flame, new Color(1f, 0.55f, 0.18f, 0.5f * castle * flicker), -17, 0.95f * flicker);
                    float sp = t * 1.4f + k * 0.5f;
                    sp -= Mathf.Floor(sp);
                    Put(_ember, flame + new Vector2(0.08f * Mathf.Sin(t * 5f + k), 0.15f + sp * 0.6f), new Color(1f, 1f, 1f, castle * (1f - sp)), -17);
                }
                for (int k = 0; k < _castleTowers.Length; k++)
                {
                    Vector2Int tower = _castleTowers[k];
                    float x = tile + tower.x / Ppu;
                    if (x < left - 1f || x > right + 1f) continue;
                    float lit = castle * (0.25f + 0.25f * env.Night) * (0.9f + 0.1f * Mathf.Sin(t * 9f + k));
                    Put(_glow, new Vector2(x, (tower.y - 18) / Ppu), new Color(1f, 0.6f, 0.25f, lit), -17, 0.4f);
                    Put(_glow, new Vector2(x, (tower.y - 38) / Ppu), new Color(1f, 0.6f, 0.25f, lit), -17, 0.4f);
                }
            }
        }

        // Frost: your breath clouds and a glint of frost on the ground behind you everywhere. Frost-capped headstones
        // in the Graveyard, iced crenellations and icicles on the towers in the Castle, and crisp, steady stars.
        void Frost(in EnvironmentState env, float t, Vector2 player, float left, float right, float w, float bottom, float height,
                   float nearLeft, float tileWidth)
        {
            float b = t / 1.6f - Mathf.Floor(t / 1.6f);
            if (b < 0.6f)
                Put(_fog, player + new Vector2(0.35f + b * 0.5f, 0.68f + b * 0.1f), new Color(1f, 1f, 1f, 0.45f * (1f - b / 0.6f)), 11, 0.3f + b * 0.3f);
            for (int i = 0; i < 8; i++)
            {
                float q = (t * 1.5f + i / 8f) - Mathf.Floor(t * 1.5f + i / 8f);
                Put(_frost, new Vector2(player.x - 0.3f - q * 2.4f, 0.03f), new Color(1f, 1f, 1f, 0.6f * (1f - q)), 3);
            }

            float sky = SkyWeight(env);
            if (sky > 0f)
                for (int i = 0; i < 8; i++)
                {
                    // Above the far peaks (6.1 units at most), where nothing hides them.
                    float low = Mathf.Max(6.4f, bottom + height * 0.5f), high = Mathf.Max(low + 1f, bottom + height * 0.97f);
                    var at = new Vector2(left + Hash(i, 23) * w, Mathf.Lerp(low, high, Hash(i, 24)));
                    // Steady, not twinkling; every few seconds one catches the light.
                    float catchLight = Mathf.Clamp01(1f - Mathf.Abs((t * 0.5f + Hash(i, 25) * 6f) % 6f - 0.3f) / 0.3f);
                    Put(_star, at, new Color(0.9f, 0.97f, 1f, sky), -22);
                    Put(_glint, at, new Color(1f, 1f, 1f, sky * (0.35f + 0.65f * catchLight)), -22, 1f + 0.4f * catchLight);
                }

            if (tileWidth <= 0f) return;
            float castle = CastleWeight(env);
            for (int n = 0; n < 4; n++)
            {
                float tile = nearLeft + n * tileWidth;
                if (env.Graveyard > 0f)
                    for (int s = 0; s < _graveyardStones.Length; s++)
                    {
                        Vector2Int stone = _graveyardStones[s];
                        float x = tile + stone.x / Ppu;
                        if (x < left - 0.5f || x > right + 0.5f) continue;
                        // Crosses (every third x, PixelWorld.GraveyardHills) take it on their arms; round stones on
                        // their tops, lean and all.
                        bool cross = stone.x % 3 == 0;
                        int lean = !cross && stone.x % 2 == 0 ? 1 : 0;
                        Put(_frostCap, new Vector2(tile + (stone.x + lean) / Ppu, (stone.y + (cross ? 10 : 9)) / Ppu),
                            new Color(1f, 1f, 1f, 0.9f * env.Graveyard), -18);
                    }
                if (castle > 0f)
                {
                    // A cap on every merlon (6 px of every 12 along the wall top), icicles under each tower's lip.
                    int first = Mathf.Max(0, Mathf.FloorToInt((left - 0.5f - tile) * Ppu / 12f));
                    int last = Mathf.Min(512 / 12, Mathf.CeilToInt((right + 0.5f - tile) * Ppu / 12f));
                    for (int m = first; m <= last; m++)
                        if (!OnATower(m * 12 + 3))
                            Put(_frostCap, new Vector2(tile + m * 12 / Ppu, (_castleWallTop + 1) / Ppu), new Color(1f, 1f, 1f, 0.85f * castle), -18);
                    for (int k = 0; k < _castleTowers.Length; k++)
                    {
                        Vector2Int tower = _castleTowers[k];
                        float x = tile + tower.x / Ppu;
                        if (x < left - 1f || x > right + 1f) continue;
                        for (int j = 0; j < 4; j++)
                            Put(_icicles, new Vector2(tile + (tower.x - 14 + j * 7) / Ppu, (tower.y - 2) / Ppu), new Color(1f, 1f, 1f, 0.9f * castle), -18);
                    }
                }
            }
        }

        bool OnATower(int px)
        {
            for (int k = 0; k < _castleTowers.Length; k++)
                if (Mathf.Abs(px - _castleTowers[k].x) <= 15) return true;
            return false;
        }

        // Storm: a distant bolt now and then everywhere. A light drizzle in the Graveyard, lightning striking the
        // towers in the Castle, and faint electric flickers across the night sky. No sound.
        void Storm(in EnvironmentState env, float t, float left, float right, float w, float bottom, float height, float top,
                   float nearLeft, float tileWidth)
        {
            float castle = CastleWeight(env);
            float cycle = Mathf.Floor(t / 4.5f), p = t - cycle * 4.5f;
            bool flashing = p < 0.08f || (p > 0.16f && p < 0.24f); // two quick flashes
            if (flashing)
            {
                // In the Castle the bolt finds a tower instead, when one is in view.
                bool struck = castle > 0.5f && StrikeTower((int)cycle, left, right, nearLeft, tileWidth, castle);
                if (!struck)
                {
                    var at = new Vector2(left + w * (0.15f + 0.7f * Hash((int)cycle, 16)), bottom + height * (0.62f + 0.12f * Hash((int)cycle, 17)));
                    Put(_bolt, at, new Color(1f, 1f, 1f, 0.55f + 0.25f * env.Night), -21, 1.2f, Hash((int)cycle, 18) > 0.5f);
                }
            }

            if (env.Graveyard > 0f)
                for (int i = 0; i < 16; i++)
                {
                    float d = t * 1.4f + Hash(i, 26);
                    d -= Mathf.Floor(d);
                    float x = left + Hash(i, 27) * (w + 1f) - d * 0.8f;
                    Put(_rain, new Vector2(x, top - d * height), new Color(0.8f, 0.85f, 0.95f, 0.35f * env.Graveyard), 12);
                }

            float sky = SkyWeight(env);
            if (sky > 0f)
                for (int i = 0; i < 4; i++)
                {
                    // Each spark lives a tenth of a second, at a fresh spot every 1.3 to 2.5 s.
                    float period = 1.3f + 1.2f * Hash(i, 28), phase = t / period + Hash(i, 29), c = Mathf.Floor(phase);
                    if ((phase - c) * period > 0.1f) continue;
                    int seed = (int)c * 7 + i;
                    var at = new Vector2(left + w * Hash(seed, 30), bottom + height * (0.6f + 0.35f * Hash(seed, 31)));
                    Put(_bolt, at, new Color(0.7f, 0.85f, 1f, 0.45f * sky), -22, 0.45f, Hash(seed, 32) > 0.5f);
                }
        }

        // A bolt down onto a visible tower's pennant pole, with a flash at its tip. False when no tower is in view.
        bool StrikeTower(int cycle, float left, float right, float nearLeft, float tileWidth, float castle)
        {
            int count = _castleTowers.Length * 4;
            if (tileWidth <= 0f || count == 0) return false;
            int pick = (int)(Hash(cycle, 33) * count);
            for (int tries = 0; tries < count; tries++)
            {
                int k = (pick + tries) % count;
                Vector2Int tower = _castleTowers[k % _castleTowers.Length];
                float x = nearLeft + k / _castleTowers.Length * tileWidth + tower.x / Ppu;
                if (x < left + 0.5f || x > right - 0.5f) continue;
                const float boltScale = 1.6f;
                float tip = (tower.y + 19) / Ppu;
                Put(_bolt, new Vector2(x, tip + 14f * boltScale / Ppu), new Color(1f, 1f, 1f, 0.85f * castle), -18, boltScale, cycle % 2 == 0);
                Put(_glow, new Vector2(x, tip), new Color(0.75f, 0.85f, 1f, 0.55f * castle), -18, 1.1f);
                return true;
            }
            return false;
        }

        // Tide: drops of water fall from you everywhere. Puddles shimmer at the headstones' feet in the Graveyard, a
        // moat glints along the foot of the Castle wall, and slow ripples of light cross the night sky.
        void Tide(in EnvironmentState env, float t, Vector2 player, float left, float right, float w, float bottom, float height,
                  float nearLeft, float tileWidth)
        {
            for (int i = 0; i < 3; i++)
            {
                float p = t * 0.8f + i / 3f;
                p -= Mathf.Floor(p);
                Put(_rain, player + new Vector2(-0.15f - p * 0.4f + i * 0.12f, 0.55f - p * 0.55f), new Color(0.75f, 0.95f, 1f, 0.7f * (1f - p)), 11);
            }

            float sky = SkyWeight(env);
            if (sky > 0f)
                for (int row = 0; row < 3; row++)
                    for (int i = 0; i < 5; i++)
                    {
                        float x = left + w * (i + 0.5f) / 5f + Mathf.Sin(t * 0.15f + row) * 0.6f;
                        float y = bottom + height * (0.7f + 0.08f * row) + 0.25f * Mathf.Sin(t * 0.6f + i * 1.1f + row * 2f);
                        float wave = 0.5f + 0.5f * Mathf.Sin(t * 0.8f - i * 0.9f + row * 1.7f);
                        Put(_shimmer, new Vector2(x, y), new Color(0.6f, 0.95f, 1f, 0.3f * sky * wave), -22, new Vector2(3f, 1f));
                    }

            float castle = CastleWeight(env);
            if (castle > 0f)
                for (int i = 0; i < 9; i++)
                {
                    float p = Hash(i, 34) + t * 0.015f;
                    p -= Mathf.Floor(p);
                    float twinkle = 0.5f + 0.5f * Mathf.Sin(t * 1.7f + i * 2.3f);
                    Put(_shimmer, new Vector2(left - 0.5f + p * (w + 1f), (2 + 3 * (i % 3)) / Ppu), new Color(0.6f, 0.95f, 1f, 0.55f * castle * twinkle), -18);
                }

            if (env.Graveyard <= 0f || tileWidth <= 0f) return;
            for (int n = 0; n < 4; n++)
            {
                float tile = nearLeft + n * tileWidth;
                for (int s = 0; s < _graveyardStones.Length; s++)
                {
                    Vector2Int stone = _graveyardStones[s];
                    float x = tile + (stone.x + 3) / Ppu;
                    if (x < left - 0.5f || x > right + 0.5f) continue;
                    float twinkle = 0.45f + 0.55f * Mathf.Sin(t * 1.4f + s * 1.9f);
                    Put(_shimmer, new Vector2(x + 0.05f * Mathf.Sin(t * 0.7f + s), stone.y / Ppu), new Color(0.6f, 0.95f, 1f, 0.6f * env.Graveyard * twinkle), -18);
                }
            }
        }

        // A short trail behind the runner: positions sampled every TrailEvery seconds, fading with age.
        void Trail(Vector2 player, Sprite sprite, Color color, float alpha, float t)
        {
            _trailTimer += Time.unscaledDeltaTime;
            if (_trailTimer >= TrailEvery || _trailCount == 0)
            {
                _trailTimer = 0f;
                _trailHead = (_trailHead + 1) % TrailLength;
                _trail[_trailHead] = player + new Vector2(-0.15f, 0.35f + 0.2f * Mathf.Sin(t * 9f));
                if (_trailCount < TrailLength) _trailCount++;
            }
            for (int k = 0; k < _trailCount; k++)
            {
                Vector2 at = _trail[(_trailHead - k + TrailLength) % TrailLength];
                float fade = 1f - k / (float)TrailLength;
                Put(sprite, at + new Vector2(0f, 0.06f * Mathf.Sin(t * 7f + k)), new Color(color.r, color.g, color.b, alpha * fade), 9);
            }
        }

        void Put(Sprite sprite, Vector2 at, Color color, int order, float scale = 1f, bool flip = false) =>
            Put(sprite, at, color, order, new Vector2(scale, scale), flip);

        void Put(Sprite sprite, Vector2 at, Color color, int order, Vector2 scale, bool flip = false)
        {
            if (sprite == null || _used >= PoolSize || color.a <= 0.003f) return;
            SpriteRenderer sr = _pool[_used++];
            if (sr.sprite != sprite) sr.sprite = sprite;
            sr.transform.localPosition = new Vector3(SimUnits.Pixel(at.x), SimUnits.Pixel(at.y), 0f);
            sr.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            sr.flipX = flip;
            sr.color = color;
            sr.sortingOrder = order;
            if (!sr.enabled) sr.enabled = true;
        }

        // Stable per-index pseudo-random numbers in [0, 1): placement only, never the sim.
        static float Hash(int i, int salt)
        {
            uint x = (uint)(i * 374761393 + salt * 668265263);
            x = (x ^ (x >> 13)) * 1274126177u;
            return ((x ^ (x >> 16)) & 0xFFFFFF) / 16777216f;
        }
    }
}
