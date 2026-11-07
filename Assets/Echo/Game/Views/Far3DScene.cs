using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// World Time Run doc (art pass phase 6): the Far Far Future's real-time 3D backdrop, a vast future megacity. Built
    /// procedurally when the scene starts (nothing is built mid-run): towers with window-lit facades in three copies of one
    /// repeating stretch (so the camera can travel for ever), a grid-lit ground far below, lanes of flying traffic, an
    /// orbital ring and a planet in a gradient sky, under fog. It lives far from the 2D world on its own layer and is only
    /// seen through Far3DView's camera. Everything moves as a pure function of the run's distance and time, and the
    /// traffic repeats exactly every <see cref="LoopSeconds"/>, so the reduced-effects video loop is the same scene.
    /// </summary>
    public sealed class Far3DScene : MonoBehaviour
    {
        [SerializeField] Material _tower;
        [SerializeField] Material _ground;
        [SerializeField] Material _sky;
        [SerializeField] Material _ring;
        [SerializeField] Material _traffic;

        /// <summary>The city repeats every Period units along the camera's path.</summary>
        public const float Period = 600f;
        /// <summary>The camera travels one Period in this many seconds in the video loop, and the traffic repeats with it.</summary>
        public const float LoopSeconds = 12f;
        /// <summary>Units the camera travels per metre of the run.</summary>
        public const float UnitsPerMetre = 0.8f;
        public static readonly Vector3 Origin = new Vector3(0f, -5000f, 0f);

        const int Towers = 110, Lanes = 6, CarsPerLane = 10;
        Transform[] _cars;
        Transform _skyPart, _ringPart, _planetPart;
        float[] _laneY, _laneZ, _laneSpeed;
        bool _built;

        static float Hash01(int a, int b) => (((uint)(a * 73856093) ^ (uint)(b * 19349663)) % 10000u) / 10000f;

        /// <summary>Builds the city (once); the scene builder's video tool calls it in the editor too.</summary>
        public void Build()
        {
            if (_built) return;
            _built = true;
            transform.position = Origin;
            int layer = gameObject.layer;

            // Towers: one mesh per copy of the stretch, each a box whose facade texture tiles with its height.
            var towers = new CombineInstance[Towers];
            Mesh box = Box();
            for (int i = 0; i < Towers; i++)
            {
                // Lower towers near the camera, the tallest towards the horizon, so the city reads as a vast spread.
                float depth = Mathf.Pow(Hash01(i, 2), 0.8f);
                float x = Hash01(i, 1) * Period, z = 160f + depth * 1100f;
                float w = 16f + Hash01(i, 3) * 30f, d = 16f + Hash01(i, 4) * 30f;
                float h = 40f + depth * 180f + Mathf.Pow(Hash01(i, 5), 2f) * 380f;
                towers[i] = new CombineInstance
                {
                    mesh = ScaledBox(box, w, h, d),
                    transform = Matrix4x4.Translate(new Vector3(x, h / 2f, z)),
                };
            }
            var city = new Mesh { name = "Far3DCity", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            city.CombineMeshes(towers, true, true);
            // The lit windows are the facade's emission. The keyword is switched on here, on the city's own copy of the
            // material, because the scene is built without a graphics device, where keywords can't be set.
            var tower = new Material(_tower) { name = "Far3DTowerLit" };
            tower.EnableKeyword("_EMISSION");
            for (int copy = -1; copy <= 1; copy++) Part("City" + copy, city, tower, new Vector3(copy * Period, 0f, 0f), Vector3.one, layer);

            // The ground far below, lit by its grid of lanes, and the sky: a vast curved backdrop.
            Part("Ground", Quad(), _ground, new Vector3(Period / 2f, 0f, 700f), new Vector3(Period * 4f, 1800f, 1f), layer, Quaternion.Euler(90f, 0f, 0f));
            // The sky, the orbital ring and a planet are so far away they keep pace with the camera (Follow).
            _skyPart = Part("Sky", Quad(), _sky, new Vector3(0f, 600f, 1900f), new Vector3(2400f, 1600f, 1f), layer).transform;
            _ringPart = Part("Ring", Torus(340f, 7f, 96, 8), _ring, new Vector3(0f, 560f, 1700f), Vector3.one, layer, Quaternion.Euler(64f, 0f, 18f)).transform;
            _planetPart = Part("Planet", Sphere(110f, 24, 16), _sky, new Vector3(0f, 640f, 1800f), Vector3.one, layer).transform;

            // Flying traffic in lanes at different heights and depths, both ways.
            _laneY = new float[Lanes];
            _laneZ = new float[Lanes];
            _laneSpeed = new float[Lanes];
            _cars = new Transform[Lanes * CarsPerLane];
            Mesh car = ScaledBox(box, 6f, 1.6f, 2.4f);
            for (int l = 0; l < Lanes; l++)
            {
                _laneY[l] = 90f + l * 34f;
                _laneZ[l] = 260f + l * 120f;
                // A whole number of Periods per loop each way, so the traffic repeats exactly with the video.
                _laneSpeed[l] = (l % 2 == 0 ? 1 : -1) * (1 + l % 3) * Period / LoopSeconds;
                for (int c = 0; c < CarsPerLane; c++)
                    _cars[l * CarsPerLane + c] = Part("Car" + l + "_" + c, car, _traffic, Vector3.zero, Vector3.one, layer).transform;
            }
        }

        /// <summary>Keeps the sky, the ring and the planet with the camera (they are too far away to drift).</summary>
        public void Follow(Vector3 camera)
        {
            if (!_built) return;
            float x = camera.x - Origin.x;
            _skyPart.localPosition = new Vector3(x, 600f, 1900f);
            _ringPart.localPosition = new Vector3(x - 40f, 560f, 1700f);
            _planetPart.localPosition = new Vector3(x - 40f, 560f, 1720f);
        }

        /// <summary>The traffic at <paramref name="time"/> seconds (a pure function, repeating every LoopSeconds).</summary>
        public void Animate(float time)
        {
            if (!_built) return;
            for (int l = 0; l < Lanes; l++)
                for (int c = 0; c < CarsPerLane; c++)
                {
                    float x = Mathf.Repeat(c * Period / CarsPerLane + Hash01(l, c) * 20f + time * _laneSpeed[l], Period * 2f) - Period / 2f;
                    _cars[l * CarsPerLane + c].localPosition = new Vector3(x, _laneY[l] + 4f * Mathf.Sin(c + l), _laneZ[l]);
                }
        }

        /// <summary>
        /// Where the camera is for a distance along its path: gliding past the towers at mid height, looking out across the
        /// city with a slight tilt down, gently rising and falling. Wraps every Period, which the three copies hide.
        /// </summary>
        public static void CameraAt(float along, out Vector3 position, out Quaternion rotation)
        {
            float x = Mathf.Repeat(along, Period);
            position = Origin + new Vector3(x, 270f + 14f * Mathf.Sin(along * 2f * Mathf.PI / Period), -320f);
            rotation = Quaternion.Euler(11f + 2f * Mathf.Sin(along * 4f * Mathf.PI / Period), 0f, 0f);
        }

        GameObject Part(string name, Mesh mesh, Material material, Vector3 at, Vector3 scale, int layer, Quaternion rotation = default)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(transform, false);
            go.transform.localPosition = at;
            go.transform.localRotation = rotation == default ? Quaternion.identity : rotation;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        // ---- procedural meshes ----

        static Mesh Quad()
        {
            var m = new Mesh { name = "Far3DQuad" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            m.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            return m;
        }

        static Mesh Box()
        {
            var m = new Mesh { name = "Far3DBox" };
            Vector3[] n = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up, Vector3.down };
            var v = new Vector3[24];
            var nn = new Vector3[24];
            var uv = new Vector2[24];
            var t = new int[36];
            for (int f = 0; f < 6; f++)
            {
                Vector3 normal = n[f], u = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, normal), w = Vector3.Cross(normal, u);
                for (int k = 0; k < 4; k++)
                {
                    float a = k % 2 == 0 ? -0.5f : 0.5f, b = k < 2 ? -0.5f : 0.5f;
                    v[f * 4 + k] = normal * 0.5f + u * a + w * b;
                    nn[f * 4 + k] = normal;
                    uv[f * 4 + k] = new Vector2(a + 0.5f, b + 0.5f);
                }
                int o = f * 4;
                t[f * 6] = o; t[f * 6 + 1] = o + 2; t[f * 6 + 2] = o + 1; t[f * 6 + 3] = o + 2; t[f * 6 + 4] = o + 3; t[f * 6 + 5] = o + 1;
            }
            m.vertices = v;
            m.normals = nn;
            m.uv = uv;
            m.triangles = t;
            return m;
        }

        // A box w x h x d whose side UVs tile with its size, so the facade's windows stay one size on every tower.
        static Mesh ScaledBox(Mesh box, float w, float h, float d)
        {
            var m = Object.Instantiate(box);
            Vector3[] v = m.vertices;
            Vector3[] n = m.normals;
            Vector2[] uv = m.uv;
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = Vector3.Scale(v[i], new Vector3(w, h, d));
                bool side = Mathf.Abs(n[i].y) < 0.5f;
                float across = Mathf.Abs(n[i].x) > 0.5f ? d : w;
                // One facade tile is 40 x 75 units (16 x 25 windows, each about 2.5 units across).
                uv[i] = side ? new Vector2(uv[i].x * across / 40f, uv[i].y * h / 75f) : new Vector2(0.02f, 0.02f);
            }
            m.vertices = v;
            m.uv = uv;
            m.RecalculateBounds();
            return m;
        }

        static Mesh Torus(float radius, float tube, int segments, int sides)
        {
            var m = new Mesh { name = "Far3DRing" };
            var v = new Vector3[(segments + 1) * (sides + 1)];
            var uv = new Vector2[v.Length];
            var nn = new Vector3[v.Length];
            for (int s = 0; s <= segments; s++)
                for (int k = 0; k <= sides; k++)
                {
                    float a = s * 2f * Mathf.PI / segments, b = k * 2f * Mathf.PI / sides;
                    Vector3 centre = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                    Vector3 normal = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Sin(a) * Mathf.Cos(b));
                    v[s * (sides + 1) + k] = centre + normal * tube;
                    nn[s * (sides + 1) + k] = normal;
                    uv[s * (sides + 1) + k] = new Vector2(s * 8f / segments, k / (float)sides);
                }
            var t = new int[segments * sides * 6];
            int i = 0;
            for (int s = 0; s < segments; s++)
                for (int k = 0; k < sides; k++)
                {
                    int a = s * (sides + 1) + k, b = a + sides + 1;
                    t[i++] = a; t[i++] = b; t[i++] = a + 1; t[i++] = b; t[i++] = b + 1; t[i++] = a + 1;
                }
            m.vertices = v;
            m.normals = nn;
            m.uv = uv;
            m.triangles = t;
            return m;
        }

        static Mesh Sphere(float radius, int segments, int rings)
        {
            var m = new Mesh { name = "Far3DPlanet" };
            var v = new Vector3[(segments + 1) * (rings + 1)];
            var uv = new Vector2[v.Length];
            var nn = new Vector3[v.Length];
            for (int r = 0; r <= rings; r++)
                for (int s = 0; s <= segments; s++)
                {
                    float a = s * 2f * Mathf.PI / segments, b = Mathf.PI * r / rings;
                    Vector3 n = new Vector3(Mathf.Sin(b) * Mathf.Cos(a), Mathf.Cos(b), Mathf.Sin(b) * Mathf.Sin(a));
                    v[r * (segments + 1) + s] = n * radius;
                    nn[r * (segments + 1) + s] = n;
                    uv[r * (segments + 1) + s] = new Vector2(0.5f, 0.04f + 0.3f * r / rings);   // the sky's magenta horizon
                }
            var t = new int[segments * rings * 6];
            int i = 0;
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s, b = a + segments + 1;
                    t[i++] = a; t[i++] = a + 1; t[i++] = b; t[i++] = b; t[i++] = a + 1; t[i++] = b + 1;
                }
            m.vertices = v;
            m.normals = nn;
            m.uv = uv;
            m.triangles = t;
            return m;
        }
    }
}
