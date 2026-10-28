using Echo.Game.World;
using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// The parallax pixel-art layers behind the course, tinted from the background hue so the whole scene shifts
    /// colour together (game spec: one bold background colour, world a shade darker). Drawn at their native 32 px
    /// per unit and snapped to the pixel grid.
    /// Themed Biomes (value-adds doc): the near layer crossfades Graveyard, Castle and Space as the run goes on,
    /// the far mountains fade out in Space where a planet hangs in the sky instead, and the Castle can fly the
    /// royal banners (a character flourish).
    /// </summary>
    public sealed class BackgroundView : MonoBehaviour
    {
        [SerializeField] Material _material;
        [SerializeField] Sprite _far;
        [SerializeField] Sprite _near;
        [SerializeField] Sprite _castle;
        [SerializeField] Sprite _castleBanners;
        [SerializeField] Sprite _space;
        [SerializeField] Sprite _planet;
        [SerializeField] float _farParallax = 0.15f;
        [SerializeField] float _nearParallax = 0.4f;
        [SerializeField] float _farScale = 1f;
        [SerializeField] float _nearScale = 1f;

        SpriteRenderer _farLayer, _nearLayer, _castleLayer, _bannerLayer, _spaceLayer, _planetSprite;
        Sprite _baseFar, _baseNear;
        bool _banners;

        /// <summary>World x where the Graveyard layer's tiles start this frame, and the tile width (for the wisps).</summary>
        public float GraveyardLeft { get; private set; }
        public float NearTileWidth => _near != null ? _near.bounds.size.x * _nearScale : 16f;

        void Awake()
        {
            _baseFar = _far;
            _baseNear = _near;
            _planetSprite = Layer("Planet", _planet, -21, false);
            _farLayer = Layer("Far", _far, -20);
            _nearLayer = Layer("Near", _near, -19);
            _castleLayer = Layer("Castle", _castle, -19);
            _spaceLayer = Layer("Space", _space, -19);
            _bannerLayer = Layer("CastleBanners", _castleBanners, -18);
        }

        SpriteRenderer Layer(string name, Sprite sprite, int order, bool tiled = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = _material;
            sr.sprite = sprite;
            if (tiled) sr.drawMode = SpriteDrawMode.Tiled;
            sr.sortingOrder = order;
            sr.enabled = sprite != null;
            return sr;
        }

        /// <summary>A season's far and Graveyard layers (seasons doc); null keeps the normal art.</summary>
        public void SetSeason(Sprite far, Sprite near)
        {
            _far = far != null ? far : _baseFar;
            _near = near != null ? near : _baseNear;
            if (_farLayer != null) _farLayer.sprite = _far;
            if (_nearLayer != null) _nearLayer.sprite = _near;
        }

        /// <summary>The RoyalBanners flourish: pennants, lit windows and torches over the Castle.</summary>
        public void SetBanners(bool on) => _banners = on;

        public void Render(float cameraX, float viewWidth, Color background) =>
            Render(cameraX, viewWidth, 0f, 16f, background, Biomes.At(0f));

        public void Render(float cameraX, float viewWidth, float viewBottom, float viewHeight, Color background, in EnvironmentState env)
        {
            // Far peaks hazy and a touch lighter than the sky, near layers darker: depth from value, 16-bit style.
            Color far = Color.Lerp(background, Color.white, 0.14f);
            far.a = 1f - env.Space;
            Place(_farLayer, _far, cameraX, viewWidth, _farParallax, _farScale, far);

            Color near = PaletteSampler.Darker(background, 0.72f);
            GraveyardLeft = Place(_nearLayer, _near, cameraX, viewWidth, _nearParallax, _nearScale, Faded(near, env.Graveyard));
            Place(_castleLayer, _castle, cameraX, viewWidth, _nearParallax, _nearScale, Faded(near, env.Castle - env.Space));
            // The moon surface keeps its own grey: tinted from a near-black sky it would vanish.
            Place(_spaceLayer, _space, cameraX, viewWidth, _nearParallax, _nearScale, Faded(new Color(0.3f, 0.32f, 0.42f), env.Space));

            // Banner colours dim with the night, but the lit windows and torches are the point of it after dark.
            float banners = _banners ? env.Castle - env.Space : 0f;
            Color bannerTint = Color.Lerp(Color.white, new Color(0.75f, 0.7f, 0.65f), env.Night * 0.5f);
            bannerTint.a = banners * (0.92f + 0.08f * Mathf.Sin(Time.time * 11f));
            Place(_bannerLayer, _castleBanners, cameraX, viewWidth, _nearParallax, _nearScale, bannerTint);

            if (_planetSprite != null && _planet != null)
            {
                _planetSprite.enabled = env.Space > 0f;
                // Almost fixed in the sky (it is very far away), high on the right.
                float x = cameraX + viewWidth * 0.22f;
                float y = viewBottom + viewHeight * 0.78f;
                _planetSprite.transform.localPosition = new Vector3(SimUnits.Pixel(x), SimUnits.Pixel(y), 0f);
                _planetSprite.color = new Color(1f, 1f, 1f, env.Space);
            }
        }

        static Color Faded(Color c, float alpha)
        {
            c.a = Mathf.Clamp01(alpha);
            return c;
        }

        // Returns the world x where the layer's first tile starts.
        static float Place(SpriteRenderer layer, Sprite sprite, float cameraX, float viewWidth, float parallax, float scale, Color tint)
        {
            if (sprite == null || layer == null) return 0f;
            layer.enabled = tint.a > 0.001f;
            float tileW = sprite.bounds.size.x * scale;
            float tileH = sprite.bounds.size.y * scale;
            // The layer drifts at (1 - parallax) of the camera; snap it so a tile edge never enters the view.
            float drift = cameraX * parallax;
            float offset = drift - Mathf.Floor(drift / tileW) * tileW;
            var t = layer.transform;
            float left = SimUnits.Pixel(cameraX - viewWidth / 2f - tileW - offset);
            t.localScale = new Vector3(scale, scale, 1f);
            t.localPosition = new Vector3(left, 0f, 0f);
            layer.size = new Vector2((viewWidth + 3f * tileW) / scale, tileH / scale);
            layer.color = tint;
            return left;
        }
    }
}
