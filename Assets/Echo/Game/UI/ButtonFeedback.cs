using UnityEngine;
using UnityEngine.EventSystems;

namespace Echo.Game.UI
{
    /// <summary>Buttons sink slightly under the finger and spring back on release.</summary>
    public sealed class ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        const float Pressed = 0.93f;
        const float Speed = 18f;

        float _target = 1f;

        public void OnPointerDown(PointerEventData eventData) => _target = Pressed;
        public void OnPointerUp(PointerEventData eventData) => _target = 1f;
        public void OnPointerExit(PointerEventData eventData) => _target = 1f;

        void OnDisable()
        {
            _target = 1f;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            float s = transform.localScale.x;
            if (Mathf.Approximately(s, _target)) return;
            s = Mathf.Lerp(s, _target, 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
