using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// World Time Run doc (art pass phase 5): the Far Future's particles. Rain splashes on the wet floor (rings that open and
    /// fade where the rain lands) and glowing motes drifting up through the air, bright enough for the bloom to catch. Pure
    /// functions of time and the view from fixed pools, so nothing is created or allocated while running. The motes drift
    /// behind the floor and the gameplay (sorting order -15); the splashes sit on the floor's edge, under every obstacle.
    /// </summary>
    public sealed class FutureFxView : MonoBehaviour
    {
        [SerializeField] Material _material;
        [SerializeField] Sprite _splash;
        [SerializeField] Sprite _mote;

        public const int Splashes = 18;
        public const int Motes = 24;
        public const int SortingOrder = -15;
        public const int SplashOrder = 1;   // the floor edge's order, drawn just in front of it; hazards are 2
        const float SplashSeconds = 0.45f;

        readonly SpriteRenderer[] _splashes = new SpriteRenderer[Splashes];
        readonly SpriteRenderer[] _motes = new SpriteRenderer[Motes];
        bool _on;

        void Awake()
        {
            for (int i = 0; i < Splashes; i++)
            {
                _splashes[i] = Make("Splash" + i, _splash);
                _splashes[i].sortingOrder = SplashOrder;
            }
            for (int i = 0; i < Motes; i++) _motes[i] = Make("Mote" + i, _mote);
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

        public void SetOn(bool on)
        {
            _on = on;
            if (on) return;
            foreach (SpriteRenderer sr in _splashes) if (sr != null) sr.enabled = false;
            foreach (SpriteRenderer sr in _motes) if (sr != null) sr.enabled = false;
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
                sr.color = new Color(0.7f, 0.95f, 1f, 0.55f * (1f - t));
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
                sr.color = magenta ? new Color(1.6f, 0.5f, 1.4f, 0.8f * glow) : new Color(0.6f, 1.6f, 1.8f, 0.8f * glow);
            }
        }
    }
}
