using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Echo.Game.Views
{
    /// <summary>
    /// Orthographic camera 9 units wide, player held 25% from the left edge, fixed height (tech spec 11).
    /// With the pixel-perfect camera the world renders at 288 x (288 / aspect) px and is stretched to the
    /// screen, so every phone sees exactly 9 units across on a 32-px-per-unit grid.
    /// The death screen can zoom in on a spot (a user revision: the grave reveal): the pixel-perfect camera's
    /// pixels per unit rise with the zoom, so it settles on a whole multiple and the art stays crisp.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        public const float WidthUnits = 9f;
        public const int PixelsPerUnit = 32;
        public const int PixelsAcross = (int)(WidthUnits * PixelsPerUnit);
        const float PlayerFromLeft = 0.25f;
        const float FloorFromBottom = 0.3f;

        Camera _camera;
        bool _mirror;
        PixelPerfectCamera _pixel;

        // Zoom towards a spot, eased in over _focusSeconds after _focusDelay; 1 = following the player.
        Vector2 _focusAt;
        float _focusZoom = 1f, _focusGroundY, _focusDelay, _focusSeconds, _focusTime = -1f;

        /// <summary>How far the zoom has eased in, 0 (following the player) to 1 (on the spot).</summary>
        public float FocusAmount { get; private set; }

        public float Left { get; private set; }
        public float Right { get; private set; }
        public float Bottom { get; private set; }
        public float Height { get; private set; } = 16f;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _pixel = GetComponent<PixelPerfectCamera>();
        }

        /// <summary>
        /// Eases the view in on <paramref name="spot"/> (world units, centred across) by <paramref name="zoom"/>,
        /// with the ground (y = 0) at <paramref name="groundViewportY"/> of the screen height.
        /// </summary>
        public void FocusOn(Vector2 spot, int zoom, float groundViewportY, float delay, float seconds)
        {
            _focusAt = spot;
            _focusZoom = Mathf.Max(1, zoom);
            _focusGroundY = groundViewportY;
            _focusDelay = delay;
            _focusSeconds = Mathf.Max(0.01f, seconds);
            _focusTime = 0f;
        }

        /// <summary>A world rectangle in viewport units (0 to 1) as the camera sees it now; empty stays empty.</summary>
        public Rect ToViewport(Rect world)
        {
            if (world.width <= 0f || _camera == null) return Rect.zero;
            Vector3 a = _camera.WorldToViewportPoint(world.min), b = _camera.WorldToViewportPoint(world.max);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>Straight back to following the player.</summary>
        public void ClearFocus()
        {
            _focusTime = -1f;
            FocusAmount = 0f;
        }

        public void Follow(float playerX)
        {
            if (_focusTime >= 0f)
            {
                _focusTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01((_focusTime - _focusDelay) / _focusSeconds);
                FocusAmount = t * t * (3f - 2f * t);
            }
            float e = FocusAmount;
            float zoom = Mathf.Lerp(1f, _focusZoom, e);
            int ppu = Mathf.RoundToInt(PixelsPerUnit * zoom);
            zoom = ppu / (float)PixelsPerUnit;

            float aspect = Mathf.Max(TargetAspect(), 0.01f);
            float width = WidthUnits / zoom;
            float height = width / aspect;
            _camera.orthographicSize = height / 2f;
            if (_pixel != null)
            {
                int rows = Mathf.Max(2, Mathf.RoundToInt(PixelsAcross / aspect) / 2 * 2);
                if (_pixel.refResolutionY != rows) _pixel.refResolutionY = rows;
                if (_pixel.assetsPPU != ppu) _pixel.assetsPPU = ppu;
                height = rows / (float)ppu;
            }

            float x = Mathf.Lerp(playerX + WidthUnits * (0.5f - PlayerFromLeft), _focusAt.x, e);
            float y = height * (0.5f - Mathf.Lerp(FloorFromBottom, _focusGroundY, e));
            Vector2 shake = Vector2.zero;
            if (_shakeLeft > 0f)
            {
                _shakeLeft -= Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_shakeLeft / _shakeSeconds);
                shake = Random.insideUnitCircle * (_shakeStrength * t * t);
            }
            // Snapped to whole pixels so the scenery never shimmers between them.
            transform.position = new Vector3(Snap(x + shake.x, ppu), Snap(y + shake.y, ppu), -10f);
            Left = x - width / 2f;
            Right = x + width / 2f;
            Bottom = y - height / 2f;
            Height = height;
            if (_mirror)
            {
                // Mirror twist: flip the projection, so everything the camera draws (trails and scarves too) runs
                // right to left while the sim is untouched. Rebuilt each frame from the current size.
                _camera.ResetProjectionMatrix();
                _camera.projectionMatrix = _camera.projectionMatrix * Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
            }
        }

        static float Snap(float v, int ppu) => Mathf.Round(v * ppu) / ppu;

        // The screen, or the texture the camera renders into (screenshots).
        float TargetAspect()
        {
            RenderTexture target = _camera.targetTexture;
            return target != null ? (float)target.width / target.height : (float)Screen.width / Mathf.Max(1, Screen.height);
        }

        float _shakeLeft, _shakeSeconds, _shakeStrength;

        /// <summary>A short, decaying view-only shake (on death). The sim never sees it.</summary>
        public void Shake(float strength, float seconds)
        {
            _shakeStrength = strength;
            _shakeSeconds = seconds;
            _shakeLeft = seconds;
        }

        /// <summary>The Mirror twist (value-adds doc): the course runs right to left on screen.</summary>
        public bool Mirror
        {
            get => _mirror;
            set
            {
                if (_mirror == value) return;
                _mirror = value;
                if (!value && _camera != null) _camera.ResetProjectionMatrix();
            }
        }

        /// <summary>Bloom and the other post effects; the Glow setting turns them off for low-end phones.</summary>
        public void SetGlow(bool on)
        {
            var data = GetComponent<UniversalAdditionalCameraData>();
            if (data != null) data.renderPostProcessing = on;
        }

        public Color Background
        {
            get => _camera.backgroundColor;
            set => _camera.backgroundColor = value;
        }
    }
}
