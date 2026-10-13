using UnityEngine;
using UnityEngine.InputSystem;

namespace Echo.Game.Input
{
    /// <summary>
    /// Touch press, left mouse button and Space all produce one tap (tech spec 13). Several presses in one
    /// frame count as one. Held reports whether any of them is still down, for the variable jump.
    /// </summary>
    public sealed class TapInput
    {
        /// <summary>Screen position of a pointer tap; null for a keyboard tap, which is never over UI.</summary>
        public Vector2? Position { get; private set; }

        /// <summary>Any touch, the left mouse button or Space is down after this frame's Poll.</summary>
        public bool Held { get; private set; }

        public bool Poll()
        {
            Position = null;
            bool tapped = false;

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (!touch.press.wasPressedThisFrame) continue;
                    tapped = true;
                    Position = touch.position.ReadValue();
                    break;
                }
            }

            var mouse = Mouse.current;
            if (!tapped && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                tapped = true;
                Position = mouse.position.ReadValue();
            }

            var keyboard = Keyboard.current;
            if (!tapped && keyboard != null && keyboard.spaceKey.wasPressedThisFrame) tapped = true;

            bool held = false;
            if (touchscreen != null)
                foreach (var touch in touchscreen.touches)
                    if (touch.press.isPressed) { held = true; break; }
            held |= mouse != null && mouse.leftButton.isPressed;
            held |= keyboard != null && keyboard.spaceKey.isPressed;
            Held = held;

            return tapped;
        }
    }
}
