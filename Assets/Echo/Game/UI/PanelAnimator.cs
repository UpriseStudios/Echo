using UnityEngine;

namespace Echo.Game.UI
{
    /// <summary>Fades and scales a screen in whenever it is shown.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PanelAnimator : MonoBehaviour
    {
        const float Seconds = 0.18f;
        const float StartScale = 0.96f;

        CanvasGroup _group;
        float _t;

        void Awake() => _group = GetComponent<CanvasGroup>();

        void OnEnable()
        {
            _t = 0f;
            Apply(0f);
        }

        void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / Seconds);
            Apply(1f - (1f - _t) * (1f - _t));
        }

        void Apply(float k)
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
            _group.alpha = k;
            transform.localScale = Vector3.one * Mathf.Lerp(StartScale, 1f, k);
        }
    }
}
