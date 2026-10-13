using System.Collections.Generic;
using Echo.Core.Courses;
using Echo.Core.Ghosts;
using Echo.Core.Rules;
using Echo.Core.Sim;
using Echo.Game.Cosmetics;
using TMPro;
using UnityEngine;

namespace Echo.Game.Views
{
    /// <summary>
    /// Ghosts (tech spec 11): a pool of 31 outline boxes sharing one material. The newest is opaque and the
    /// oldest 25%; fading ghosts scale that down. Each replaying ghost shows a preview trail 18 ticks ahead,
    /// computed on a copy. Each ghost wears the skin its run wore; the Nemesis keeps it and adds red horns,
    /// is named, and taunts when the player overtakes it (skins addendum).
    /// </summary>
    public sealed class GhostViews : MonoBehaviour
    {
        public const int PoolSize = 31;
        public const int PreviewTicks = 18;
        const float OldestAlpha = 0.25f;
        const float TauntSeconds = 0.6f;
        const float TrailWidth = 0.05f;
        const float TrailAlpha = 0.45f;
        // Ghost Lantern (a power-up): every ghost's path shown much further ahead, and brighter.
        public const int LanternPreviewTicks = 90;
        const float LanternTrailAlpha = 0.85f;
        // Phase (a power-up): ghosts go see-through while they cannot hurt.
        const float PhasedAlpha = 0.25f;
        // Ghosts the encounter planner made harmless for this run are drawn faint so players can read them.
        const float HarmlessAlpha = 0.3f;
        const float ShadowFullUnits = 1f;
        const float ShadowFadeUnits = 1.5f;
        static readonly Color NemesisColor = new Color(1f, 0.25f, 0.25f);
        static readonly Color BountyColor = new Color(1f, 0.82f, 0.25f);

        [SerializeField] Material _material;
        [SerializeField] TMP_Text _nemesisLabel;
        // Ghost Challenges: the friend's ghost (the only one) wears its name, e.g. "Jamie's Ghost", in ghost cyan.
        string _nameTag;
        static readonly Color NameTagColor = new Color(0.45f, 0.95f, 1f);
        [SerializeField] Sprite[] _runFrames = new Sprite[0];
        [SerializeField] Sprite _jumpFrame;
        [SerializeField] Sprite _horns;
        [SerializeField] Sprite _bountyIcon;

        const float StrideUnits = 0.42f;

        SpriteRenderer[] _boxes;
        SpriteRenderer _hornsRenderer;
        SpriteRenderer _bountyRenderer;
        Twist _twist;
        int _bountyIndex = -1;
        bool _bountyClaimed;
        readonly Dictionary<string, CosmeticItem> _skins = new Dictionary<string, CosmeticItem>();
        LineRenderer[] _trails;
        readonly int[] _previewX = new int[LanternPreviewTicks];
        readonly int[] _previewY = new int[LanternPreviewTicks];
        bool _lantern, _phased;

        public void SetLantern(bool on) => _lantern = on;
        public void SetPhased(bool on) => _phased = on;
        Color _color = new Color(0.45f, 0.95f, 1f, 1f);
        bool _popped;
        int _labelRunIndex = -1;
        float _tauntLeft;
        float _lastNemesisDx;

        void Awake()
        {
            _boxes = new SpriteRenderer[PoolSize];
            _trails = new LineRenderer[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                _boxes[i] = _runFrames.Length > 0
                    ? BoxSprite.Create(transform, "Ghost" + i, _material, 5)
                    : BoxSprite.CreateOutline(transform, "Ghost" + i, _material, 5);
                _boxes[i].enabled = false;

                var trailGo = new GameObject("Trail" + i);
                trailGo.transform.SetParent(transform, false);
                var line = trailGo.AddComponent<LineRenderer>();
                line.sharedMaterial = _material;
                line.useWorldSpace = true;
                line.widthMultiplier = TrailWidth;
                line.numCapVertices = 2;
                line.sortingOrder = 4;
                line.positionCount = 0;
                line.enabled = false;
                _trails[i] = line;
            }
            if (_bountyIcon != null)
            {
                var bountyGo = new GameObject("BountyIcon");
                bountyGo.transform.SetParent(transform, false);
                _bountyRenderer = bountyGo.AddComponent<SpriteRenderer>();
                _bountyRenderer.sharedMaterial = _material;
                _bountyRenderer.sprite = _bountyIcon;
                _bountyRenderer.sortingOrder = 7;
                _bountyRenderer.enabled = false;
            }
            if (_horns != null)
            {
                var hornsGo = new GameObject("NemesisHorns");
                hornsGo.transform.SetParent(transform, false);
                _hornsRenderer = hornsGo.AddComponent<SpriteRenderer>();
                _hornsRenderer.sharedMaterial = _material;
                _hornsRenderer.sprite = _horns;
                _hornsRenderer.sortingOrder = 7;
                _hornsRenderer.enabled = false;
            }
            if (_nemesisLabel != null) _nemesisLabel.gameObject.SetActive(false);
        }

        /// <summary>This run's twist (value-adds doc). Mirror flips the camera, so the world-space name is flipped back.</summary>
        public void SetTwist(Twist twist)
        {
            _twist = twist;
            if (_nemesisLabel != null) _nemesisLabel.transform.localScale = new Vector3(twist == Twist.Mirror ? -1f : 1f, 1f, 1f);
        }

        /// <summary>Ghost Bounties: which ghost wears the coin (-1 for none), and whether it has been claimed.</summary>
        public void SetBounty(int index, bool claimed)
        {
            _bountyIndex = index;
            _bountyClaimed = claimed;
        }

        /// <summary>Every character's ghost frames by id, so each ghost can wear its run's skin. Built once.</summary>
        public void SetSkinLibrary(CosmeticCatalog catalog)
        {
            _skins.Clear();
            if (catalog == null) return;
            foreach (CosmeticItem item in catalog.Items)
                if (item.Type == CosmeticType.Character) _skins[item.Id] = item;
        }

        CosmeticItem SkinFor(string id) =>
            !string.IsNullOrEmpty(id) && _skins.TryGetValue(id, out CosmeticItem item) ? item : null;

        public void SetTrailColor(Color color) => _color = color;

        /// <summary>Ghosts are echoes of the player, so they wear the equipped character's outline.</summary>
        public void SetFrames(Sprite[] runFrames, Sprite jumpFrame)
        {
            if (runFrames != null && runFrames.Length > 0) _runFrames = runFrames;
            if (jumpFrame != null) _jumpFrame = jumpFrame;
        }

        /// <param name="playerTicks">Player ticks stepped so far; with alpha it places each ghost between its own ticks.</param>
        /// <summary>Ghost Challenges: name the only ghost ("Jamie's Ghost"); null for ordinary runs.</summary>
        /// <param name="runIndex">The tagged ghost's run; -1 tags the first ghost (a Lite challenge has only one).</param>
        /// <param name="nemesisPrefix">Before the Nemesis's name in a full challenge: "Jamie's Nemesis: ".</param>
        public void SetNameTag(string name, int runIndex = -1, string nemesisPrefix = "")
        {
            _nameTag = name;
            _nameTagRun = runIndex;
            _nemesisPrefix = nemesisPrefix ?? "";
            _labelRunIndex = int.MinValue;
        }

        int _nameTagRun = -1;
        string _nemesisPrefix = "";

        /// <summary>Daily Haunt (version 2.0): leader ghosts show their player's name.</summary>
        public void SetLeaderNames(bool on)
        {
            _leaderNames = on;
            _labelRunIndex = int.MinValue;
        }

        bool _leaderNames;
        readonly float[] _leaderX = new float[PoolSize];
        readonly bool[] _leaderNamed = new bool[PoolSize];

        public void Render(GhostManager ghosts, int playerTicks, float alpha, in SimConfig cfg,
                           Course course, GhostRecord nemesis, float playerX, bool previews)
        {
            int count = Mathf.Min(ghosts.Count, PoolSize);
            float w = SimUnits.ToWorld(cfg.PlayerHitboxW), h = SimUnits.ToWorld(cfg.PlayerHitboxH);
            bool labelShown = false;
            bool hornsShown = false;
            bool bountyShown = false;
            if (_tauntLeft > 0f) _tauntLeft -= Time.unscaledDeltaTime;

            // Ghosts tick slower than the player; this is how far the current frame is towards their next own tick.
            float ownFraction = Mathf.Clamp01((playerTicks * ghosts.ClockNum % ghosts.ClockDen + alpha * ghosts.ClockNum) / ghosts.ClockDen);

            // Daily Haunt: the one name label goes on the next leader ahead, wherever the others are in the pool.
            int labelledLeader = -1;
            if (_leaderNames && !_popped)
            {
                for (int i = 0; i < count; i++)
                {
                    GhostRecord record = ghosts.RecordAt(i);
                    ref readonly GhostReplay r = ref ghosts.ReplayAt(i);
                    _leaderNamed[i] = r.Phase != GhostPhase.Gone && record.Leader && !string.IsNullOrEmpty(record.Name);
                    _leaderX[i] = SimUnits.Lerp(r.PrevState, r.State, ownFraction).x;
                }
                labelledLeader = LeaderLabel.NextAhead(playerX, _leaderX, _leaderNamed, count);
            }

            for (int i = 0; i < PoolSize; i++)
            {
                SpriteRenderer box = _boxes[i];
                LineRenderer trail = _trails[i];
                if (_popped || i >= count || ghosts.ReplayAt(i).Phase == GhostPhase.Gone)
                {
                    box.enabled = false;
                    trail.enabled = false;
                    continue;
                }

                ref readonly GhostReplay r = ref ghosts.ReplayAt(i);
                GhostRecord record = ghosts.RecordAt(i);
                bool isNemesis = record == nemesis;
                Vector2 pos = SimUnits.Lerp(r.PrevState, r.State, ownFraction);

                // Ghosts are oldest first, so the last index is the newest.
                float age = count > 1 ? (float)i / (count - 1) : 1f;
                float a = Mathf.Lerp(OldestAlpha, 1f, age);
                if (r.Phase == GhostPhase.Fading) a *= (float)r.FadeTicksLeft / GhostManager.FadeTicks;

                // Skins addendum: each ghost wears the skin its run wore; the Nemesis keeps its own and adds horns.
                CosmeticItem skin = SkinFor(record.SkinId);
                Sprite[] runFrames = skin != null && skin.GhostFrames.Length > 0 ? skin.GhostFrames : _runFrames;
                Sprite jumpFrame = skin != null && skin.GhostJumpFrame != null ? skin.GhostJumpFrame : _jumpFrame;
                float headTop = skin != null ? skin.HeadTop : h;

                float scale = _twist == Twist.GiantGhosts ? WeeklyTwists.GiantGhostPct / 100f : 1f;
                float lift = 0f;
                if (isNemesis)
                {
                    a = Mathf.Max(a, 0.8f) * (r.Phase == GhostPhase.Fading ? (float)r.FadeTicksLeft / GhostManager.FadeTicks : 1f);
                    float dx = pos.x - playerX;
                    // Taunt as the player overtakes it (tech spec 11).
                    if (_lastNemesisDx > 0f && dx <= 0f && r.Phase == GhostPhase.Replaying) _tauntLeft = TauntSeconds;
                    _lastNemesisDx = dx;
                    if (_tauntLeft > 0f)
                    {
                        float t = 1f - _tauntLeft / TauntSeconds;
                        lift = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f)) * 0.25f * (1f - t);
                        scale *= 1f + 0.08f * Mathf.Sin(t * Mathf.PI * 6f);
                    }
                }

                if (r.Harmless) a *= HarmlessAlpha;
                if (_phased) a *= PhasedAlpha;
                // Shadow Week: a ghost ahead shows only in the last couple of units (about 0.5 s) before it is reached.
                if (_twist == Twist.ShadowWeek)
                {
                    float ahead = pos.x - playerX;
                    if (ahead > 0f) a *= Mathf.Clamp01(1f - (ahead - ShadowFullUnits) / ShadowFadeUnits);
                }

                float bw = w * scale, bh = h * scale;
                if (runFrames.Length > 0)
                {
                    // The run's figure drawn as an outline (game spec), animated by its own distance.
                    box.sprite = !r.State.Grounded && jumpFrame != null ? jumpFrame
                        : runFrames[(int)(Mathf.Abs(pos.x) / StrideUnits) % runFrames.Length];
                    box.transform.localPosition = new Vector3(SimUnits.Pixel(pos.x), SimUnits.Pixel(pos.y + lift), 0f);
                    box.transform.localScale = new Vector3(scale, scale, 1f);
                }
                else
                {
                    BoxSprite.PlaceSliced(box, pos.x - bw / 2f, pos.y + lift, bw, bh);
                }
                Color baseColor = _color;
                box.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                box.sortingOrder = isNemesis ? 6 : 5;
                box.enabled = true;

                // Ghost Bounties: a coin over the bounty ghost until it is claimed.
                if (i == _bountyIndex && !_bountyClaimed && r.Phase == GhostPhase.Replaying && _bountyRenderer != null)
                {
                    bountyShown = true;
                    _bountyRenderer.transform.localPosition = new Vector3(SimUnits.Pixel(pos.x), SimUnits.Pixel(pos.y + lift + headTop * scale + 0.15f), 0f);
                    _bountyRenderer.color = new Color(BountyColor.r, BountyColor.g, BountyColor.b, Mathf.Max(a, 0.6f));
                }

                bool tagged = _nameTag != null && (_nameTagRun < 0 ? i == 0 : record.RunIndex == _nameTagRun)
                              || i == labelledLeader;
                if (isNemesis || tagged)
                {
                    hornsShown |= isNemesis;
                    float top = headTop * scale;
                    if (isNemesis && _hornsRenderer != null)
                    {
                        _hornsRenderer.transform.localPosition = new Vector3(SimUnits.Pixel(pos.x), SimUnits.Pixel(pos.y + lift + top), 0f);
                        _hornsRenderer.color = new Color(NemesisColor.r, NemesisColor.g, NemesisColor.b, a);
                    }
                    if (_nemesisLabel != null)
                    {
                        labelShown = true;
                        int labelKey = tagged ? -2 - record.RunIndex : record.RunIndex;
                        if (_labelRunIndex != labelKey)
                        {
                            _labelRunIndex = labelKey;
                            _nemesisLabel.text = tagged ? (i == labelledLeader ? record.Name : _nameTag) : _nemesisPrefix + NemesisNames.For(record.RunIndex);
                        }
                        Color labelColor = tagged ? NameTagColor : NemesisColor;
                        _nemesisLabel.transform.position = new Vector3(pos.x, pos.y + top + 0.45f + lift, 0f);
                        _nemesisLabel.color = new Color(labelColor.r, labelColor.g, labelColor.b, a);
                    }
                }

                DrawTrail(trail, ghosts, i, course, cfg, h, baseColor, a, previews && r.Phase == GhostPhase.Replaying);
            }

            if (_hornsRenderer != null) _hornsRenderer.enabled = hornsShown;
            if (_bountyRenderer != null) _bountyRenderer.enabled = bountyShown;
            if (_nemesisLabel != null && _nemesisLabel.gameObject.activeSelf != labelShown)
                _nemesisLabel.gameObject.SetActive(labelShown);
        }

        // tech spec 11: step a copy of the ghost 18 of its own ticks ahead with its taps; the real ghost is untouched.
        void DrawTrail(LineRenderer trail, GhostManager ghosts, int i, Course course, in SimConfig cfg,
                       float h, Color color, float a, bool show)
        {
            int n = show ? ghosts.Preview(i, course, _lantern ? LanternPreviewTicks : PreviewTicks, cfg, _previewX, _previewY) : 0;
            if (n < 2)
            {
                trail.enabled = false;
                return;
            }
            trail.positionCount = n;
            for (int p = 0; p < n; p++)
                trail.SetPosition(p, new Vector3(SimUnits.ToWorld(_previewX[p]), SimUnits.ToWorld(_previewY[p]) + h / 2f, 0f));
            var c = new Color(color.r, color.g, color.b, a * (_lantern ? LanternTrailAlpha : TrailAlpha));
            trail.startColor = c;
            trail.endColor = new Color(c.r, c.g, c.b, 0f);
            trail.enabled = true;
        }

        /// <summary>The ghost-clear pop (tech spec 11): every visible ghost bursts at once, then all stay hidden.</summary>
        public int PopAll(ShardBurst shards)
        {
            int popped = 0;
            for (int i = 0; i < PoolSize; i++)
            {
                if (!_boxes[i].enabled) continue;
                Bounds b = _boxes[i].bounds;
                shards.Burst(b.min, b.size, _boxes[i].color, 14, 7f, 0.8f);
                popped++;
            }
            _popped = true;
            return popped;
        }

        public void ResetPop()
        {
            _popped = false;
            _tauntLeft = 0f;
            _lastNemesisDx = 0f;
        }
    }
}
