using UnityEngine;

namespace Echo.Game.Audio
{
    /// <summary>One-shot sound effects (tech spec 12). Ghosts are silent, so only the player's events play.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SfxPlayer : MonoBehaviour
    {
        [SerializeField] AudioClip _jump;
        [SerializeField] AudioClip _nearMiss;
        [SerializeField] AudioClip _death;
        [SerializeField] AudioClip _ghostClear;
        [SerializeField] AudioClip _nemesisSting;
        [SerializeField] AudioClip _graveThud;
        [SerializeField] AudioClip _chisel;
        [SerializeField] AudioClip _howl;

        AudioSource _source;
        bool _enabled = true;

        void Awake() => _source = GetComponent<AudioSource>();

        public void SetEnabled(bool enabled) => _enabled = enabled;

        public void Jump() => Play(_jump, 0.5f);
        public void NearMiss() => Play(_nearMiss, 0.7f);
        public void Death() => Play(_death, 0.9f);
        public void GhostClear() => Play(_ghostClear, 0.9f);
        public void NemesisSting() => Play(_nemesisSting, 0.9f);
        public void GraveThud() => Play(_graveThud, 0.8f);
        public void Chisel() => Play(_chisel, 0.35f);
        public void Howl() => Play(_howl, 0.7f);

        void Play(AudioClip clip, float volume)
        {
            if (_enabled && clip != null) _source.PlayOneShot(clip, volume);
        }
    }
}
