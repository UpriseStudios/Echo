using TMPro;
using UnityEngine;

namespace Echo.Game.UI
{
    /// <summary>Gently pulses a text's opacity and scale, for "tap" prompts.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class Pulse : MonoBehaviour
    {
        [SerializeField] float _speed = 3.2f;
        [SerializeField] float _minAlpha = 0.45f;

        TMP_Text _text;

        void Awake() => _text = GetComponent<TMP_Text>();

        void Update()
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * _speed);
            _text.alpha = Mathf.Lerp(_minAlpha, 1f, t);
            transform.localScale = Vector3.one * (1f + 0.04f * t);
        }
    }
}
