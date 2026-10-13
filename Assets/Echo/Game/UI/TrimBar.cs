using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// The clip editor's timeline (a user revision): the whole run as a bar, the chosen stretch filled in between two
    /// handles, and a playhead. Pressing picks up whichever handle is nearer; dragging moves it. Reports positions as
    /// fractions of the bar; EchoGame turns them into ticks and applies the limits.
    /// </summary>
    public sealed class TrimBar : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform _track;
        [SerializeField] RectTransform _selection;
        [SerializeField] RectTransform _startHandle;
        [SerializeField] RectTransform _endHandle;
        [SerializeField] RectTransform _playhead;

        float _start, _end = 1f;
        bool _draggingEnd;

        /// <summary>A handle moved: true for the end handle, and where to, 0 to 1.</summary>
        public event Action<bool, float> HandleMoved;
        public event Action HandleReleased;

        public bool Dragging { get; private set; }

        public void SetRange(float start, float end)
        {
            _start = Mathf.Clamp01(start);
            _end = Mathf.Clamp01(end);
            Place(_startHandle, _start);
            Place(_endHandle, _end);
            _selection.anchorMin = new Vector2(_start, 0f);
            _selection.anchorMax = new Vector2(_end, 1f);
            _selection.offsetMin = _selection.offsetMax = Vector2.zero;
        }

        public void SetPlayhead(float at, bool visible)
        {
            _playhead.gameObject.SetActive(visible);
            if (visible) Place(_playhead, at);
        }

        static void Place(RectTransform rt, float at)
        {
            rt.anchorMin = new Vector2(at, rt.anchorMin.y);
            rt.anchorMax = new Vector2(at, rt.anchorMax.y);
            rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
        }

        public void OnPointerDown(PointerEventData e)
        {
            float at = Fraction(e);
            _draggingEnd = Mathf.Abs(at - _end) < Mathf.Abs(at - _start) || (at > _end);
            if (at < _start) _draggingEnd = false;
            Dragging = true;
            HandleMoved?.Invoke(_draggingEnd, at);
        }

        public void OnDrag(PointerEventData e)
        {
            if (Dragging) HandleMoved?.Invoke(_draggingEnd, Fraction(e));
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!Dragging) return;
            Dragging = false;
            HandleReleased?.Invoke();
        }

        float Fraction(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_track, e.position, e.pressEventCamera, out Vector2 local);
            Rect r = _track.rect;
            return Mathf.Clamp01((local.x - r.xMin) / Mathf.Max(1f, r.width));
        }
    }
}
