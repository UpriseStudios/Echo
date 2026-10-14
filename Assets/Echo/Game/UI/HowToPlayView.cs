using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// How to play (a user revision): the overlay on the tutorial clips. The caption for the moment, the topic and how
    /// far through it is, "Tap for next" and Skip. The clips themselves play in the world behind it.
    /// </summary>
    public sealed class HowToPlayView : MonoBehaviour
    {
        const float PopSeconds = 0.18f;

        [SerializeField] GameObject _root;
        [SerializeField] GameObject _captionCard;
        [SerializeField] TMP_Text _caption;
        [SerializeField] TMP_Text _topic;
        [SerializeField] TMP_Text _hint;
        [SerializeField] Image[] _dots = new Image[0];
        [SerializeField] Button _skip;
        [SerializeField] Color _dotOn = Color.white;
        [SerializeField] Color _dotOff = new Color(1f, 1f, 1f, 0.3f);

        string _shown;
        float _pop;

        public event Action SkipClicked;

        public bool IsOpen => _root.activeSelf;

        void Awake() => _skip.onClick.AddListener(() => SkipClicked?.Invoke());

        public void ShowLoading()
        {
            _root.SetActive(true);
            _topic.text = "HOW TO PLAY";
            _hint.text = "";
            SetCaption("GETTING READY...");
            for (int i = 0; i < _dots.Length; i++) _dots[i].color = _dotOff;
        }

        /// <param name="topic">0-based topic; its dot lights up.</param>
        public void SetTopic(int topic, int topics, string name, bool last)
        {
            _root.SetActive(true);
            _topic.text = $"{topic + 1}/{topics}  {name}";
            _hint.text = last ? "TAP TO FINISH" : "TAP FOR NEXT";
            for (int i = 0; i < _dots.Length; i++)
            {
                _dots[i].gameObject.SetActive(i < topics);
                _dots[i].color = i <= topic ? _dotOn : _dotOff;
            }
        }

        /// <summary>The caption for this moment; null or empty hides the card.</summary>
        public void SetCaption(string text)
        {
            if (text == _shown) return;
            _shown = text;
            bool show = !string.IsNullOrEmpty(text);
            _captionCard.SetActive(show);
            if (!show) return;
            _caption.text = text;
            _pop = PopSeconds;
        }

        public string Caption => _shown;

        public void Hide()
        {
            _root.SetActive(false);
            _shown = null;
        }

        void Update()
        {
            if (_pop <= 0f) return;
            _pop -= Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(_pop / PopSeconds);
            _captionCard.transform.localScale = Vector3.one * Mathf.Lerp(0.88f, 1f, 1f - (1f - t) * (1f - t));
        }
    }
}
