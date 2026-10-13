using UnityEngine;

namespace Echo.Game.UI
{
    /// <summary>Keeps this RectTransform inside Screen.safeArea (tech spec 1).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect _applied;
        Vector2Int _screen;

        void Update()
        {
            Rect safe = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (safe == _applied && screen == _screen) return;
            _applied = safe;
            _screen = screen;
            if (screen.x <= 0 || screen.y <= 0) return;

            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            rt.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
