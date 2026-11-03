using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// Time Run's time rift (World Time Run doc): at each era boundary a 0.3 s flash and a bright ripple sweeping across the
    /// screen. Drawn behind the floor, the runner and the ghosts (sorting order between the scenery and the course), so it
    /// never hides them or an obstacle. Its two sprites are made in the scene; it allocates nothing while it plays.
    /// </summary>
    public sealed class TimeRiftView : MonoBehaviour
    {
        public const float Seconds = 0.3f;
        public const int SortingOrder = -16;

        [SerializeField] SpriteRenderer _flash;
        [SerializeField] SpriteRenderer _ripple;

        float _left = -1f;

        /// <summary>Playing now (tests).</summary>
        public bool Playing => _left > 0f;

        public void Play()
        {
            _left = Seconds;
            _flash.enabled = _ripple.enabled = true;
        }

        void Awake() => _flash.enabled = _ripple.enabled = false;

        /// <summary>Each frame, with the camera's view.</summary>
        public void Render(float left, float right, float bottom, float height)
        {
            if (_left <= 0f) return;
            _left -= Time.unscaledDeltaTime;
            if (_left <= 0f)
            {
                _flash.enabled = _ripple.enabled = false;
                return;
            }
            float t = 1f - _left / Seconds;
            float width = right - left;
            Vector2 flashSize = _flash.sprite.bounds.size;
            _flash.transform.position = new Vector3((left + right) * 0.5f, bottom + height * 0.5f, 0f);
            _flash.transform.localScale = new Vector3(width / flashSize.x, height / flashSize.y, 1f);
            _flash.color = new Color(1f, 1f, 1f, 0.45f * (1f - t) * (1f - t));

            Vector2 rippleSize = _ripple.sprite.bounds.size;
            float x = Mathf.Lerp(left - 2f, right + 2f, t);
            _ripple.transform.position = new Vector3(x, bottom + height * 0.5f, 0f);
            _ripple.transform.localScale = new Vector3(3f / rippleSize.x, height * 1.2f / rippleSize.y, 1f);
            _ripple.color = new Color(0.8f, 0.95f, 1f, 0.85f * Mathf.Sin(Mathf.PI * t));
        }
    }
}
