using Echo.Core.Courses;
using Echo.Core.Sim;
using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// The Easter egg hunt (seasons doc): during Easter, up to 2 eggs hang over a main-game run, at chunk boundaries (clear
    /// floor by the chunk contract) just above the standing runner's head, so any hop reaches them. Touching one collects
    /// it; it changes nothing about movement or scoring, and lives only here, outside the simulation, so replays and the
    /// server never see it. Eggs found count towards Egg Hunt (AchievementRules.OnEggsFound). Allocates nothing per frame.
    /// </summary>
    public sealed class EggHuntView : MonoBehaviour
    {
        public const int PerRun = 2;

        /// <summary>The egg's centre above the floor, and its size, in sub-units: over the 0.8-unit runner, under a hop.</summary>
        const int HeightSub = 1300, SizeSub = 400;

        const int FirstChunk = 3, ChunkSpread = 10;

        [SerializeField] Sprite _egg;
        [SerializeField] Material _material;

        SpriteRenderer[] _eggs;
        readonly int[] _x = new int[PerRun];
        readonly bool[] _taken = new bool[PerRun];
        int _count;

        /// <summary>Eggs collected this run.</summary>
        public int Found { get; private set; }

        void Awake()
        {
            _eggs = new SpriteRenderer[PerRun];
            for (int i = 0; i < PerRun; i++)
            {
                var go = new GameObject("Egg" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _egg;
                if (_material != null) sr.sharedMaterial = _material;
                sr.sortingOrder = 2;   // over the floor and graves, under spikes, ghosts and the player
                sr.enabled = false;
                _eggs[i] = sr;
            }
        }

        /// <summary>Hides the hunt (not Easter, or not a main-game run).</summary>
        public void Clear()
        {
            _count = 0;
            Found = 0;
            if (_eggs == null) return;
            foreach (SpriteRenderer sr in _eggs) sr.enabled = false;
        }

        /// <summary>Hides two eggs on this run's course, chosen from its seed and run index, so each run hides them anew.</summary>
        public void Plant(Course course, int runIndex)
        {
            Clear();
            uint h = (uint)(course.Seed * 2654435761u) ^ (uint)(runIndex * 40503);
            int first = FirstChunk + (int)(h % ChunkSpread);
            int second = first + 2 + (int)((h >> 8) % ChunkSpread);
            _x[0] = course.ChunkStartX(first);
            _x[1] = course.ChunkStartX(second);
            _taken[0] = _taken[1] = false;
            _count = PerRun;
        }

        /// <summary>Eggs the runner touches this frame (0 almost always).</summary>
        public int Collect(in RunnerState player, in SimConfig cfg)
        {
            if (_count == 0) return 0;
            Aabb box = Runner.PlayerBox(player, cfg);
            int found = 0;
            for (int i = 0; i < _count; i++)
            {
                if (_taken[i]) continue;
                var egg = new Aabb(_x[i] - SizeSub / 2, HeightSub - SizeSub / 2, _x[i] + SizeSub / 2, HeightSub + SizeSub / 2);
                if (!box.Overlaps(egg)) continue;
                _taken[i] = true;
                found++;
            }
            Found += found;
            return found;
        }

        /// <summary>The eggs bob gently in place.</summary>
        public void Render(float time)
        {
            if (_eggs == null) return;
            for (int i = 0; i < PerRun; i++)
            {
                SpriteRenderer sr = _eggs[i];
                sr.enabled = i < _count && !_taken[i];
                if (!sr.enabled) continue;
                float bob = Mathf.Sin(time * 3f + i) * 0.06f;
                sr.transform.localPosition = new Vector3(SimUnits.Pixel(SimUnits.ToWorld(_x[i])), SimUnits.Pixel(SimUnits.ToWorld(HeightSub) + bob), 0f);
            }
        }

        /// <summary>Where an egg hangs, in world units (tests).</summary>
        public Vector2 EggAt(int i) => new Vector2(SimUnits.ToWorld(_x[i]), SimUnits.ToWorld(HeightSub));
    }
}
