using UnityEngine;

namespace Echo.Game.World
{
    /// <summary>
    /// World Time Run doc, phase 3: notices a phone that can't keep the Far Future's full effects smooth (a smoothed frame
    /// rate under 45 fps for 4 seconds), so the game can switch to reduced effects on its own. Allocation-free.
    /// </summary>
    public sealed class FrameWatch
    {
        public const float SlowFps = 45f;
        public const float SlowSeconds = 4f;

        float _average = 1f / 60f, _slowFor;

        public void Reset()
        {
            _average = 1f / 60f;
            _slowFor = 0f;
        }

        /// <summary>One frame's time; true once it has been slow for long enough.</summary>
        public bool Feed(float dt)
        {
            dt = Mathf.Clamp(dt, 0f, 0.25f);
            _average = Mathf.Lerp(_average, dt, 0.05f);
            _slowFor = _average > 1f / SlowFps ? _slowFor + dt : 0f;
            return _slowFor >= SlowSeconds;
        }
    }
}
