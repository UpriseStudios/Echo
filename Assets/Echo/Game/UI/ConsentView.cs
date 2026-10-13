using System;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// First-launch privacy choice (tech spec 19), shown before any ad or analytics SDK starts.
    /// Also reachable from Settings to change the choice later.
    /// </summary>
    public sealed class ConsentView : MonoBehaviour
    {
        [SerializeField] GameObject _root;
        [SerializeField] Button _acceptAll;
        [SerializeField] Button _essentialOnly;

        /// <summary>(personalisedAds, analytics)</summary>
        public event Action<bool, bool> Chosen;

        void Awake()
        {
            _acceptAll.onClick.AddListener(() => Chosen?.Invoke(true, true));
            _essentialOnly.onClick.AddListener(() => Chosen?.Invoke(false, false));
        }

        public void Show() => _root.SetActive(true);
        public void Hide() => _root.SetActive(false);
    }
}
