using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Game.Seasons;
using Echo.Game.World;
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

        // World: Time Run (phase 2): each spike, block and gap drawn as the era it stands in (by its own position, so a
        // boundary changes in place rather than all at once). Null outside Time Run.
        SeasonTheme[] _eras;

        public void SetEras(SeasonTheme[] eras) => _eras = eras != null && eras.Length > 0 ? eras : null;

        SeasonTheme EraAt(float x) => _eras == null ? null : _eras[TimeEras.At(Mathf.FloorToInt(x), _eras.Length)];

        /// <summary>The spike drawn at x (tests and the render loop).</summary>
        public Sprite SpikeAt(float x) => EraAt(x) is SeasonTheme e && e.Spike != null ? e.Spike : _spike;

        /// <summary>The block drawn at x.</summary>
        public Sprite BlockAt(float x) => EraAt(x) is SeasonTheme e && e.Block != null ? e.Block : _block;

        /// <summary>The floor's top edge drawn from x: an era's own (Dino Land's grass), else the stone edge.</summary>
        public Sprite FloorTopAt(float x) => EraAt(x) is SeasonTheme e && e.FloorTop != null ? e.FloorTop : _groundTop;

        /// <summary>The hanging ceiling drawn from x: an era's own (Dino Land's rock overhang), else the bricks.</summary>
        public Sprite CeilingAt(float x) => EraAt(x) is SeasonTheme e && e.Ceiling != null ? e.Ceiling : _groundFill;

        // Art pass phase 2: the era whose floor and pits go to the half-resolution layer (Dino Land's, while it's showing).
        int _lowResEra = -1, _lowResLayer = -1;

        /// <summary>Floor and pits of era <paramref name="era"/> (index into the eras given) drawn on <paramref name="layer"/>; -1 for none.</summary>
        public void SetLowRes(int era, int layer)
        {
            _lowResEra = era;
            _lowResLayer = layer;
        }

        // The last piece drawn joins the half-resolution layer when it belongs to that era.
        void LowResPiece(float x)
        {
            if (_lowResEra < 0 || _lowResLayer < 0 || _eras == null || _used == 0) return;
            if (TimeEras.At(Mathf.FloorToInt(x), _eras.Length) == _lowResEra) _pool[_used - 1].gameObject.layer = _lowResLayer;
        }

        /// <summary>The floor drawn from x: an era's own (the Far Future's neon grid), else the bricks.</summary>
        public Sprite FloorAt(float x) => EraAt(x) is SeasonTheme e && e.Floor != null ? e.Floor : _groundFill;

        /// <summary>The pit drawn in a gap starting at x (null: the plain hole).</summary>
        public Sprite GapAt(float x) => EraAt(x) is SeasonTheme e ? e.Gap : null;

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
                    // Time Run: the era's pit (lava, a moat, a void), in its own colours; the hole itself is unchanged.
                    if (GapAt(gapStart) is Sprite pit)
                    {
                        Tiled(pit, gapStart, -GroundTopDrop, segmentStart - gapStart, FloorDepth, Color.white, 0);
                        LowResPiece(gapStart);
                        GapWheel(gapStart, segmentStart - gapStart);
                    }
                }
                Floor(segmentStart, x0 + SimUnits.ToWorld(c.Length), floor, lip);

                foreach (var s in c.Spikes)
                {
                    float sx = x0 + SimUnits.ToWorld(s.X), sw = SimUnits.ToWorld(s.W), sh = SimUnits.ToWorld(s.H);
                    int teeth = Mathf.Max(1, Mathf.RoundToInt(sw / SpikeTargetWidth));
                    Sprite spike = SpikeAt(sx);
                    for (int t = 0; t < teeth; t++)
                        Piece(spike, sx + sw * t / teeth, 0f, sw / teeth, sh, hazard, 2, false);
                }

                foreach (var cl in c.Ceilings)
                {
                    float cx = x0 + SimUnits.ToWorld(cl.X), cw = SimUnits.ToWorld(cl.W), cy = SimUnits.ToWorld(cl.Y);
                    Sprite ceiling = CeilingAt(cx);
                    Tiled(ceiling, cx, cy + ToothHeight + CeilingHeight, cw, CeilingHeight, ceiling == _groundFill ? block : Color.white, 2);
                    int teeth = Mathf.Max(1, Mathf.RoundToInt(cw / ToothWidth));
                    Sprite tooth = EraAt(cx) is SeasonTheme te && te.Teeth != null ? te.Teeth : SpikeAt(cx);
                    for (int t = 0; t < teeth; t++)
                        Piece(tooth, cx + cw * t / teeth, cy, cw / teeth, ToothHeight, hazard, 2, true);
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

        // Art pass phase 3: Medieval's waterwheel turning in the millstream, centred in the gap, wholly below the floor's
        // edge (so it never reads as something to stand on), sized to the gap.
        const float WheelUnits = 1.5f;

        void GapWheel(float x, float w)
        {
            if (!(EraAt(x) is SeasonTheme e) || e.GapWheel == null || e.GapWheel.Length == 0) return;
            Sprite frame = e.GapWheel[(int)(Time.time * e.GapWheelFps) % e.GapWheel.Length];
            // As wide as fits (the wheel 1.5 units, less in a narrow gap), keeping the frame's own shape.
            Vector2 s = frame.bounds.size;
            float k = Mathf.Min(WheelUnits, w * 0.8f) / Mathf.Max(s.x, s.y);
            SpriteRenderer sr = Next(1);
            sr.drawMode = SpriteDrawMode.Simple;
            sr.sprite = frame;
            sr.color = Color.white;
            sr.transform.localScale = new Vector3(k, k, 1f);
            sr.transform.localPosition = new Vector3(SimUnits.Pixel(x + w / 2f), SimUnits.Pixel(-GroundTopDrop - s.y * k * 0.5f - 0.1f), 0f);
        }

        /// <summary>The waterwheel's frames drawn in a gap at x, or none (tests).</summary>
        public Sprite[] GapWheelAt(float x) => EraAt(x) is SeasonTheme e ? e.GapWheel : null;

        void Floor(float from, float to, Color floor, Color lip)
        {
            if (to <= from) return;
            // The bricks hang from just under the lip; both tile from the segment's left edge.
            // Time Run (phase 3): an era's floor is in its own colours.
            Sprite fill = FloorAt(from);
            Tiled(fill, from, -GroundTopDrop, to - from, FloorDepth, fill == _groundFill ? floor : Color.white, 0);
            LowResPiece(from);
            Sprite top = FloorTopAt(from);
            Tiled(top, from, 0f, to - from, GroundTopHeight, top == _groundTop ? lip : Color.white, 1);
            LowResPiece(from);
        }

        /// <summary>Art pass phase 6: the material the course's pieces use (lit in the Far Far Future), existing and new.</summary>
        public void SetPieceMaterial(Material material)
        {
            if (material == null || material == _material) return;
            foreach (SpriteRenderer sr in _pool) sr.sharedMaterial = material;
            _material = material;
        }

        SpriteRenderer Next(int order)
        {
            if (_used == _pool.Count) _pool.Add(BoxSprite.Create(transform, "CoursePiece" + _used, _material, order));
            SpriteRenderer sr = _pool[_used++];
            if (sr.gameObject.layer != 0) sr.gameObject.layer = 0;
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
            Sprite block = BlockAt(x);
            if (block == null)
            {
                Piece(null, x, y, w, h, color, order, false);
                return;
            }
            SpriteRenderer sr = Next(order);
            sr.sprite = block;
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
