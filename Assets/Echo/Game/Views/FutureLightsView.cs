using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Echo.Game.Views
{
    /// <summary>
    /// World Time Run doc, phase 3: the Far Future's real-time lighting. A dim global light and pools of neon light
    /// drifting over the HD backdrop (the only lit sprites; the pixel gameplay layer stays unlit and bright, so it always
    /// stands out). Reduced effects keep only the global light, raised so the world still looks as painted.
    /// </summary>
    public sealed class FutureLightsView : MonoBehaviour
    {
        [SerializeField] Light2D _global;
        [SerializeField] Light2D[] _neon = new Light2D[0];

        public const float FullGlobal = 0.85f;
        public const float ReducedGlobal = 1f;

        bool _on, _full;

        /// <summary>Lights on in the Far Future; the neon pools only with full effects.</summary>
        public void Set(bool on, bool full)
        {
            _on = on;
            _full = full;
            if (_global != null)
            {
                _global.enabled = on;
                _global.intensity = full ? FullGlobal : ReducedGlobal;
            }
            foreach (Light2D l in _neon)
                if (l != null) l.enabled = on && full;
        }

        /// <summary>The lights switched on now (tests).</summary>
        public int LightsOn
        {
            get
            {
                int n = _global != null && _global.enabled ? 1 : 0;
                foreach (Light2D l in _neon) if (l != null && l.enabled) n++;
                return n;
            }
        }

        // Each pool drifts across the view at its own pace and height and breathes, like signs and passing cars.
        public void Render(float left, float right, float bottom, float height, float time)
        {
            if (!_on || !_full) return;
            float span = right - left + 6f;
            for (int i = 0; i < _neon.Length; i++)
            {
                Light2D l = _neon[i];
                if (l == null) continue;
                float p = Mathf.Repeat(i * 0.37f - time * (0.02f + 0.013f * i), 1f);
                float x = left - 3f + p * span;
                float y = bottom + height * (0.42f + 0.07f * i % 0.3f) + 0.4f * Mathf.Sin(time * 0.7f + i * 1.9f);
                l.transform.position = new Vector3(x, y, 0f);
                l.intensity = 0.75f + 0.25f * Mathf.Sin(time * (1.3f + 0.4f * i) + i);
            }
        }
    }
}
