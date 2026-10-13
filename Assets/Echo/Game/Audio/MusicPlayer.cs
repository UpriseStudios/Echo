using UnityEngine;

namespace Echo.Game.Audio
{
    /// <summary>
    /// Layered music (tech spec 12): 6 stems of equal length started together with PlayScheduled.
    /// Stem k (1-5) fades in over 0.5 s once 5k ghosts are on screen; stem 0 always plays.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        public const int GhostsPerLayer = 5;
        const float FadeSeconds = 0.5f;
        const float MasterVolume = 0.6f;

        [SerializeField] AudioClip[] _stems = new AudioClip[0];

        AudioSource[] _sources;
        float[] _level;
        int _ghosts;
        bool _enabled = true;

        void Start()
        {
            _sources = new AudioSource[_stems.Length];
            _level = new float[_stems.Length];
            double startAt = AudioSettings.dspTime + 0.2;
            for (int i = 0; i < _stems.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.clip = _stems[i];
                src.loop = true;
                src.playOnAwake = false;
                src.volume = 0f;
                src.PlayScheduled(startAt);
                _sources[i] = src;
            }
            if (_level.Length > 0) _level[0] = 1f;
        }

        public void SetGhostsOnScreen(int ghosts) => _ghosts = ghosts;

        public void SetEnabled(bool enabled) => _enabled = enabled;

        public static bool LayerWanted(int stem, int ghostsOnScreen) => stem == 0 || ghostsOnScreen >= GhostsPerLayer * stem;

        void Update()
        {
            if (_sources == null) return;
            float step = Time.unscaledDeltaTime / FadeSeconds;
            for (int i = 0; i < _sources.Length; i++)
            {
                float target = LayerWanted(i, _ghosts) ? 1f : 0f;
                _level[i] = Mathf.MoveTowards(_level[i], target, step);
                _sources[i].volume = _enabled ? _level[i] * MasterVolume : 0f;
            }
        }
    }
}
