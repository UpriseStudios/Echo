using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// In-run HUD (tech spec 14): score top-centre, ghost count top-right, a held 1-UP top-left, plus the
    /// "close" pop for a near-miss, the first-run tap hint, the 3-2-1 resume countdown and short toasts.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        const float PopSeconds = 0.6f;
        const float ToastSeconds = 2.5f;

        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _score;
        [SerializeField] TMP_Text _ghosts;
        [SerializeField] TMP_Text _countdown;
        [SerializeField] TMP_Text _boardCloses;
        [SerializeField] TMP_Text _nearMiss;
        [SerializeField] TMP_Text _tapHint;
        [SerializeField] TMP_Text _toast;
        [SerializeField] GameObject _life;
        [Tooltip("Power-ups (a user revision): Shield, Phase and Lantern buttons along the floor, tapped to use one.")]
        [SerializeField] Button[] _powerUps = new Button[0];

        readonly Queue<string> _toasts = new Queue<string>(8);
        int _shownScore = -1, _shownGhosts = -1, _shownCountdown = -1;
        float _popLeft, _toastLeft;

        void Awake()
        {
            // TMP grows its text and mesh buffers the first time a number gains a digit, which would allocate
            // mid-run (tech spec 18). Render the widest values once so the buffers are already big enough.
            _score.SetText("{0}", 99999999);
            _score.ForceMeshUpdate();
            _ghosts.SetText("{0}", 99);
            _ghosts.ForceMeshUpdate();
            // Every character a toast can show, so the font's atlas never has to add a glyph mid-run (a "1-UP!"
            // toast allocated 16 KB the first time), then the longest toast, so the mesh is big enough.
            _toast.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz 0123456789 !?-:.,'+#·");
            _toast.ForceMeshUpdate();
            _toast.SetText("WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW"); // longer than any toast
            _toast.ForceMeshUpdate();
            _score.SetText("{0}", 0);

            // Same for the 1-UP heart: build its label mesh now, not the first time a life is earned mid-run.
            if (_life != null)
            {
                _life.SetActive(true);
                foreach (TMP_Text text in _life.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                _life.SetActive(false);
            }

            // And show the toast once with the canvas rebuilt, so its first real appearance costs nothing either.
            _toast.SetText("1-UP!");
            _toast.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            _toast.SetText("WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW");
            _toast.ForceMeshUpdate();

            _nearMiss.gameObject.SetActive(false);
            _toast.gameObject.SetActive(false);
            _tapHint.gameObject.SetActive(false);
        }

        /// <summary>A power-up button was tapped: Shield, Phase or Lantern. The tap is not a jump.</summary>
        public event System.Action<Echo.Core.Rules.PowerUp> PowerUpClicked;

        static readonly Echo.Core.Rules.PowerUp[] Usable =
            { Echo.Core.Rules.PowerUp.Shield, Echo.Core.Rules.PowerUp.Phase, Echo.Core.Rules.PowerUp.Lantern };

        void Start()
        {
            for (int i = 0; i < _powerUps.Length && i < Usable.Length; i++)
            {
                Echo.Core.Rules.PowerUp p = Usable[i];
                _powerUps[i].onClick.AddListener(() => PowerUpClicked?.Invoke(p));
            }
        }

        /// <summary>Which power-up buttons show along the floor: held, and not yet used this run.</summary>
        public void SetPowerUps(bool shield, bool phase, bool lantern)
        {
            Show(0, shield);
            Show(1, phase);
            Show(2, lantern);
        }

        void Show(int i, bool on)
        {
            if (i < _powerUps.Length && _powerUps[i].gameObject.activeSelf != on) _powerUps[i].gameObject.SetActive(on);
        }

        public void SetVisible(bool visible) => _root.SetActive(visible);

        // SetText with int arguments avoids a string allocation per frame (tech spec 18).
        public void SetScore(int score)
        {
            if (score == _shownScore) return;
            _shownScore = score;
            _score.SetText("{0}", score);
        }

        public void SetGhosts(int ghosts)
        {
            if (ghosts == _shownGhosts) return;
            _shownGhosts = ghosts;
            _ghosts.SetText("{0}", ghosts);
        }

        /// <summary>Shows the 1-UP heart while a life is held.</summary>
        public void SetLives(int lives)
        {
            bool show = lives > 0;
            if (_life != null && _life.activeSelf != show) _life.SetActive(show);
        }

        /// <summary>0 hides the countdown.</summary>
        public void SetCountdown(int seconds)
        {
            if (seconds == _shownCountdown) return;
            _shownCountdown = seconds;
            _countdown.gameObject.SetActive(seconds > 0);
            if (seconds > 0) _countdown.SetText("{0}", seconds);
        }

        // Written into a fixed buffer, so the once-a-second update allocates nothing mid-run (tech spec 18).
        readonly char[] _boardText = "Board closes in 0:00".ToCharArray();
        int _shownBoardCloses = -1;

        /// <summary>Daily Haunt: "Board closes in m:ss"; 0 hides it.</summary>
        public void SetBoardCloses(int seconds)
        {
            if (_boardCloses == null || seconds == _shownBoardCloses) return;
            _shownBoardCloses = seconds;
            _boardCloses.gameObject.SetActive(seconds > 0);
            if (seconds <= 0) return;
            DailyClock.Write(seconds, _boardText);
            _boardCloses.SetCharArray(_boardText, 0, _boardText.Length);
        }

        public void SetTapHint(bool visible)
        {
            if (_tapHint.gameObject.activeSelf != visible) _tapHint.gameObject.SetActive(visible);
        }

        /// <summary>Game spec: a near-miss shows a small "close" pop.</summary>
        public void PopNearMiss()
        {
            _popLeft = PopSeconds;
            _nearMiss.gameObject.SetActive(true);
        }

        public void QueueToast(string text) => _toasts.Enqueue(text);

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_popLeft > 0f)
            {
                _popLeft -= dt;
                float t = Mathf.Clamp01(_popLeft / PopSeconds);
                _nearMiss.alpha = t;
                _nearMiss.rectTransform.localScale = Vector3.one * (1f + 0.3f * (1f - t));
                if (_popLeft <= 0f) _nearMiss.gameObject.SetActive(false);
            }

            if (_toastLeft > 0f)
            {
                _toastLeft -= dt;
                _toast.alpha = Mathf.Clamp01(_toastLeft / 0.4f);
                if (_toastLeft <= 0f) _toast.gameObject.SetActive(false);
            }
            else if (_toasts.Count > 0 && _root.activeInHierarchy)
            {
                _toast.text = _toasts.Dequeue();
                _toast.alpha = 1f;
                _toast.gameObject.SetActive(true);
                _toastLeft = ToastSeconds;
            }
        }
    }
}
