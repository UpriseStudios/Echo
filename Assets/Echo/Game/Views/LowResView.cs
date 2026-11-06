using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// World Time Run doc (art pass phase 2): Dino Land's backdrop and floor at half the internal resolution. While on, those
    /// renderers move to the LowRes layer, which the main camera doesn't draw; this camera draws only that layer into a
    /// texture at 16 px per unit (half the world's 32) and the main camera shows it, point filtered, behind everything at
    /// sorting order -40. So every backdrop pixel is twice the size of a gameplay pixel, and it scrolls in those bigger
    /// steps, while the runner, ghosts and hazards stay at full resolution. The texture is made again only when the view's
    /// size changes (the grave reveal's zoom), never as the run scrolls.
    /// </summary>
    public sealed class LowResView : MonoBehaviour
    {
        [SerializeField] Camera _camera;
        [SerializeField] MeshRenderer _screen;
        [SerializeField] Material _material;

        public const string LayerName = "LowRes";
        public const int PixelsPerUnit = CameraRig.PixelsPerUnit / 2;
        public const int SortingOrder = -40;

        RenderTexture _target;
        Material _shown;
        bool _on;
        static int s_layer = -2;

        /// <summary>The LowRes layer's index (the scene builder adds it to the project).</summary>
        public static int Layer => s_layer != -2 ? s_layer : s_layer = LayerMask.NameToLayer(LayerName);

        /// <summary>Drawing at half resolution now (tests).</summary>
        public bool On => _on;

        /// <summary>The texture's size in pixels (tests).</summary>
        public Vector2Int Size => _target != null ? new Vector2Int(_target.width, _target.height) : Vector2Int.zero;

        void Awake()
        {
            // A quad with white vertex colours, so the sprite shader shows the texture as it is.
            var mesh = new Mesh { name = "LowResScreen" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _screen.GetComponent<MeshFilter>().sharedMesh = mesh;
            _shown = new Material(_material);
            _screen.sharedMaterial = _shown;
            _screen.sortingOrder = SortingOrder;
            SetOn(false);
        }

        public void SetOn(bool on)
        {
            _on = on && Layer >= 0;
            if (_camera != null) _camera.enabled = _on;
            if (_screen != null) _screen.enabled = _on;
        }

        /// <summary>
        /// Follows the main camera, snapped to the half-resolution grid so the backdrop moves in whole big pixels; the
        /// texture has a pixel to spare on each side, so the snap never shows an edge.
        /// </summary>
        public void Render(Vector3 mainCamera, float viewWidth, float viewHeight, Color sky)
        {
            if (!_on) return;
            int w = Mathf.CeilToInt(viewWidth * PixelsPerUnit) + 2, h = Mathf.CeilToInt(viewHeight * PixelsPerUnit) + 2;
            bool resized = _target == null || _target.width != w || _target.height != h;
            if (resized)
            {
                if (_target != null) _target.Release();
                _target = new RenderTexture(w, h, 0) { filterMode = FilterMode.Point, name = "LowResBackdrop" };
                _camera.targetTexture = _target;
                _shown.mainTexture = _target;
            }
            float x = Mathf.Round(mainCamera.x * PixelsPerUnit) / PixelsPerUnit, y = Mathf.Round(mainCamera.y * PixelsPerUnit) / PixelsPerUnit;
            _camera.transform.position = new Vector3(x, y, -10f);
            _camera.orthographicSize = h / (float)PixelsPerUnit / 2f;
            _camera.backgroundColor = sky;
            _screen.transform.position = new Vector3(x, y, 0f);
            _screen.transform.localScale = new Vector3(w / (float)PixelsPerUnit, h / (float)PixelsPerUnit, 1f);
            // A new size (the grave reveal's zoom, a screenshot's framing) is drawn now, not a frame late.
            if (resized) _camera.Render();
        }

        /// <summary>Moves a view and its children (its renderers) onto <paramref name="layer"/>; allocates nothing.</summary>
        public static void SetLayer(Transform root, int layer)
        {
            if (root == null || layer < 0) return;
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i), layer);
        }

        void OnDestroy()
        {
            if (_target != null) _target.Release();
        }
    }
}
