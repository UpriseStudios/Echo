using System;
using System.Collections.Generic;
using Echo.Core.Achievements;
using Echo.Core.Rules;
using Echo.Game.Cosmetics;
using Echo.Game.Persistence;
using Echo.Services.Iap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Game.UI
{
    /// <summary>
    /// The store and wardrobe (tech spec 14 Store, cosmetics part), in tabs (a user revision: it had grown into one very
    /// long list, and this is where players spend money, so it is laid out like a shop): Runners, Packs, Worlds, Style,
    /// Extras and Free (<see cref="SkinsTab"/>). Each paid tab leads with its bundle as a featured card. Skins addendum:
    /// the base characters are free, Pumpkin unlocks after rewarded ads (any rewarded ad counts) and earned skins show
    /// their requirement and progress until won. A locked pack character shows its pack's price; tapping it buys the pack.
    /// </summary>
    public sealed class SkinsView : MonoBehaviour
    {
        [SerializeField] GameObject _root;
        [SerializeField] ScrollRect _scroll;
        [SerializeField] RectTransform _content;
        [SerializeField] TMP_Text _headerTemplate;
        [SerializeField] RectTransform _characterGridTemplate;
        [SerializeField] RectTransform _itemGridTemplate;
        [SerializeField] Button _characterTemplate;
        [SerializeField] Button _itemTemplate;
        [SerializeField] Button _productTemplate;
        [SerializeField] Button _close;
        [SerializeField] Button _restore;

        // The tab bar, in SkinsTab order, and the worlds' picture cards.
        [SerializeField] Button[] _tabs = new Button[0];
        [SerializeField] RectTransform _worldGridTemplate;
        [SerializeField] RectTransform _packGridTemplate;
        [SerializeField] Button _worldTemplate;
        [SerializeField] Sprite _normalFar;
        [SerializeField] Sprite _normalNear;
        [SerializeField] Color _tabOn = new Color(1f, 0.79f, 0.29f);
        [SerializeField] Color _tabOff = new Color(0.16f, 0.2f, 0.27f);

        SkinsTab _tab = SkinsTab.Runners;

        /// <summary>The section showing.</summary>
        public SkinsTab Tab => _tab;

        public void SelectTab(SkinsTab tab)
        {
            _tab = tab;
            Refresh();
            _scroll.verticalNormalizedPosition = 1f;
        }

        readonly List<GameObject> _built = new List<GameObject>();
        CosmeticCatalog _catalog;
        CosmeticsSaveData _cosmetics;
        int _best;
        Func<string, string> _price;
        Func<string, bool> _ownsProduct;
        Func<bool> _adReady;
        LifetimeStats _stats = new LifetimeStats();
        int _achievements;
        bool _christmasOnSale;
        // Whether a rewarded ad was ready at the last redraw: the next ad loads a few seconds after one is
        // watched, so the ad buttons are redrawn when that changes (device testing: they stayed grey).
        bool _adWasReady;
        float _adCheckIn;

        public event Action Changed;
        public event Action Closed;
        public event Action<string> BuyClicked;
        public event Action WatchAdClicked;
        public event Action RestoreClicked;
        public event Action PowerUpsClicked;
        public event Action TrialAdClicked;

        void Awake()
        {
            foreach (Component t in new Component[] { _headerTemplate, _characterGridTemplate, _itemGridTemplate, _characterTemplate, _itemTemplate, _productTemplate })
                t.gameObject.SetActive(false);
            if (_worldGridTemplate != null) _worldGridTemplate.gameObject.SetActive(false);
            if (_packGridTemplate != null) _packGridTemplate.gameObject.SetActive(false);
            if (_worldTemplate != null) _worldTemplate.gameObject.SetActive(false);
            for (int i = 0; i < _tabs.Length; i++)
            {
                SkinsTab tab = (SkinsTab)i;
                _tabs[i].onClick.AddListener(() => SelectTab(tab));
            }
            _close.onClick.AddListener(() => Closed?.Invoke());
            _restore.onClick.AddListener(() => RestoreClicked?.Invoke());
        }

        public void Show(CosmeticCatalog catalog, CosmeticsSaveData cosmetics, int best,
                         Func<string, string> price, Func<string, bool> ownsProduct, Func<bool> adReady, LifetimeStats stats,
                         int achievementsEarned = 0, bool christmasOnSale = false)
        {
            _achievements = achievementsEarned;
            _christmasOnSale = christmasOnSale;
            _catalog = catalog;
            _cosmetics = cosmetics;
            _best = best;
            _price = price;
            _ownsProduct = ownsProduct;
            _adReady = adReady;
            _stats = stats ?? new LifetimeStats();
            _root.SetActive(true);
            Refresh();
            _scroll.verticalNormalizedPosition = 1f;
        }

        void Update()
        {
            if (_adReady == null || !_root.activeSelf || (_adCheckIn -= Time.unscaledDeltaTime) > 0f) return;
            _adCheckIn = 0.5f;
            if (_adReady() != _adWasReady)
            {
                float scroll = _scroll.verticalNormalizedPosition;
                Refresh();
                _scroll.verticalNormalizedPosition = scroll;
            }
        }

        /// <summary>Redraws after a purchase, restore or equip.</summary>
        public void Refresh()
        {
            if (_catalog == null || !_root.activeSelf) return;
            _adWasReady = _adReady != null && _adReady();
            foreach (GameObject go in _built) Destroy(go);
            _built.Clear();
            for (int i = 0; i < _tabs.Length; i++)
            {
                bool on = (int)_tab == i;
                _tabs[i].GetComponent<Image>().color = on ? _tabOn : _tabOff;
                _tabs[i].GetComponentInChildren<TMP_Text>().color = on ? new Color(0.08f, 0.1f, 0.12f) : Color.white;
            }

            switch (_tab)
            {
                case SkinsTab.Runners:
                    Header("Runners", "tap one to wear it");
                    RectTransform characters = Grid(_characterGridTemplate);
                    foreach (CosmeticItem item in _catalog.Items)
                        if (item.Type == CosmeticType.Character && !item.PurchaseOnly && !item.AllAchievements && !Seasonal(item)) CharacterCard(characters, item);
                    ElementalsSection();
                    TimeRunSection();
                    SeasonalSection();
                    break;

                case SkinsTab.Packs:
                    Featured(SkinPacks.SeasonName, "All 8 packs · 32 characters · best value", ProductIds.SeasonOfEcho, PackOwned(null));
                    foreach (var (name, productId) in SkinPacks.All)
                    {
                        Header($"{name} Pack", "4 characters");
                        RectTransform grid = Grid(_packGridTemplate != null ? _packGridTemplate : _characterGridTemplate);
                        foreach (CosmeticItem item in _catalog.Items)
                            if (item.Type == CosmeticType.Character && item.ProductId == productId) CharacterCard(grid, item);
                        ProductButton($"{name} Pack", productId, PackOwned(productId));
                    }
                    break;

                case SkinsTab.Worlds:
                    WorldsSection();
                    break;

                case SkinsTab.Style:
                    Section("Colours", "for the Hooded runner", CosmeticType.Runner);
                    Section("Trails", "behind you and your ghosts", CosmeticType.Trail);
                    Section("Palettes", "the sky's colours", CosmeticType.Palette);
                    break;

                case SkinsTab.Extras:
                    Section("Gravestones", "left where your runs end", CosmeticType.Grave);
                    Section("Accessories", "worn by you and your ghosts", CosmeticType.Accessory);
                    break;

                default:
                    // Power-ups (a user revision): earned with rewarded ads, opened as their own card.
                    Header("Power-ups", "free with ads");
                    Button powerUps = Instantiate(_productTemplate, _content);
                    powerUps.gameObject.SetActive(true);
                    _built.Add(powerUps.gameObject);
                    powerUps.interactable = true;
                    powerUps.GetComponentInChildren<TMP_Text>().text = "Shield, Phase, Lantern, 1-UP <size=75%>Get</size>";
                    powerUps.onClick.AddListener(() => PowerUpsClicked?.Invoke());
                    // Colour packs (a user revision: no longer sold): each unlocks at a score, or after enough rewarded ads.
                    Header("Colour packs", "score or ads");
                    var packs = new List<string>();
                    foreach (CosmeticItem item in _catalog.Items)
                        if (!string.IsNullOrEmpty(item.AdPack) && !packs.Contains(item.AdPack)) packs.Add(item.AdPack);
                    foreach (string pack in packs) AdPackButton(pack);
                    break;
            }
        }

        // A bundle, first in its tab: its name and price on one line, what's in it on the next.
        void Featured(string name, string blurb, string productId, bool owned)
        {
            Header("Best value", null);
            Button button = Instantiate(_productTemplate, _content);
            button.gameObject.SetActive(true);
            _built.Add(button.gameObject);
            string price = _price(productId);
            button.interactable = !owned && price != null;
            button.GetComponentInChildren<TMP_Text>().text =
                $"{name}  <size=80%>{(owned ? "Owned" : price ?? "Unavailable")}</size>\n<size=60%>{blurb}</size>";
            button.onClick.AddListener(() => BuyClicked?.Invoke(productId));
        }

        // Seasons doc: the skins earned in their event (kept for good once earned), and the Christmas Pack, on sale during
        // Christmas and New Year (and always shown to anyone who owns part of it).
        void SeasonalSection()
        {
            Header("Seasonal", "earned during their event");
            RectTransform grid = Grid(_characterGridTemplate);
            foreach (CosmeticItem item in _catalog.Items)
                if (item.Type == CosmeticType.Character && Seasonal(item) && !TimeRunReward(item)) CharacterCard(grid, item);

            var (name, productId) = SkinPacks.Christmas;
            bool anyOwned = false;
            foreach (CosmeticItem item in _catalog.Items)
                if (item.ProductId == productId && CosmeticUnlocks.IsOwned(item, _best, _cosmetics.Owned)) anyOwned = true;
            if (!_christmasOnSale && !anyOwned) return;
            Header($"{name} Pack", "back every December");
            RectTransform pack = Grid(_packGridTemplate != null ? _packGridTemplate : _characterGridTemplate);
            foreach (CosmeticItem item in _catalog.Items)
                if (item.Type == CosmeticType.Character && item.ProductId == productId) CharacterCard(pack, item);
            ProductButton($"{name} Pack", productId, PackOwned(productId));
        }

        // A user revision: worlds, the backdrop of every run. Store worlds (each with a price, and the Worlds Bundle) and
        // mode rewards (never sold) share one grid; a season's own look still shows in its dates unless Settings says not.
        void WorldsSection()
        {
            int sold = 0;
            bool allOwned = true;
            foreach (CosmeticItem item in _catalog.Items)
                if (item.Type == CosmeticType.World && item.BundleProductId == ProductIds.WorldsBundle)
                {
                    sold++;
                    allOwned &= CosmeticUnlocks.IsOwned(item, _best, _cosmetics.Owned);
                }
            if (sold > 0) Featured("Worlds Bundle", $"All {sold} store worlds · best value", ProductIds.WorldsBundle, allOwned);
            Header("Worlds", "the backdrop of every run");
            RectTransform grid = Grid(_worldGridTemplate != null ? _worldGridTemplate : _itemGridTemplate);
            foreach (CosmeticItem item in _catalog.Items)
                if (item.Type == CosmeticType.World && !item.ServerReward) WorldButton(grid, item);
            Header("Earned in modes", "never sold");
            RectTransform earned = Grid(_worldGridTemplate != null ? _worldGridTemplate : _itemGridTemplate);
            foreach (CosmeticItem item in _catalog.Items)
                if (item.Type == CosmeticType.World && item.ServerReward) WorldButton(earned, item);
        }

        void WorldButton(RectTransform grid, CosmeticItem item)
        {
            Button button = Instantiate(_worldTemplate != null ? _worldTemplate : _itemTemplate, grid);
            button.gameObject.SetActive(true);
            bool owned = CosmeticUnlocks.IsOwned(item, _best, _cosmetics.Owned);
            bool equipped = Equipped(item.Type) == item.Id;
            string price = item.PurchaseOnly ? _price(item.ProductId) : null;
            // World Time Run doc, phase 4: free runs to try it ("2 free runs left"); worn, a tap buys it.
            int free = owned ? 0 : WorldTrial.Left(item, _cosmetics);

            // A little picture of the world: its sky, far layer and near layer, tinted from its own palette.
            Transform preview = button.transform.Find("Preview");
            // Time Run's picture is its first era (its own look lives in its eras).
            Echo.Game.Seasons.SeasonTheme look = item.World != null && item.World.Eras.Length > 0 ? item.World.Eras[0] : item.World;
            if (preview != null)
            {
                Color sky = look != null && look.PaletteStops.Length > 0 ? look.PaletteStops[0] : item.Color;
                preview.GetComponent<Image>().color = sky;
                var far = preview.Find("Far").GetComponent<Image>();
                var near = preview.Find("Near").GetComponent<Image>();
                far.sprite = look != null && look.FarLayer != null ? look.FarLayer : _normalFar;
                near.sprite = look != null && look.NearLayer != null ? look.NearLayer : _normalNear;
                far.color = Color.Lerp(sky, Color.white, 0.45f);
                near.color = Color.Lerp(sky, Color.black, 0.35f);
                // A world painted in its own colours (Time Run's eras) shows as drawn.
                if (look != null && look.FarTint == Color.white)
                {
                    far.color = Color.white;
                    near.color = Color.white;
                }
                if (!owned && free == 0)
                {
                    far.color = Color.Lerp(far.color, Color.grey, 0.25f);
                    near.color = Color.Lerp(near.color, Color.grey, 0.25f);
                }
            }
            var label = button.GetComponentInChildren<TMP_Text>();
            string runs = free == 1 ? "1 free run left" : $"{free} free runs left";
            label.text = $"{item.DisplayName}\n<size=72%>" +
                         (free > 0 && equipped ? $"<color=#FFC94A>{runs}</color> · {price ?? "Buy"}"
                             : free > 0 ? $"{runs} · Try it"
                             : owned && equipped ? "<color=#FFC94A>Equipped</color>" : owned ? "Tap to wear" : item.ServerReward ? item.UnlockText : price ?? "Unavailable") + "</size>";

            // Still worn after its free runs (the run falls back to the normal world): it asks to be bought.
            button.interactable = free > 0 ? (!equipped || price != null) : owned ? !equipped : price != null;
            CosmeticItem captured = item;
            button.onClick.AddListener(() =>
            {
                bool wearing = Equipped(captured.Type) == captured.Id;
                if (CosmeticUnlocks.IsOwned(captured, _best, _cosmetics.Owned)) Equip(captured);
                else if (WorldTrial.Left(captured, _cosmetics) > 0 && !wearing) Equip(captured);
                else BuyClicked?.Invoke(captured.ProductId);
            });
        }

        // Earned with an achievement (an event's, or the Time Run world's); Pumpkin also unlocks with ads, so it stays with
        // the main cast.
        static bool Seasonal(CosmeticItem item) => !string.IsNullOrEmpty(item.UnlockAchievement) && item.AdsToUnlock == 0;

        // World Time Run doc, phase 4: the Caveman and the Chrono Android, earned with the world's achievements.
        static bool TimeRunReward(CosmeticItem item) => System.Array.IndexOf(AchievementCatalog.TimeRun, item.UnlockAchievement) >= 0;

        void TimeRunSection()
        {
            Header("Time Run", "earned in the Time Run world");
            RectTransform grid = Grid(_characterGridTemplate);
            foreach (CosmeticItem item in _catalog.Items)
                if (item.Type == CosmeticType.Character && TimeRunReward(item)) CharacterCard(grid, item);
        }

        void AdPackButton(string pack)
        {
            int ads = 0;
            bool owned = true;
            foreach (CosmeticItem item in _catalog.Items)
            {
                if (item.AdPack != pack) continue;
                ads = Mathf.Max(ads, item.AdsToUnlock);
                owned &= CosmeticUnlocks.IsOwned(item, _best, _cosmetics.Owned);
            }
            Button button = Instantiate(_productTemplate, _content);
            button.gameObject.SetActive(true);
            _built.Add(button.gameObject);
            bool ready = _adReady();
            button.interactable = !owned && ready;
            button.GetComponentInChildren<TMP_Text>().text = owned
                ? $"{pack} Pack <size=75%>Unlocked</size>"
                : $"{pack} Pack <size=75%>Watch ad {Mathf.Min(_cosmetics.AdsWatched, ads)}/{ads}</size>";
            button.onClick.AddListener(() => WatchAdClicked?.Invoke());
        }

        // A skin pack is owned once all its characters are, however they were bought; null asks about every pack in the
        // Season of Echo bundle (the Christmas Pack isn't one).
        bool PackOwned(string productId)
        {
            foreach (CosmeticItem item in _catalog.Items)
                if (item.Type == CosmeticType.Character && item.PurchaseOnly
                    && (productId == null ? item.BundleProductId == ProductIds.SeasonOfEcho : item.ProductId == productId)
                    && !CosmeticUnlocks.IsOwned(item, _best, _cosmetics.Owned))
                    return false;
            return true;
        }

        public void Hide() => _root.SetActive(false);

        void Section(string title, string subtitle, CosmeticType type)
        {
            Header(title, subtitle);
            RectTransform grid = Grid(_itemGridTemplate);
            foreach (CosmeticItem item in _catalog.Items)
                if (item.Type == type) ItemButton(grid, item);
        }

        void Header(string title, string subtitle)
        {
            TMP_Text header = Instantiate(_headerTemplate, _content);
            header.gameObject.SetActive(true);
            header.text = subtitle == null ? title.ToUpperInvariant() : $"{title.ToUpperInvariant()}  <size=60%>{subtitle}</size>";
            _built.Add(header.gameObject);
        }

        RectTransform Grid(RectTransform template)
        {
            RectTransform grid = Instantiate(template, _content);
            grid.gameObject.SetActive(true);
            _built.Add(grid.gameObject);
            return grid;
        }

        // The Elementals (a user revision): earned for good with every achievement; until then, 5 ads lend the pack
        // for 5 runs. The button shows the trial, or the ads towards the next one.
        void ElementalsSection()
        {
            bool any = false, allOwned = true;
            foreach (CosmeticItem item in _catalog.Items)
                if (item.AllAchievements)
                {
                    any = true;
                    allOwned &= CosmeticUnlocks.IsOwned(item, _best, _cosmetics.Owned);
                }
            if (!any) return;

            Header("Elementals", allOwned ? "earned" : $"all {ElementalTrial.AchievementsNeeded} achievements");
            RectTransform grid = Grid(_characterGridTemplate);
            foreach (CosmeticItem item in _catalog.Items)
                if (item.Type == CosmeticType.Character && item.AllAchievements) CharacterCard(grid, item);
            if (allOwned) return;

            Button trial = Instantiate(_productTemplate, _content);
            trial.gameObject.SetActive(true);
            _built.Add(trial.gameObject);
            bool active = ElementalTrial.Active(_cosmetics);
            trial.interactable = !active && _adReady();
            trial.GetComponentInChildren<TMP_Text>().text = active
                ? $"Trial running <size=75%>{_cosmetics.TrialRunsLeft} runs left</size>"
                : $"Try for {ElementalTrial.TrialRuns} runs <size=75%>Watch ad {_cosmetics.TrialAds}/{ElementalTrial.AdsForTrial}</size>";
            trial.onClick.AddListener(() => TrialAdClicked?.Invoke());
        }

        void CharacterCard(RectTransform grid, CosmeticItem item)
        {
            Button card = Instantiate(_characterTemplate, grid);
            card.gameObject.SetActive(true);
            bool owned = ElementalTrial.Available(item, _best, _cosmetics);
            bool borrowed = owned && !CosmeticUnlocks.IsOwned(item, _best, _cosmetics.Owned);
            bool equipped = Equipped(item.Type) == item.Id;

            var preview = card.transform.Find("Preview").GetComponent<Image>();
            preview.sprite = item.RunFrames.Length > 0 ? item.RunFrames[0] : null;
            preview.color = item.Tintable ? TintFor() : Color.white;

            bool byAds = item.AdsToUnlock > 0;
            bool earned = item.UnlockStat != CosmeticUnlockStat.None;
            bool prestige = item.AllAchievements;
            bool seasonal = Seasonal(item);
            string status = equipped ? "<color=#FFC94A>Equipped</color>"
                : borrowed ? $"Trial: {_cosmetics.TrialRunsLeft} runs left"
                : owned ? "Tap to wear"
                : seasonal ? item.UnlockText
                : prestige ? $"Achievements {Mathf.Min(_achievements, ElementalTrial.AchievementsNeeded)}/{ElementalTrial.AchievementsNeeded}"
                : byAds ? $"Watch ad {Mathf.Min(_cosmetics.AdsWatched, item.AdsToUnlock)}/{item.AdsToUnlock}"
                : earned ? Requirement(item)
                : _price(item.ProductId) ?? "Unavailable";
            card.GetComponentInChildren<TMP_Text>().text = $"{item.DisplayName}\n<size=70%>{status}</size>";
            card.interactable = !equipped && (owned || (!prestige && !seasonal && (byAds ? _adReady() : !earned && _price(item.ProductId) != null)));
            // Locked earned and seasonal skins, and Elementals not yet earned or on trial, show as a dark silhouette.
            if (!owned && (earned || prestige || seasonal)) preview.color = new Color(0.12f, 0.12f, 0.16f, 1f);

            CosmeticItem captured = item;
            card.onClick.AddListener(() =>
            {
                if (ElementalTrial.Available(captured, _best, _cosmetics)) Equip(captured);
                else if (captured.AdsToUnlock > 0) WatchAdClicked?.Invoke();
                else BuyClicked?.Invoke(captured.ProductId);
            });
        }

        void ItemButton(RectTransform grid, CosmeticItem item)
        {
            Button button = Instantiate(_itemTemplate, grid);
            button.gameObject.SetActive(true);
            bool owned = CosmeticUnlocks.IsOwned(item, _best, _cosmetics.Owned);
            bool equipped = Equipped(item.Type) == item.Id;

            var image = button.GetComponent<Image>();
            image.color = Swatch(item, owned);
            var label = button.GetComponentInChildren<TMP_Text>();
            label.text = $"{item.DisplayName}\n<size=72%>" +
                         (equipped ? "Equipped" : owned ? "Tap to equip" : Seasonal(item) || item.ServerReward ? item.UnlockText
                             : item.UnlockStat != CosmeticUnlockStat.None ? Requirement(item)
                             : item.AdsToUnlock > 0 ? $"Score {item.UnlockScore} or {item.AdsToUnlock} ads" : $"Score {item.UnlockScore}") + "</size>";
            label.color = Luminance(image.color) > 0.55f ? new Color(0.08f, 0.1f, 0.12f) : Color.white;

            button.interactable = owned && !equipped;
            CosmeticItem captured = item;
            button.onClick.AddListener(() => Equip(captured));
        }

        void ProductButton(string name, string productId, bool owned)
        {
            Button button = Instantiate(_productTemplate, _content);
            button.gameObject.SetActive(true);
            _built.Add(button.gameObject);
            string price = _price(productId);
            button.interactable = !owned && price != null;
            button.GetComponentInChildren<TMP_Text>().text = owned
                ? $"{name} <size=75%>Owned</size>"
                : $"{name} <size=75%>{price ?? "Unavailable"}</size>";
            button.onClick.AddListener(() => BuyClicked?.Invoke(productId));
        }

        // How to earn it and how far along the player is, e.g. "Die 250 times" over "37/250".
        string Requirement(CosmeticItem item)
        {
            int value = Mathf.Min(CosmeticUnlocks.StatValue(item.UnlockStat, _stats), item.UnlockTarget);
            return $"{item.UnlockText}\n{value}/{item.UnlockTarget}";
        }

        void Equip(CosmeticItem item)
        {
            var e = _cosmetics.Equipped;
            switch (item.Type)
            {
                case CosmeticType.Character: e.Character = item.Id; break;
                case CosmeticType.Runner: e.Runner = item.Id; break;
                case CosmeticType.Trail: e.Trail = item.Id; break;
                case CosmeticType.Grave: e.Grave = item.Id; break;
                case CosmeticType.Accessory: e.Accessory = item.Id; break;
                case CosmeticType.World: e.World = item.Id; break;
                default: e.Palette = item.Id; break;
            }
            Refresh();
            Changed?.Invoke();
        }

        string Equipped(CosmeticType type) => type switch
        {
            CosmeticType.Character => _cosmetics.Equipped.Character,
            CosmeticType.Runner => _cosmetics.Equipped.Runner,
            CosmeticType.Trail => _cosmetics.Equipped.Trail,
            CosmeticType.Grave => _cosmetics.Equipped.Grave,
            CosmeticType.Accessory => _cosmetics.Equipped.Accessory,
            CosmeticType.World => _cosmetics.Equipped.World,
            _ => _cosmetics.Equipped.Palette,
        };

        // The hooded runner's preview wears the equipped colour, as it will in the run.
        Color TintFor()
        {
            CosmeticItem colour = _catalog.Resolve(_cosmetics.Equipped.Runner, CosmeticType.Runner);
            return colour != null ? colour.Color : Color.white;
        }

        static Color Swatch(CosmeticItem item, bool owned)
        {
            Color c = item.Type == CosmeticType.Palette && item.PaletteStops.Length > 0 ? item.PaletteStops[0] : item.Color;
            return owned ? c : Color.Lerp(c, new Color(0.25f, 0.25f, 0.25f), 0.7f);
        }

        static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
    }
}
