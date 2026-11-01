using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// Ghost Town (a user decision; "Modes" in code), from the title: the online hub where Echo's community meets. Your
    /// profile line, the Daily Haunt as the hero card (runners today, the leader, your place, the countdown), then a card
    /// per event: live ones first, then those coming up, each in its own colour with its live player count. Cards
    /// only raise events; EchoGame decides what they do.
    /// </summary>
    public sealed class ModesView : MonoBehaviour
    {
        /// <summary>One event's card.</summary>
        public struct Event
        {
            public string Id;
            public string Name;
            public string Pitch;
            public string Players;
            public string Countdown;
            public bool Live;
            public Color Colour;
        }

        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _profile;
        [Header("The Daily Haunt")]
        [SerializeField] Button _hero;
        [SerializeField] TMP_Text _heroRunners;
        [SerializeField] TMP_Text _heroLeader;
        [SerializeField] TMP_Text _heroYou;
        [SerializeField] TMP_Text _heroCountdown;
        [SerializeField] GameObject _heroDot;
        [Header("Events")]
        [SerializeField] GameObject _liveHeader;
        [SerializeField] GameObject _soonHeader;
        [SerializeField] Button[] _events = new Button[0];
        [SerializeField] TMP_Text _status;
        [SerializeField] Button _back;

        /// <summary>The Daily Haunt card, or an event card, by mode id (DailyId for the Daily Haunt).</summary>
        public event Action<string> ModeClicked;
        public event Action BackClicked;

        public const string DailyId = "daily";

        string[] _ids = new string[0];

        public bool IsOpen => _root.activeSelf;

        /// <summary>How many event cards there are room for.</summary>
        public int Capacity => _events.Length;

        void Awake()
        {
            _ids = new string[_events.Length];
            _hero.onClick.AddListener(() => ModeClicked?.Invoke(DailyId));
            for (int i = 0; i < _events.Length; i++)
            {
                int index = i;
                _events[i].onClick.AddListener(() => { if (_ids[index] != null) ModeClicked?.Invoke(_ids[index]); });
            }
            _back.onClick.AddListener(() => BackClicked?.Invoke());
        }

        public void Show()
        {
            _root.SetActive(true);
            SetStatus("");
        }

        public void Hide() => _root.SetActive(false);

        public void SetProfile(string line) => _profile.text = line;

        public void SetHero(string runners, string leader, string you, string countdown, bool dot, bool enabled)
        {
            _heroRunners.text = runners;
            _heroLeader.text = leader;
            _heroYou.text = you;
            _heroCountdown.text = countdown;
            _heroDot.SetActive(dot);
            _hero.interactable = enabled;
        }

        /// <summary>The events: live ones under LIVE NOW, the rest under COMING UP; a section with none is hidden.</summary>
        public void SetEvents(IReadOnlyList<Event> events)
        {
            int live = 0, soon = 0, used = 0, sibling = 0;
            foreach (Event e in events)
                if (e.Live) live++;
                else soon++;
            _liveHeader.SetActive(live > 0);
            _liveHeader.transform.SetSiblingIndex(sibling++);
            foreach (bool wantLive in new[] { true, false })
            {
                if (!wantLive)
                {
                    _soonHeader.SetActive(soon > 0);
                    _soonHeader.transform.SetSiblingIndex(sibling++);
                }
                foreach (Event e in events)
                {
                    if (e.Live != wantLive || used == _events.Length) continue;
                    Button card = _events[used];
                    _ids[used] = e.Id;
                    Fill(card, e);
                    card.gameObject.SetActive(true);
                    card.transform.SetSiblingIndex(sibling++);
                    used++;
                }
            }
            for (int i = used; i < _events.Length; i++)
            {
                _ids[i] = null;
                _events[i].gameObject.SetActive(false);
            }
        }

        static readonly Color SoonChip = new Color(1f, 1f, 1f, 0.16f);
        static readonly Color Ink = new Color(0.06f, 0.16f, 0.19f);

        static void Fill(Button card, Event e)
        {
            Transform t = card.transform;
            t.Find("Stripe").GetComponent<Image>().color = e.Live ? e.Colour : new Color(e.Colour.r, e.Colour.g, e.Colour.b, 0.35f);
            t.Find("Name").GetComponent<TMP_Text>().text = e.Name;
            t.Find("Pitch").GetComponent<TMP_Text>().text = e.Pitch;
            var players = t.Find("Players").GetComponent<TMP_Text>();
            players.text = e.Players;
            players.color = e.Colour;
            t.Find("Countdown").GetComponent<TMP_Text>().text = e.Countdown;
            Transform chip = t.Find("Chip");
            chip.GetComponent<Image>().color = e.Live ? e.Colour : SoonChip;
            var chipLabel = chip.Find("Label").GetComponent<TMP_Text>();
            chipLabel.text = e.Live ? "LIVE" : "SOON";
            chipLabel.color = e.Live ? Ink : Color.white;
        }

        public void SetStatus(string text) => _status.text = text;
    }
}
