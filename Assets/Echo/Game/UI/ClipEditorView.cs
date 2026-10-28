using System;
using TMPro;
using Echo.Game.Clips;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// The clip editor (a user revision): a 9:16 preview of exactly what will be recorded, the timeline with its two
    /// handles, GIF or Video, Preview and Make clip. While a clip records or saves only its progress and Cancel show.
    /// Buttons only raise events; EchoGame decides what they do.
    /// </summary>
    public sealed class ClipEditorView : MonoBehaviour
    {
        [SerializeField] GameObject _root;
        [SerializeField] RawImage _preview;
        [SerializeField] TrimBar _trim;
        [SerializeField] TMP_Text _start;
        [SerializeField] TMP_Text _end;
        [SerializeField] TMP_Text _length;
        [SerializeField] TMP_Text _status;
        [SerializeField] GameObject _recording;
        [SerializeField] Button _gif;
        [SerializeField] Button _video;
        [SerializeField] Button _vertical;

        // The Vertical preset's options (marketing content plan): a hook line and a 1-second end card.
        [SerializeField] GameObject _verticalOptions;
        [SerializeField] TMP_InputField _hook;
        [SerializeField] Button _endCard;
        [SerializeField] Button _play;
        [SerializeField] Button _make;
        [SerializeField] Button _back;
        [SerializeField] Button _cancel;
        [SerializeField] Color _selected = Color.white;
        [SerializeField] Color _unselected = Color.gray;

        public event Action<bool, float> HandleMoved;
        public event Action HandleReleased;
        public event Action GifClicked;
        public event Action VideoClicked;
        public event Action VerticalClicked;
        public event Action EndCardClicked;

        /// <summary>The hook typed for a Vertical clip ("" for none).</summary>
        public string Hook => _hook != null ? _hook.text : "";
        public event Action PlayClicked;
        public event Action MakeClicked;
        public event Action BackClicked;
        public event Action CancelClicked;

        public bool IsOpen => _root.activeSelf;
        public bool Dragging => _trim.Dragging;

        void Awake()
        {
            _trim.HandleMoved += (end, at) => HandleMoved?.Invoke(end, at);
            _trim.HandleReleased += () => HandleReleased?.Invoke();
            _gif.onClick.AddListener(() => GifClicked?.Invoke());
            _video.onClick.AddListener(() => VideoClicked?.Invoke());
            if (_vertical != null) _vertical.onClick.AddListener(() => VerticalClicked?.Invoke());
            if (_endCard != null) _endCard.onClick.AddListener(() => EndCardClicked?.Invoke());
            _play.onClick.AddListener(() => PlayClicked?.Invoke());
            _make.onClick.AddListener(() => MakeClicked?.Invoke());
            _back.onClick.AddListener(() => BackClicked?.Invoke());
            _cancel.onClick.AddListener(() => CancelClicked?.Invoke());
        }

        public void Show(Texture preview, bool videoSupported)
        {
            _root.SetActive(true);
            _preview.texture = preview;
            _videoAllowed = videoSupported;
            SetStatus("");
            SetBusy(false, false);
        }

        public void Hide() => _root.SetActive(false);

        /// <param name="length">The whole timeline, in ticks.</param>
        public void SetRange(int start, int end, int length, string startText, string endText, string lengthText)
        {
            _trim.SetRange(start / (float)Mathf.Max(1, length), end / (float)Mathf.Max(1, length));
            _start.text = startText;
            _end.text = endText;
            _length.text = lengthText;
        }

        public void SetPlayhead(int tick, int length, bool visible) => _trim.SetPlayhead(tick / (float)Mathf.Max(1, length), visible);

        public void SetFormat(ClipFormat format)
        {
            Tint(_gif, format == ClipFormat.Gif);
            Tint(_video, format == ClipFormat.Video);
            if (_vertical != null) Tint(_vertical, format == ClipFormat.Vertical);
            _format = format;
            if (_verticalOptions != null) _verticalOptions.SetActive(format == ClipFormat.Vertical && !_busy);
            _make.GetComponentInChildren<TMP_Text>().text = format == ClipFormat.Gif ? "Make GIF" : "Make video";
        }

        public void SetEndCard(bool on)
        {
            if (_endCard != null) _endCard.GetComponentInChildren<TMP_Text>().text = on ? "End card: on" : "End card: off";
        }

        ClipFormat _format;
        bool _busy;

        public void SetPlaying(bool playing) => _play.GetComponentInChildren<TMP_Text>().text = playing ? "Stop" : "Preview";

        public void SetStatus(string text) => _status.text = text;

        /// <summary>Recording or saving: only the progress and Cancel (while recording) show.</summary>
        public void SetBusy(bool busy, bool recording)
        {
            _busy = busy;
            _recording.SetActive(recording);
            foreach (Button b in new[] { _gif, _play, _make, _back }) b.gameObject.SetActive(!busy);
            _video.gameObject.SetActive(!busy && _videoAllowed);
            if (_vertical != null) _vertical.gameObject.SetActive(!busy && _videoAllowed);
            if (_verticalOptions != null) _verticalOptions.SetActive(!busy && _format == ClipFormat.Vertical);
            _cancel.gameObject.SetActive(recording);
            _trim.enabled = !busy;
        }

        // Video needs the platform's encoder; without one only GIF is offered.
        bool _videoAllowed = true;

        void Tint(Button b, bool on)
        {
            b.GetComponent<Image>().color = on ? _selected : _unselected;
        }
    }
}
