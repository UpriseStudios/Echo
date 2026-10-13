using Echo.Core.Rules;
using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// Lights Out twist (value-adds doc): darkness everywhere but a small dithered circle around the player,
    /// above the world and below the UI. Rendering only.
    /// </summary>
    public sealed class TwistOverlay : MonoBehaviour
    {
        const float Darkness = 0.94f;
        const float CentreHeight = 0.4f;

        [SerializeField] Material _material;
        [SerializeField] Sprite _spotlight;

        SpriteRenderer _dark;

        void Awake()
        {
            var go = new GameObject("LightsOut");
            go.transform.SetParent(transform, false);
            _dark = go.AddComponent<SpriteRenderer>();
            _dark.sharedMaterial = _material;
            _dark.sprite = _spotlight;
            _dark.sortingOrder = 40;
            _dark.color = new Color(0f, 0f, 0f, Darkness);
            _dark.enabled = false;
        }

        public void SetTwist(Twist twist) => _dark.enabled = twist == Twist.LightsOut && _spotlight != null;

        public void Render(Vector2 player)
        {
            if (!_dark.enabled) return;
            _dark.transform.localPosition = new Vector3(SimUnits.Pixel(player.x), SimUnits.Pixel(player.y + CentreHeight), 0f);
        }
    }
}
