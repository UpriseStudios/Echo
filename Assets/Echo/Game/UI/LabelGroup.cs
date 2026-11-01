using TMPro;
using UnityEngine;

namespace Echo.Game.UI
{
    /// <summary>
    /// A row of buttons whose labels share one size: each label stays on one line, and all of them take the largest
    /// size that fits every one (a user report: labels wrapped onto two cramped lines, and neighbours shrank to
    /// different sizes). Refits only when a label's text or visibility changes, so it costs nothing per frame.
    /// </summary>
    public sealed class LabelGroup : MonoBehaviour
    {
        [SerializeField] TMP_Text[] _labels;
        [SerializeField] float _maxSize = 34f;
        [SerializeField] float _minSize = 16f;

        string[] _seen;
        bool[] _shown;

        void OnEnable() => Fit();

        void LateUpdate()
        {
            if (_seen == null) return;
            for (int i = 0; i < _labels.Length; i++)
            {
                if (_labels[i] == null) continue;
                if (!ReferenceEquals(_labels[i].text, _seen[i]) || _labels[i].gameObject.activeInHierarchy != _shown[i])
                {
                    Fit();
                    return;
                }
            }
        }

        /// <summary>Sets the size again now (after labels change in the same frame they are drawn).</summary>
        public void Fit()
        {
            if (_labels == null) return;
            _seen ??= new string[_labels.Length];
            _shown ??= new bool[_labels.Length];
            float size = _maxSize;
            foreach (TMP_Text label in _labels)
            {
                if (label == null || !label.gameObject.activeInHierarchy) continue;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.enableAutoSizing = true;
                label.fontSizeMin = _minSize;
                label.fontSizeMax = _maxSize;
                label.ForceMeshUpdate();
                size = Mathf.Min(size, label.fontSize);
            }
            for (int i = 0; i < _labels.Length; i++)
            {
                TMP_Text label = _labels[i];
                if (label == null) continue;
                _seen[i] = label.text;
                _shown[i] = label.gameObject.activeInHierarchy;
                label.enableAutoSizing = false;
                label.fontSize = size;
            }
        }
    }
}
