using UnityEngine;
using UnityEngine.Video;

namespace Echo.Game.Views
{
    /// <summary>
    /// World Time Run doc (art pass phase 6): shows the Far Far Future's 3D city behind the 2D gameplay. Its own perspective
    /// camera (the Universal renderer, on the Far3D layer only) renders the city into an HDR texture at a fraction of the
    /// screen's resolution, following the run's distance; the main camera shows the texture on a quad behind everything
    /// (sorting order -40), so the gameplay stays on its 2D plane and the main bloom catches the city's lights. Reduced
    /// effects play a pre-rendered loop of the same scene into the same quad instead. A render texture rather than camera
    /// stacking: the 3D scene needs the Universal renderer while the game runs on the 2D renderer (a stack shares one
    /// renderer type, and the pixel-perfect camera doesn't stack), and a texture lets the city render below screen
    /// resolution and lets the video stand in for it on the same quad.
    /// </summary>
    public sealed class Far3DView : MonoBehaviour
    {
        public enum Mode { Off, RealTime, Video }

        [SerializeField] Camera _camera;
        [SerializeField] Far3DScene _scene;
        [SerializeField] MeshRenderer _screen;
        [SerializeField] Material _material;
        [SerializeField] VideoPlayer _video;
        [SerializeField] VideoClip _loop;
        [Tooltip("The city's texture against the screen's size: below 1 renders it cheaper.")]
        [SerializeField] float _resolution = 0.6f;

        public const string LayerName = "Far3D";
        public const int SortingOrder = -40;

        RenderTexture _target, _videoTarget;
        Material _shown;
        Mode _mode = Mode.Off;

        /// <summary>What's showing (tests).</summary>
        public Mode Showing => _mode;

        /// <summary>The city's texture size in pixels (tests).</summary>
        public Vector2Int Size => _target != null ? new Vector2Int(_target.width, _target.height) : Vector2Int.zero;

        /// <summary>A video loop is set up for reduced effects.</summary>
        public bool HasLoop => _loop != null;

        void Awake()
        {
            var mesh = new Mesh { name = "Far3DScreen" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _screen.GetComponent<MeshFilter>().sharedMesh = mesh;
            _shown = new Material(_material);
            _screen.sharedMaterial = _shown;
            _screen.sortingOrder = SortingOrder;
            if (_scene != null) _scene.Build();
            SetMode(Mode.Off);
        }

        /// <summary>Real time, the video loop (reduced effects; real time if there is no loop), or off.</summary>
        public void SetMode(Mode mode)
        {
            if (mode == Mode.Video && _loop == null) mode = Mode.RealTime;
            _mode = mode;
            if (_camera != null) _camera.enabled = mode == Mode.RealTime;
            if (_screen != null) _screen.enabled = mode != Mode.Off;
            if (_scene != null) _scene.gameObject.SetActive(mode == Mode.RealTime);
            if (_video == null) return;
            if (mode == Mode.Video)
            {
                if (_videoTarget == null)
                {
                    _videoTarget = new RenderTexture((int)_loop.width, (int)_loop.height, 0) { name = "Far3DLoop" };
                    _video.targetTexture = _videoTarget;
                }
                _video.clip = _loop;
                _video.isLooping = true;
                _video.Play();
                _shown.mainTexture = _videoTarget;
            }
            else
            {
                _video.Stop();
                if (_target != null) _shown.mainTexture = _target;
            }
        }

        /// <summary>
        /// Each frame: the quad covers the view behind the gameplay, and in real time the camera follows the run's
        /// distance and the traffic moves. The texture is made again only when the view's size changes.
        /// </summary>
        public void Render(Vector3 mainCamera, float viewWidth, float viewHeight, float metres, float time)
        {
            if (_mode == Mode.Off) return;
            _screen.transform.position = new Vector3(mainCamera.x, mainCamera.y, 0f);
            _screen.transform.localScale = new Vector3(viewWidth, viewHeight, 1f);
            if (_mode != Mode.RealTime) return;
            int w = Mathf.Max(64, Mathf.RoundToInt(Screen.width * _resolution)), h = Mathf.Max(64, Mathf.RoundToInt(w * viewHeight / viewWidth));
            bool resized = _target == null || _target.width != w || _target.height != h;
            if (resized)
            {
                if (_target != null) _target.Release();
                _target = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR) { name = "Far3DCity" };
                _camera.targetTexture = _target;
                _shown.mainTexture = _target;
            }
            Far3DScene.CameraAt(metres * Far3DScene.UnitsPerMetre, out Vector3 at, out Quaternion look);
            _camera.transform.SetPositionAndRotation(at, look);
            _scene.Follow(at);
            _scene.Animate(time);
            // A new size (the grave reveal's zoom, a screenshot's framing) is drawn now, not a frame late.
            if (resized) _camera.Render();
        }

        void OnDestroy()
        {
            if (_target != null) _target.Release();
            if (_videoTarget != null) _videoTarget.Release();
        }
    }
}
