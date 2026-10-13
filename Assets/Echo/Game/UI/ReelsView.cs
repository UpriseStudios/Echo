using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// The reel cupboard (a user revision): five slots for saved runs, each with Watch, Clip (make and share a GIF)
    /// and Delete. When a sixth is saved it opens in replace mode, where each slot offers Replace instead.
    /// Buttons only raise events; EchoGame decides what they do.
    /// </summary>
    public sealed class ReelsView : MonoBehaviour
    {
        /// <summary>What one slot shows.</summary>
        public struct Card
        {
            public Sprite Preview;
            public string Title;
            public string Detail;
            public bool Playable;
        }

        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _heading;
        [SerializeField] TMP_Text _hint;
        [SerializeField] GameObject[] _slots = new GameObject[0];
        [SerializeField] Image[] _previews = new Image[0];
        [SerializeField] TMP_Text[] _titles = new TMP_Text[0];
        [SerializeField] TMP_Text[] _details = new TMP_Text[0];
        [SerializeField] Button[] _watch = new Button[0];
        [SerializeField] Button[] _clip = new Button[0];
        [SerializeField] Button[] _delete = new Button[0];
        [SerializeField] Button[] _replace = new Button[0];
        [SerializeField] Button _close;

        public event Action<int> WatchClicked;
        public event Action<int> ClipClicked;
        public event Action<int> DeleteClicked;
        public event Action<int> ReplaceClicked;
        public event Action CloseClicked;

        public bool IsOpen => _root.activeSelf;
        public bool ReplaceMode { get; private set; }

        void Awake()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                int slot = i;
                _watch[i].onClick.AddListener(() => WatchClicked?.Invoke(slot));
                _clip[i].onClick.AddListener(() => ClipClicked?.Invoke(slot));
                _delete[i].onClick.AddListener(() => DeleteClicked?.Invoke(slot));
                _replace[i].onClick.AddListener(() => ReplaceClicked?.Invoke(slot));
            }
            _close.onClick.AddListener(() => CloseClicked?.Invoke());
        }

        /// <param name="replaceMode">The cupboard is full and a new run is waiting: pick one to replace.</param>
        public void Show(IReadOnlyList<Card> cards, bool replaceMode)
        {
            ReplaceMode = replaceMode;
            _root.SetActive(true);
            _heading.text = replaceMode ? "CUPBOARD FULL" : "REEL CUPBOARD";
            _hint.text = replaceMode
                ? "Pick a reel to replace with this run."
                : cards.Count == 0 ? "Save a run from the death screen to keep it here." : $"{cards.Count} of {_slots.Length} saved";
            Label(_close, replaceMode ? "Cancel" : "Back");
            for (int i = 0; i < _slots.Length; i++)
            {
                bool filled = i < cards.Count;
                _previews[i].enabled = filled && cards[i].Preview != null;
                if (filled) _previews[i].sprite = cards[i].Preview;
                _titles[i].text = filled ? cards[i].Title : "Empty slot";
                _details[i].text = filled ? cards[i].Detail : "";
                bool playable = filled && cards[i].Playable;
                _watch[i].gameObject.SetActive(filled && !replaceMode);
                _clip[i].gameObject.SetActive(filled && !replaceMode);
                _delete[i].gameObject.SetActive(filled && !replaceMode);
                _watch[i].interactable = playable;
                _clip[i].interactable = playable;
                _replace[i].gameObject.SetActive(filled && replaceMode);
            }
        }

        public void Hide() => _root.SetActive(false);

        static void Label(Button b, string text) => b.GetComponentInChildren<TMP_Text>().text = text;
    }
}
