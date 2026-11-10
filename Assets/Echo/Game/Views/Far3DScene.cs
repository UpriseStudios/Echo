using System.Collections.Generic;
using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// World Time Run doc, the Far Far Future ("The Infinite City", a user brief: awe, not density). The real-time 3D backdrop
    /// is a few impossibly large structures standing apart in a violet haze, under a gigantic orbital ring:
    /// <list type="bullet">
    /// <item>four architecturally distinct megastructures a stretch (a faceted needle spire with a halo, a stepped arcology,
    /// a blade monolith, a tower ringed by floating discs), separated by
    /// open space and sky, shaded by a cool moonlight from the left, with thin cyan edge highlights and warm amber only as
    /// small accents high up;</item>
    /// <item>depth from strong exponential-squared haze in the horizon's colour (near structures clear, the far ones gone) (contrast and saturation fall away with distance, the
    /// far structures dissolve into the sky), two elevated transit rails running to the horizon, and now and then a
    /// foreground arch passed under or a pylon sweeping past;</item>
    /// <item>the landmark: an orbital ring thousands of units away, high across the frame, unfogged, turning and drifting
    /// slowly as the run goes on; a faint skyline outline on the horizon and a gradient sky with stars.</item>
    /// </list>
    /// No windows, no traffic, nothing bright low in the frame: the bottom third, behind the gameplay, stays the darkest and
    /// calmest part (Far3DView adds a calm band there too). Every part shares a few materials and is combined into one mesh
    /// each, so the whole city is about a dozen draw calls. One stretch of <see cref="Period"/> units repeats along the path
    /// (the camera wraps within it); everything is a pure function of the distance travelled and the time, repeating with the
    /// camera every Period and every <see cref="LoopSeconds"/>, so the reduced-effects video is the same scene and loops.
    /// On the rift into the era the camera starts low and rises as the ring climbs into place (<see cref="CameraAt"/> with a
    /// reveal under 1, <see cref="Follow"/>).
    /// </summary>
    public sealed class Far3DScene : MonoBehaviour
    {
        [Tooltip("Simple Lit: the structures (copies tinted midnight blue and violet; the panel texture as their base).")]
        [SerializeField] Material _tower;
        [Tooltip("Unlit, fogged: the ground far below.")]
        [SerializeField] Material _ground;
        [Tooltip("Echo/Far3D Unfogged with the sky's gradient.")]
        [SerializeField] Material _sky;
        [Tooltip("Echo/Far3D Unfogged, white: the orbital ring and the skyline outline (tinted copies).")]
        [SerializeField] Material _unfogged;
        [Tooltip("Unlit, fogged, white: the cyan edges and amber accents (HDR-tinted copies, so they bloom).")]
        [SerializeField] Material _strip;
        [Tooltip("A subtle panelled surface for the structures (no windows).")]
        [SerializeField] Texture2D _panels;

        /// <summary>The city repeats every Period units along the path.</summary>
        public const float Period = 2000f;
        /// <summary>The camera travels one Period in this many seconds in the video loop.</summary>
        public const float LoopSeconds = 75f;
        /// <summary>Units the camera travels per metre of the run (slow: about 10 units a second at running speed).</summary>
        public const float UnitsPerMetre = 0.8f;
        public static readonly Vector3 Origin = new Vector3(0f, -5000f, 0f);

        /// <summary>The camera's height, its downward tilt and its vertical field of view (the builder sets them).</summary>
        public const float CameraHeight = 95f, CameraPitch = 9f, FieldOfView = 62f, FarClip = 8500f;
        /// <summary>How far the camera starts below its height, and its tilt there, at the start of the reveal.</summary>
        public const float RevealDrop = 28f, RevealPitch = -2f;

        // Enough stretches to reach past where the haze has taken everything (about 2500 units from the camera).
        const int Copies = 3;
        // Ahead of the camera, always: the skyline outline, the ring and the sky.
        const float SkylineAt = 2500f, RingAt = 4100f, SkyAt = 7000f, SkyHeight = 5100f, SkyWidth = 9200f;
        const float RingRadius = 2500f, RingHeight = 1150f, RingRise = 1400f;

        /// <summary>Where the sky texture's horizon is, up its height.</summary>
        public const float HorizonV = 0.16f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color Cyan = new Color(0.45f, 1.5f, 1.8f), Amber = new Color(2.2f, 1.25f, 0.45f);
        Transform _skyPart, _horizon, _ring;
        Material _beacons;
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
            var made = new List<Mesh>();

            // One stretch: (material, mesh, transform) for the solids, light parts as boxes.
            var blue = new List<(Mesh, Matrix4x4)>();
            var violet = new List<(Mesh, Matrix4x4)>();
            var cyan = new List<Matrix4x4>();
            var amber = new List<Matrix4x4>();
            var beacons = new List<Matrix4x4>();
            var railLine = new List<Matrix4x4>();
            Mesh Keep(Mesh m) { made.Add(m); return m; }
            void Solid(List<(Mesh, Matrix4x4)> to, Mesh m, Vector3 at, float yaw = 0f) => to.Add((m, Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f), Vector3.one)));
            void Edge(List<Matrix4x4> to, Vector3 a, Vector3 b, float thick) => to.Add(Beam(a, b, thick));

            // A. The needle spire: three faceted stages to a point, a halo round its upper stage, a beacon at the tip.
            {
                var at = new Vector3(210f, 0f, 150f);   // near the path: dark, contrasty
                float y = 0f;
                foreach (var (r0, r1, h) in new[] { (90f, 58f, 760f), (50f, 28f, 380f), (22f, 2f, 300f) })
                {
                    Solid(blue, Keep(Prism(8, r0, r1, h)), at + new Vector3(0f, y, 0f), 22.5f);
                    // Two of its edges catch a cyan line: the ones facing the path.
                    for (int k = 4; k <= 5; k++)
                    {
                        float a = (k * 45f + 22.5f) * Mathf.Deg2Rad;
                        var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                        Edge(cyan, at + d * r0 + Vector3.up * y, at + d * r1 + Vector3.up * (y + h), 1.2f);
                    }
                    y += h;
                }
                beacons.Add(Matrix4x4.TRS(at + new Vector3(0f, y + 4f, 0f), Quaternion.identity, Vector3.one * 7f));
                cyan.AddRange(RingOfBeams(at + new Vector3(0f, 960f, 0f), 120f, 24, 1.8f));
            }
            // B. The stepped arcology: five diamond tiers, a cyan rim round each, amber only on the top tier.
            {
                var at = new Vector3(-560f, 0f, 760f);   // far out: dissolving into the haze
                float y = 0f;
                for (int t = 0; t < 5; t++)
                {
                    float r = 300f - t * 58f, h = t == 4 ? 160f : 130f;
                    Solid(violet, Keep(Prism(4, r, r * 0.94f, h)), at + new Vector3(0f, y, 0f), 45f);
                    cyan.AddRange(RingOfBeams(at + new Vector3(0f, y + h, 0f), r * 0.94f, 4, 1.3f, 45f));
                    y += h;
                }
                for (int k = 0; k < 4; k++)
                {
                    float a = (k * 90f) * Mathf.Deg2Rad;
                    amber.Add(Matrix4x4.TRS(at + new Vector3(Mathf.Sin(a) * 40f, y + 6f, Mathf.Cos(a) * 40f), Quaternion.identity, new Vector3(6f, 6f, 6f)));
                }
            }
            // C. The blade monolith, a sheer slab turned to the path.
            {
                var at = new Vector3(450f, 0f, 1150f);   // far out
                Solid(violet, Keep(ScaledBox(box, 46f, 980f, 280f)), at + new Vector3(0f, 490f, 0f), 18f);
                Quaternion turn = Quaternion.Euler(0f, 18f, 0f);
                Vector3 lead = at + turn * new Vector3(-23f, 0f, -140f);
                Edge(cyan, lead, lead + Vector3.up * 980f, 1.8f);
                amber.Add(Matrix4x4.TRS(at + new Vector3(0f, 988f, 0f), Quaternion.identity, new Vector3(7f, 7f, 7f)));
            }
            // D. The disc tower: a slender core with three floating discs, each with a cyan rim.
            {
                var at = new Vector3(-240f, 0f, 1760f);   // nearer again
                Solid(blue, Keep(Prism(16, 40f, 30f, 1100f)), at);
                foreach (var (y, r) in new[] { (400f, 170f), (640f, 140f), (860f, 110f) })
                {
                    Solid(violet, Keep(Prism(24, r, r * 0.9f, 14f)), at + new Vector3(0f, y, 0f));
                    cyan.AddRange(RingOfBeams(at + new Vector3(0f, y + 7f, 0f), r * 0.97f, 24, 1.2f));
                }
                amber.Add(Matrix4x4.TRS(at + new Vector3(0f, 1106f, 0f), Quaternion.identity, new Vector3(7f, 7f, 7f)));
            }
            // Foreground, now and then: an arch over the path (passed under, sweeping over the top of the view) and a pylon
            // sweeping past on the right.
            {
                const float z = 1450f;
                foreach (float x in new[] { -88f, 88f })
                    Solid(blue, Keep(ScaledBox(box, 16f, 236f, 22f)), new Vector3(x, 118f, z));
                Solid(blue, Keep(ScaledBox(box, 212f, 26f, 26f)), new Vector3(0f, 249f, z));
                Edge(cyan, new Vector3(-96f, 236f, z - 13.5f), new Vector3(96f, 236f, z - 13.5f), 1f);
                Solid(violet, Keep(Prism(4, 11f, 5f, 430f)), new Vector3(78f, 0f, 480f), 45f);
                Edge(cyan, new Vector3(70f, 0f, 480f), new Vector3(74.5f, 430f, 480f), 0.8f);
                beacons.Add(Matrix4x4.TRS(new Vector3(78f, 434f, 480f), Quaternion.identity, Vector3.one * 4f));
            }
            // Perspective: two floating transit rails to the horizon (below the eye, never low in the frame).
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 36f;
                Solid(blue, Keep(ScaledBox(box, 7f, 4f, Period)), new Vector3(x, 62f, Period / 2f));
                Edge(railLine, new Vector3(x - side * 3.6f, 64.3f, 0f), new Vector3(x - side * 3.6f, 64.3f, Period), 0.4f);
            }

            // The stretch, Copies times along the path, one mesh per material.
            var blueParts = new List<CombineInstance>();
            var violetParts = new List<CombineInstance>();
            var cyanParts = new List<CombineInstance>();
            var amberParts = new List<CombineInstance>();
            var beaconParts = new List<CombineInstance>();
            var railParts = new List<CombineInstance>();
            for (int copy = 0; copy < Copies; copy++)
            {
                Matrix4x4 shift = Matrix4x4.Translate(new Vector3(0f, 0f, copy * Period));
                foreach (var (m, t) in blue) blueParts.Add(new CombineInstance { mesh = m, transform = shift * t });
                foreach (var (m, t) in violet) violetParts.Add(new CombineInstance { mesh = m, transform = shift * t });
                foreach (Matrix4x4 t in cyan) cyanParts.Add(new CombineInstance { mesh = box, transform = shift * t });
                foreach (Matrix4x4 t in amber) amberParts.Add(new CombineInstance { mesh = box, transform = shift * t });
                foreach (Matrix4x4 t in beacons) beaconParts.Add(new CombineInstance { mesh = box, transform = shift * t });
                foreach (Matrix4x4 t in railLine) railParts.Add(new CombineInstance { mesh = box, transform = shift * t });
            }
            Part("Blue", Combined("Far3DBlue", blueParts), Surface(new Color(0.3f, 0.34f, 0.58f)), Vector3.zero, Vector3.one, layer);
            Part("Violet", Combined("Far3DViolet", violetParts), Surface(new Color(0.38f, 0.3f, 0.58f)), Vector3.zero, Vector3.one, layer);
            Part("Edges", Combined("Far3DEdges", cyanParts), Tinted(_strip, Cyan), Vector3.zero, Vector3.one, layer);
            Part("Amber", Combined("Far3DAmber", amberParts), Tinted(_strip, Amber), Vector3.zero, Vector3.one, layer);
            Part("RailLines", Combined("Far3DRailLines", railParts), Tinted(_strip, Cyan * 0.45f), Vector3.zero, Vector3.one, layer);
            _beacons = Tinted(_strip, Amber);
            Part("Beacons", Combined("Far3DBeacons", beaconParts), _beacons, Vector3.zero, Vector3.one, layer);
            foreach (Mesh m in made)
                if (Application.isPlaying) Object.Destroy(m);
                else Object.DestroyImmediate(m);

            // The ground far below: dark, its faint lines running to the horizon (a tile every 100 units divides the Period).
            Part("Ground", Quad(), _ground, new Vector3(0f, 0f, Copies * Period / 2f), new Vector3(2400f, Copies * Period, 1f), layer,
                Quaternion.Euler(90f, 0f, 0f));

            BuildHorizon(box, layer);
        }

        // Ahead of the camera, always: the faint skyline outline (in the haze), the orbital ring and the sky (beyond it,
        // unfogged, so they carry the haze's colour themselves).
        void BuildHorizon(Mesh box, int layer)
        {
            var skyline = new List<CombineInstance>();
            for (int i = 0; i < 26; i++)
            {
                float x = -620f + i * 48f + Hash01(i, 31) * 24f, w = 22f + Hash01(i, 32) * 46f;
                float h = 40f + Hash01(i, 33) * 140f + (Hash01(i, 36) > 0.85f ? 130f : 0f);
                skyline.Add(new CombineInstance { mesh = box, transform = Matrix4x4.TRS(new Vector3(x, h / 2f, SkylineAt + Hash01(i, 34) * 120f), Quaternion.identity, new Vector3(w, h, 30f)) });
            }
            _horizon = new GameObject("Horizon") { layer = layer }.transform;
            _horizon.SetParent(transform, false);
            // Inside the haze, fogged like everything else: dark blocks that the fog leaves as a faint outline in exactly its
            // own colour, so whatever stands as far off melts into it rather than showing against it.
            Color haze = FogColour;
            Part("Skyline", Combined("Far3DSkyline", skyline), Tinted(_strip, new Color(0.04f, 0.03f, 0.1f)),
                Vector3.zero, Vector3.one, layer).transform.SetParent(_horizon, false);

            // The orbital ring: a broad structural band, a bright cyan seam along it and stations spaced round it, tilted a
            // little and lying almost flat, so from here it is a vast arc across the top of the sky.
            _ring = new GameObject("Ring") { layer = layer }.transform;
            _ring.SetParent(transform, false);
            Part("RingBand", Torus(RingRadius, 150f, 256, 8), Unfogged(Color.Lerp(haze, new Color(0.5f, 0.52f, 0.8f), 0.3f)),
                Vector3.zero, new Vector3(1f, 0.45f, 1f), layer).transform.SetParent(_ring, false);
            Part("RingSeam", Torus(RingRadius - 128f, 11f, 256, 4), Tinted(_unfogged, new Color(0.35f, 1.1f, 1.4f)),
                new Vector3(0f, -40f, 0f), Vector3.one, layer).transform.SetParent(_ring, false);
            var stations = new List<CombineInstance>();
            for (int k = 0; k < 24; k++)
            {
                float a = k * 15f * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * RingRadius;
                stations.Add(new CombineInstance { mesh = box, transform = Matrix4x4.TRS(p, Quaternion.Euler(0f, -k * 15f, 0f), new Vector3(220f, 120f, 360f)) });
            }
            Part("RingStations", Combined("Far3DRingStations", stations), Unfogged(Color.Lerp(haze, new Color(0.42f, 0.44f, 0.7f), 0.3f)),
                Vector3.zero, Vector3.one, layer).transform.SetParent(_ring, false);
            var lights = new List<CombineInstance>();
            for (int k = 0; k < 24; k += 3)
            {
                float a = k * 15f * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (RingRadius - 2f);
                lights.Add(new CombineInstance { mesh = box, transform = Matrix4x4.TRS(p + Vector3.down * 66f, Quaternion.identity, Vector3.one * 26f) });
            }
            Part("RingLights", Combined("Far3DRingLights", lights), Tinted(_unfogged, new Color(2.4f, 1.4f, 0.55f)),
                Vector3.zero, Vector3.one, layer).transform.SetParent(_ring, false);

            // The sky: a gradient behind everything, its horizon at the camera's eye level.
            _skyPart = Part("Sky", Quad(), _sky, Vector3.zero, new Vector3(SkyWidth, SkyHeight, 1f), layer).transform;
        }

        /// <summary>The haze's colour, which the sky's horizon matches (Far3DArt's sky, the scene's fog).</summary>
        public static readonly Color FogColour = new Color(0.3f, 0.24f, 0.46f);

        /// <summary>How far the reveal has come: 0 as the era arrives, 1 once the ring is in place (Far3DView sets it).</summary>
        public float Reveal { get; set; } = 1f;

        /// <summary>
        /// Keeps the skyline, the ring and the sky ahead of the camera (they are too far away to near). The ring turns a station
        /// a stretch and drifts gently side to side as the run goes on, so it moves, slowly, like something immense; in the
        /// reveal it climbs from behind the horizon into place.
        /// </summary>
        public void Follow(Vector3 camera, float along)
        {
            if (!_built) return;
            Vector3 c = camera - Origin;
            _horizon.localPosition = new Vector3(c.x, 0f, c.z);
            _skyPart.localPosition = new Vector3(c.x, c.y + SkyHeight * (0.5f - HorizonV), c.z + SkyAt);
            float phase = along / Period * 2f * Mathf.PI;
            float rise = 1f - Reveal;
            rise *= rise;   // eases out: nearly in place a second after the rift
            _ring.localPosition = new Vector3(c.x + 160f * Mathf.Sin(phase), CameraHeight + RingHeight - RingRise * rise, c.z + RingAt);
            _ring.localRotation = Quaternion.Euler(-6f, along / Period * 15f, 7f);
        }

        /// <summary>The beacons' slow pulse at <paramref name="time"/> seconds (a whole number of pulses each loop).</summary>
        public void Animate(float time, float along)
        {
            if (!_built) return;
            float pulse = 0.4f + 0.6f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(time * 2f * Mathf.PI * 24f / LoopSeconds), 2f);
            _beacons.SetColor(BaseColor, Amber * pulse);
        }

        /// <summary>
        /// Where the camera is for a distance along its path: above the city, looking along the path to the horizon (tilted
        /// down so the horizon sits a third of the way down), drifting gently side to side. Wraps every Period, which the
        /// repeating city hides. With <paramref name="reveal"/> under 1 (the rift into the era) it starts low, tilted up a
        /// little, and rises into place.
        /// </summary>
        public static void CameraAt(float along, out Vector3 position, out Quaternion rotation, float reveal = 1f)
        {
            float a = along * 2f * Mathf.PI / Period;
            float low = 1f - Mathf.Clamp01(reveal);
            low = low * low * (3f - 2f * low);
            position = Origin + new Vector3(9f * Mathf.Sin(a), CameraHeight + 5f * Mathf.Sin(2f * a) - RevealDrop * low, Mathf.Repeat(along, Period));
            rotation = Quaternion.Euler(Mathf.Lerp(CameraPitch + Mathf.Sin(2f * a), RevealPitch, low), -2.5f * Mathf.Cos(a), 0f);
        }

        public static void CameraAt(float along, out Vector3 position, out Quaternion rotation) => CameraAt(along, out position, out rotation, 1f);

        Material Surface(Color colour)
        {
            var m = new Material(_tower) { name = "Far3DSurface" };
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
            if (_panels != null) m.SetTexture("_BaseMap", _panels);
            m.SetColor(BaseColor, colour);
            return m;
        }

        // Echo/Far3D Unfogged takes its colour as given (linear), so a colour meant to sit beside the fogged scene (which
        // works in gamma, like the fog colour) is converted.
        Material Unfogged(Color gamma) => Tinted(_unfogged, gamma.linear);

        Material Tinted(Material source, Color colour)
        {
            var m = new Material(source) { name = source.name + "Tinted" };
            m.SetColor(BaseColor, colour);
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

        // A thin box from a to b: an edge highlight.
        static Matrix4x4 Beam(Vector3 a, Vector3 b, float thick)
        {
            Vector3 d = b - a;
            return Matrix4x4.TRS((a + b) * 0.5f, Quaternion.LookRotation(d.normalized, Mathf.Abs(d.normalized.y) > 0.99f ? Vector3.forward : Vector3.up),
                new Vector3(thick, thick, d.magnitude));
        }

        // Beams round a polygon: a lit rim.
        static IEnumerable<Matrix4x4> RingOfBeams(Vector3 centre, float radius, int sides, float thick, float startDeg = 0f)
        {
            for (int k = 0; k < sides; k++)
            {
                float a0 = (startDeg + k * 360f / sides) * Mathf.Deg2Rad, a1 = (startDeg + (k + 1) * 360f / sides) * Mathf.Deg2Rad;
                yield return Beam(centre + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * radius,
                                  centre + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * radius, thick);
            }
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

        /// <summary>The panel texture covers this many units of surface.</summary>
        public const float PanelTileWidth = 60f, PanelTileHeight = 60f;

        // A box w x h x d whose side UVs tile with its size, so the panels stay one size on every structure.
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
                uv[i] = side ? new Vector2(uv[i].x * across / PanelTileWidth, uv[i].y * h / PanelTileHeight) : new Vector2(0.5f, 0.5f);
            }
            m.vertices = v;
            m.uv = uv;
            m.RecalculateBounds();
            return m;
        }

        // A faceted frustum, flat-shaded (each face its own vertices) so the moonlight picks out every facet; bottom at 0,
        // from radius r0 to r1 over h, with a cap on top. Side UVs tile with size like ScaledBox.
        static Mesh Prism(int sides, float r0, float r1, float h)
        {
            var v = new List<Vector3>();
            var nn = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * 2f * Mathf.PI / sides, a1 = (k + 1) * 2f * Mathf.PI / sides;
                Vector3 d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)), d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                Vector3 b0 = d0 * r0, b1 = d1 * r0, t0 = d0 * r1 + Vector3.up * h, t1 = d1 * r1 + Vector3.up * h;
                Vector3 normal = Vector3.Cross(t0 - b0, b1 - b0).normalized;
                if (Vector3.Dot(normal, d0 + d1) < 0f) normal = -normal;
                float width = (b1 - b0).magnitude / PanelTileWidth, height = h / PanelTileHeight;
                int o = v.Count;
                v.AddRange(new[] { b0, b1, t0, t1 });
                nn.AddRange(new[] { normal, normal, normal, normal });
                uv.AddRange(new[] { new Vector2(0f, 0f), new Vector2(width, 0f), new Vector2(0f, height), new Vector2(width, height) });
                t.AddRange(new[] { o, o + 2, o + 1, o + 2, o + 3, o + 1 });
            }
            int c = v.Count;
            v.Add(Vector3.up * h);
            nn.Add(Vector3.up);
            uv.Add(new Vector2(0.5f, 0.5f));
            for (int k = 0; k <= sides; k++)
            {
                float a = k * 2f * Mathf.PI / sides;
                v.Add(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r1 + Vector3.up * h);
                nn.Add(Vector3.up);
                uv.Add(new Vector2(0.5f, 0.5f));
            }
            for (int k = 0; k < sides; k++) t.AddRange(new[] { c, c + 1 + k, c + 2 + k });
            var m = new Mesh { name = "Far3DPrism" };
            m.SetVertices(v);
            m.SetNormals(nn);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        static Mesh Torus(float radius, float tube, int segments, int sides)
        {
            var m = new Mesh { name = "Far3DTorus" };
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
    }
}
