using System;
using Echo.Core.Rules;
using Echo.Game.Cosmetics;
using Echo.Game.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// Power-ups (a user revision): a card listing the four, how many of each are held, and a rewarded-ad button
    /// to earn one more. Opened from the title, the death screen and the Skins screen; never during a run.
    /// </summary>
    public sealed class PowerUpsView : MonoBehaviour
    {
        [SerializeField] GameObject _root;
        [Tooltip("One row per power-up, in PowerUpRules.All order: its text and its watch button.")]
        [SerializeField] TMP_Text[] _rows = new TMP_Text[0];
        [SerializeField] Button[] _watch = new Button[0];
        [SerializeField] Button _close;

        PowerUpsSaveData _held;
        Func<bool> _adReady;
        bool _adWasReady;
        float _adCheckIn;

        public event Action<PowerUp> WatchClicked;
        public event Action Closed;

        public bool IsOpen => _root.activeSelf;

        void Awake()
        {
            for (int i = 0; i < _watch.Length && i < PowerUpRules.All.Length; i++)
            {
                PowerUp p = PowerUpRules.All[i];
                _watch[i].onClick.AddListener(() => WatchClicked?.Invoke(p));
            }
            _close.onClick.AddListener(() => Closed?.Invoke());
        }

        public void Show(PowerUpsSaveData held, Func<bool> adReady)
        {
            _held = held;
            _adReady = adReady;
            _root.SetActive(true);
            Refresh();
        }

        // The next ad loads a few seconds after one is watched: redraw when it becomes ready (or stops being).
        void Update()
        {
            if (_held == null || !_root.activeSelf || (_adCheckIn -= Time.unscaledDeltaTime) > 0f) return;
            _adCheckIn = 0.5f;
            if ((_adReady == null || _adReady()) != _adWasReady) Refresh();
        }

        public void Refresh()
        {
            if (_held == null) return;
            bool ready = _adReady == null || _adReady();
            _adWasReady = ready;
            for (int i = 0; i < _rows.Length && i < PowerUpRules.All.Length; i++)
            {
                PowerUp p = PowerUpRules.All[i];
                int count = PowerUpInventory.Count(_held, p);
                _rows[i].text = $"{PowerUpRules.Name(p)}  <color=#FFC94A>{count}/{PowerUpRules.MaxHeld}</color>\n<size=70%>{PowerUpRules.Description(p)}</size>";
                bool full = !PowerUpInventory.CanAdd(_held, p);
                _watch[i].interactable = !full && ready;
                _watch[i].GetComponentInChildren<TMP_Text>().text = full ? "Full" : ready ? "Watch ad" : "No ad yet";
            }
        }

        public void Hide() => _root.SetActive(false);
    }
}
