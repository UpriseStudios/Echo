using System;
using System.Collections.Generic;
using Echo.Core.Achievements;
using Echo.Core.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// Stats screen (tech spec 14): best, runs, deaths and Nemesis stats, then the 30 achievements (skins
    /// addendum) as a scrolling list with a progress bar each. Hidden ones read "???" until they make progress.
    /// </summary>
    public sealed class StatsView : MonoBehaviour
    {
        const string Accent = "#FFC94A";
        const string Label = "#FFFFFFB3";
        static readonly Color Done = new Color(1f, 0.79f, 0.29f);
        static readonly Color Going = new Color(0.45f, 0.95f, 1f);

        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _summary;
        [SerializeField] TMP_Text _header;
        [SerializeField] ScrollRect _scroll;
        [SerializeField] RectTransform _content;
        [SerializeField] RectTransform _rowTemplate;
        [SerializeField] Button _close;
        [Tooltip("Ghost Challenges: send the run behind the best, and open a code from the clipboard.")]
        [SerializeField] Button _challenge;
        [SerializeField] Button _paste;

        readonly List<GameObject> _rows = new List<GameObject>();

        public event Action Closed;
        public event Action ChallengeClicked;
        public event Action PasteClicked;

        void Awake()
        {
            _rowTemplate.gameObject.SetActive(false);
            _close.onClick.AddListener(() => Closed?.Invoke());
            if (_challenge != null) _challenge.onClick.AddListener(() => ChallengeClicked?.Invoke());
            if (_paste != null) _paste.onClick.AddListener(() => PasteClicked?.Invoke());
        }

        public void Show(Progress p, bool showGallery = true)
        {
            if (_challenge != null) _challenge.interactable = p.BestRun != null;
            _root.SetActive(true);
            _summary.text = Summary(p);

            int earned = 0;
            foreach (AchievementDef def in AchievementCatalog.All) if (p.Achievements.Contains(def.Id)) earned++;
            _header.text = $"ACHIEVEMENTS <color={Accent}>{earned}/{AchievementCatalog.All.Length}</color>";

            foreach (GameObject row in _rows) Destroy(row);
            _rows.Clear();
            foreach (AchievementDef def in AchievementCatalog.All) _rows.Add(Row(def, p));
            if (showGallery) AddGallery(p);
            _scroll.verticalNormalizedPosition = 1f;
        }

        GameObject Row(AchievementDef def, Progress p)
        {
            RectTransform row = Instantiate(_rowTemplate, _content);
            row.gameObject.SetActive(true);
            bool unlocked = p.Achievements.Contains(def.Id);
            int progress = unlocked ? def.Target : AchievementRules.ProgressOf(p, def.Id);
            bool secret = def.Hidden && !unlocked && progress == 0;

            var title = row.Find("Title").GetComponent<TMP_Text>();
            var description = row.Find("Description").GetComponent<TMP_Text>();
            var count = row.Find("Count").GetComponent<TMP_Text>();
            var fill = (RectTransform)row.Find("Bar/Fill");

            title.text = secret ? "???" : def.Title;
            title.color = unlocked ? Done : Color.white;
            description.text = secret ? "Hidden achievement" : def.Description;
            count.text = unlocked ? "DONE" : secret ? "" : $"{progress}/{def.Target}";
            count.color = unlocked ? Done : new Color(1f, 1f, 1f, 0.7f);

            float k = def.Target > 0 ? Mathf.Clamp01(progress / (float)def.Target) : 0f;
            fill.anchorMax = new Vector2(k, 1f);
            fill.GetComponent<Image>().color = unlocked ? Done : Going;
            row.Find("Bar").gameObject.SetActive(!secret);
            return row.gameObject;
        }

        // Rogues' Gallery (value-adds doc): every defeated Nemesis, most kills first, after the achievements.
        void AddGallery(Progress p)
        {
            _rows.Add(TextRow($"ROGUES' GALLERY  {p.Gallery.Count}", "Every Nemesis you have defeated", "", Done));
            if (p.Gallery.Count == 0)
                _rows.Add(TextRow("Nobody yet", "Beat your best while a Nemesis haunts you", "", Color.white));
            foreach (GalleryEntry e in p.Gallery)
                _rows.Add(TextRow(e.Name, $"{e.Epitaph} · {e.DefeatedOn}", $"{e.Kills} kills", Color.white));
        }

        GameObject TextRow(string titleText, string descriptionText, string countText, Color titleColor)
        {
            RectTransform row = Instantiate(_rowTemplate, _content);
            row.gameObject.SetActive(true);
            var title = row.Find("Title").GetComponent<TMP_Text>();
            title.text = titleText;
            title.color = titleColor;
            row.Find("Description").GetComponent<TMP_Text>().text = descriptionText;
            var count = row.Find("Count").GetComponent<TMP_Text>();
            count.text = countText;
            count.color = new Color(1f, 0.33f, 0.33f);
            row.Find("Bar").gameObject.SetActive(false);
            return row.gameObject;
        }

        public static string Summary(Progress p)
        {
            string worst = p.Stats.WorstNemesisKills > 0
                ? $"{p.Stats.WorstNemesisName} ({p.Stats.WorstNemesisKills})"
                : "none yet";
            return Row("Best", p.Best.ToString()) + Row("Total runs", p.Stats.TotalRuns.ToString())
                 + Row("Deaths", p.Stats.Deaths.ToString()) + Row("Furthest", $"{p.Stats.MaxDistance} m")
                 + Row("Nemeses defeated", p.Stats.NemesesDefeated.ToString()) + Row("Worst Nemesis", worst);
        }

        static string Row(string label, string value) => $"<color={Label}>{label}</color><pos=58%>{value}\n";

        public void Hide() => _root.SetActive(false);
    }
}
