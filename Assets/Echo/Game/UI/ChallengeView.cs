using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// Ghost Challenges (value-adds doc): one small modal card for everything around a challenge: a challenge
    /// arriving, one from an older version, asking the sender's name, and plain messages. Buttons only raise
    /// events; EchoGame decides what they do.
    /// </summary>
    public sealed class ChallengeView : MonoBehaviour
    {
        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _body;
        [SerializeField] TMP_InputField _input;
        [SerializeField] Button _primary;
        [SerializeField] Button _secondary;

        public event Action PrimaryClicked;
        public event Action SecondaryClicked;

        public bool IsOpen => _root.activeSelf;

        /// <summary>What the player typed into the name box.</summary>
        public string InputText => _input != null ? _input.text : "";

        void Awake()
        {
            _primary.onClick.AddListener(() => PrimaryClicked?.Invoke());
            _secondary.onClick.AddListener(() => SecondaryClicked?.Invoke());
        }

        public void Show(string title, string body, string primary, string secondary)
        {
            _root.SetActive(true);
            _title.text = title;
            _body.text = body;
            Label(_primary, primary);
            _secondary.gameObject.SetActive(!string.IsNullOrEmpty(secondary));
            if (!string.IsNullOrEmpty(secondary)) Label(_secondary, secondary);
            if (_input != null) _input.gameObject.SetActive(false);
        }

        /// <summary>The same card with the name box showing, filled with <paramref name="current"/>.</summary>
        public void AskName(string title, string body, string current)
        {
            Show(title, body, "OK", "Cancel");
            if (_input == null) return;
            _input.gameObject.SetActive(true);
            _input.text = current ?? "";
            _input.ActivateInputField();
        }

        public void Hide() => _root.SetActive(false);

        static void Label(Button b, string text) => b.GetComponentInChildren<TMP_Text>().text = text;
    }
}
