using System.Collections.Generic;
using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// World Time Run doc (art pass phase 6; art fix phase 3, "readable 3D"): the Far Far Future's real-time 3D backdrop, a
    /// vast future megacity seen down one of its canyons. The camera hangs above the canyon's street, looking along it to
    /// the horizon (sky in the top third), and glides slowly forward as the run goes on, so the towers on either side slide
    /// past with real parallax. Built procedurally when the scene starts (nothing is built mid-run):
    /// <list type="bullet">
    /// <item>rows of towers either side, taller further out, in three tints, shaded by a moonlight from the left (one canyon
    /// wall lit, the other in shade), with mostly dark windows (about one in five lit, mipmapped, so distant facades become
    /// a dim glow), glowing edge strips on the corners facing the canyon and pulsing lights on the roofs;</item>
    /// <item>sky bridges across the canyon overhead, lit underneath;</item>
    /// <item>streams of traffic at several heights and depths: white headlights coming, red tail lights going;</item>
    /// <item>exponential coloured fog (the scene's lighting settings) fading it all into a glowing horizon, and beyond the
    /// fog a crisp skyline outline with landmarks (a spire with a beacon and a halo, an arcology, twin towers), a ringed
    /// planet and a starry gradient sky, unfogged (Echo/Far3D Unfogged) and keeping pace with the camera.</item>
    /// </list>
    /// One stretch of <see cref="Period"/> units repeats along the canyon, so the camera can travel for ever. Everything is a
    /// pure function of the distance travelled and the time, and the traffic repeats exactly every <see cref="LoopSeconds"/>
    /// while the camera travels one Period, so the reduced-effects video loop is the same scene and loops seamlessly.
    /// </summary>
    public sealed class Far3DScene : MonoBehaviour
    {
        [Tooltip("Simple Lit with the lit windows as emission: the towers.")]
        [SerializeField] Material _tower;
        [Tooltip("Unlit, fogged: the canyon's street, its glowing lanes.")]
        [SerializeField] Material _ground;
        [Tooltip("Echo/Far3D Unfogged with the sky's gradient.")]
        [SerializeField] Material _sky;
        [Tooltip("Echo/Far3D Unfogged, white: the skyline, the landmarks' lights, the planet and its rings (tinted copies).")]
        [SerializeField] Material _unfogged;
        [Tooltip("Unlit, fogged, white: edge strips, roof lights and traffic (HDR-tinted copies, so they bloom).")]
        [SerializeField] Material _strip;

        /// <summary>The city repeats every Period units along the canyon.</summary>
        public const float Period = 400f;
        /// <summary>The camera travels one Period in this many seconds in the video loop, and the traffic repeats with it.</summary>
        public const float LoopSeconds = 30f;
        /// <summary>Units the camera travels per metre of the run (slow: about 10 units a second at running speed).</summary>
        public const float UnitsPerMetre = 0.8f;
        public static readonly Vector3 Origin = new Vector3(0f, -5000f, 0f);

        /// <summary>The camera's height above the street, its downward tilt and its vertical field of view (the builder sets it).</summary>
        public const float CameraHeight = 95f, CameraPitch = 9f, FieldOfView = 62f;

        // The city is laid out as Copies stretches one after another; the camera stays in the first.
        const int Copies = 6;
        const float CanyonHalfWidth = 60f;
        // How far ahead the skyline, the landmarks and the sky stand (always: they keep pace with the camera).
        const float SkylineAt = 1950f, SkyAt = 2600f, PlanetAt = 2400f;

        struct Lane
        {
            public float X, Y, Speed;
            public bool Coming;
        }

        const int CarsPerLane = 10;
        const float TrafficWindow = Period * 5f;   // a car's spacing (200) divides the Period, so the loop is seamless
        Lane[] _lanes;
        Transform[] _cars;
        Transform _skyPart, _horizon, _planet;
        Material _roofLights;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color RoofRed = new Color(3.2f, 0.35f, 0.3f);
        bool _built;

        static float Hash01(int a, int b) => (((uint)(a * 73856093) ^ (uint)(b * 19349663)) % 10000u) / 10000f;

        /// <summary>Builds the city (once); the scene builder's video tool calls it in the editor too.</summary>
        public void Build()
        {
            if (_built) return;
            _built = true;
            transform.position = Origin;
            int layer = gameObject.layer;
            Mesh box = Box();

            // Three tints of the same facade; the keyword is switched on here because the scene is built without a graphics
            // device, where keywords can't be set.
            var tints = new[]
            {
                (new Color(0.78f, 0.86f, 1f), new Color(1f, 1.05f, 1.15f)),      // steel blue glass, cool light
                (new Color(0.9f, 0.84f, 0.84f), new Color(1.2f, 1f, 0.8f)),      // warm grey stone, warm light
                (new Color(0.62f, 0.85f, 0.85f), new Color(0.8f, 1.15f, 1.2f)),  // dark teal glass
            };
            var towerParts = new List<CombineInstance>[tints.Length];
            for (int k = 0; k < tints.Length; k++) towerParts[k] = new List<CombineInstance>();
            var cyan = new List<CombineInstance>();
            var magenta = new List<CombineInstance>();
            var roofs = new List<CombineInstance>();
            var made = new List<Mesh>();

            // One stretch of the city, laid out once, then placed Copies times along the canyon.
            var stretch = new List<(int tint, Mesh mesh, Vector3 at)>();
            var lights = new List<(int kind, Vector3 at, Vector3 size)>();   // 0 cyan, 1 magenta, 2 roof light
            int n = 0;
            // The canyon's walls stay below the camera's eye; each row behind is taller, so the city rises away like a bowl
            // round an open sky.
            float[] rowInner = { CanyonHalfWidth, 130f, 220f, 340f };
            float[] rowLow = { 40f, 80f, 130f, 180f }, rowRange = { 70f, 90f, 120f, 160f };
            for (int row = 0; row < rowInner.Length; row++)
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = Hash01(row, side + 5) * 12f;
                    while (true)
                    {
                        n++;
                        float along = 26f + Hash01(n, 1) * (20f + row * 14f);
                        if (z + along > Period - 4f) break;
                        float across = 24f + Hash01(n, 2) * (16f + row * 20f);
                        float h = rowLow[row] + Hash01(n, 3) * rowRange[row];
                        float x = side * (rowInner[row] + across / 2f);
                        float zc = z + along / 2f;
                        Mesh m = ScaledBox(box, across, h, along);
                        made.Add(m);
                        int tint = (int)(Hash01(n, 4) * tints.Length) % tints.Length;
                        stretch.Add((tint, m, new Vector3(x, h / 2f, zc)));

                        // The canyon's walls and the row behind them: glowing strips up the corners facing the canyon and along
                        // the roof's edge; most roofs carry a warning light, the tallest an aerial with one at its tip.
                        float inner = side * rowInner[row];
                        if (row < 2 && Hash01(n, 5) > 0.75f)
                        {
                            int kind = Hash01(n, 6) > 0.5f ? 0 : 1;
                            lights.Add((kind, new Vector3(inner, h / 2f, z + 0.4f), new Vector3(0.8f, h, 0.8f)));
                            lights.Add((kind, new Vector3(inner, h + 0.4f, zc), new Vector3(0.8f, 0.8f, along)));
                        }
                        if (Hash01(n, 7) > 0.45f)
                        {
                            bool aerial = h > 200f && Hash01(n, 8) > 0.4f;
                            if (aerial) stretch.Add((tint, Track(made, ScaledBox(box, 1.2f, 30f, 1.2f)), new Vector3(x, h + 15f, zc)));
                            lights.Add((2, new Vector3(x, h + (aerial ? 31f : 1.5f), zc), new Vector3(2.6f, 2.6f, 2.6f)));
                        }
                        z += along + 4f + Hash01(n, 9) * (Hash01(n, 10) > 0.8f ? 22f : 10f);
                    }
                }

            // Sky bridges across the canyon, dark underneath with a glowing strip along each edge.
            for (int b = 0; b < 1; b++)
            {
                // Overhead (above the camera's eye), so they sweep over the top of the view rather than across it.
                float zb = Period * 0.55f, yb = 128f;
                float span = 2f * (CanyonHalfWidth + 14f);
                stretch.Add((0, Track(made, ScaledBox(box, span, 4f, 9f)), new Vector3(0f, yb, zb)));
                int kind = b % 2;
                lights.Add((kind, new Vector3(0f, yb - 2.2f, zb - 4.5f), new Vector3(span, 0.6f, 0.6f)));
                lights.Add((kind, new Vector3(0f, yb - 2.2f, zb + 4.5f), new Vector3(span, 0.6f, 0.6f)));
                // Pylons up the canyon walls where it lands, so a bridge is plainly held up.
                lights.Add((kind, new Vector3(-CanyonHalfWidth - 0.5f, yb / 2f, zb), new Vector3(0.6f, yb, 0.6f)));
                lights.Add((kind, new Vector3(CanyonHalfWidth + 0.5f, yb / 2f, zb), new Vector3(0.6f, yb, 0.6f)));
            }

            for (int copy = 0; copy < Copies; copy++)
            {
                var offset = new Vector3(0f, 0f, copy * Period);
                foreach (var (tint, mesh, at) in stretch)
                    towerParts[tint].Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.Translate(at + offset) });
                foreach (var (kind, at, size) in lights)
                {
                    var ci = new CombineInstance { mesh = box, transform = Matrix4x4.TRS(at + offset, Quaternion.identity, size) };
                    (kind == 0 ? cyan : kind == 1 ? magenta : roofs).Add(ci);
                }
            }
            for (int k = 0; k < tints.Length; k++)
            {
                var tower = new Material(_tower) { name = "Far3DTower" + k };
                tower.EnableKeyword("_EMISSION");
                tower.SetColor(BaseColor, tints[k].Item1);
                tower.SetColor("_EmissionColor", tints[k].Item2);
                Part("Towers" + k, Combined("Far3DTowers" + k, towerParts[k]), tower, Vector3.zero, Vector3.one, layer);
            }
            Part("EdgesCyan", Combined("Far3DEdgesCyan", cyan), Tinted(_strip, new Color(0.5f, 2.4f, 2.9f)), Vector3.zero, Vector3.one, layer);
            Part("EdgesMagenta", Combined("Far3DEdgesMagenta", magenta), Tinted(_strip, new Color(2.8f, 0.5f, 2.2f)), Vector3.zero, Vector3.one, layer);
            _roofLights = Tinted(_strip, RoofRed);
            Part("RoofLights", Combined("Far3DRoofLights", roofs), _roofLights, Vector3.zero, Vector3.one, layer);
            // The per-tower meshes only fed the combined ones (the video tool builds the city in the editor, outside play).
            foreach (Mesh m in made)
                if (Application.isPlaying) Object.Destroy(m);
                else Object.DestroyImmediate(m);

            // The canyon's street far below, its lanes glowing (a grid square every 100 units divides the Period).
            Part("Street", Quad(), _ground, new Vector3(0f, 0f, Copies * Period / 2f), new Vector3(1200f, Copies * Period, 1f), layer,
                Quaternion.Euler(90f, 0f, 0f));

            BuildHorizon(box, layer);
            BuildTraffic(box, layer);
        }

        // The skyline outline beyond the fog, the landmarks and the sky: unfogged, keeping pace with the camera.
        void BuildHorizon(Mesh box, int layer)
        {
            var dark = new List<CombineInstance>();
            var glow = new List<CombineInstance>();
            var beacons = new List<CombineInstance>();
            void Block(List<CombineInstance> to, Vector3 at, Vector3 size) =>
                to.Add(new CombineInstance { mesh = box, transform = Matrix4x4.TRS(at, Quaternion.identity, size) });

            // The skyline: a broken row of blocks, taller away from the canyon's line, a few with aerials.
            for (int i = 0; i < 44; i++)
            {
                float x = -780f + i * 36f + Hash01(i, 31) * 20f, w = 26f + Hash01(i, 32) * 44f;
                float h = 120f + Hash01(i, 33) * 260f + Mathf.Abs(x) * 0.15f;
                float z = SkylineAt + Hash01(i, 34) * 120f;
                Block(dark, new Vector3(x, h / 2f, z), new Vector3(w, h, 30f));
                if (Hash01(i, 35) > 0.6f) Block(beacons, new Vector3(x, h + 3f, z - 16f), new Vector3(5f, 5f, 2f));
            }
            // The spire at the end of the canyon: tapering stages, edge lights, a halo and a beacon at its tip.
            float sx = 50f, sz = SkylineAt + 220f, y = 0f;
            foreach (var (w, h) in new[] { (70f, 230f), (44f, 200f), (24f, 170f), (5f, 120f) })
            {
                Block(dark, new Vector3(sx, y + h / 2f, sz), new Vector3(w, h, w));
                if (w > 6f)
                {
                    for (float band = y + 30f; band < y + h - 10f; band += 34f)
                        Block(glow, new Vector3(sx, band, sz - w / 2f - 1f), new Vector3(w * 0.8f, 2f, 1.6f));
                    Block(glow, new Vector3(sx - w / 2f, y + h / 2f, sz - w / 2f - 1f), new Vector3(1.6f, h, 1.6f));
                    Block(glow, new Vector3(sx + w / 2f, y + h / 2f, sz - w / 2f - 1f), new Vector3(1.6f, h, 1.6f));
                    Block(glow, new Vector3(sx, y + h, sz - w / 2f - 1f), new Vector3(w, 1.6f, 1.6f));
                }
                y += h;
            }
            Block(beacons, new Vector3(sx, y + 4f, sz - 3f), new Vector3(9f, 9f, 3f));
            // An arcology, stepped like a ziggurat, with bands of light; and twin towers joined by a bridge.
            float ay = 0f;
            for (int s = 0; s < 5; s++)
            {
                float w = 300f - s * 58f, h = s == 4 ? 80f : 60f;
                Block(dark, new Vector3(-430f, ay + h / 2f, SkylineAt + 120f), new Vector3(w, h, 80f));
                Block(glow, new Vector3(-430f, ay + h * 0.55f, SkylineAt + 78f), new Vector3(w * 0.9f, 1.8f, 1.8f));
                ay += h;
            }
            foreach (float tx in new[] { 360f, 440f })
            {
                Block(dark, new Vector3(tx, 250f, SkylineAt + 60f), new Vector3(48f, 500f, 48f));
                Block(glow, new Vector3(tx - 24f, 250f, SkylineAt + 35f), new Vector3(1.6f, 500f, 1.6f));
                Block(beacons, new Vector3(tx, 506f, SkylineAt + 34f), new Vector3(6f, 6f, 2f));
            }
            Block(dark, new Vector3(400f, 330f, SkylineAt + 60f), new Vector3(40f, 10f, 20f));
            Block(glow, new Vector3(400f, 325f, SkylineAt + 49f), new Vector3(40f, 1.6f, 1.6f));

            _horizon = new GameObject("Horizon") { layer = layer }.transform;
            _horizon.SetParent(transform, false);
            Part("Skyline", Combined("Far3DSkyline", dark), Tinted(_unfogged, new Color(0.025f, 0.012f, 0.045f)), Vector3.zero, Vector3.one, layer).transform.SetParent(_horizon, false);
            Part("SkylineLights", Combined("Far3DSkylineLights", glow), Tinted(_unfogged, new Color(0.9f, 2f, 2.6f)), Vector3.zero, Vector3.one, layer).transform.SetParent(_horizon, false);
            Part("Beacons", Combined("Far3DBeacons", beacons), Tinted(_unfogged, new Color(3.5f, 0.6f, 0.5f)), Vector3.zero, Vector3.one, layer).transform.SetParent(_horizon, false);
            var halo = Part("Halo", Torus(48f, 1.6f, 64, 6), Tinted(_unfogged, new Color(0.8f, 2.2f, 2.8f)), new Vector3(sx, 470f, sz), Vector3.one, layer,
                Quaternion.Euler(8f, 0f, 0f));
            halo.transform.SetParent(_horizon, false);

            // The sky: a gradient behind everything, its horizon at the camera's eye level (SkyTexture's HorizonV).
            _skyPart = Part("Sky", Quad(), _sky, Vector3.zero, new Vector3(3400f, 1900f, 1f), layer).transform;
            // A ringed planet high on the right, lit from the left.
            var planet = new Material(_unfogged) { name = "Far3DPlanet" };
            planet.SetColor(BaseColor, new Color(0.62f, 0.2f, 0.11f));
            planet.SetFloat("_Shade", 1f);
            planet.SetVector("_LightDir", new Vector4(-0.8f, 0.35f, -0.5f, 0f));
            _planet = Part("Planet", Sphere(140f, 32, 20), planet, Vector3.zero, Vector3.one, layer).transform;
            Material rings = Tinted(_unfogged, new Color(0.55f, 0.4f, 0.3f));
            // Nearly edge-on, tilted a little: a thin ellipse crossing the planet.
            foreach (float r in new[] { 215f, 240f, 262f })
                Part("Ring" + r, Torus(r, r > 250f ? 3f : 5f, 96, 4), rings, Vector3.zero, Vector3.one, layer, Quaternion.Euler(-14f, 0f, -16f))
                    .transform.SetParent(_planet, false);
        }

        void BuildTraffic(Mesh box, int layer)
        {
            // Lanes down the canyon (low, level with the camera, overhead) and in the streets behind its walls; a whole
            // number of Periods per loop each way, so the traffic repeats exactly with the video.
            float unit = Period / LoopSeconds;
            _lanes = new[]
            {
                new Lane { X = -20f, Y = 30f, Speed = 3f * unit },
                new Lane { X = 20f, Y = 30f, Speed = -2f * unit, Coming = true },
                new Lane { X = -32f, Y = 64f, Speed = 4f * unit },
                new Lane { X = 32f, Y = 64f, Speed = -3f * unit, Coming = true },
                new Lane { X = -12f, Y = 128f, Speed = 2f * unit },
                new Lane { X = 12f, Y = 128f, Speed = -1f * unit, Coming = true },
                new Lane { X = -115f, Y = 110f, Speed = 2f * unit },
                new Lane { X = 115f, Y = 120f, Speed = -2f * unit, Coming = true },
            };
            Material coming = Tinted(_strip, new Color(2.4f, 2.6f, 3f)), going = Tinted(_strip, new Color(3f, 0.35f, 0.35f));
            Mesh car = ScaledBox(box, 1.5f, 0.9f, 9f);
            _cars = new Transform[_lanes.Length * CarsPerLane];
            for (int l = 0; l < _lanes.Length; l++)
                for (int c = 0; c < CarsPerLane; c++)
                    _cars[l * CarsPerLane + c] = Part("Car" + l + "_" + c, car, _lanes[l].Coming ? coming : going, Vector3.zero, Vector3.one, layer).transform;
        }

        /// <summary>Keeps the skyline, the landmarks, the sky and the planet with the camera (they are too far away to near).</summary>
        public void Follow(Vector3 camera)
        {
            if (!_built) return;
            Vector3 c = camera - Origin;
            _horizon.localPosition = new Vector3(c.x, 0f, c.z);
            // The sky's horizon (HorizonV up its height) at the camera's eye level.
            const float height = 1900f;
            _skyPart.localPosition = new Vector3(c.x, c.y + height * (0.5f - HorizonV), c.z + SkyAt);
            _planet.localPosition = new Vector3(c.x + 360f, c.y + 640f, c.z + PlanetAt);
        }

        /// <summary>Where the sky texture's horizon is, up its height.</summary>
        public const float HorizonV = 0.16f;

        /// <summary>The traffic and the roof lights at <paramref name="time"/> seconds, the camera <paramref name="along"/> its path.</summary>
        public void Animate(float time, float along)
        {
            if (!_built) return;
            float wrap = Mathf.Floor(along / Period) * Period, start = along - 60f;
            for (int l = 0; l < _lanes.Length; l++)
            {
                Lane lane = _lanes[l];
                for (int c = 0; c < CarsPerLane; c++)
                {
                    // Each car moves at its lane's speed through a window that slides with the camera (a pure function of the
                    // time and the distance), then shifts by the camera's wrap, like the camera itself.
                    float z = start + Mathf.Repeat(c * TrafficWindow / CarsPerLane + Hash01(l, c) * 30f + time * lane.Speed - start, TrafficWindow);
                    _cars[l * CarsPerLane + c].localPosition = new Vector3(lane.X + 2f * Mathf.Sin(c * 1.7f + l), lane.Y + 3f * Mathf.Sin(c + l), z - wrap);
                }
            }
            // The roof lights pulse slowly, all together (a whole number of pulses per loop).
            float pulse = 0.35f + 0.65f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(time * 2f * Mathf.PI * 18f / LoopSeconds), 3f);
            _roofLights.SetColor(BaseColor, RoofRed * pulse);
        }

        /// <summary>
        /// Where the camera is for a distance along its path: above the canyon's street, looking along it to the horizon
        /// (tilted down so the horizon sits a third of the way down), drifting gently side to side. Wraps every Period, which
        /// the repeating city hides.
        /// </summary>
        public static void CameraAt(float along, out Vector3 position, out Quaternion rotation)
        {
            float a = along * 2f * Mathf.PI / Period;
            position = Origin + new Vector3(7f * Mathf.Sin(a), CameraHeight + 5f * Mathf.Sin(2f * a), Mathf.Repeat(along, Period));
            rotation = Quaternion.Euler(CameraPitch + 1f * Mathf.Sin(2f * a), -2.5f * Mathf.Cos(a), 0f);
        }

        Material Tinted(Material source, Color colour)
        {
            var m = new Material(source) { name = source.name + "Tinted" };
            m.SetColor(BaseColor, colour);
            return m;
        }

        static Mesh Track(List<Mesh> made, Mesh m)
        {
            made.Add(m);
            return m;
        }

        static Mesh Combined(string name, List<CombineInstance> parts)
        {
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.CombineMeshes(parts.ToArray(), true, true);
            return m;
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
                // One facade tile is 20 x 40 units (16 x 32 windows, each 1.25 units across); roofs take a dark corner.
                uv[i] = side ? new Vector2(uv[i].x * across / FacadeTileWidth, uv[i].y * h / FacadeTileHeight) : new Vector2(0.01f, 0.01f);
            }
            m.vertices = v;
            m.uv = uv;
            m.RecalculateBounds();
            return m;
        }

        /// <summary>The facade texture covers this many units of wall.</summary>
        public const float FacadeTileWidth = 20f, FacadeTileHeight = 40f;

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
                    uv[r * (segments + 1) + s] = new Vector2(s / (float)segments, r / (float)rings);
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
