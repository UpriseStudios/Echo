using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// The Modes screen (modes doc), from the title: the main game, the Daily Haunt and any live event modes as cards,
    /// each with its name, a countdown and a dot when it hasn't been played today. Buttons only raise events; EchoGame
    /// decides what they do.
    /// </summary>
    public sealed class ModesView : MonoBehaviour
    {
        /// <summary>One card: title, a line under it (the countdown), and the not-played-today dot.</summary>
        public struct Card
        {
            public string Title;
            public string Detail;
            public bool Dot;
        }

        [SerializeField] GameObject _root;
        [SerializeField] Button[] _cards = new Button[0];
        [SerializeField] TMP_Text _status;
        [SerializeField] Button _back;

        public event Action<int> CardClicked;
        public event Action BackClicked;

        int _shown;

        public bool IsOpen => _root.activeSelf;

        /// <summary>How many cards there are room for.</summary>
        public int Capacity => _cards.Length;

        void Awake()
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                int index = i;
                _cards[i].onClick.AddListener(() => { if (index < _shown) CardClicked?.Invoke(index); });
            }
            _back.onClick.AddListener(() => BackClicked?.Invoke());
        }

        public void Show()
        {
            _root.SetActive(true);
            SetStatus("");
        }

        public void Hide() => _root.SetActive(false);

        public void SetCards(IReadOnlyList<Card> cards)
        {
            _shown = Mathf.Min(cards.Count, _cards.Length);
            for (int i = 0; i < _cards.Length; i++)
            {
                bool on = i < _shown;
                _cards[i].gameObject.SetActive(on);
                if (!on) continue;
                Card c = cards[i];
                _cards[i].GetComponentInChildren<TMP_Text>().text =
                    $"{c.Title}{(c.Dot ? "  <color=#FFC94A>●</color>" : "")}\n<size=70%><color=#9FB8C0>{c.Detail}</color></size>";
            }
        }

        public void SetStatus(string text) => _status.text = text;
    }
}
