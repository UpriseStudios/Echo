using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// The bar under a reel playing back (a user revision): what is playing, Exit and the speed while it plays,
    /// then Watch again, Make clip and Back at the end. While a clip is made it shows only its progress.
    /// </summary>
    public sealed class ReplayView : MonoBehaviour
    {
        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _label;
        [SerializeField] TMP_Text _status;
        [SerializeField] Button _exit;
        [SerializeField] Button _speed;
        [SerializeField] Button _again;
        [SerializeField] Button _clip;
        [SerializeField] Button _back;

        public event Action ExitClicked;
        public event Action SpeedClicked;
        public event Action AgainClicked;
        public event Action ClipClicked;

        public bool IsOpen => _root.activeSelf;

        void Awake()
        {
            _exit.onClick.AddListener(() => ExitClicked?.Invoke());
            _back.onClick.AddListener(() => ExitClicked?.Invoke());
            _speed.onClick.AddListener(() => SpeedClicked?.Invoke());
            _again.onClick.AddListener(() => AgainClicked?.Invoke());
            _clip.onClick.AddListener(() => ClipClicked?.Invoke());
        }

        public void ShowPlaying(string label, float speed)
        {
            _root.SetActive(true);
            _label.text = label;
            _status.text = "";
            SetSpeed(speed);
            Buttons(playing: true, ended: false);
        }

        public void SetSpeed(float speed) => _speed.GetComponentInChildren<TMP_Text>().text = speed > 1f ? "Speed 2x" : "Speed 1x";

        public void ShowEnded()
        {
            _status.text = "";
            Buttons(playing: false, ended: true);
        }

        /// <summary>A clip is being made: no buttons, just how it is going.</summary>
        public void ShowMaking(string label, string status)
        {
            _root.SetActive(true);
            _label.text = label;
            _status.text = status;
            Buttons(playing: false, ended: false);
        }

        public void SetStatus(string status) => _status.text = status;

        public void Hide() => _root.SetActive(false);

        void Buttons(bool playing, bool ended)
        {
            _exit.gameObject.SetActive(playing);
            _speed.gameObject.SetActive(playing);
            _again.gameObject.SetActive(ended);
            _clip.gameObject.SetActive(ended);
            _back.gameObject.SetActive(ended);
        }
    }
}
