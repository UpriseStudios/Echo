using UnityEngine;

namespace Echo.Game.UI
{
    /// <summary>
    /// A menu laid out on the 1080 x 1920 design canvas, centred and scaled down only when the safe area is smaller,
    /// so its lists, cards and buttons keep their places relative to each other on every screen shape (a user
    /// report: on tall phones a list that stretched with the screen scrolled over buttons that didn't). The panel's
    /// background is a child that reaches far past the frame, so the whole screen is still covered.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DesignFrame : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(1080, 1920);

        Vector2 _fitted = new Vector2(-1, -1);

        /// <summary>How much the frame is scaled down to fit (1 on any screen at least 1080 x 1920 in canvas units).</summary>
        public float Scale { get; private set; } = 1f;

        void OnEnable() => Fit();

        /// <summary>Fits now (screenshots change the canvas size within a frame).</summary>
        public void Refit()
        {
            _fitted = new Vector2(-1, -1);
            Fit();
        }

        void LateUpdate() => Fit();

        void Fit()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null) return;
            Vector2 room = parent.rect.size;
            if (room == _fitted) return;
            _fitted = room;
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Size;
            Scale = Mathf.Min(1f, room.x / Size.x, room.y / Size.y);
            rt.localScale = new Vector3(Scale, Scale, 1f);
        }
    }
}
