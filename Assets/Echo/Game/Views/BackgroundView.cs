using Echo.Game.Seasons;
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
    /// World Time Run doc, phase 3: a backdrop can bring extra layers in their own colours (Today's skyline and window
    /// light, the Far Future's sky), and in high definition every layer is lit by the Far Future's 2D lights.
    /// Sorting: sky -30, stars -24, moon and planet -23, extra -22, far -21, extra -20, near -19, extra and banners -18.
    /// </summary>
    public sealed class BackgroundView : MonoBehaviour
    {
        [SerializeField] Material _material;
        [Tooltip("Sprite-Lit-Default: the high-definition backdrop takes the Far Future's lights.")]
        [SerializeField] Material _litMaterial;
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
        const int MaxExtraLayers = 6;
        readonly SpriteRenderer[] _extra = new SpriteRenderer[MaxExtraLayers];
        // Art pass phase 3: each extra layer's animated frames, one renderer per tile it can show at once.
        const int FramesPerLayer = 4;
        readonly SpriteRenderer[] _frames = new SpriteRenderer[MaxExtraLayers * FramesPerLayer];
        // Art pass phase 4: vehicles driving along a layer, a fixed pool.
        const int MaxTraffic = 10;
        readonly SpriteRenderer[] _traffic = new SpriteRenderer[MaxTraffic];
        int _trafficLayer = -1;
        ExtraLayer[] _extraLayers;
        bool _highDefinition;
        Sprite _baseFar, _baseNear;
        bool _banners;

        /// <summary>World x where the Graveyard layer's tiles start this frame, and the tile width (for the wisps).</summary>
        public float GraveyardLeft { get; private set; }
        public float NearTileWidth => _near != null ? _near.bounds.size.x * _nearScale : 16f;

        void Awake()
        {
            _baseFar = _far;
            _baseNear = _near;
            _planetSprite = Layer("Planet", _planet, -23, false);
            _farLayer = Layer("Far", _far, -21);
            _nearLayer = Layer("Near", _near, -19);
            _castleLayer = Layer("Castle", _castle, -19);
            _spaceLayer = Layer("Space", _space, -19);
            _bannerLayer = Layer("CastleBanners", _castleBanners, -18);
            for (int i = 0; i < MaxExtraLayers; i++) _extra[i] = Layer("Extra" + i, null, -22);
            for (int i = 0; i < _frames.Length; i++) _frames[i] = Layer("Frames" + i, null, -22, false);
            for (int i = 0; i < MaxTraffic; i++) _traffic[i] = Layer("Traffic" + i, null, -22, false);
            _backing = Layer("FloorBacking", BoxSprite.Get(), -6, false);
            _backing.enabled = false;
            for (int i = 0; i < Reflected; i++) _reflections[i] = Layer("Reflection" + i, null, -5);
            _reflectionFade = Layer("ReflectionFade", _fade, -4, false);
            _reflectionFade.enabled = false;
        }

        /// <summary>A backdrop's extra layers (null or empty: none).</summary>
        public void SetExtraLayers(ExtraLayer[] layers)
        {
            _extraLayers = layers;
            _trafficLayer = -1;
            for (int i = 0; layers != null && i < layers.Length && i < MaxExtraLayers; i++)
                if (layers[i] != null && layers[i].Traffic != null && layers[i].Traffic.Length > 0 && layers[i].TrafficCount > 0) _trafficLayer = i;
            foreach (SpriteRenderer t in _traffic)
                if (t != null)
                {
                    t.enabled = false;
                    t.sortingOrder = _trafficLayer >= 0 ? layers[_trafficLayer].Order : -22;
                }
            for (int i = 0; i < MaxExtraLayers; i++)
            {
                if (_extra[i] == null) continue;
                ExtraLayer l = layers != null && i < layers.Length ? layers[i] : null;
                _extra[i].sprite = l != null ? l.Sprite : null;
                _extra[i].sortingOrder = l != null ? l.Order : -22;
                _extra[i].enabled = l != null && l.Sprite != null;
                for (int k = 0; k < FramesPerLayer; k++)
                {
                    SpriteRenderer f = _frames[i * FramesPerLayer + k];
                    if (f == null) continue;
                    f.enabled = false;
                    f.sortingOrder = l != null ? l.Order : -22;
                }
            }
        }

        // Art pass phase 5: the Far Future's wet floor. A solid backing under its glass floor (always, so reduced effects
        // still show a floor), and with full effects the city mirrored below the floor line, fading into the backing with
        // depth: the far and near layers and the drifting extra layers, upside down, at their own parallax.
        const int Reflected = MaxExtraLayers + 2;
        const float ReflectionDepth = 4f;
        static readonly Color ReflectionTint = new Color(0.8f, 0.86f, 1f, 0.9f);
        readonly SpriteRenderer[] _reflections = new SpriteRenderer[Reflected];
        SpriteRenderer _backing, _reflectionFade;
        [Tooltip("A vertical fade (transparent to opaque), stretched under the reflections so they fade with depth.")]
        [SerializeField] Sprite _fade;
        bool _floorGlass, _reflect;
        Color _floorBacking = Color.black;

        /// <summary>A glass floor with its backing, and the reflections in it (full effects only).</summary>
        public void SetFloorGlass(bool glass, bool reflections, Color backing)
        {
            _floorGlass = glass;
            _reflect = glass && reflections;
            _floorBacking = backing;
            if (_backing != null) _backing.enabled = glass;
            if (_reflectionFade != null) _reflectionFade.enabled = _reflect;
            foreach (SpriteRenderer r in _reflections) if (r != null && !_reflect) r.enabled = false;
        }

        /// <summary>Reflections drawn now (tests).</summary>
        public int ReflectionsShown
        {
            get
            {
                int n = 0;
                foreach (SpriteRenderer sr in _reflections) if (sr != null && sr.enabled) n++;
                return n;
            }
        }

        /// <summary>The solid backing under a glass floor is drawn (tests).</summary>
        public bool FloorBackingShown => _backing != null && _backing.enabled;

        // A layer's mirror image: the same tiles, upside down about the floor line.
        void Reflect(int k, SpriteRenderer source)
        {
            SpriteRenderer r = _reflections[k];
            if (r == null) return;
            r.enabled = _reflect && source != null && source.enabled && source.sprite != null;
            if (!r.enabled) return;
            r.sprite = source.sprite;
            r.drawMode = source.drawMode;
            r.size = source.size;
            r.sharedMaterial = source.sharedMaterial;
            Color c = source.color;
            r.color = new Color(c.r * ReflectionTint.r, c.g * ReflectionTint.g, c.b * ReflectionTint.b, c.a * ReflectionTint.a);
            Vector3 p = source.transform.localPosition, s = source.transform.localScale;
            r.transform.localPosition = new Vector3(p.x, -p.y - 0.06f, 0f);
            r.transform.localScale = new Vector3(s.x, -s.y, 1f);
        }

        void PlaceFloor(float cameraX, float viewWidth, float viewBottom)
        {
            if (!_floorGlass || _backing == null) return;
            float depth = Mathf.Max(1f, -viewBottom + 1f), w = viewWidth + 4f;
            _backing.transform.localPosition = new Vector3(cameraX - w / 2f, -depth, 0f);
            _backing.transform.localScale = new Vector3(w, depth, 1f);
            _backing.color = _floorBacking;
            if (!_reflect || _reflectionFade == null) return;
            Vector2 f = _fade.bounds.size;
            _reflectionFade.transform.localPosition = new Vector3(cameraX - w / 2f, -0.06f, 0f);
            _reflectionFade.transform.localScale = new Vector3(w / f.x, -ReflectionDepth / f.y, 1f);
            _reflectionFade.color = _floorBacking;
        }

        // Art pass phase 4: dusk falling across an era (Today): the scenery dims towards a shade; lights and skies don't.
        float _duskFrom, _duskTo;
        Color _duskShade = Color.white;

        public void SetDusk(float from, float to, Color shade)
        {
            _duskFrom = from;
            _duskTo = to;
            _duskShade = shade;
        }

        Color _dusk = Color.white;

        /// <summary>The dimming dusk has brought so far (tests): white by day.</summary>
        public Color Dusk => _dusk;

        static Color Dimmed(Color c, Color by) => new Color(c.r * by.r, c.g * by.g, c.b * by.b, c.a);

        /// <summary>Vehicles drawn now (tests).</summary>
        public int TrafficShown
        {
            get
            {
                int n = 0;
                foreach (SpriteRenderer sr in _traffic) if (sr != null && sr.enabled) n++;
                return n;
            }
        }

        /// <summary>How far an extra layer has faded in (tests): 1 shown, 0 hidden.</summary>
        public float ExtraAlpha(int layer) => layer >= 0 && layer < MaxExtraLayers && _extra[layer] != null && _extra[layer].enabled ? _extra[layer].color.a : 0f;

        /// <summary>Animated frames drawn now (tests).</summary>
        public int FramesShown
        {
            get
            {
                int n = 0;
                foreach (SpriteRenderer sr in _frames) if (sr != null && sr.enabled) n++;
                return n;
            }
        }

        /// <summary>The extra layers drawn now (tests).</summary>
        public int ExtraLayersShown
        {
            get
            {
                int n = 0;
                foreach (SpriteRenderer sr in _extra) if (sr != null && sr.enabled) n++;
                return n;
            }
        }

        /// <summary>High definition (the Far Future): every layer takes the 2D lights.</summary>
        public void SetHighDefinition(bool on)
        {
            _highDefinition = on;
            Material m = on && _litMaterial != null ? _litMaterial : _material;
            foreach (SpriteRenderer sr in new[] { _farLayer, _nearLayer })
                if (sr != null) sr.sharedMaterial = m;
            foreach (SpriteRenderer sr in _extra)
                if (sr != null) sr.sharedMaterial = m;
            foreach (SpriteRenderer sr in _frames)
                if (sr != null) sr.sharedMaterial = m;
            foreach (SpriteRenderer sr in _traffic)
                if (sr != null) sr.sharedMaterial = m;
        }

        /// <summary>The layers are lit (tests).</summary>
        public bool Lit => _highDefinition && _litMaterial != null && _farLayer != null && _farLayer.sharedMaterial == _litMaterial;

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

        Color _farTint = Color.clear, _nearTint = Color.clear;

        /// <summary>A world's own layer colours (a user revision); alpha 0 keeps the colours drawn from the sky.</summary>
        public void SetTints(Color far, Color near)
        {
            _farTint = far;
            _nearTint = near;
        }

        /// <summary>The RoyalBanners flourish: pennants, lit windows and torches over the Castle.</summary>
        public void SetBanners(bool on) => _banners = on;

        public void Render(float cameraX, float viewWidth, Color background) =>
            Render(cameraX, viewWidth, 0f, 16f, background, Biomes.At(0f));

        public void Render(float cameraX, float viewWidth, float viewBottom, float viewHeight, Color background, in EnvironmentState env)
        {
            // Far peaks hazy and a touch lighter than the sky, near layers darker: depth from value, 16-bit style.
            _dusk = _duskTo > _duskFrom ? Color.Lerp(Color.white, _duskShade, Mathf.Clamp01((cameraX - _duskFrom) / (_duskTo - _duskFrom))) : Color.white;
            Color far = _farTint.a > 0f ? Color.Lerp(_farTint, background, env.Night * 0.45f) : Color.Lerp(background, Color.white, 0.14f);
            far = Dimmed(far, _dusk);
            far.a = 1f - env.Space;
            Place(_farLayer, _far, cameraX, viewWidth, _farParallax, _farScale, far);

            Color near = PaletteSampler.Darker(background, 0.72f);
            Color worldNear = Dimmed(_nearTint.a > 0f ? Color.Lerp(_nearTint, background, env.Night * 0.35f) : near, _dusk);
            GraveyardLeft = Place(_nearLayer, _near, cameraX, viewWidth, _nearParallax, _nearScale, Faded(worldNear, env.Graveyard));
            Place(_castleLayer, _castle, cameraX, viewWidth, _nearParallax, _nearScale, Faded(near, env.Castle - env.Space));
            // The moon surface keeps its own grey: tinted from a near-black sky it would vanish.
            Place(_spaceLayer, _space, cameraX, viewWidth, _nearParallax, _nearScale, Faded(new Color(0.3f, 0.32f, 0.42f), env.Space));

            // Banner colours dim with the night, but the lit windows and torches are the point of it after dark.
            float banners = _banners ? env.Castle - env.Space : 0f;
            Color bannerTint = Color.Lerp(Color.white, new Color(0.75f, 0.7f, 0.65f), env.Night * 0.5f);
            bannerTint.a = banners * (0.92f + 0.08f * Mathf.Sin(Time.time * 11f));
            Place(_bannerLayer, _castleBanners, cameraX, viewWidth, _nearParallax, _nearScale, bannerTint);

            Reflect(0, _farLayer);
            Reflect(1, _nearLayer);
            PlaceFloor(cameraX, viewWidth, viewBottom);
            if (_extraLayers != null)
                for (int i = 0; i < MaxExtraLayers && i < _extraLayers.Length; i++)
                {
                    ExtraLayer l = _extraLayers[i];
                    if (l == null || l.Sprite == null) continue;
                    // World units are metres, so the camera's x is how far the run has come: a layer can fade in with it.
                    Color tint = l.Tint;
                    if (l.FadeTo > l.FadeFrom) tint.a *= Mathf.Clamp01((cameraX - l.FadeFrom) / (l.FadeTo - l.FadeFrom));
                    else if (l.Parallax > 0f) tint = Dimmed(tint, _dusk);   // scenery dims at dusk; skies and lights don't
                    float left = Place(_extra[i], l.Sprite, cameraX, viewWidth, l.Parallax, 1f, tint);
                    PlaceFrames(i, l, left, cameraX, viewWidth);
                    if (i == _trafficLayer) PlaceTraffic(l, cameraX, viewWidth);
                    // The sky stays a sky: only layers that drift are mirrored in the floor.
                    if (l.Parallax > 0f) Reflect(2 + i, _extra[i]);
                    else if (_reflections[2 + i] != null) _reflections[2 + i].enabled = false;
                }

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

        // The frames at their spot in each tile in view, a touch nearer the camera than the layer so they draw over it.
        void PlaceFrames(int layer, ExtraLayer l, float left, float cameraX, float viewWidth)
        {
            bool any = l.Frames != null && l.Frames.Length > 0;
            float tileW = l.Sprite.bounds.size.x;
            Sprite frame = any ? l.Frames[(int)(Time.time * l.FramesPerSecond) % l.Frames.Length] : null;
            for (int k = 0; k < FramesPerLayer; k++)
            {
                SpriteRenderer f = _frames[layer * FramesPerLayer + k];
                if (f == null) continue;
                float x = left + k * tileW + l.FramesAt.x;
                f.enabled = any && x > cameraX - viewWidth / 2f - 2f && x < cameraX + viewWidth / 2f + 2f;
                if (!f.enabled) continue;
                f.sprite = frame;
                f.color = l.Tint;
                f.transform.localPosition = new Vector3(SimUnits.Pixel(x), SimUnits.Pixel(l.FramesAt.y), -0.1f);
            }
        }

        // Vehicles drive along the layer: half one way, half the other, each at its own speed, wrapping round a stretch a
        // little wider than the view. They move with the layer's parallax and along it.
        void PlaceTraffic(ExtraLayer l, float cameraX, float viewWidth)
        {
            int count = Mathf.Min(l.TrafficCount, MaxTraffic);
            float span = viewWidth + 6f, drift = cameraX * l.Parallax;   // the layer slides across the screen at its parallax
            for (int i = 0; i < MaxTraffic; i++)
            {
                SpriteRenderer t = _traffic[i];
                if (t == null) continue;
                t.enabled = i < count;
                if (!t.enabled) continue;
                bool back = i % 2 == 1;
                float speed = l.TrafficSpeed * (0.7f + 0.15f * (i % 4)) * (back ? -1f : 1f);
                float x = cameraX - viewWidth / 2f - 3f + Mathf.Repeat(i * 2.9f + Time.time * speed - drift, span);
                t.sprite = l.Traffic[i % l.Traffic.Length];
                t.flipX = back;
                t.color = Dimmed(l.Tint, _dusk);
                t.transform.localPosition = new Vector3(SimUnits.Pixel(x), SimUnits.Pixel(l.TrafficY + (back ? 0.12f : 0f)), -0.05f);
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
