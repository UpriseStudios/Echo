using System.Collections.Generic;
using Echo.Core.Courses;
using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// Draws the chunks on screen in 16-bit tiles (game spec art direction, pixel revision): ground with a
    /// grassy lip over stone bricks, pixel spikes, brick ceilings with teeth whose tips mark the hazard edge,
    /// and bevelled mover blocks. The tiles are greyscale, tinted from the palette every frame. Renderers come
    /// from a pool re-placed every frame. Reads the course; never changes what it returns.
    /// </summary>
    public sealed class CourseView : MonoBehaviour
    {
        const float FloorDepth = 30f;
        const float CeilingHeight = 30f;
        const float MarginUnits = 1f;
        const float GroundTopHeight = 10f / CameraRig.PixelsPerUnit;   // 8 px of lip under the line, 2 px of tufts above
        const float GroundTopDrop = 8f / CameraRig.PixelsPerUnit;
        const float ToothWidth = 0.3f;
        const float ToothHeight = 0.22f;
        const float SpikeTargetWidth = 0.5f;

        // Two screens of the busiest chunk need about 60 pieces; the pool starts larger so it never grows
        // (and allocates) mid-run (tech spec 18).
        const int PrewarmPieces = 128;

        [SerializeField] Material _material;
        [SerializeField] Sprite _spike;
        [SerializeField] Sprite _block;
        [SerializeField] Sprite _groundTop;
        [SerializeField] Sprite _groundFill;

        // Seasons doc: a season can reskin the hazards and the floor's edge; the boxes they fill never change.
        Sprite _baseSpike, _baseBlock, _baseGroundTop;
        bool _baseKept;

        public void SetSeason(Sprite spike, Sprite block, Sprite groundTop)
        {
            if (!_baseKept)
            {
                _baseSpike = _spike;
                _baseBlock = _block;
                _baseGroundTop = _groundTop;
                _baseKept = true;
            }
            _spike = spike != null ? spike : _baseSpike;
            _block = block != null ? block : _baseBlock;
            _groundTop = groundTop != null ? groundTop : _baseGroundTop;
        }

        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>(PrewarmPieces);
        int _used;

        void Awake()
        {
            for (int i = 0; i < PrewarmPieces; i++)
            {
                var sr = BoxSprite.Create(transform, "CoursePiece" + i, _material, 0);
                sr.enabled = false;
                _pool.Add(sr);
            }
        }

        public void Render(Course course, float left, float right, int tick, Color background, Color floor, Color hazard)
        {
            _used = 0;
            Color lip = Color.Lerp(floor, background, 0.55f);
            Color block = Color.Lerp(hazard, background, 0.25f);
            int first = course.ChunkIndexAt(Mathf.Max(0, Mathf.FloorToInt((left - MarginUnits) * SimUnits.PerUnit)));
            int last = course.ChunkIndexAt(Mathf.Max(0, Mathf.CeilToInt((right + MarginUnits) * SimUnits.PerUnit)));
            if (first < 0) first = 0;

            if (left < 0) Floor(left - MarginUnits, 0f, floor, lip);

            for (int i = first; i <= last; i++)
            {
                Chunk c = course.ChunkAt(i);
                float x0 = SimUnits.ToWorld(course.ChunkStartX(i));

                // Floor between gaps (content keeps gaps sorted and apart; ChunkValidatorTests checks it).
                float segmentStart = x0;
                foreach (var g in c.Gaps)
                {
                    float gapStart = x0 + SimUnits.ToWorld(g.X);
                    Floor(segmentStart, gapStart, floor, lip);
                    segmentStart = gapStart + SimUnits.ToWorld(g.W);
                }
                Floor(segmentStart, x0 + SimUnits.ToWorld(c.Length), floor, lip);

                foreach (var s in c.Spikes)
                {
                    float sx = x0 + SimUnits.ToWorld(s.X), sw = SimUnits.ToWorld(s.W), sh = SimUnits.ToWorld(s.H);
                    int teeth = Mathf.Max(1, Mathf.RoundToInt(sw / SpikeTargetWidth));
                    for (int t = 0; t < teeth; t++)
                        Piece(_spike, sx + sw * t / teeth, 0f, sw / teeth, sh, hazard, 2, false);
                }

                foreach (var cl in c.Ceilings)
                {
                    float cx = x0 + SimUnits.ToWorld(cl.X), cw = SimUnits.ToWorld(cl.W), cy = SimUnits.ToWorld(cl.Y);
                    Tiled(_groundFill, cx, cy + ToothHeight + CeilingHeight, cw, CeilingHeight, block, 2);
                    int teeth = Mathf.Max(1, Mathf.RoundToInt(cw / ToothWidth));
                    for (int t = 0; t < teeth; t++)
                        Piece(_spike, cx + cw * t / teeth, cy, cw / teeth, ToothHeight, hazard, 2, true);
                }

                foreach (var m in c.Movers)
                {
                    float mx = x0 + SimUnits.ToWorld(m.X), mw = SimUnits.ToWorld(m.W);
                    float my = SimUnits.ToWorld(m.BottomAt(tick)), mh = SimUnits.ToWorld(m.H);
                    Block(mx, my, mw, mh, block, 2);
                }
            }

            for (int i = _used; i < _pool.Count; i++) _pool[i].enabled = false;
        }

        void Floor(float from, float to, Color floor, Color lip)
        {
            if (to <= from) return;
            // The bricks hang from just under the lip; both tile from the segment's left edge.
            Tiled(_groundFill, from, -GroundTopDrop, to - from, FloorDepth, floor, 0);
            Tiled(_groundTop, from, 0f, to - from, GroundTopHeight, lip, 1);
        }

        SpriteRenderer Next(int order)
        {
            if (_used == _pool.Count) _pool.Add(BoxSprite.Create(transform, "CoursePiece" + _used, _material, order));
            SpriteRenderer sr = _pool[_used++];
            sr.sortingOrder = order;
            sr.enabled = true;
            return sr;
        }

        /// <summary>A sprite (or plain box) stretched over a rectangle given by its bottom-left corner.</summary>
        void Piece(Sprite sprite, float x, float y, float w, float h, Color color, int order, bool flipY)
        {
            SpriteRenderer sr = Next(order);
            sr.drawMode = SpriteDrawMode.Simple;
            sr.sprite = sprite != null ? sprite : BoxSprite.Get();
            sr.color = color;
            // Sprites here are 1 unit square with a bottom-left pivot. A negative y scale turns a tooth upside
            // down while still filling the same rectangle.
            var t = sr.transform;
            t.localPosition = new Vector3(x, flipY ? y + h : y, 0f);
            t.localScale = new Vector3(w, flipY ? -h : h, 1f);
        }

        /// <summary>A sprite repeated at its native pixel size across a width, placed by its own pivot.</summary>
        void Tiled(Sprite sprite, float x, float y, float w, float h, Color color, int order)
        {
            if (sprite == null)
            {
                Piece(null, x, y - h, w, h, color, order, false);
                return;
            }
            SpriteRenderer sr = Next(order);
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.color = color;
            sr.size = new Vector2(w, h);
            var t = sr.transform;
            t.localScale = Vector3.one;
            t.localPosition = new Vector3(SimUnits.Pixel(x), y, 0f);
        }

        void Block(float x, float y, float w, float h, Color color, int order)
        {
            if (_block == null)
            {
                Piece(null, x, y, w, h, color, order, false);
                return;
            }
            SpriteRenderer sr = Next(order);
            sr.sprite = _block;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.color = color;
            var t = sr.transform;
            t.localScale = Vector3.one;
            // The block sprite is centre-pivoted.
            t.localPosition = new Vector3(x + w / 2f, y + h / 2f, 0f);
            sr.size = new Vector2(w, h);
        }
    }
}
