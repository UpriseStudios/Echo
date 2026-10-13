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

        SpriteRenderer _body;
        LineRenderer _scarf;
        readonly Vector3[] _scarfPositions = new Vector3[ScarfPoints];
        bool _scarfPlaced;
        Vector2 _deform = Vector2.one;

        public Vector2 Position { get; private set; }

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

            UpdateScarf(Position + new Vector2(_scarfAnchor.x * _deform.x, _scarfAnchor.y * _deform.y), current.Alive, dt);
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
