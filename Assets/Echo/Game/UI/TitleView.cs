using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>Title over the idle course: logo, "Tap to run", best score, Skins / Stats / Settings (tech spec 14).</summary>
    public sealed class TitleView : MonoBehaviour
    {
        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _best;
        [SerializeField] GameObject _bestPill;
        [SerializeField] Button _skins;
        [SerializeField] Button _stats;
        [SerializeField] Button _settings;
        [SerializeField] Button _twist;
        [SerializeField] Button _powerUps;

        public event Action SkinsClicked;
        public event Action StatsClicked;
        public event Action SettingsClicked;
        public event Action TwistClicked;
        public event Action PowerUpsClicked;

        [SerializeField] Button _reels;
        public event Action ReelsClicked;

        // Exit (a user revision): Android only; iOS apps may not quit themselves.
        [SerializeField] Button _exit;
        public event Action ExitClicked;

        // Version 2.0: the Daily Haunt, with its countdown and a dot until today is played.
        [SerializeField] Button _daily;
        public event Action DailyClicked;

        // A build talking to the development server says so, so it is never mistaken for the store build.
        [SerializeField] GameObject _devServer;

        void Awake()
        {
            _skins.onClick.AddListener(() => SkinsClicked?.Invoke());
            _stats.onClick.AddListener(() => StatsClicked?.Invoke());
            _settings.onClick.AddListener(() => SettingsClicked?.Invoke());
            if (_twist != null) _twist.onClick.AddListener(() => TwistClicked?.Invoke());
            if (_powerUps != null) _powerUps.onClick.AddListener(() => PowerUpsClicked?.Invoke());
            if (_reels != null) _reels.onClick.AddListener(() => ReelsClicked?.Invoke());
            if (_exit != null) _exit.onClick.AddListener(() => ExitClicked?.Invoke());
            if (_daily != null) _daily.onClick.AddListener(() => DailyClicked?.Invoke());
        }

        public void SetDevServer(bool on)
        {
            if (_devServer != null && _devServer.activeSelf != on) _devServer.SetActive(on);
        }

        public void Show(int best)
        {
            _root.SetActive(true);
            _best.gameObject.SetActive(best > 0);
            if (_bestPill != null) _bestPill.SetActive(best > 0);
            _best.SetText("BEST {0}", best);
        }

        /// <summary>Weekly Twists banner: this week's twist (tap for Classic), or Classic (tap for the twist). Null hides it.</summary>
        public void SetTwist(string text)
        {
            if (_twist == null) return;
            _twist.gameObject.SetActive(text != null);
            if (text != null) _twist.GetComponentInChildren<TMP_Text>().text = text;
        }

        public void Hide() => _root.SetActive(false);

        /// <summary>The Daily Haunt button's label ("Daily Haunt · 5h 12m left"), or null to hide it.</summary>
        public void SetDaily(string label)
        {
            if (_daily == null) return;
            _daily.gameObject.SetActive(label != null);
            if (label != null) _daily.GetComponentInChildren<TMP_Text>().text = label;
        }

        public void SetExitVisible(bool visible)
        {
            if (_exit != null) _exit.gameObject.SetActive(visible);
        }
    }
}
