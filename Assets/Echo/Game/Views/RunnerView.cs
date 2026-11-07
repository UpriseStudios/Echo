using Echo.Core.Sim;
using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// The player: the equipped character, animated by distance so the stride matches the speed, a tucked pose
    /// in the air, view-only squash and stretch on jump and land (tech spec 11), and a scarf (or cape, tail, hair)
    /// that trails and flutters behind. Interpolated between ticks; never writes sim state.
    /// </summary>
    public sealed class RunnerView : MonoBehaviour
    {
        const float Stretch = 0.15f;
        const float Recover = 12f;
        const float StrideUnits = 0.42f;
        const int ScarfPoints = 9;
        const float ScarfSegment = 0.075f;

        [SerializeField] Material _material;
        [SerializeField] Material _scarfMaterial;
        [SerializeField] Sprite[] _runFrames = new Sprite[0];
        [SerializeField] Sprite _jumpFrame;
        [SerializeField] Color _scarfColor = new Color(1f, 0.78f, 0.28f);
        [SerializeField] float _scarfWidth = 0.09f;
        [SerializeField] Vector2 _scarfAnchor = new Vector2(-0.03f, 0.53f);
        [Tooltip("World Time Run doc, phase 3: the soft glow behind the runner in the Far Future.")]
        [SerializeField] Sprite _glow;
        [Tooltip("Art pass phase 6: a soft blob shadow on the floor in the Far Far Future.")]
        [SerializeField] Sprite _shadowSprite;
        SpriteRenderer _shadow;
        bool _shadowOn;

        /// <summary>The blob shadow under the runner on or off.</summary>
        public void SetShadow(bool on)
        {
            _ = Body;
            _shadowOn = on && _shadowSprite != null;
            _shadow.enabled = _shadowOn;
        }

        /// <summary>The runner's shadow is drawn (tests).</summary>
        public bool ShadowShown => _shadow != null && _shadow.enabled;

        SpriteRenderer _body;
        LineRenderer _scarf;
        readonly Vector3[] _scarfPositions = new Vector3[ScarfPoints];
        bool _scarfPlaced;
        Vector2 _deform = Vector2.one;

        public Vector2 Position { get; private set; }

        // World Time Run doc, phase 3: in the Far Future the pixel runner isn't redrawn but gets modern treatment: a soft
        // glow, rim light from the neon on both edges (a magenta and a cyan copy a pixel either side, behind the body) and a
        // smooth motion trail of fading afterimages. All made with the body, so nothing is created mid-run.
        const int Afterimages = 4;
        const float AfterimageEvery = 1f / 30f;
        const float GlowUnits = 1.5f;
        static readonly Color RimTrailing = new Color(1f, 0.25f, 0.85f, 0.9f);
        static readonly Color RimLeading = new Color(0.3f, 0.95f, 1f, 0.9f);
        static readonly Color AfterimageColour = new Color(0.35f, 0.9f, 1f, 1f);
        bool _future;
        float _afterClock;
        SpriteRenderer _futureGlow, _rimTrailing, _rimLeading;
        readonly SpriteRenderer[] _after = new SpriteRenderer[Afterimages];
        readonly Vector3[] _afterAt = new Vector3[Afterimages];
        readonly Sprite[] _afterSprite = new Sprite[Afterimages];

        /// <summary>The Far Future's treatment on or off.</summary>
        public void SetFuture(bool on)
        {
            _ = Body;
            _future = on;
            _futureGlow.enabled = _rimTrailing.enabled = _rimLeading.enabled = on;
            for (int i = 0; i < Afterimages; i++)
            {
                _after[i].enabled = false;
                _afterSprite[i] = null;
            }
        }

        /// <summary>The Far Future's treatment is on (tests).</summary>
        public bool Future => _future;

        SpriteRenderer Extra(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = _material;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        // Modes doc (a user decision): an accessory worn on the head, e.g. the Halo, moving and blinking with the body.
        SpriteRenderer _accessory;
        float _headTop;

        public void SetAccessory(Sprite sprite, float headTop)
        {
            if (_accessory == null)
            {
                var go = new GameObject("Accessory");
                go.transform.SetParent(transform, false);
                _accessory = go.AddComponent<SpriteRenderer>();
                _accessory.sharedMaterial = _material;
                _accessory.sortingOrder = 11;
            }
            _accessory.sprite = sprite;
            _accessory.enabled = sprite != null;
            _headTop = headTop;
        }

        // Created on first use: Awake order across objects is not guaranteed.
        SpriteRenderer Body
        {
            get
            {
                if (_body != null) return _body;
                var go = new GameObject("Runner");
                go.transform.SetParent(transform, false);
                _body = go.AddComponent<SpriteRenderer>();
                _body.sharedMaterial = _material;
                _body.sortingOrder = 10;
                _body.sprite = _runFrames.Length > 0 ? _runFrames[0] : BoxSprite.Get();
                _futureGlow = Extra("FutureGlow", 7);
                _futureGlow.sprite = _glow;
                _rimTrailing = Extra("RimTrailing", 8);
                _rimLeading = Extra("RimLeading", 8);
                for (int i = 0; i < Afterimages; i++) _after[i] = Extra("Afterimage" + i, 7);
                _shadow = Extra("Shadow", 1);
                _shadow.sprite = _shadowSprite;
                return _body;
            }
        }

        LineRenderer Scarf
        {
            get
            {
                if (_scarf != null) return _scarf;
                var go = new GameObject("Scarf");
                go.transform.SetParent(transform, false);
                _scarf = go.AddComponent<LineRenderer>();
                _scarf.sharedMaterial = _scarfMaterial != null ? _scarfMaterial : _material;
                _scarf.useWorldSpace = true;
                _scarf.positionCount = ScarfPoints;
                _scarf.numCapVertices = 3;
                _scarf.sortingOrder = 9;
                _scarf.widthCurve = new AnimationCurve(new Keyframe(0f, _scarfWidth), new Keyframe(1f, _scarfWidth * 0.45f));
                _scarf.startColor = _scarf.endColor = _scarfColor;
                return _scarf;
            }
        }

        /// <summary>Switches character: its frames, a tint (white unless the character is tintable) and its scarf.</summary>
        public void SetCharacter(Sprite[] runFrames, Sprite jumpFrame, Color tint, Color scarfColor, float scarfWidth, Vector2 scarfAnchor)
        {
            if (runFrames != null && runFrames.Length > 0) _runFrames = runFrames;
            if (jumpFrame != null) _jumpFrame = jumpFrame;
            _scarfColor = scarfColor;
            _scarfWidth = scarfWidth;
            _scarfAnchor = scarfAnchor;
            _scarfPlaced = false;
            Body.color = tint;
            Body.sprite = _runFrames.Length > 0 ? _runFrames[0] : BoxSprite.Get();
            Scarf.startColor = scarfColor;
            Scarf.endColor = scarfColor;
            Scarf.widthCurve = new AnimationCurve(new Keyframe(0f, scarfWidth), new Keyframe(1f, scarfWidth * 0.45f));
        }

        public void OnJump() => _deform = new Vector2(1f - Stretch, 1f + Stretch);
        public void OnLand() => _deform = new Vector2(1f + Stretch, 1f - Stretch);

        public void Render(in RunnerState previous, in RunnerState current, float alpha, in SimConfig cfg)
        {
            float dt = Time.unscaledDeltaTime;
            _deform = Vector2.Lerp(_deform, Vector2.one, 1f - Mathf.Exp(-Recover * dt));
            Position = SimUnits.Lerp(previous, current, alpha);

            SpriteRenderer body = Body;
            body.sprite = Frame(current.Grounded, Position.x);
            body.transform.localPosition = new Vector3(SimUnits.Pixel(Position.x), SimUnits.Pixel(Position.y), 0f);
            body.transform.localScale = new Vector3(_deform.x, _deform.y, 1f);
            // Blinks while invulnerable after a revive (Continue or 1-UP), as old platformers did.
            body.enabled = current.Alive && (current.Invuln == 0 || current.Invuln / 4 % 2 == 0);
            if (_accessory != null && _accessory.sprite != null)
            {
                _accessory.enabled = body.enabled;
                _accessory.transform.localPosition = new Vector3(SimUnits.Pixel(Position.x), SimUnits.Pixel(Position.y + _headTop * _deform.y), 0f);
            }

            UpdateScarf(Position + new Vector2(_scarfAnchor.x * _deform.x, _scarfAnchor.y * _deform.y), current.Alive, dt);
            if (_future) RenderFuture(body, dt);
            if (_shadowOn) BlobShadow.Place(_shadow, Position.x, Position.y, body.enabled ? 1f : 0f);
        }

        void RenderFuture(SpriteRenderer body, float dt)
        {
            Vector3 at = body.transform.localPosition, scale = body.transform.localScale;
            float px = 1f / CameraRig.PixelsPerUnit;
            Rim(_rimTrailing, body, at + new Vector3(-px, 0f, 0f), scale, RimTrailing);
            Rim(_rimLeading, body, at + new Vector3(px, 0f, 0f), scale, RimLeading);
            if (_glow != null)
            {
                Vector2 g = _glow.bounds.size;
                _futureGlow.enabled = body.enabled;
                _futureGlow.transform.localPosition = at + new Vector3(0f, body.bounds.size.y * 0.5f, 0f);
                _futureGlow.transform.localScale = new Vector3(GlowUnits / g.x, GlowUnits / g.y, 1f);
                _futureGlow.color = new Color(0.45f, 0.85f, 1f, 0.3f);
            }

            // The trail: where the body was, every 1/30 s, oldest faintest.
            _afterClock += dt;
            if (_afterClock >= AfterimageEvery)
            {
                _afterClock = 0f;
                for (int i = Afterimages - 1; i > 0; i--)
                {
                    _afterAt[i] = _afterAt[i - 1];
                    _afterSprite[i] = _afterSprite[i - 1];
                }
                _afterAt[0] = at;
                _afterSprite[0] = body.enabled ? body.sprite : null;
            }
            for (int i = 0; i < Afterimages; i++)
            {
                SpriteRenderer a = _after[i];
                a.enabled = _afterSprite[i] != null && body.enabled;
                if (!a.enabled) continue;
                a.sprite = _afterSprite[i];
                a.transform.localPosition = _afterAt[i];
                a.transform.localScale = scale;
                Color c = AfterimageColour;
                c.a = 0.32f * (1f - i / (float)Afterimages);
                a.color = c;
            }
        }

        static void Rim(SpriteRenderer rim, SpriteRenderer body, Vector3 at, Vector3 scale, Color colour)
        {
            rim.enabled = body.enabled;
            rim.sprite = body.sprite;
            rim.transform.localPosition = at;
            rim.transform.localScale = scale;
            rim.color = colour;
        }

        Sprite Frame(bool grounded, float x)
        {
            if (_runFrames.Length == 0) return BoxSprite.Get();
            if (!grounded && _jumpFrame != null) return _jumpFrame;
            int i = (int)(Mathf.Abs(x) / StrideUnits) % _runFrames.Length;
            return _runFrames[i];
        }

        // Each point hangs a fixed distance behind the previous one, pulled back by the run and lifted by a wave.
        void UpdateScarf(Vector2 anchor, bool alive, float dt)
        {
            LineRenderer scarf = Scarf;
            scarf.enabled = alive && _scarfWidth > 0f;
            if (!scarf.enabled) return;

            if (!_scarfPlaced)
            {
                for (int i = 0; i < ScarfPoints; i++) _scarfPositions[i] = anchor + new Vector2(-ScarfSegment * i, 0f);
                _scarfPlaced = true;
            }

            float time = Time.time;
            _scarfPositions[0] = anchor;
            for (int i = 1; i < ScarfPoints; i++)
            {
                Vector2 prev = _scarfPositions[i - 1];
                Vector2 cur = _scarfPositions[i];
                float wave = Mathf.Sin(time * 14f - i * 0.9f) * 0.35f * i / ScarfPoints;
                Vector2 wind = new Vector2(-1f, 0.18f + wave).normalized;
                Vector2 dir = ((cur - prev).normalized + wind * 2.5f).normalized;
                _scarfPositions[i] = prev + dir * ScarfSegment;
            }
            scarf.SetPositions(_scarfPositions);
        }
    }
}
