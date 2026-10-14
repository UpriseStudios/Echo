using System;
using Echo.Game.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// Settings (tech spec 14): music, sound effects, haptics, privacy choices. Development builds also get
    /// a toggle that makes the mock rewarded ad fail (tech spec 15).
    /// </summary>
    public sealed class SettingsView : MonoBehaviour
    {
        [SerializeField] GameObject _root;
        [SerializeField] Button _music;
        [SerializeField] Button _sfx;
        [SerializeField] Button _haptics;
        [SerializeField] Button _glow;
        [SerializeField] Button _privacy;
        [SerializeField] Button _deleteOnline;
        [SerializeField] Button _howToPlay;
        [SerializeField] Button _devAdFailure;
        // Version 2.0, development builds: replay the newest reel on the server and compare.
        [SerializeField] Button _serverCheck;
        public event System.Action ServerCheckClicked;
        // Version 2.0, development builds: step the Daily Haunt's date to test midnight.
        [SerializeField] Button _dailyDate;
        public event Func<string> DailyDateClicked;
        [SerializeField] Button _close;
        [Tooltip("The name on Ghost Challenges.")]
        [SerializeField] Button _name;
        [Tooltip("Development builds: the services status readout.")]
        [SerializeField] TMP_Text _status;
        [Tooltip("The heading: five quick taps show the services status, even in release builds.")]
        [SerializeField] Button _heading;

        int _headingTaps;
        float _headingTapAt;

        SettingsSaveData _settings;

        public event Action Changed;
        public event Action PrivacyClicked;
        public event Action DeleteOnlineClicked;
        public event Action HowToPlayClicked;
        public event Func<bool> DevAdFailureToggled;
        public event Action Closed;
        public event Action NameClicked;
        public event Action StatusRequested;

        void Awake()
        {
            _music.onClick.AddListener(() => Toggle(() => _settings.Music = !_settings.Music));
            _sfx.onClick.AddListener(() => Toggle(() => _settings.Sfx = !_settings.Sfx));
            _haptics.onClick.AddListener(() => Toggle(() => _settings.Haptics = !_settings.Haptics));
            _glow.onClick.AddListener(() => Toggle(() => _settings.Glow = !_settings.Glow));
            _privacy.onClick.AddListener(() => PrivacyClicked?.Invoke());
            if (_deleteOnline != null) _deleteOnline.onClick.AddListener(() => DeleteOnlineClicked?.Invoke());
            if (_howToPlay != null) _howToPlay.onClick.AddListener(() => HowToPlayClicked?.Invoke());
            _devAdFailure.onClick.AddListener(() =>
            {
                bool failing = DevAdFailureToggled?.Invoke() ?? false;
                SetLabel(_devAdFailure, $"Dev: ads {(failing ? "fail" : "succeed")}");
            });
            _close.onClick.AddListener(() => Closed?.Invoke());
            if (_serverCheck != null) _serverCheck.onClick.AddListener(() => ServerCheckClicked?.Invoke());
            if (_dailyDate != null) _dailyDate.onClick.AddListener(() =>
            {
                string label = DailyDateClicked?.Invoke();
                if (label != null) SetLabel(_dailyDate, label);
            });
            if (_name != null) _name.onClick.AddListener(() => NameClicked?.Invoke());
            if (_heading != null) _heading.onClick.AddListener(OnHeadingTapped);
        }

        public void Show(SettingsSaveData settings, bool showDevToggle, bool showServerCheck = false)
        {
            _settings = settings;
            _root.SetActive(true);
            _devAdFailure.gameObject.SetActive(showDevToggle);
            if (_serverCheck != null) _serverCheck.gameObject.SetActive(showServerCheck);
            if (_dailyDate != null) _dailyDate.gameObject.SetActive(showServerCheck);
            Refresh();
        }

        public void Hide() => _root.SetActive(false);

        /// <summary>The services status readout, or null to hide it (release builds).</summary>
        public void SetStatus(string text)
        {
            if (_status == null) return;
            _status.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (!string.IsNullOrEmpty(text)) _status.text = text;
        }

        void OnHeadingTapped()
        {
            _headingTaps = Time.unscaledTime - _headingTapAt < 1f ? _headingTaps + 1 : 1;
            _headingTapAt = Time.unscaledTime;
            if (_headingTaps < 5) return;
            _headingTaps = 0;
            StatusRequested?.Invoke();
        }

        /// <summary>Redraws after the name changes.</summary>
        public void RefreshLabels()
        {
            if (_settings != null) Refresh();
        }

        void Toggle(Action flip)
        {
            flip();
            Refresh();
            Changed?.Invoke();
        }

        void Refresh()
        {
            SetLabel(_music, Toggle("Music", _settings.Music));
            SetLabel(_sfx, Toggle("Sound effects", _settings.Sfx));
            SetLabel(_haptics, Toggle("Haptics", _settings.Haptics));
            SetLabel(_glow, Toggle("Glow", _settings.Glow));
            if (_name != null)
                SetLabel(_name, $"Name<pos=60%><color=#FFC94A>{(string.IsNullOrEmpty(_settings.PlayerName) ? "not set" : _settings.PlayerName)}</color>");
        }

        // Name on the left, state on the right: amber ON, dim OFF.
        static string Toggle(string name, bool on) =>
            $"{name}<pos=80%>{(on ? "<color=#FFC94A>ON</color>" : "<color=#FFFFFF66>OFF</color>")}";

        static void SetLabel(Button b, string text) => b.GetComponentInChildren<TMP_Text>().text = text;
    }
}
