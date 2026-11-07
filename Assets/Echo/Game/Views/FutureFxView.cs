using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// World Time Run doc (art pass phase 5): the Far Future's particles. Rain splashes on the wet floor (rings that open and
    /// fade where the rain lands) and glowing motes drifting up through the air, bright enough for the bloom to catch. Pure
    /// functions of time and the view from fixed pools, so nothing is created or allocated while running. The motes drift
    /// behind the floor and the gameplay (sorting order -15); the splashes sit on the floor's edge, under every obstacle.
    /// Art fix phase 2: drawn through the HDR glow material, so the motes really bloom; and the 2D Far Future's atmosphere:
    /// searchlight beams sweeping slowly up from the city behind the mid-distance towers (-24) and low mist drifting between
    /// the chrome towers and the street (-20).
    /// </summary>
    public sealed class FutureFxView : MonoBehaviour
    {
        [SerializeField] Material _material;
        [SerializeField] Sprite _splash;
        [SerializeField] Sprite _mote;
        [SerializeField] Sprite _beam;
        [SerializeField] Sprite _mist;
        [Tooltip("Plain sprites: the mist is soft, never bright.")]
        [SerializeField] Material _plainMaterial;

        public const int Splashes = 18;
        public const int Motes = 24;
        public const int Beams = 4;
        public const int Wisps = 7;
        public const int BeamOrder = -24;
        public const int MistOrder = -20;
        public const int SortingOrder = -15;
        public const int SplashOrder = 1;   // the floor edge's order, drawn just in front of it; hazards are 2
        const float SplashSeconds = 0.45f;

        readonly SpriteRenderer[] _splashes = new SpriteRenderer[Splashes];
        readonly SpriteRenderer[] _motes = new SpriteRenderer[Motes];
        readonly SpriteRenderer[] _beams = new SpriteRenderer[Beams];
        readonly SpriteRenderer[] _wisps = new SpriteRenderer[Wisps];
        bool _on, _atmosphere;

        void Awake()
        {
            for (int i = 0; i < Splashes; i++)
            {
                _splashes[i] = Make("Splash" + i, _splash);
                _splashes[i].sortingOrder = SplashOrder;
            }
            for (int i = 0; i < Motes; i++) _motes[i] = Make("Mote" + i, _mote);
            for (int i = 0; i < Beams; i++)
            {
                _beams[i] = Make("Beam" + i, _beam);
                _beams[i].sortingOrder = BeamOrder;
            }
            for (int i = 0; i < Wisps; i++)
            {
                _wisps[i] = Make("Mist" + i, _mist);
                _wisps[i].sortingOrder = MistOrder;
                if (_plainMaterial != null) _wisps[i].sharedMaterial = _plainMaterial;
            }
        }

        SpriteRenderer Make(string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = _material;
            sr.sprite = sprite;
            sr.sortingOrder = SortingOrder;
            sr.enabled = false;
            return sr;
        }

        public void SetOn(bool on) => SetOn(on, false);

        /// <summary>Splashes and motes; <paramref name="atmosphere"/> adds the searchlights and mist.</summary>
        public void SetOn(bool on, bool atmosphere)
        {
            _on = on;
            _atmosphere = on && atmosphere;
            if (!_atmosphere)
            {
                foreach (SpriteRenderer sr in _beams) if (sr != null) sr.enabled = false;
                foreach (SpriteRenderer sr in _wisps) if (sr != null) sr.enabled = false;
            }
            if (on) return;
            foreach (SpriteRenderer sr in _splashes) if (sr != null) sr.enabled = false;
            foreach (SpriteRenderer sr in _motes) if (sr != null) sr.enabled = false;
        }

        /// <summary>Searchlight beams and mist wisps drawn now (tests).</summary>
        public int AtmosphereShown
        {
            get
            {
                int n = 0;
                foreach (SpriteRenderer sr in _beams) if (sr != null && sr.enabled) n++;
                foreach (SpriteRenderer sr in _wisps) if (sr != null && sr.enabled) n++;
                return n;
            }
        }

        /// <summary>Particles drawn now (tests).</summary>
        public int Shown
        {
            get
            {
                int n = 0;
                foreach (SpriteRenderer sr in _splashes) if (sr != null && sr.enabled) n++;
                foreach (SpriteRenderer sr in _motes) if (sr != null && sr.enabled) n++;
                return n;
            }
        }

        static float Hash01(int a, int b) => (((uint)(a * 73856093) ^ (uint)(b * 19349663)) % 1000u) / 1000f;

        public void Render(float left, float right, float bottom, float height, float time)
        {
            if (!_on) return;
            float width = right - left;
            for (int i = 0; i < Splashes; i++)
            {
                // Each splash lands at its own time and place, then the cycle moves it on.
                float cycle = time / SplashSeconds + Hash01(i, 1) * 7f;
                int n = (int)cycle;
                float t = cycle - n;
                SpriteRenderer sr = _splashes[i];
                sr.enabled = true;
                float x = left + Hash01(i, n) * width;
                sr.transform.localPosition = new Vector3(x, 0.02f, -0.1f);
                float s = Mathf.Lerp(0.15f, 0.55f, t);
                sr.transform.localScale = new Vector3(s, s * 0.35f, 1f);
                sr.color = new Color(0.7f, 0.95f, 1f, 0.4f * (1f - t));
            }
            for (int i = 0; i < Motes; i++)
            {
                SpriteRenderer sr = _motes[i];
                sr.enabled = true;
                float u = Hash01(i, 11), v = Hash01(i, 12);
                float rise = Mathf.Repeat(v + time * (0.03f + 0.03f * u), 1f);
                float x = left + Mathf.Repeat(u * width + 0.4f * Mathf.Sin(time * 0.6f + i) - left * 0.1f, width);
                sr.transform.localPosition = new Vector3(x, bottom + height * (0.3f + 0.6f * rise), 0f);
                float s = 0.08f + 0.06f * Hash01(i, 13);
                sr.transform.localScale = new Vector3(s, s, 1f);
                bool magenta = i % 3 == 0;
                float glow = Mathf.Sin(rise * Mathf.PI);
                sr.color = magenta ? new Color(1f, 0.35f, 0.9f, 0.8f * glow) : new Color(0.4f, 1f, 1f, 0.8f * glow);
            }
            if (!_atmosphere) return;
            for (int i = 0; i < Beams; i++)
            {
                // Each searchlight stands somewhere in the city (drifting past slowly, at the mid towers' parallax) and sweeps.
                SpriteRenderer sr = _beams[i];
                float span = width + 6f;
                float x = left - 3f + Mathf.Repeat(i * span / Beams + Hash01(i, 21) * 2f - left * 0.09f, span);
                sr.enabled = true;
                sr.transform.localPosition = new Vector3(x, bottom + height * 0.2f, 0f);
                float angle = 18f * Mathf.Sin(time * (0.18f + 0.05f * i) + i * 1.7f) + (i % 2 == 0 ? -8f : 8f);
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                sr.transform.localScale = new Vector3(1.6f, 1.5f, 1f);
                float pulse = 0.85f + 0.15f * Mathf.Sin(time * 0.7f + i);
                sr.color = i % 2 == 0 ? new Color(0.45f, 0.95f, 1f, 0.16f * pulse) : new Color(1f, 0.45f, 0.95f, 0.13f * pulse);
            }
            for (int i = 0; i < Wisps; i++)
            {
                SpriteRenderer sr = _wisps[i];
                float span = width + 8f;
                float x = left - 4f + Mathf.Repeat(i * span / Wisps + time * (0.15f + 0.1f * Hash01(i, 31)) - left * 0.25f, span);
                sr.enabled = true;
                sr.transform.localPosition = new Vector3(x, 1.6f + 1.4f * Hash01(i, 32) + 0.15f * Mathf.Sin(time * 0.3f + i), -0.2f);
                float s = 2.2f + 1.6f * Hash01(i, 33);
                sr.transform.localScale = new Vector3(s, s * 0.8f, 1f);
                sr.color = new Color(0.62f, 0.55f, 0.9f, 0.16f + 0.06f * Mathf.Sin(time * 0.4f + i * 2f));
            }
        }
    }
}
