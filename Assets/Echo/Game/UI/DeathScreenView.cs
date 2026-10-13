using System;
using TMPro;
using Echo.Game.Views;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// Death screen (tech spec 14). Buttons only raise events; EchoGame applies the rules.
    /// The grave reveal (a user revision): when the run leaves a grave, a large headstone grows out of the small
    /// grave in the world while the camera zooms in on it, lands with a thud, and then the run's epitaph is
    /// chiselled into it one letter at a time. It is only a view: the buttons work, and a tap retries, throughout.
    /// </summary>
    public sealed class DeathScreenView : MonoBehaviour
    {
        /// <summary>The height of the screen, 0 to 1, that the stone stands on: the camera puts the ground there.</summary>
        public const float StoneBase = 0.42f;
        const float ChiselPerSecond = 28f;     // letters cut per second once it has landed
        // The 56-px-wide art over the 96 world pixels a 3x zoom shows across: each stone pixel matches a world pixel.
        const float StoneWidth = 56f / (CameraRig.PixelsAcross / (float)RevealZoom);

        /// <summary>How far the camera zooms in on the grave; the stone's pixels are drawn to match.</summary>
        public const int RevealZoom = 3;

        [Header("Grave reveal")]
        [SerializeField] RectTransform _headstone;
        [Tooltip("Engraved lines, top to bottom: HERE LIES, the run, the epitaph, the distance.")]
        [SerializeField] TMP_Text[] _carved = new TMP_Text[0];
        [Tooltip("The lit lower edge of each engraved line, drawn just below it.")]
        [SerializeField] TMP_Text[] _carvedLight = new TMP_Text[0];

        Rect _graveViewport;
        float _stoneTime = -1f, _stoneDelay, _stoneSeconds, _chisel;
        bool _landed;
        int _totalLetters, _lastLetters;

        /// <summary>The headstone hit the ground: EchoGame adds the thud, the dust and a little shake.</summary>
        public event Action HeadstoneLanded;

        /// <summary>One more letter cut into the stone.</summary>
        public event Action Chiselled;
        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _score;
        [SerializeField] TMP_Text _best;
        [SerializeField] TMP_Text _killer;
        [SerializeField] TMP_Text _message;
        [SerializeField] Button _continue;
        [SerializeField] Button _banish;
        [SerializeField] Button _share;
        [SerializeField] Button _menu;
        [SerializeField] Button _duel;
        [Tooltip("Ghost Challenges: send this run to a friend (or, after a challenge, send one back).")]
        [SerializeField] Button _challenge;
        [Tooltip("Power-ups: earn one with a rewarded ad for the next run.")]
        [SerializeField] Button _powerUps;
        [SerializeField] TMP_Text _banner;

        public event Action ContinueClicked;
        public event Action BanishClicked;
        public event Action ShareClicked;
        public event Action MenuClicked;
        public event Action DuelClicked;
        public event Action ChallengeClicked;
        public event Action PowerUpsClicked;

        // Highlights reel (a user revision): keep this run in the reel cupboard.
        [SerializeField] Button _saveReel;
        public event Action SaveReelClicked;

        void Awake()
        {
            _continue.onClick.AddListener(() => ContinueClicked?.Invoke());
            _banish.onClick.AddListener(() => BanishClicked?.Invoke());
            _share.onClick.AddListener(() => ShareClicked?.Invoke());
            _menu.onClick.AddListener(() => MenuClicked?.Invoke());
            if (_duel != null) _duel.onClick.AddListener(() => DuelClicked?.Invoke());
            if (_challenge != null) _challenge.onClick.AddListener(() => ChallengeClicked?.Invoke());
            if (_powerUps != null) _powerUps.onClick.AddListener(() => PowerUpsClicked?.Invoke());
            if (_saveReel != null) _saveReel.onClick.AddListener(() => SaveReelClicked?.Invoke());
        }

        /// <summary>The Save reel button: its label, gold to suggest it on a new best, null to hide it.</summary>
        public void SetSaveReel(string label, bool highlight, bool interactable)
        {
            if (_saveReel == null) return;
            _saveReel.gameObject.SetActive(label != null);
            if (label == null) return;
            _saveReel.GetComponentInChildren<TMP_Text>().text = label;
            _saveReel.interactable = interactable;
            _saveReel.GetComponent<Image>().color = highlight ? HighlightColor : _saveReelColor;
        }

        static readonly Color HighlightColor = new Color(1f, 0.79f, 0.29f);
        [SerializeField] Color _saveReelColor = Color.white;

        /// <param name="bestLabel">"Best", or "Week best" for a Weekly Twist run (its own board).</param>
        public void Show(int score, int best, bool newBest, string killerLine, string message, string bestLabel = "Best")
        {
            _root.SetActive(true);
            _score.SetText("{0}", score);
            _best.text = newBest ? $"New {bestLabel.ToLowerInvariant()}!" : $"{bestLabel} {best}";
            _killer.text = killerLine;
            _message.text = message;
            SetBanner(null);
        }

        public void SetOffers(bool canContinue, bool canBanish, bool banishTen = false)
        {
            _continue.gameObject.SetActive(canContinue);
            _banish.gameObject.SetActive(canBanish);
            // Balance addendum: Banish 10 takes the single Banish's place while it is available.
            if (canBanish)
                _banish.GetComponentInChildren<TMP_Text>().text = banishTen
                    ? "Banish 10 ghosts · ad\n<size=60%>The ones that keep getting you</size>"
                    : "Banish ghost · ad";
        }

        public void SetMessage(string message) => _message.text = message;

        /// <summary>The Challenge button's label, or null to hide it.</summary>
        public void SetChallenge(string label)
        {
            if (_challenge == null) return;
            _challenge.gameObject.SetActive(label != null);
            if (label != null) _challenge.GetComponentInChildren<TMP_Text>().text = label;
        }

        /// <summary>Nemesis Duel: offered in place of Banish; null hides it.</summary>
        public void SetDuel(string nemesisName)
        {
            if (_duel == null) return;
            _duel.gameObject.SetActive(nemesisName != null);
            if (nemesisName != null) _duel.GetComponentInChildren<TMP_Text>().text = $"Duel {nemesisName}";
        }

        /// <summary>Ghost Photo Mode (skins addendum): on a new best the Share button takes a photo of the frozen ghosts.</summary>
        public void SetPhoto(bool on) => _share.GetComponentInChildren<TMP_Text>().text = on ? "PHOTO" : "Share";

        /// <summary>The 1-second "Nemesis defeated" moment before the ghost clear (tech spec 11). Null hides it.</summary>
        public void SetBanner(string text)
        {
            _banner.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (!string.IsNullOrEmpty(text)) _banner.text = text;
        }

        public void Hide()
        {
            HideHeadstone();
            _root.SetActive(false);
        }

        /// <summary>
        /// Starts the grave reveal: after <paramref name="delay"/> the stone grows from the world grave to its place
        /// over <paramref name="seconds"/> (the camera's zoom uses the same timing), then the lines are chiselled.
        /// </summary>
        public void ShowHeadstone(string name, string line, string footer, float delay, float seconds)
        {
            if (_headstone == null) return;
            string[] lines = { "HERE LIES", name, line, footer };
            _totalLetters = 0;
            for (int i = 0; i < _carved.Length && i < lines.Length; i++)
            {
                _carved[i].text = lines[i];
                if (i < _carvedLight.Length) _carvedLight[i].text = lines[i];
                if (i > 0) _totalLetters += lines[i].Length;
            }
            _stoneDelay = delay;
            _stoneSeconds = Mathf.Max(0.01f, seconds);
            _stoneTime = 0f;
            _chisel = 0f;
            _landed = false;
            _lastLetters = -1;
            _graveViewport = new Rect(0.5f, StoneBase, StoneWidth, 0f);
            Reveal(0);
            _headstone.gameObject.SetActive(false);
        }

        public void HideHeadstone()
        {
            _stoneTime = -1f;
            if (_headstone != null) _headstone.gameObject.SetActive(false);
        }

        /// <summary>Where the small grave is on screen this frame (viewport units), so the stone grows out of it.</summary>
        public void TrackGrave(Rect viewport)
        {
            if (viewport.width > 0f) _graveViewport = viewport;
        }

        void Update()
        {
            if (_stoneTime < 0f || _headstone == null) return;
            _stoneTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01((_stoneTime - _stoneDelay) / _stoneSeconds);
            if (_stoneTime < _stoneDelay) return;
            if (!_headstone.gameObject.activeSelf) _headstone.gameObject.SetActive(true);

            // From the small grave to the stone's own place, eased like the camera. Everything is in fractions of the
            // screen (the stone is anchored across StoneWidth of it and keeps its shape), so any resolution matches.
            float e = t * t * (3f - 2f * t);
            float x = Mathf.Lerp(_graveViewport.center.x, 0.5f, e);
            float y = Mathf.Lerp(_graveViewport.yMin, StoneBase, e);
            // The screen to the safe area this screen lives in (a notch insets it).
            Rect safe = Screen.safeArea;
            float sw = Mathf.Max(1f, safe.width) / Mathf.Max(1, Screen.width), sh = Mathf.Max(1f, safe.height) / Mathf.Max(1, Screen.height);
            float sx = safe.x / Mathf.Max(1, Screen.width), sy = safe.y / Mathf.Max(1, Screen.height);
            float ax = (x - sx) / sw, ay = (y - sy) / sh, half = StoneWidth / 2f / sw;
            _headstone.anchorMin = new Vector2(ax - half, ay);
            _headstone.anchorMax = new Vector2(ax + half, ay);
            _headstone.anchoredPosition = Vector2.zero;
            float scale = Mathf.Lerp(Mathf.Clamp(_graveViewport.width / StoneWidth, 0.02f, 1f), 1f, e);
            _headstone.localScale = new Vector3(scale, scale, 1f);

            if (t < 1f) return;
            if (!_landed)
            {
                _landed = true;
                Reveal(0);
                HeadstoneLanded?.Invoke();
            }
            if (_lastLetters >= _totalLetters) return;
            _chisel += Time.unscaledDeltaTime * ChiselPerSecond;
            int letters = Mathf.Min(_totalLetters, (int)_chisel);
            if (letters == _lastLetters) return;
            Reveal(letters);
            if (letters % 2 == 0) Chiselled?.Invoke();
        }

        // "HERE LIES" is carved from the start; the rest appears letter by letter, line after line.
        void Reveal(int letters)
        {
            _lastLetters = letters;
            for (int i = 0; i < _carved.Length; i++)
            {
                int shown = i == 0 ? int.MaxValue : Mathf.Max(0, letters);
                if (i > 0) letters -= _carved[i].text.Length;
                _carved[i].maxVisibleCharacters = shown;
                if (i < _carvedLight.Length) _carvedLight[i].maxVisibleCharacters = shown;
            }
        }
    }
}
