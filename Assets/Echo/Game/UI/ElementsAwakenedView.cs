using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// The reward for the 30th achievement (a user revision): the screen darkens, the four Elementals rise one after
    /// another as dark silhouettes with a pulse of their element, then all four flare into colour under
    /// ELEMENTS AWAKENED. A tap skips to the end; a tap at the end closes it. Unscaled time, no allocation per frame.
    /// </summary>
    public sealed class ElementsAwakenedView : MonoBehaviour
    {
        const float DarkenSeconds = 0.6f;
        const float FirstFigure = 0.8f;
        const float FigureEvery = 0.7f;
        const float FigureSeconds = 0.45f;
        const float FlareAt = 3.9f;
        const float FlareSeconds = 0.5f;
        const float TitleAt = 4.3f;
        const float SubtitleAt = 4.9f;
        const float TextSeconds = 0.5f;
        const float CloseableAt = 5.4f;
        const float ScrimAlpha = 0.985f; // blended in linear space, so anything lower lets the title show through

        [SerializeField] GameObject _root;
        [SerializeField] Image _scrim;
        [SerializeField] Image[] _figures = new Image[0];
        [SerializeField] Image[] _glows = new Image[0];
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _subtitle;
        [SerializeField] Button _tap;

        Color[] _colors = new Color[0];
        float _time;

        public event Action Closed;

        public bool IsOpen => _root != null && _root.activeSelf;

        void Awake()
        {
            if (_tap != null) _tap.onClick.AddListener(OnTap);
        }

        /// <param name="figures">The four Elementals' standing frames, in reveal order.</param>
        /// <param name="colors">Each one's element colour, for its glow.</param>
        public void Play(Sprite[] figures, Color[] colors)
        {
            _colors = colors;
            for (int i = 0; i < _figures.Length; i++)
            {
                _figures[i].sprite = i < figures.Length ? figures[i] : null;
                _figures[i].enabled = _figures[i].sprite != null;
            }
            _time = 0f;
            _root.SetActive(true);
            Apply();
        }

        /// <summary>Jumps to the finished picture (tests, and a tap mid-sequence).</summary>
        public void Skip()
        {
            _time = Mathf.Max(_time, CloseableAt);
            Apply();
        }

        public void Close()
        {
            if (!IsOpen) return;
            _root.SetActive(false);
            Closed?.Invoke();
        }

        void OnTap()
        {
            if (_time < CloseableAt) Skip();
            else Close();
        }

        void Update()
        {
            if (!IsOpen) return;
            _time += Time.unscaledDeltaTime;
            Apply();
        }

        void Apply()
        {
            float t = _time;
            _scrim.color = new Color(0.01f, 0.01f, 0.03f, ScrimAlpha * Ease(t / DarkenSeconds));
            float flare = Ease((t - FlareAt) / FlareSeconds);
            for (int i = 0; i < _figures.Length; i++)
            {
                float p = Ease((t - FirstFigure - i * FigureEvery) / FigureSeconds);
                Color element = i < _colors.Length ? _colors[i] : Color.white;
                // A dark silhouette rising into place, then the flare lights it in full colour.
                Color body = Color.Lerp(new Color(0.06f, 0.06f, 0.09f), Color.white, flare);
                body.a = p;
                _figures[i].color = body;
                var rt = _figures[i].rectTransform;
                rt.localScale = Vector3.one * (0.8f + 0.2f * p);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -60f * (1f - p));

                if (i < _glows.Length)
                {
                    // A pulse as each one appears, settling to a soft breathing glow, brightest at the flare.
                    float arrive = Mathf.Clamp01(1f - (t - FirstFigure - i * FigureEvery) / 0.8f);
                    float breathe = 0.28f + 0.08f * Mathf.Sin(t * 2.4f + i * 1.3f);
                    float flash = flare > 0f && flare < 1f ? Mathf.Sin(flare * Mathf.PI) * 0.6f : 0f;
                    Color g = element;
                    g.a = p * Mathf.Clamp01(breathe + arrive * 0.6f + flash + flare * 0.12f);
                    _glows[i].color = g;
                    _glows[i].rectTransform.localScale = Vector3.one * (0.9f + 0.25f * arrive + 0.1f * flare);
                }
            }
            SetAlpha(_title, Ease((t - TitleAt) / TextSeconds));
            SetAlpha(_subtitle, Ease((t - SubtitleAt) / TextSeconds));
        }

        static void SetAlpha(TMP_Text text, float a)
        {
            if (text == null) return;
            Color c = text.color;
            c.a = a;
            text.color = c;
        }

        static float Ease(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
