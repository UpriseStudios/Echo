using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>Full-screen white flash for the ghost clear (tech spec 11).</summary>
    [RequireComponent(typeof(Image))]
    public sealed class ScreenFlash : MonoBehaviour
    {
        const float Seconds = 0.4f;

        Image _image;
        float _left;

        void Awake()
        {
            _image = GetComponent<Image>();
            _image.raycastTarget = false;
            SetAlpha(0f);
        }

        public void Flash()
        {
            _left = Seconds;
            SetAlpha(0.85f);
        }

        void Update()
        {
            if (_left <= 0f) return;
            _left -= Time.unscaledDeltaTime;
            SetAlpha(Mathf.Max(0f, _left / Seconds) * 0.85f);
        }

        void SetAlpha(float a)
        {
            var c = _image.color;
            c.a = a;
            _image.color = c;
            _image.enabled = a > 0f;
        }
    }
}
