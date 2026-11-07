using Echo.Game.Seasons;
using TMPro;
using UnityEngine;

namespace Echo.Game.UI
{
    /// <summary>
    /// Time Run art pass, phase 7: an era's name, big and centred, as a run reaches it: it lands, holds for about 1.5 s and
    /// fades. Each era in the type of its era of games:
    /// <list type="bullet">
    /// <item>Pixel (the first three): the arcade face, cyan with a hard drop shadow, popping in in chunky steps;</item>
    /// <item>Modern (3026): Rubik, white with a soft cyan glow behind it, easing in smoothly;</item>
    /// <item>Extruded (10,000 AD): Rubik Black with a gradient face over a stack of depth layers, swinging round as it lands
    /// so the extrusion slides like a solid turning towards you, then gently rocking.</item>
    /// </list>
    /// The texts are set once per title, then only moved, scaled and faded, so a title allocates nothing while it plays.
    /// </summary>
    public sealed class EraTitleView : MonoBehaviour
    {
        [SerializeField] TMP_Text _face;
        [Tooltip("Drawn behind the face, back to front: the drop shadow, the glow or the extrusion's layers.")]
        [SerializeField] TMP_Text[] _depth = new TMP_Text[0];
        [SerializeField] TMP_FontAsset _pixel;
        [SerializeField] TMP_FontAsset _modern;
        [SerializeField] TMP_FontAsset _extruded;

        public const float InSeconds = 0.3f, HoldSeconds = 1.5f, FadeSeconds = 0.45f;
        const float Total = InSeconds + HoldSeconds + FadeSeconds;

        static readonly Color PixelFace = new Color(0.55f, 0.95f, 1f), PixelShadow = new Color(0.02f, 0.03f, 0.08f, 0.9f);
        static readonly Color ExtrudeFront = new Color(0.55f, 0.12f, 0.62f), ExtrudeBack = new Color(0.08f, 0.02f, 0.16f);

        EraTitleStyle _style;
        float _left;
        Vector2 _home;

        /// <summary>Showing now, and in which type (tests).</summary>
        public bool Showing => _left > 0f;
        public EraTitleStyle Style => _style;
        public string Text => _face != null ? _face.text : "";
        public TMP_FontAsset FaceFont => _face != null ? _face.font : null;
        public int DepthLayersShown
        {
            get
            {
                int n = 0;
                foreach (TMP_Text d in _depth) if (d != null && d.gameObject.activeSelf) n++;
                return n;
            }
        }

        // The view sits on the face, which the scene starts hidden, so Awake first runs inside Show as the face is switched
        // on: it only remembers where the title sits.
        void Awake() => _home = _face.rectTransform.anchoredPosition;

        public void Show(string label, EraTitleStyle style)
        {
            if (string.IsNullOrEmpty(label)) return;
            _style = style;
            _left = Total;
            TMP_FontAsset font = style == EraTitleStyle.Pixel ? _pixel : style == EraTitleStyle.Modern ? _modern : _extruded;
            _face.font = font;
            _face.SetText(label);
            // As big as its box allows: the arcade face is wide, so a long label shrinks rather than overflowing.
            _face.enableAutoSizing = true;
            _face.textWrappingMode = TextWrappingModes.NoWrap;
            _face.fontSizeMin = 36f;
            _face.fontSizeMax = style == EraTitleStyle.Pixel ? 84f : style == EraTitleStyle.Modern ? 118f : 132f;
            _face.characterSpacing = style == EraTitleStyle.Modern ? 6f : 0f;
            _face.enableVertexGradient = style == EraTitleStyle.Extruded;
            if (style == EraTitleStyle.Extruded)
                _face.colorGradient = new VertexGradient(new Color(1f, 1f, 1f), new Color(0.85f, 1f, 1f), new Color(1f, 0.45f, 0.95f), new Color(0.45f, 0.95f, 1f));
            _face.color = style == EraTitleStyle.Pixel ? PixelFace : Color.white;
            _face.gameObject.SetActive(true);
            // How many layers sit behind the face: a drop shadow, a glow, or the whole extrusion.
            int layers = style == EraTitleStyle.Extruded ? _depth.Length : 1;
            for (int i = 0; i < _depth.Length; i++)
            {
                TMP_Text d = _depth[i];
                bool on = i < layers;
                d.gameObject.SetActive(on);
                if (!on) continue;
                d.font = font;
                d.SetText(label);
                d.enableAutoSizing = true;
                d.textWrappingMode = TextWrappingModes.NoWrap;
                d.fontSizeMin = _face.fontSizeMin;
                d.fontSizeMax = _face.fontSizeMax;
                d.characterSpacing = _face.characterSpacing;
                d.enableVertexGradient = false;
                d.color = style == EraTitleStyle.Pixel ? PixelShadow
                    : style == EraTitleStyle.Modern ? new Color(0.3f, 0.9f, 1f, 0.35f)
                    : Color.Lerp(ExtrudeBack, ExtrudeFront, i / (float)Mathf.Max(1, layers - 1));
            }
            Render();
        }

        void Hide()
        {
            _left = 0f;
            _face.gameObject.SetActive(false);
            foreach (TMP_Text d in _depth) if (d != null) d.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_left <= 0f) return;
            _left -= Time.unscaledDeltaTime;
            if (_left <= 0f)
            {
                Hide();
                return;
            }
            Render();
        }

        void Render()
        {
            float age = Total - _left;
            float landing = Mathf.Clamp01(age / InSeconds);
            float alpha = Mathf.Clamp01(_left / FadeSeconds) * Mathf.Clamp01(age / 0.08f);
            RectTransform face = _face.rectTransform;
            switch (_style)
            {
                case EraTitleStyle.Pixel:
                {
                    // Chunky steps from twice the size, like a sprite scaled on old hardware; a hard shadow down and right.
                    float s = 1f + Mathf.Round((1f - landing) * 4f) * 0.25f;
                    face.localScale = new Vector3(s, s, 1f);
                    face.anchoredPosition = _home;
                    _face.alpha = alpha;
                    Place(_depth[0], _home + new Vector2(8f, -8f) * s, s, 1f, alpha * PixelShadow.a);
                    break;
                }
                case EraTitleStyle.Modern:
                {
                    // A smooth ease out of a slight zoom, the glow breathing behind it.
                    float e = 1f - (1f - landing) * (1f - landing) * (1f - landing);
                    float s = Mathf.Lerp(1.12f, 1f, e);
                    face.localScale = new Vector3(s, s, 1f);
                    face.anchoredPosition = _home + new Vector2(0f, -24f * (1f - e));
                    _face.alpha = alpha;
                    float glow = 1.06f + 0.03f * Mathf.Sin(age * 5f);
                    Place(_depth[0], face.anchoredPosition, s * glow, 1f, alpha * 0.35f);
                    break;
                }
                default:
                {
                    // Swinging round from 55 degrees as it lands, then rocking gently: the face narrows with the turn and the
                    // extrusion's layers slide out to the side it turns from, like a solid seen from an angle.
                    float e = 1f - (1f - landing) * (1f - landing);
                    float angle = Mathf.Lerp(55f, 0f, e) + 9f * Mathf.Sin(age * 2.2f) * e;
                    float rad = angle * Mathf.Deg2Rad;
                    float s = Mathf.Lerp(1.5f, 1f, e);
                    face.localScale = new Vector3(s * Mathf.Cos(rad * 0.55f), s, 1f);
                    face.anchoredPosition = _home;
                    _face.alpha = alpha;
                    Vector2 step = new Vector2(1.2f + 7f * Mathf.Sin(rad), -4.2f) * s;
                    int n = _depth.Length;
                    for (int i = 0; i < n; i++)
                        Place(_depth[i], _home + step * (n - i), s, Mathf.Cos(rad * 0.55f), alpha);
                    break;
                }
            }
        }

        static void Place(TMP_Text t, Vector2 at, float scale, float squash, float alpha)
        {
            t.rectTransform.anchoredPosition = at;
            t.rectTransform.localScale = new Vector3(scale * squash, scale, 1f);
            t.alpha = alpha;
        }
    }
}
