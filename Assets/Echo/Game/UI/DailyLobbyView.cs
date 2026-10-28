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
        // A user revision: while the board loads, the player's own character runs and jumps on the empty board.
        [SerializeField] RectTransform _loader;
        [SerializeField] Image _loaderFigure;
        [SerializeField] RectTransform _loaderShadow;

        const float RunFps = 10f, JumpEvery = 1.5f, JumpTime = 0.6f, JumpHeight = 120f;

        Sprite[] _runFrames = new Sprite[0];
        Sprite _jumpFrame;
        Vector2 _figureHome;
        bool _loading;

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
            if (_loaderFigure != null) _figureHome = _loaderFigure.rectTransform.anchoredPosition;
            SetLoading(false);
        }

        public void Show()
        {
            _root.SetActive(true);
            SetMessage("Checking today's course...", playable: false);
            SetLoading(true);
        }

        /// <summary>The character the loading runner wears: the player's own, tinted as in the run.</summary>
        public void SetRunner(Sprite[] runFrames, Sprite jumpFrame, Color tint)
        {
            _runFrames = runFrames ?? new Sprite[0];
            _jumpFrame = jumpFrame;
            if (_loaderFigure != null) _loaderFigure.color = tint;
        }

        public bool Loading => _loading;

        void SetLoading(bool on)
        {
            _loading = on && _runFrames.Length > 0;
            if (_loader != null) _loader.gameObject.SetActive(_loading);
            if (_loading) Animate(0f);
        }

        void Update()
        {
            if (_loading) Animate(Time.unscaledTime);
        }

        // Runs in place, with a hop every JumpEvery seconds; the shadow shrinks while it is in the air.
        void Animate(float time)
        {
            float t = time % JumpEvery;
            bool airborne = t < JumpTime;
            float lift = airborne ? Mathf.Sin(t / JumpTime * Mathf.PI) : 0f;
            _loaderFigure.sprite = airborne && _jumpFrame != null ? _jumpFrame : _runFrames[(int)(time * RunFps) % _runFrames.Length];
            _loaderFigure.rectTransform.anchoredPosition = _figureHome + new Vector2(0f, Mathf.Round(lift * JumpHeight));
            if (_loaderShadow != null) _loaderShadow.localScale = new Vector3(1f - 0.5f * lift, 1f, 1f);
        }

        public void Hide() => _root.SetActive(false);

        /// <summary>No course to play: offline, switched off, or loading. Clears the board.</summary>
        public void SetMessage(string text, bool playable)
        {
            SetLoading(false);
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
            SetLoading(false);
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
