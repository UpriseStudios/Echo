using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// Time Run's time rift (World Time Run doc): at each era boundary a flash and bright ripples sweeping across the screen.
    /// Drawn behind the floor, the runner and the ghosts (sorting order between the scenery and the course), so it never
    /// hides them or an obstacle. Art pass phase 7: each rift is bigger than the last (longer, brighter, more and wider
    /// ripples), and the last, into the Far Far Future, breaks the screen open: the Far Future's skyline cracks into shards
    /// that burst towards you and fall away, showing the 3D city behind. Everything is made in Awake (the shards are one mesh
    /// whose vertices are rewritten each frame), so a rift allocates nothing while it plays.
    /// </summary>
    public sealed class TimeRiftView : MonoBehaviour
    {
        /// <summary>The first rift's length; later ones run longer (<see cref="SecondsFor"/>).</summary>
        public const float Seconds = 0.3f;
        public const int SortingOrder = -16;
        /// <summary>The rift that breaks open into 3D (into the fifth era).</summary>
        public const int ShatterLevel = 4;
        const float CoverTop = 16f, CoverBottom = -6f;

        [SerializeField] SpriteRenderer _flash;
        [SerializeField] SpriteRenderer _ripple;
        [SerializeField] SpriteRenderer _rippleBack;
        [Tooltip("The Far Future's skyline as one 9 x 16-unit picture (floor line 28.6% up), for the shards.")]
        [SerializeField] Sprite _shatter;

        // The shards: a jittered grid over the view, two triangles a cell, each triangle its own three vertices.
        const int Cols = 6, Rows = 11, Shards = Cols * Rows * 2;
        // The skyline picture's world height and how far below the floor line it starts.
        const float PictureHeight = 16f, PictureBelow = 16f * 293f / 1024f;
        MeshRenderer _shards;
        Mesh _mesh;
        Material _shardMaterial;
        readonly Vector2[] _rest = new Vector2[Shards * 3];
        readonly Vector2[] _centre = new Vector2[Shards];
        readonly float[] _spin = new float[Shards];
        readonly Vector3[] _vertices = new Vector3[Shards * 3];
        readonly Vector2[] _uv = new Vector2[Shards * 3];
        readonly Color32[] _colours = new Color32[Shards * 3];

        float _left = -1f, _length = Seconds;
        int _level = 1;

        /// <summary>Playing now (tests).</summary>
        public bool Playing => _left > 0f;

        /// <summary>The rift playing or last played (tests): 1 into the second era ... 4 into the fifth.</summary>
        public int Level => _level;

        /// <summary>The shards are drawn now (tests).</summary>
        public bool Shattering => _shards != null && _shards.enabled;

        /// <summary>How long the rift into era <paramref name="level"/> + 1 lasts.</summary>
        public static float SecondsFor(int level) => level >= ShatterLevel ? 1.3f : Seconds + 0.15f * (Mathf.Clamp(level, 1, 3) - 1);

        static float Hash01(int a, int b) => (((uint)(a * 73856093) ^ (uint)(b * 19349663)) % 10000u) / 10000f;

        void Awake()
        {
            _flash.enabled = _ripple.enabled = false;
            if (_rippleBack != null) _rippleBack.enabled = false;
            if (_shatter == null) return;

            // A jittered grid in the unit square: the edges stay straight so the shards cover the view.
            var points = new Vector2[(Cols + 1) * (Rows + 1)];
            for (int y = 0; y <= Rows; y++)
                for (int x = 0; x <= Cols; x++)
                {
                    float jx = x > 0 && x < Cols ? (Hash01(x, y + 50) - 0.5f) * 0.7f : 0f;
                    float jy = y > 0 && y < Rows ? (Hash01(x + 50, y) - 0.5f) * 0.7f : 0f;
                    points[y * (Cols + 1) + x] = new Vector2((x + jx) / Cols, (y + jy) / Rows);
                }
            int s = 0;
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Cols; x++)
                {
                    Vector2 a = points[y * (Cols + 1) + x], b = points[y * (Cols + 1) + x + 1];
                    Vector2 c = points[(y + 1) * (Cols + 1) + x], d = points[(y + 1) * (Cols + 1) + x + 1];
                    bool flip = Hash01(x, y) > 0.5f;   // the diagonal both ways, so the cracks don't line up
                    AddShard(s++, a, flip ? d : b, c);
                    AddShard(s++, flip ? a : b, d, flip ? b : c);
                }
            var triangles = new int[Shards * 3];
            for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
            _mesh = new Mesh { name = "RiftShards" };
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.uv = _uv;
            _mesh.colors32 = _colours;
            _mesh.triangles = triangles;
            var go = new GameObject("Shards");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _shards = go.AddComponent<MeshRenderer>();
            _shardMaterial = new Material(_flash.sharedMaterial) { name = "RiftShards", mainTexture = _shatter.texture };
            _shards.sharedMaterial = _shardMaterial;
            _shards.sortingOrder = SortingOrder;
            _shards.enabled = false;
        }

        void AddShard(int s, Vector2 a, Vector2 b, Vector2 c)
        {
            _rest[s * 3] = a;
            _rest[s * 3 + 1] = b;
            _rest[s * 3 + 2] = c;
            _centre[s] = (a + b + c) / 3f;
            _spin[s] = (Hash01(s, 7) - 0.5f) * 5f;
        }

        void OnDestroy()
        {
            if (_shardMaterial != null) Destroy(_shardMaterial);
            if (_mesh != null) Destroy(_mesh);
        }

        /// <summary>The rift into era <paramref name="level"/> + 1 (1 into the second era, 4 into the fifth).</summary>
        public void Play(int level)
        {
            _level = Mathf.Max(1, level);
            _length = SecondsFor(_level);
            _left = _length;
            // The breaking open is the shards' alone: no ripples over it.
            _flash.enabled = true;
            _ripple.enabled = _level < ShatterLevel;
            if (_rippleBack != null) _rippleBack.enabled = _level == 3;
            if (_shards != null) _shards.enabled = _level >= ShatterLevel;
        }

        public void Play() => Play(1);

        /// <summary>Each frame, with the camera's view.</summary>
        public void Render(float left, float right, float bottom, float height)
        {
            if (_left <= 0f) return;
            _left -= Time.unscaledDeltaTime;
            if (_left <= 0f)
            {
                _flash.enabled = _ripple.enabled = false;
                if (_rippleBack != null) _rippleBack.enabled = false;
                if (_shards != null) _shards.enabled = false;
                return;
            }
            // At least from well under the floor to above the tallest phone's view: the view can change shape between frames
            // (a resized window, a screenshot's framing), and a rift sized to an old view would leave part of the screen.
            float top = Mathf.Max(bottom + height, CoverTop);
            bottom = Mathf.Min(bottom, CoverBottom);
            height = top - bottom;
            float t = 1f - _left / _length;
            float width = right - left;
            int level = Mathf.Min(_level, 3);
            Vector2 flashSize = _flash.sprite.bounds.size;
            _flash.transform.position = new Vector3((left + right) * 0.5f, bottom + height * 0.5f, 0f);
            _flash.transform.localScale = new Vector3(width / flashSize.x, height / flashSize.y, 1f);
            float flash = _level >= ShatterLevel ? 0.6f * Mathf.Clamp01(1f - t * 10f) : (0.35f + 0.12f * level) * (1f - t) * (1f - t);
            _flash.color = new Color(1f, 1f, 1f, flash);

            // A ripple sweeping across, wider and brighter each rift; from the third a second one sweeps back.
            if (_ripple.enabled)
                Ripple(_ripple, Mathf.Lerp(left - 2f, right + 2f, t), bottom, height, 2f + 1f * level, t, new Color(0.8f, 0.95f, 1f));
            if (_rippleBack != null && _rippleBack.enabled)
                Ripple(_rippleBack, Mathf.Lerp(right + 2f, left - 2f, t), bottom, height, 2.5f, t, new Color(1f, 0.7f, 1f));

            if (_shards != null && _shards.enabled) Shatter(t, left, width, bottom, height);
        }

        static void Ripple(SpriteRenderer r, float x, float bottom, float height, float across, float t, Color colour)
        {
            Vector2 size = r.sprite.bounds.size;
            r.transform.position = new Vector3(x, bottom + height * 0.5f, 0f);
            r.transform.localScale = new Vector3(across / size.x, height * 1.2f / size.y, 1f);
            colour.a = 0.8f * Mathf.Sin(Mathf.PI * t);
            r.color = colour;
        }

        // The skyline cracks (the shards part, so thin cracks of the 3D city show through), then bursts: each shard, the
        // nearer the crack's centre the sooner, flies outwards and towards you, spinning, and fades.
        void Shatter(float t, float left, float width, float bottom, float height)
        {
            var origin = new Vector2(left + width * 0.35f, 2.2f);
            float crack = Mathf.Clamp01(t / 0.12f);
            for (int s = 0; s < Shards; s++)
            {
                Vector2 centre = new Vector2(left + _centre[s].x * width, bottom + _centre[s].y * height);
                Vector2 away = centre - origin;
                float distance = away.magnitude;
                Vector2 dir = distance > 0.01f ? away / distance : Vector2.up;
                float delay = 0.12f + 0.035f * distance;
                float p = Mathf.Clamp01((t - delay) / 0.55f);
                float burst = p * p;
                // Towards you: the shard grows about its centre as it flies out; the parting leaves the cracks.
                float grow = (1f - 0.14f * crack) * (1f + 2.2f * burst);
                float angle = _spin[s] * burst;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                Vector2 moved = centre + dir * (0.18f * crack + burst * (7f + 4f * Hash01(s, 3))) + new Vector2(0f, -3f * burst * burst);
                byte alpha = (byte)(255f * Mathf.Clamp01(1f - p * 1.15f));
                for (int k = 0; k < 3; k++)
                {
                    int i = s * 3 + k;
                    Vector2 rest = new Vector2(left + _rest[i].x * width, bottom + _rest[i].y * height);
                    Vector2 local = (rest - centre) * grow;
                    local = new Vector2(local.x * cos - local.y * sin, local.x * sin + local.y * cos);
                    Vector2 at = moved + local;
                    _vertices[i] = new Vector3(at.x, at.y, 0f);
                    // The picture is fixed to the view as it stood: each corner keeps the part of the skyline it covered.
                    _uv[i] = new Vector2(_rest[i].x, (rest.y + PictureBelow) / PictureHeight);
                    _colours[i] = new Color32(255, 255, 255, alpha);
                }
            }
            _mesh.vertices = _vertices;
            _mesh.uv = _uv;
            _mesh.colors32 = _colours;
            _mesh.RecalculateBounds();
        }
    }
}
