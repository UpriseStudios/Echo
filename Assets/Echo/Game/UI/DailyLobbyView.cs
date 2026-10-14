using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// The Daily Haunt lobby (Daily Haunt doc): today's course with its countdown, your best and rank, the top 10 with
    /// names and distances, yesterday's winner and Play. When it can't be played it says why ("needs a connection",
    /// "back soon"). Buttons only raise events; EchoGame decides what they do.
    /// </summary>
    public sealed class DailyLobbyView : MonoBehaviour
    {
        /// <summary>A line of the board.</summary>
        public struct Line
        {
            public int Rank;
            public string Name;
            public int Distance;
            public bool IsYou;
        }

        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _countdown;
        [SerializeField] TMP_Text _you;
        [SerializeField] TMP_Text[] _rows = new TMP_Text[0];
        [SerializeField] TMP_Text _watchHint;
        [SerializeField] TMP_Text _yesterday;
        [SerializeField] TMP_Text _status;
        [SerializeField] Button _play;
        [SerializeField] Button _back;
        [SerializeField] Color _youColor = Color.yellow;
        [SerializeField] Color _rowColor = Color.white;

        public event Action PlayClicked;
        public event Action BackClicked;

        /// <summary>A run on the board was tapped (its index in the lines given to SetDay): watch it.</summary>
        public event Action<int> RowClicked;

        int _lines;

        public bool IsOpen => _root.activeSelf;

        void Awake()
        {
            _play.onClick.AddListener(() => PlayClicked?.Invoke());
            _back.onClick.AddListener(() => BackClicked?.Invoke());
            for (int i = 0; i < _rows.Length; i++)
            {
                int index = i;
                var button = _rows[i].GetComponent<Button>();
                if (button != null) button.onClick.AddListener(() => { if (index < _lines) RowClicked?.Invoke(index); });
            }
        }

        public void Show()
        {
            _root.SetActive(true);
            SetMessage("Checking today's course...", playable: false);
        }

        public void Hide() => _root.SetActive(false);

        /// <summary>No course to play: offline, switched off, or loading. Clears the board.</summary>
        public void SetMessage(string text, bool playable)
        {
            _status.text = text;
            _play.gameObject.SetActive(playable);
            if (playable) return;
            _you.text = "";
            _yesterday.text = "";
            _lines = 0;
            if (_watchHint != null) _watchHint.gameObject.SetActive(false);
            foreach (TMP_Text row in _rows) row.text = "";
        }

        public void SetDay(string you, IReadOnlyList<Line> board, string yesterday, string status)
        {
            _status.text = status;
            _you.text = you;
            _yesterday.text = yesterday;
            for (int i = 0; i < _rows.Length; i++)
            {
                bool filled = i < board.Count;
                _rows[i].text = filled ? $"{board[i].Rank,2}  {board[i].Name}" + $"<pos=78%>{board[i].Distance} m" : (i == 0 ? "No runs yet today. Be the first ghost." : "");
                _rows[i].color = filled && board[i].IsYou ? _youColor : _rowColor;
            }
            _lines = board.Count;
            if (_watchHint != null) _watchHint.gameObject.SetActive(board.Count > 0);
            _play.gameObject.SetActive(true);
        }

        /// <summary>The status line alone (watching a run: fetching, no ad ready, offline).</summary>
        public void SetStatus(string text) => _status.text = text;

        public void SetCountdown(string text) => _countdown.text = text;
    }
}
