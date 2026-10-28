using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Sim;
using TMPro;
using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// A small headstone where each ghost's run ended. Purely decorative: graves are never hazards and the sim
    /// never sees them. Positions come from replaying each ghost's inputs once per run (GhostManager.DeathX).
    /// When the player dies, a fresh grave rises at the spot; passing a grave lets out a few wisps.
    /// </summary>
    public sealed class GraveViews : MonoBehaviour
    {
        const int GhostGraves = GhostViews.PoolSize;
        const int Fresh = GhostGraves;          // the extra slot for the grave of the run that just ended
        const float RiseDelay = 0.35f;
        const float RiseSeconds = 0.45f;
        const float CrumbleSeconds = 0.3f;
        const float LedgeInset = 0.3f;          // a fall's grave sits this far back from the edge it fell from
        const int MaxBacktrackSteps = 200;
        const int BacktrackSub = 100;
        const float EpitaphAhead = 4f;
        const float EpitaphBehind = 0.75f;
        const float EpitaphCharWidth = 17f / 1080f;
        const float EpitaphHeight = -0.3f;   // just under the floor line, on the dark bricks below the grave
        static readonly Color GhostCyan = new Color(0.45f, 0.95f, 1f);
        static readonly Color NemesisRed = new Color(1f, 0.33f, 0.33f);

        [SerializeField] Material _material;
        [SerializeField] Sprite _grave;
        [SerializeField] ShardBurst _shards;
        [Tooltip("A full-resolution UI label under the menus; placed over the grave in screen space so it stays crisp.")]
        [SerializeField] TMP_Text _epitaph;
        [SerializeField] CameraRig _rig;

        SpriteRenderer[] _graves;
        readonly float[] _x = new float[GhostGraves + 1];
        readonly float[] _scale = new float[GhostGraves + 1];
        readonly float[] _age = new float[GhostGraves + 1];
        readonly float[] _anim = new float[GhostGraves + 1];    // seconds into the rise, or into the crumble
        readonly bool[] _passed = new bool[GhostGraves + 1];
        readonly bool[] _crumbling = new bool[GhostGraves + 1];
        readonly bool[] _nemesis = new bool[GhostGraves + 1];
        readonly GhostRecord[] _records = new GhostRecord[GhostGraves];
        readonly string[] _epitaphs = new string[GhostGraves + 1];
        int _epitaphShown = -1;
        float _epitaphHalf;   // half the label's width as a fraction of the screen, kept on screen
        int _count;
        Sprite _baseGrave;
        bool _graveInColour;

        void Awake()
        {
            if (_epitaph != null)
            {
                // Every character an epitaph can use, so the font never adds a glyph mid-run (a new epitaph's letters
                // allocated 12 KB), then sized for the longest line so its buffers never grow either (tech spec 18).
                _epitaph.SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz 0123456789 #·'-.,!?");
                _epitaph.ForceMeshUpdate();
                _epitaph.SetText("Run #99999 · took the long way down XX");
                _epitaph.ForceMeshUpdate();
                _epitaph.gameObject.SetActive(false);
            }
            _baseGrave = _grave;
            _graves = new SpriteRenderer[GhostGraves + 1];
            for (int i = 0; i < _graves.Length; i++)
            {
                var go = new GameObject("Grave" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = _material;
                sr.sprite = _grave;
                sr.sortingOrder = 1; // over the floor, under spikes, ghosts and the player
                sr.enabled = false;
                _graves[i] = sr;
            }
        }

        /// <summary>A season's gravestone (seasons doc); null keeps the stone. In colour, it isn't tinted like stone.</summary>
        public void SetSeason(Sprite grave, bool inColour)
        {
            _grave = grave != null ? grave : _baseGrave;
            _graveInColour = grave != null && inColour;
            if (_graves == null) return;
            foreach (SpriteRenderer sr in _graves) sr.sprite = _grave;
        }

        /// <summary>Places a grave for every ghost in this run. Called once when a run is prepared.</summary>
        public void Plant(GhostManager ghosts, Course course, in SimConfig cfg, GhostRecord nemesis)
        {
            _epitaphShown = -1;
            _count = Mathf.Min(ghosts.Count, GhostGraves);
            for (int i = 0; i < _graves.Length; i++)
            {
                _graves[i].enabled = false;
                _passed[i] = false;
                _crumbling[i] = false;
                _anim[i] = RiseDelay + RiseSeconds; // fully risen
            }
            for (int i = 0; i < _count; i++)
            {
                GhostRecord rec = ghosts.RecordAt(i);
                _records[i] = rec;
                _epitaphs[i] = Epitaphs.For(rec);
                _nemesis[i] = rec == nemesis;
                // Ghosts are oldest first, so the newest grave is the brightest.
                _age[i] = _count > 1 ? (float)i / (_count - 1) : 1f;
                // Recorded at death (skins addendum); ghosts from v1 saves fall back to a replay.
                int deathX = rec.DeathX >= 0 ? rec.DeathX : ghosts.DeathX(i, course, cfg);
                Place(i, GroundX(deathX, course), rec.RunIndex);
            }
            for (int i = _count; i < GhostGraves; i++) _records[i] = null;
        }

        /// <summary>Replaces one grave's epitaph, e.g. "Jamie fell here" on a Ghost Challenge's finish line.</summary>
        public void SetEpitaph(int i, string text)
        {
            if (i < 0 || i >= _count) return;
            _epitaphs[i] = text;
            _epitaphShown = -1;
        }

        /// <summary>The grave of the run that just ended rises where the player fell.</summary>
        public void PlantFresh(int deathX, Course course, int runIndex, string epitaph)
        {
            _epitaphs[Fresh] = epitaph;
            _epitaphShown = -1;
            _age[Fresh] = 1f;
            _nemesis[Fresh] = false;
            _crumbling[Fresh] = false;
            _passed[Fresh] = true;
            Place(Fresh, GroundX(deathX, course), runIndex);
            _anim[Fresh] = 0f;
        }

        /// <summary>The fresh grave's world rectangle once fully risen, or an empty rect when there is none.</summary>
        public Rect FreshRect
        {
            get
            {
                SpriteRenderer sr = _graves[Fresh];
                if (!sr.enabled || _crumbling[Fresh] || sr.sprite == null) return Rect.zero;
                Vector2 size = sr.sprite.bounds.size * _scale[Fresh];
                return new Rect(_x[Fresh] - size.x / 2f, 0f, size.x, size.y);
            }
        }

        /// <summary>The fresh grave sinks away again (a Continue revived the run).</summary>
        public void ClearFresh() => StartCrumble(Fresh);

        /// <summary>A banished ghost's grave crumbles.</summary>
        public void Crumble(GhostRecord record)
        {
            for (int i = 0; i < _count; i++)
                if (_records[i] == record) StartCrumble(i);
        }

        /// <summary>The ghost-clear pop takes every grave with it.</summary>
        public void CrumbleAll()
        {
            for (int i = 0; i < _graves.Length; i++) StartCrumble(i);
        }

        /// <param name="epitaphs">False on the title and menus, where the line would sit under their text.</param>
        public void Render(float playerX, Color floor, Color background, bool epitaphs = true)
        {
            if (epitaphs) ShowEpitaph(playerX);
            else if (_epitaph != null && _epitaph.gameObject.activeSelf) _epitaph.gameObject.SetActive(false);
            float dt = Time.unscaledDeltaTime;
            Color stone = Color.Lerp(floor, background, 0.45f);
            for (int i = 0; i < _graves.Length; i++)
            {
                SpriteRenderer sr = _graves[i];
                if (!sr.enabled) continue;

                float rise = 1f;
                if (_crumbling[i])
                {
                    _anim[i] += dt;
                    float t = _anim[i] / CrumbleSeconds;
                    if (t >= 1f)
                    {
                        sr.enabled = false;
                        continue;
                    }
                    rise = 1f - t * t;
                }
                else if (_anim[i] < RiseDelay + RiseSeconds)
                {
                    _anim[i] += dt;
                    float t = Mathf.Clamp01((_anim[i] - RiseDelay) / RiseSeconds);
                    // Ease-out-back: overshoots slightly, then settles, a little pop out of the ground.
                    float u = t - 1f;
                    rise = 1f + 2.2f * u * u * u + 1.2f * u * u;
                }

                float s = _scale[i];
                sr.transform.localScale = new Vector3(s, s * rise, 1f);

                Color tint = _nemesis[i] ? NemesisRed : GhostCyan;
                Color c = _graveInColour ? Color.white : Color.Lerp(stone, tint, Mathf.Lerp(0.22f, 0.45f, _age[i]));
                c.a = Mathf.Lerp(0.75f, 1f, _age[i]);
                sr.color = c;

                if (!_passed[i] && playerX > _x[i])
                {
                    _passed[i] = true;
                    if (_shards != null)
                        _shards.Burst(new Vector2(_x[i] - 0.12f, 0.25f * s), new Vector2(0.24f, 0.3f * s),
                            new Color(tint.r, tint.g, tint.b, 0.6f), 5, 2.4f, 0.45f);
                }
            }
        }

        // Skins addendum: the grave just ahead of the player shows its epitaph; one label serves them all.
        void ShowEpitaph(float playerX)
        {
            if (_epitaph == null) return;
            int best = -1;
            float bestDx = float.MaxValue;
            for (int i = 0; i < _graves.Length; i++)
            {
                if (!_graves[i].enabled || _crumbling[i] || _epitaphs[i] == null) continue;
                float dx = _x[i] - playerX;
                if (dx < -EpitaphBehind || dx > EpitaphAhead || Mathf.Abs(dx) >= bestDx) continue;
                best = i;
                bestDx = Mathf.Abs(dx);
            }
            if (best < 0)
            {
                if (_epitaph.gameObject.activeSelf) _epitaph.gameObject.SetActive(false);
                return;
            }
            if (best != _epitaphShown)
            {
                _epitaphShown = best;
                _epitaph.SetText(_epitaphs[best]);
                // Estimated from the length (about 17 of 1080 canvas units a character) rather than measured,
                // which could allocate mid-run.
                _epitaphHalf = Mathf.Min(0.45f, _epitaphs[best].Length * EpitaphCharWidth / 2f);
            }
            if (!_epitaph.gameObject.activeSelf) _epitaph.gameObject.SetActive(true);
            if (_rig != null)
            {
                var rect = _epitaph.rectTransform;
                float vx = (_x[best] - _rig.Left) / (_rig.Right - _rig.Left);
                if (_rig.Mirror) vx = 1f - vx;
                vx = Mathf.Clamp(vx, _epitaphHalf + 0.03f, 0.97f - _epitaphHalf);
                var at = new Vector2(vx, (EpitaphHeight - _rig.Bottom) / _rig.Height);
                rect.anchorMin = rect.anchorMax = at;
                rect.anchoredPosition = Vector2.zero;
            }
            float fade = 1f - Mathf.Clamp01((bestDx - 1.5f) / (EpitaphAhead - 1.5f));
            _epitaph.alpha = 0.85f * fade;
        }

        void StartCrumble(int i)
        {
            if (!_graves[i].enabled || _crumbling[i]) return;
            _crumbling[i] = true;
            _anim[i] = 0f;
            if (_shards == null) return;
            Bounds b = _graves[i].bounds;
            _shards.Burst(b.min, b.size, _graves[i].color, 8, 3f, 0.5f);
        }

        void Place(int i, float x, int runIndex)
        {
            // Stable per-run offset in whole pixels, so graves that share a death spot read as a little cluster,
            // not one stone. Pixel art is never rotated or scaled oddly, so there is no tilt.
            uint h = (uint)runIndex * 2654435761u;
            float jitter = ((int)((h >> 8) % 9) - 4) / (float)CameraRig.PixelsPerUnit;
            _scale[i] = 1f;
            _x[i] = SimUnits.Pixel(x) + jitter;

            SpriteRenderer sr = _graves[i];
            sr.transform.localPosition = new Vector3(_x[i], 0f, 0f);
            sr.transform.localScale = new Vector3(_scale[i], _scale[i], 1f);
            sr.enabled = true;
        }

        /// <summary>The grave stands on the floor: a death over a gap is moved back to the ledge it fell from.</summary>
        static float GroundX(int deathX, Course course)
        {
            int x = Mathf.Max(0, deathX);
            if (course.HasFloorAt(x)) return SimUnits.ToWorld(x);
            for (int step = 0; step < MaxBacktrackSteps && x > 0 && !course.HasFloorAt(x); step++) x -= BacktrackSub;
            return SimUnits.ToWorld(Mathf.Max(0, x)) - LedgeInset;
        }
    }
}
