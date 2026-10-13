using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// Pooled square shards for the death shatter and the ghost-clear pop (tech spec 11). Purely visual,
    /// uses UnityEngine.Random and frame time; nothing here feeds back into the sim.
    /// </summary>
    public sealed class ShardBurst : MonoBehaviour
    {
        const int PoolSize = 400;
        const float Gravity = -16f;
        const float ShardSize = 0.12f;

        [SerializeField] Material _material;

        SpriteRenderer[] _renderers;
        Vector2[] _velocity;
        float[] _life;
        float[] _maxLife;
        Color[] _color;
        int _next;

        void Awake()
        {
            _renderers = new SpriteRenderer[PoolSize];
            _velocity = new Vector2[PoolSize];
            _life = new float[PoolSize];
            _maxLife = new float[PoolSize];
            _color = new Color[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                _renderers[i] = BoxSprite.Create(transform, "Shard", _material, 20);
                _renderers[i].enabled = false;
            }
        }

        /// <summary>Breaks a rectangle (bottom-left corner, size) into <paramref name="count"/> shards flying outwards.</summary>
        public void Burst(Vector2 corner, Vector2 size, Color color, int count, float speed = 6f, float life = 0.7f)
        {
            Vector2 centre = corner + size * 0.5f;
            for (int n = 0; n < count; n++)
            {
                int i = _next;
                _next = (_next + 1) % PoolSize;
                var p = new Vector2(corner.x + Random.value * size.x, corner.y + Random.value * size.y);
                Vector2 dir = (p - centre).sqrMagnitude > 1e-6f ? (p - centre).normalized : Random.insideUnitCircle.normalized;
                _velocity[i] = dir * (speed * (0.4f + Random.value)) + Vector2.up * (speed * 0.5f);
                _maxLife[i] = _life[i] = life * (0.6f + 0.4f * Random.value);
                _color[i] = color;
                var r = _renderers[i];
                r.transform.localPosition = p;
                r.transform.localScale = new Vector3(ShardSize, ShardSize, 1f);
                r.color = color;
                r.enabled = true;
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < PoolSize; i++)
            {
                if (_life[i] <= 0f) continue;
                _life[i] -= dt;
                var r = _renderers[i];
                if (_life[i] <= 0f)
                {
                    r.enabled = false;
                    continue;
                }
                _velocity[i].y += Gravity * dt;
                r.transform.localPosition += (Vector3)(_velocity[i] * dt);
                float t = _life[i] / _maxLife[i];
                Color c = _color[i];
                c.a *= t;
                r.color = c;
                r.transform.localScale = new Vector3(ShardSize * t, ShardSize * t, 1f);
            }
        }
    }
}
