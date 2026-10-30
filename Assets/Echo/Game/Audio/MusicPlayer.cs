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

        AudioClip[] _playing;

        void Start()
        {
            _sources = new AudioSource[_stems.Length];
            _level = new float[_stems.Length];
            for (int i = 0; i < _stems.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.loop = true;
                src.playOnAwake = false;
                src.volume = 0f;
                _sources[i] = src;
            }
            if (_level.Length > 0) _level[0] = 1f;
            Play(_wanted ?? _stems);
        }

        AudioClip[] _wanted;

        /// <summary>
        /// A user revision: a season's own stems (Christmas), or null for the main tune. Every stem restarts together, so
        /// they stay in step; only called between runs (seasons doc: checked at the title and run start).
        /// </summary>
        public void SetStems(AudioClip[] stems)
        {
            AudioClip[] wanted = stems != null && stems.Length == _stems.Length ? stems : _stems;
            _wanted = wanted;
            if (_sources != null && wanted != _playing) Play(wanted);
        }

        /// <summary>The stems playing now (tests).</summary>
        public AudioClip[] Playing => _playing;

        void Play(AudioClip[] stems)
        {
            _playing = stems;
            double startAt = AudioSettings.dspTime + 0.2;
            for (int i = 0; i < _sources.Length; i++)
            {
                _sources[i].Stop();
                _sources[i].clip = stems[i];
                _sources[i].PlayScheduled(startAt);
            }
        }

        public void SetGhostsOnScreen(int ghosts) => _ghosts = ghosts;

        public void SetEnabled(bool enabled) => _enabled = enabled;

        AudioSource _stinger;

        /// <summary>A season's short festive variation (seasons doc), once over the music; silent with the music off.</summary>
        public void PlayStinger(AudioClip clip)
        {
            if (clip == null || !_enabled) return;
            if (_stinger == null)
            {
                _stinger = gameObject.AddComponent<AudioSource>();
                _stinger.playOnAwake = false;
            }
            _stinger.PlayOneShot(clip, MasterVolume);
        }

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
