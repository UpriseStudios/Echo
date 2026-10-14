using Echo.Editor.Art;
using Echo.Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Editor
{
    /// <summary>The screens (tech spec 14) in the Echo style.</summary>
    public static partial class EchoSceneBuilder
    {
        enum ButtonStyle { Primary, Secondary }

        static Sprite _frameSprite;

        // The pixel frame is 12 px with a 4 px border; each of its pixels is drawn 6 canvas units wide.
        const float FramePixelScale = 1f / 6f;

        static HudView BuildHud(RectTransform parent)
        {
            RectTransform root = Stretch(NewRect("Hud", parent));
            var view = root.gameObject.AddComponent<HudView>();
            var score = MakeText(root, "Score", "0", 96, _black, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(700, 150));

            // Ghost count: the hood icon and a number, top-right.
            RectTransform icon = NewRect("GhostIcon", root);
            icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(1f, 1f);
            icon.anchoredPosition = new Vector2(-150, -72);
            icon.sizeDelta = new Vector2(64, 64);
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = LoadSprite(EchoArt.GhostIconPath);
            iconImage.color = GhostCyan;
            iconImage.raycastTarget = false;
            var ghosts = MakeText(root, "Ghosts", "0", 40, _black, new Vector2(1f, 1f), new Vector2(-44, -66), new Vector2(110, 80));
            ghosts.alignment = TextAlignmentOptions.Left;
            ghosts.rectTransform.pivot = new Vector2(1f, 1f);

            // 1-UP: a heart and label top-left, shown only while a life is held.
            RectTransform life = NewRect("Life", root);
            life.anchorMin = life.anchorMax = life.pivot = new Vector2(0f, 1f);
            life.anchoredPosition = new Vector2(44, -64);
            life.sizeDelta = new Vector2(220, 80);
            RectTransform heart = NewRect("Heart", life);
            heart.anchorMin = heart.anchorMax = heart.pivot = new Vector2(0f, 0.5f);
            heart.anchoredPosition = Vector2.zero;
            heart.sizeDelta = new Vector2(66, 60);
            var heartImage = heart.gameObject.AddComponent<Image>();
            heartImage.sprite = LoadSprite(EchoArt.HeartIconPath);
            heartImage.color = NemesisRed;
            heartImage.raycastTarget = false;
            var lifeLabel = MakeText(life, "Label", "1UP", 30, _black, new Vector2(0f, 0.5f), new Vector2(84, 0), new Vector2(130, 60));
            lifeLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
            lifeLabel.alignment = TextAlignmentOptions.Left;
            life.gameObject.SetActive(false);

            var nearMiss = MakeText(root, "NearMiss", "CLOSE +5", 44, _black, new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(600, 80));
            nearMiss.color = Accent;
            var toast = MakeText(root, "Toast", "", 42, _bold, new Vector2(0.5f, 1f), new Vector2(0, -300), new Vector2(1000, 80));
            var tapHint = MakeText(root, "TapHint", "TAP TO JUMP", 50, _black, new Vector2(0.5f, 0.5f), new Vector2(0, -420), new Vector2(1000, 110));
            tapHint.gameObject.AddComponent<Pulse>();
            var countdown = MakeText(root, "Countdown", "3", 200, _black, new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(400, 320));
            // Daily Haunt: the last 5 minutes before the board closes at 00:00 UTC.
            var boardCloses = MakeText(root, "BoardCloses", "", 34, _bold, new Vector2(0.5f, 1f), new Vector2(0, -380), new Vector2(900, 60));
            boardCloses.color = NemesisRed;
            boardCloses.gameObject.SetActive(false);

            Wire(view, "_root", root.gameObject);
            Wire(view, "_score", score);
            Wire(view, "_ghosts", ghosts);
            Wire(view, "_countdown", countdown);
            Wire(view, "_boardCloses", boardCloses);
            Wire(view, "_nearMiss", nearMiss);
            Wire(view, "_tapHint", tapHint);
            Wire(view, "_toast", toast);
            Wire(view, "_life", life.gameObject);

            // Power-ups (a user revision): Shield, Phase and Lantern along the floor, bottom left, tapped to use.
            string[] icons = { EchoArt.ShieldIconPath, EchoArt.PhaseIconPath, EchoArt.LanternIconPath };
            string[] names = { "PowerShield", "PowerPhase", "PowerLantern" };
            var buttons = new Button[icons.Length];
            for (int i = 0; i < icons.Length; i++)
            {
                RectTransform rt = NewRect(names[i], root);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(40 + i * 160, 150);
                rt.sizeDelta = new Vector2(136, 136);
                var bg = rt.gameObject.AddComponent<Image>();
                bg.sprite = Frame;
                bg.type = Image.Type.Sliced;
                bg.pixelsPerUnitMultiplier = FramePixelScale;
                bg.color = new Color(0f, 0f, 0f, 0.45f);
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                rt.gameObject.AddComponent<ButtonFeedback>();
                RectTransform iconRt = NewRect("Icon", rt);
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.sizeDelta = new Vector2(96, 96);
                var image = iconRt.gameObject.AddComponent<Image>();
                image.sprite = LoadSprite(icons[i]);
                image.raycastTarget = false;
                rt.gameObject.SetActive(false);
                buttons[i] = button;
            }
            WireArray(view, "_powerUps", buttons);
            return view;
        }

        static TitleView BuildTitle(RectTransform parent)
        {
            RectTransform root = Stretch(NewRect("Title", parent));
            Animate(root);
            var view = root.gameObject.AddComponent<TitleView>();

            // The logo echoes itself: cyan copies trailing behind the white word.
            for (int e = 2; e >= 1; e--)
            {
                var echo = MakeText(root, $"LogoEcho{e}", "ECHO", 190, _black, new Vector2(0.5f, 0.5f), new Vector2(-24 * e, 560 - 24 * e), new Vector2(1000, 300));
                echo.color = new Color(GhostCyan.r, GhostCyan.g, GhostCyan.b, 0.7f - e * 0.25f);
            }
            var logo = MakeText(root, "Logo", "ECHO", 190, _black, new Vector2(0.5f, 0.5f), new Vector2(0, 560), new Vector2(1000, 300));

            var tagline = MakeText(root, "Tagline", "Your past is trying to kill you.", 44, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, 400), new Vector2(1000, 80));
            tagline.color = Soft;

            var bestPill = Pill(root, "BestPill", new Vector2(0, 296), new Vector2(380, 92));
            var best = MakeText(root, "Best", "BEST 0", 36, _black, new Vector2(0.5f, 0.5f), new Vector2(0, 296), new Vector2(380, 92));

            var tap = MakeText(root, "TapToRun", "TAP TO RUN", 60, _black, new Vector2(0.5f, 0.5f), new Vector2(0, -480), new Vector2(1000, 130));
            tap.gameObject.AddComponent<Pulse>();

            var skins = MakeButton(root, "Skins", "Skins", new Vector2(-390, -740), new Vector2(240, 112), ButtonStyle.Secondary);
            var stats = MakeButton(root, "Stats", "Stats", new Vector2(-130, -740), new Vector2(240, 112), ButtonStyle.Secondary);
            // Highlights reel (a user revision): the reel cupboard.
            var reels = MakeButton(root, "Reels", "Reels", new Vector2(130, -740), new Vector2(240, 112), ButtonStyle.Secondary);
            var settings = MakeButton(root, "Settings", "Settings", new Vector2(390, -740), new Vector2(240, 112), ButtonStyle.Secondary);
            Wire(view, "_reels", reels);
            // Exit (a user revision): top left, out of the way of the logo; shown on Android only.
            var exit = MakeButton(root, "Exit", "Exit", Vector2.zero, new Vector2(200, 96), ButtonStyle.Secondary);
            var exitRt = (RectTransform)exit.transform;
            exitRt.anchorMin = exitRt.anchorMax = new Vector2(0f, 1f);
            exitRt.pivot = new Vector2(0f, 1f);
            exitRt.anchoredPosition = new Vector2(36, -36);
            Wire(view, "_exit", exit);
            // Version 2.0: the Daily Haunt, above Tap to run, with its countdown.
            var daily = MakeButton(root, "Daily", "Daily Haunt", new Vector2(0, -350), new Vector2(720, 104), ButtonStyle.Secondary);
            daily.GetComponent<Image>().color = new Color(Accent.r, Accent.g, Accent.b, 0.85f);
            daily.GetComponentInChildren<TMP_Text>().color = Ink;
            Wire(view, "_daily", daily);
            // Weekly Twists banner (value-adds doc): this week's twist, tap to switch to Classic and back.
            var twist = MakeButton(root, "Twist", "THIS WEEK: TWIST", new Vector2(0, 165), new Vector2(820, 120), ButtonStyle.Secondary);
            // Power-ups (a user revision): earned with rewarded ads before a run.
            var powerUps = MakeButton(root, "PowerUps", "Power-ups", new Vector2(0, -610), new Vector2(520, 104), ButtonStyle.Secondary);
            powerUps.GetComponent<Image>().color = GhostCyan;
            powerUps.GetComponentInChildren<TMP_Text>().color = Ink;
            Wire(view, "_powerUps", powerUps);
            twist.GetComponentInChildren<TMP_Text>().fontSizeMax = 30;
            twist.gameObject.SetActive(false);

            Wire(view, "_root", root.gameObject);
            Wire(view, "_best", best);
            Wire(view, "_bestPill", bestPill.gameObject);
            Wire(view, "_skins", skins);
            Wire(view, "_stats", stats);
            Wire(view, "_settings", settings);
            Wire(view, "_twist", twist);
            return view;
        }

        static DeathScreenView BuildDeathScreen(RectTransform parent)
        {
            RectTransform root = Stretch(NewRect("DeathScreen", parent));
            Animate(root);
            var view = root.gameObject.AddComponent<DeathScreenView>();
            var scrim = root.gameObject.AddComponent<Image>();
            scrim.color = Scrim;
            scrim.raycastTarget = false;

            // Grave reveal layout (a user revision): the score up top, the headstone in the middle standing on the
            // zoomed-in ground, the offers and Tap to retry below it.
            Pill(root, "Card", new Vector2(0, 750), new Vector2(920, 300), Card);
            var label = MakeText(root, "ScoreLabel", "SCORE", 28, _black, new Vector2(0.5f, 0.5f), new Vector2(0, 855), new Vector2(600, 60));
            Wire(view, "_scoreLabel", label);
            label.color = Soft;
            var score = MakeText(root, "Score", "0", 120, _black, new Vector2(0.5f, 0.5f), new Vector2(0, 755), new Vector2(900, 180));
            var best = MakeText(root, "Best", "Best 0", 32, _black, new Vector2(0.5f, 0.5f), new Vector2(0, 650), new Vector2(900, 70));
            best.color = Accent;

            RectTransform stone = BuildHeadstone(root, out TMP_Text[] carved, out TMP_Text[] carvedLight);

            // Shown on a new best, when there is no grave and the frozen ghosts fill the middle instead.
            var banner = MakeText(root, "NemesisBanner", "", 40, _black, new Vector2(0.5f, 0.5f), new Vector2(0, 420), new Vector2(1040, 110));
            banner.color = NemesisRed;

            var killer = MakeText(root, "Killer", "", 40, _bold, new Vector2(0.5f, 0.5f), new Vector2(0, -205), new Vector2(880, 70));
            killer.color = GhostCyan;
            var message = MakeText(root, "Message", "", 34, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, -262), new Vector2(880, 60));
            message.color = Soft;

            var cont = MakeButton(root, "Continue", "Continue · ad", new Vector2(-125, -370), new Vector2(510, 120), ButtonStyle.Primary);
            var powerUps = MakeButton(root, "PowerUps", "Power-ups", new Vector2(265, -370), new Vector2(240, 120), ButtonStyle.Secondary);
            powerUps.GetComponent<Image>().color = GhostCyan;
            powerUps.GetComponentInChildren<TMP_Text>().color = Ink;
            var banish = MakeButton(root, "Banish", "Banish ghost · ad", new Vector2(0, -502), new Vector2(760, 112), ButtonStyle.Secondary);
            // Nemesis Duel (value-adds doc): shown in Banish's place, which never applies to the Nemesis.
            var duel = MakeButton(root, "Duel", "Duel", new Vector2(0, -502), new Vector2(760, 112), ButtonStyle.Primary);
            duel.GetComponent<Image>().color = NemesisRed;
            duel.GetComponentInChildren<TMP_Text>().color = Color.white;
            duel.gameObject.SetActive(false);
            var share = MakeButton(root, "Share", "Share", new Vector2(-375, -625), new Vector2(230, 100), ButtonStyle.Secondary);
            // Ghost Challenges (value-adds doc): send this run to a friend.
            var challenge = MakeButton(root, "Challenge", "Challenge", new Vector2(-125, -625), new Vector2(230, 100), ButtonStyle.Secondary);
            challenge.GetComponent<Image>().color = GhostCyan;
            challenge.GetComponentInChildren<TMP_Text>().color = Ink;
            // Highlights reel (a user revision): keep this run, suggested in gold on a new best.
            var saveReel = MakeButton(root, "SaveReel", "Save reel", new Vector2(125, -625), new Vector2(230, 100), ButtonStyle.Secondary);
            Wire(view, "_saveReel", saveReel);
            {
                var so = new UnityEditor.SerializedObject(view);
                so.FindProperty("_saveReelColor").colorValue = SecondaryButton;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var menu = MakeButton(root, "Menu", "Menu", new Vector2(375, -625), new Vector2(230, 100), ButtonStyle.Secondary);

            var retry = MakeText(root, "TapToRetry", "TAP TO RETRY", 50, _black, new Vector2(0.5f, 0.5f), new Vector2(0, -760), new Vector2(1000, 110));
            retry.gameObject.AddComponent<Pulse>();

            Wire(view, "_headstone", stone);
            WireArray(view, "_carved", carved);
            WireArray(view, "_carvedLight", carvedLight);
            Wire(view, "_root", root.gameObject);
            Wire(view, "_score", score);
            Wire(view, "_best", best);
            Wire(view, "_killer", killer);
            Wire(view, "_message", message);
            Wire(view, "_continue", cont);
            Wire(view, "_banish", banish);
            Wire(view, "_share", share);
            Wire(view, "_menu", menu);
            Wire(view, "_duel", duel);
            Wire(view, "_challenge", challenge);
            Wire(view, "_powerUps", powerUps);
            Wire(view, "_banner", banner);
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>
        /// Ghost Challenges: a modal card over everything (a challenge arriving, an old one, the sender's name), with an
        /// optional name box. It sits above the menus and the death screen.
        /// </summary>
        static ChallengeView BuildChallenge(RectTransform parent)
        {
            RectTransform root = Stretch(NewRect("Challenge", parent));
            Animate(root);
            var view = root.gameObject.AddComponent<ChallengeView>();
            var scrim = root.gameObject.AddComponent<Image>();
            scrim.color = new Color(0.02f, 0.05f, 0.07f, 0.82f);

            Pill(root, "Card", new Vector2(0, 60), new Vector2(940, 820), MenuTint);
            var title = MakeText(root, "Title", "GHOST CHALLENGE", 44, _black, new Vector2(0.5f, 0.5f), new Vector2(0, 380), new Vector2(860, 90));
            title.color = GhostCyan;
            var body = MakeText(root, "Body", "", 42, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(820, 300));

            GameObject inputGo = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            inputGo.name = "Name";
            var inputRt = (RectTransform)inputGo.transform;
            inputRt.SetParent(root, false);
            inputRt.anchorMin = inputRt.anchorMax = new Vector2(0.5f, 0.5f);
            inputRt.anchoredPosition = new Vector2(0, -10);
            inputRt.sizeDelta = new Vector2(700, 110);
            var inputBg = inputGo.GetComponent<Image>();
            inputBg.sprite = Frame;
            inputBg.type = Image.Type.Sliced;
            inputBg.pixelsPerUnitMultiplier = FramePixelScale;
            inputBg.color = SecondaryButton;
            var input = inputGo.GetComponent<TMP_InputField>();
            input.characterLimit = Echo.Core.Challenges.Challenge.MaxNameLength;
            input.lineType = TMP_InputField.LineType.SingleLine;
            foreach (TMP_Text t in inputGo.GetComponentsInChildren<TMP_Text>(true))
            {
                t.font = _black;
                t.fontSize = 40;
                t.alignment = TextAlignmentOptions.Center;
                t.color = t == input.placeholder ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            }
            ((TMP_Text)input.placeholder).text = "YOUR NAME";

            var primary = MakeButton(root, "Primary", "OK", new Vector2(0, -170), new Vector2(700, 120), ButtonStyle.Primary);
            var secondary = MakeButton(root, "Secondary", "Not now", new Vector2(0, -305), new Vector2(700, 104), ButtonStyle.Secondary);

            Wire(view, "_root", root.gameObject);
            Wire(view, "_title", title);
            Wire(view, "_body", body);
            Wire(view, "_input", input);
            Wire(view, "_primary", primary);
            Wire(view, "_secondary", secondary);
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>The reel cupboard (a user revision): five slots, each with Watch, Clip and Delete, or Replace.</summary>
        static ReelsView BuildReels(RectTransform parent)
        {
            RectTransform root = MenuPanel(parent, "Reels", "Reel cupboard");
            var view = root.gameObject.AddComponent<ReelsView>();
            var hint = MakeText(root, "Hint", "", 36, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, 760), new Vector2(980, 60));
            hint.color = Soft;
            const int slots = 5;
            var slotGos = new GameObject[slots];
            var previews = new Image[slots];
            var titles = new TMP_Text[slots];
            var details = new TMP_Text[slots];
            var watch = new Button[slots];
            var clip = new Button[slots];
            var delete = new Button[slots];
            var challenge = new Button[slots];
            var replace = new Button[slots];
            for (int i = 0; i < slots; i++)
            {
                float y = 560 - i * 272;
                RectTransform slot = NewRect("Slot" + i, root);
                slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0.5f);
                slot.anchoredPosition = new Vector2(0, y);
                slot.sizeDelta = new Vector2(990, 252);
                slotGos[i] = slot.gameObject;
                Pill(slot, "Card", Vector2.zero, new Vector2(990, 252), Card);

                RectTransform preview = NewRect("Preview", slot);
                preview.anchorMin = preview.anchorMax = new Vector2(0.5f, 0.5f);
                preview.anchoredPosition = new Vector2(-405, 0);
                preview.sizeDelta = new Vector2(24 * 5, 34 * 5);
                previews[i] = preview.gameObject.AddComponent<Image>();
                previews[i].preserveAspect = true;
                previews[i].raycastTarget = false;

                titles[i] = MakeText(slot, "Title", "Empty slot", 40, _black, new Vector2(0.5f, 0.5f), new Vector2(70, 72), new Vector2(780, 64));
                titles[i].alignment = TextAlignmentOptions.MidlineLeft;
                details[i] = MakeText(slot, "Detail", "", 32, _medium, new Vector2(0.5f, 0.5f), new Vector2(70, 18), new Vector2(780, 48));
                details[i].alignment = TextAlignmentOptions.MidlineLeft;
                details[i].color = Soft;

                watch[i] = MakeButton(slot, "Watch", "Watch", new Vector2(-260, -62), new Vector2(180, 92), ButtonStyle.Primary);
                clip[i] = MakeButton(slot, "Clip", "Clip", new Vector2(-70, -62), new Vector2(180, 92), ButtonStyle.Secondary);
                clip[i].GetComponent<Image>().color = GhostCyan;
                clip[i].GetComponentInChildren<TMP_Text>().color = Ink;
                // Balance addendum: send this reel, ghosts and all, as a friend challenge.
                challenge[i] = MakeButton(slot, "Challenge", "Challenge", new Vector2(120, -62), new Vector2(180, 92), ButtonStyle.Secondary);
                delete[i] = MakeButton(slot, "Delete", "Delete", new Vector2(310, -62), new Vector2(180, 92), ButtonStyle.Secondary);
                replace[i] = MakeButton(slot, "Replace", "Replace", new Vector2(95, -62), new Vector2(720, 92), ButtonStyle.Primary);
                replace[i].GetComponent<Image>().color = NemesisRed;
                replace[i].GetComponentInChildren<TMP_Text>().color = Color.white;
                replace[i].gameObject.SetActive(false);
            }
            var close = MakeButton(root, "Close", "Back", new Vector2(0, -830), new Vector2(500, 120), ButtonStyle.Primary);
            Wire(view, "_root", root.gameObject);
            Wire(view, "_heading", root.Find("Heading").GetComponent<TMP_Text>());
            Wire(view, "_hint", hint);
            WireArray(view, "_slots", slotGos);
            WireArray(view, "_previews", previews);
            WireArray(view, "_titles", titles);
            WireArray(view, "_details", details);
            WireArray(view, "_watch", watch);
            WireArray(view, "_clip", clip);
            WireArray(view, "_delete", delete);
            WireArray(view, "_challenge", challenge);
            WireArray(view, "_replace", replace);
            Wire(view, "_close", close);
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>
        /// The clip editor (a user revision): the 9:16 preview of what will be recorded, the timeline with its two
        /// handles and playhead, GIF or Video, Preview, Make and Back; REC and Cancel while recording.
        /// </summary>
        static ClipEditorView BuildClipEditor(RectTransform parent)
        {
            RectTransform root = Stretch(NewRect("ClipEditor", parent));
            var view = root.gameObject.AddComponent<ClipEditorView>();
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.08f, 0.1f, 1f);

            Pill(root, "PreviewFrame", new Vector2(0, 360), new Vector2(572, 992), Card);
            RectTransform previewRt = NewRect("Preview", root);
            previewRt.anchorMin = previewRt.anchorMax = new Vector2(0.5f, 0.5f);
            previewRt.anchoredPosition = new Vector2(0, 360);
            previewRt.sizeDelta = new Vector2(540, 960);
            var preview = previewRt.gameObject.AddComponent<RawImage>();
            preview.raycastTarget = false;

            RectTransform rec = NewRect("Recording", root);
            rec.anchorMin = rec.anchorMax = new Vector2(0.5f, 0.5f);
            rec.anchoredPosition = new Vector2(-190, 800);
            rec.sizeDelta = new Vector2(140, 50);
            var recText = MakeText(rec, "Rec", "REC", 30, _black, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 50));
            recText.color = NemesisRed;
            rec.gameObject.AddComponent<Pulse>();
            rec.gameObject.SetActive(false);

            var length = MakeText(root, "Length", "5.0 s", 34, _black, new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(600, 56));
            length.color = GhostCyan;

            RectTransform track = NewRect("Trim", root);
            track.anchorMin = track.anchorMax = new Vector2(0.5f, 0.5f);
            track.anchoredPosition = new Vector2(0, -250);
            track.sizeDelta = new Vector2(940, 84);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = SecondaryButton;
            var trim = track.gameObject.AddComponent<TrimBar>();

            RectTransform selection = NewRect("Selection", track);
            var selectionImage = selection.gameObject.AddComponent<Image>();
            selectionImage.color = new Color(GhostCyan.r, GhostCyan.g, GhostCyan.b, 0.35f);
            selectionImage.raycastTarget = false;
            RectTransform Marker(string name, float width, float height, Color color)
            {
                RectTransform m = NewRect(name, track);
                m.anchorMin = m.anchorMax = new Vector2(0f, 0.5f);
                m.sizeDelta = new Vector2(width, height);
                var image = m.gameObject.AddComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
                return m;
            }
            RectTransform startHandle = Marker("StartHandle", 22, 116, Color.white);
            RectTransform endHandle = Marker("EndHandle", 22, 116, Color.white);
            RectTransform playhead = Marker("Playhead", 6, 100, Accent);

            var start = MakeText(root, "Start", "0:00.0", 30, _medium, new Vector2(0.5f, 0.5f), new Vector2(-330, -330), new Vector2(280, 50));
            start.alignment = TextAlignmentOptions.MidlineLeft;
            start.color = Soft;
            var end = MakeText(root, "End", "0:05.0", 30, _medium, new Vector2(0.5f, 0.5f), new Vector2(330, -330), new Vector2(280, 50));
            end.alignment = TextAlignmentOptions.MidlineRight;
            end.color = Soft;

            var gif = MakeButton(root, "Gif", "GIF", new Vector2(-235, -440), new Vector2(440, 108), ButtonStyle.Secondary);
            var video = MakeButton(root, "Video", "Video", new Vector2(235, -440), new Vector2(440, 108), ButtonStyle.Secondary);
            var play = MakeButton(root, "Play", "Preview", new Vector2(-235, -575), new Vector2(440, 116), ButtonStyle.Secondary);
            var make = MakeButton(root, "Make", "Make GIF", new Vector2(235, -575), new Vector2(440, 116), ButtonStyle.Primary);
            var cancel = MakeButton(root, "Cancel", "Cancel", new Vector2(0, -575), new Vector2(500, 116), ButtonStyle.Secondary);
            cancel.gameObject.SetActive(false);
            var back = MakeButton(root, "Back", "Back", new Vector2(0, -710), new Vector2(500, 112), ButtonStyle.Secondary);
            var status = MakeText(root, "Status", "", 36, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, -820), new Vector2(1000, 64));
            status.color = Soft;

            Wire(trim, "_track", track);
            Wire(trim, "_selection", selection);
            Wire(trim, "_startHandle", startHandle);
            Wire(trim, "_endHandle", endHandle);
            Wire(trim, "_playhead", playhead);
            Wire(view, "_root", root.gameObject);
            Wire(view, "_preview", preview);
            Wire(view, "_trim", trim);
            Wire(view, "_start", start);
            Wire(view, "_end", end);
            Wire(view, "_length", length);
            Wire(view, "_status", status);
            Wire(view, "_recording", rec.gameObject);
            Wire(view, "_gif", gif);
            Wire(view, "_video", video);
            Wire(view, "_play", play);
            Wire(view, "_make", make);
            Wire(view, "_back", back);
            Wire(view, "_cancel", cancel);
            {
                var so = new UnityEditor.SerializedObject(view);
                so.FindProperty("_selected").colorValue = new Color(GhostCyan.r, GhostCyan.g, GhostCyan.b, 0.6f);
                so.FindProperty("_unselected").colorValue = SecondaryButton;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>
        /// The Daily Haunt lobby (version 2.0): today's date and countdown, your best and rank, the top 10, yesterday's
        /// winner, Play and Back.
        /// </summary>
        static DailyLobbyView BuildDailyLobby(RectTransform parent)
        {
            RectTransform root = MenuPanel(parent, "DailyLobby", "Daily Haunt");
            var view = root.gameObject.AddComponent<DailyLobbyView>();
            var countdown = MakeText(root, "Countdown", "", 34, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, 750), new Vector2(980, 56));
            countdown.color = GhostCyan;
            var you = MakeText(root, "You", "", 40, _black, new Vector2(0.5f, 0.5f), new Vector2(0, 650), new Vector2(980, 70));
            you.color = Accent;

            Pill(root, "BoardCard", new Vector2(0, 160), new Vector2(980, 820), Card);
            var rows = new TMP_Text[10];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = MakeText(root, "Row" + i, "", 38, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, 520 - i * 78), new Vector2(900, 70));
                rows[i].alignment = TextAlignmentOptions.MidlineLeft;
            }
            var yesterday = MakeText(root, "Yesterday", "", 32, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, -320), new Vector2(980, 56));
            yesterday.color = Soft;
            var status = MakeText(root, "Status", "", 36, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, -420), new Vector2(980, 110));
            status.color = Soft;
            var play = MakeButton(root, "Play", "Play today's course", new Vector2(0, -600), new Vector2(760, 132), ButtonStyle.Primary);
            var back = MakeButton(root, "Back", "Back", new Vector2(0, -830), new Vector2(500, 120), ButtonStyle.Secondary);

            Wire(view, "_root", root.gameObject);
            Wire(view, "_countdown", countdown);
            Wire(view, "_you", you);
            WireArray(view, "_rows", rows);
            Wire(view, "_yesterday", yesterday);
            Wire(view, "_status", status);
            Wire(view, "_play", play);
            Wire(view, "_back", back);
            {
                var so = new UnityEditor.SerializedObject(view);
                so.FindProperty("_youColor").colorValue = Accent;
                so.FindProperty("_rowColor").colorValue = Color.white;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>The bar under a reel playing back: Exit and speed, then Watch again, Make clip and Back.</summary>
        static ReplayView BuildReplay(RectTransform parent)
        {
            RectTransform root = Stretch(NewRect("Replay", parent));
            var view = root.gameObject.AddComponent<ReplayView>();
            var label = MakeText(root, "Label", "REPLAY", 40, _black, new Vector2(0.5f, 0.5f), new Vector2(0, -470), new Vector2(1000, 70));
            label.color = GhostCyan;
            var status = MakeText(root, "Status", "", 38, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, -545), new Vector2(1000, 60));
            status.color = Soft;
            var exit = MakeButton(root, "Exit", "Exit", new Vector2(-250, -800), new Vector2(400, 116), ButtonStyle.Secondary);
            var speed = MakeButton(root, "Speed", "Speed 1x", new Vector2(250, -800), new Vector2(400, 116), ButtonStyle.Secondary);
            var again = MakeButton(root, "Again", "Watch again", new Vector2(0, -620), new Vector2(620, 116), ButtonStyle.Primary);
            var clip = MakeButton(root, "Clip", "Make clip", new Vector2(0, -750), new Vector2(620, 116), ButtonStyle.Secondary);
            clip.GetComponent<Image>().color = GhostCyan;
            clip.GetComponentInChildren<TMP_Text>().color = Ink;
            var back = MakeButton(root, "Back", "Back", new Vector2(0, -880), new Vector2(620, 116), ButtonStyle.Secondary);
            Wire(view, "_root", root.gameObject);
            Wire(view, "_label", label);
            Wire(view, "_status", status);
            Wire(view, "_exit", exit);
            Wire(view, "_speed", speed);
            Wire(view, "_again", again);
            Wire(view, "_clip", clip);
            Wire(view, "_back", back);
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>The 30th achievement's reward moment (a user revision): darkness, four silhouettes, ELEMENTS AWAKENED.</summary>
        static ElementsAwakenedView BuildElementsAwakened(RectTransform parent)
        {
            RectTransform root = Stretch(NewRect("ElementsAwakened", parent));
            var view = root.gameObject.AddComponent<ElementsAwakenedView>();
            var scrim = root.gameObject.AddComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0f);
            var tap = root.gameObject.AddComponent<Button>();
            tap.targetGraphic = scrim;
            tap.transition = Selectable.Transition.None;

            Sprite glow = LoadSprite(EchoArt.GlowPath);
            var figures = new Image[4];
            var glows = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * 235f;
                RectTransform g = NewRect("Glow" + i, root);
                g.anchorMin = g.anchorMax = new Vector2(0.5f, 0.5f);
                g.anchoredPosition = new Vector2(x, 150);
                g.sizeDelta = new Vector2(300, 300);
                glows[i] = g.gameObject.AddComponent<Image>();
                glows[i].sprite = glow;
                glows[i].raycastTarget = false;
                glows[i].color = new Color(1f, 1f, 1f, 0f);

                RectTransform holder = NewRect("Figure" + i, root);
                holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
                holder.anchoredPosition = new Vector2(x, 150);
                holder.sizeDelta = new Vector2(24 * 8, 34 * 8);
                RectTransform f = Stretch(NewRect("Sprite", holder));
                figures[i] = f.gameObject.AddComponent<Image>();
                figures[i].preserveAspect = true;
                figures[i].raycastTarget = false;
                figures[i].color = new Color(1f, 1f, 1f, 0f);
            }
            var title = MakeText(root, "Title", "ELEMENTS AWAKENED", 54, _black, new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(1000, 90));
            title.color = new Color(Accent.r, Accent.g, Accent.b, 0f);
            var subtitle = MakeText(root, "Subtitle", "Ember • Frost • Storm • Tide", 46, _medium,
                new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(1000, 70));
            subtitle.color = new Color(1f, 1f, 1f, 0f);

            Wire(view, "_root", root.gameObject);
            Wire(view, "_scrim", scrim);
            WireArray(view, "_figures", figures);
            WireArray(view, "_glows", glows);
            Wire(view, "_title", title);
            Wire(view, "_subtitle", subtitle);
            Wire(view, "_tap", tap);
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>Power-ups (a user revision): the card of four, each with its count and a rewarded-ad button.</summary>
        static PowerUpsView BuildPowerUps(RectTransform parent)
        {
            RectTransform root = Stretch(NewRect("PowerUps", parent));
            Animate(root);
            var view = root.gameObject.AddComponent<PowerUpsView>();
            var scrim = root.gameObject.AddComponent<Image>();
            scrim.color = new Color(0.02f, 0.05f, 0.07f, 0.85f);

            Pill(root, "Card", new Vector2(0, 0), new Vector2(980, 1240), MenuTint);
            var title = MakeText(root, "Title", "POWER-UPS", 52, _black, new Vector2(0.5f, 0.5f), new Vector2(0, 540), new Vector2(900, 90));
            title.color = GhostCyan;
            var info = MakeText(root, "Info", "Watch an ad for one. Hold 2 of each, use 1 of each per run: tap its icon on the floor.",
                30, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, 450), new Vector2(880, 90));
            info.color = Soft;

            string[] icons = { EchoArt.ShieldIconPath, EchoArt.PhaseIconPath, EchoArt.LanternIconPath, EchoArt.HeartIconPath };
            var rows = new TMP_Text[icons.Length];
            var watch = new Button[icons.Length];
            for (int i = 0; i < icons.Length; i++)
            {
                float y = 290 - i * 200;
                Pill(root, "Row" + i, new Vector2(0, y), new Vector2(920, 180), Card);
                RectTransform iconRt = NewRect("Icon" + i, root);
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = new Vector2(-380, y);
                iconRt.sizeDelta = new Vector2(96, 96);
                var image = iconRt.gameObject.AddComponent<Image>();
                image.sprite = LoadSprite(icons[i]);
                image.color = i == 3 ? NemesisRed : Color.white;
                image.raycastTarget = false;
                rows[i] = MakeText(root, "Text" + i, "", 36, _bold, new Vector2(0.5f, 0.5f), new Vector2(-60, y), new Vector2(520, 160));
                rows[i].alignment = TextAlignmentOptions.MidlineLeft;
                watch[i] = MakeButton(root, "Watch" + i, "Watch ad", new Vector2(320, y), new Vector2(250, 110), ButtonStyle.Primary);
            }
            var close = MakeButton(root, "Close", "Done", new Vector2(0, -520), new Vector2(500, 110), ButtonStyle.Secondary);

            Wire(view, "_root", root.gameObject);
            WireArray(view, "_rows", rows);
            WireArray(view, "_watch", watch);
            Wire(view, "_close", close);
            root.gameObject.SetActive(false);
            return view;
        }

        static RectTransform MenuPanel(RectTransform parent, string name, string heading)
        {
            RectTransform root = Stretch(NewRect(name, parent));
            Animate(root);
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = MenuTint;
            var title = MakeText(root, "Heading", heading.ToUpperInvariant(), 64, _black, new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(1000, 130));
            return root;
        }

        static StatsView BuildStats(RectTransform parent)
        {
            RectTransform root = MenuPanel(parent, "Stats", "Stats");
            var view = root.gameObject.AddComponent<StatsView>();
            Pill(root, "SummaryCard", new Vector2(0, 545), new Vector2(940, 360), Card);
            var summary = MakeText(root, "Summary", "", 40, _bold, new Vector2(0.5f, 1f), new Vector2(0, -255), new Vector2(840, 340));
            summary.alignment = TextAlignmentOptions.TopLeft;
            summary.lineSpacing = 8;
            var header = MakeText(root, "AchievementsHeader", "ACHIEVEMENTS", 30, _black, new Vector2(0.5f, 1f), new Vector2(0, -660), new Vector2(980, 60));
            header.alignment = TextAlignmentOptions.Left;

            // Skins addendum: 30 achievements in a scrolling list, one row with a progress bar each.
            RectTransform scrollRt = NewRect("Scroll", root);
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(40, 340);
            scrollRt.offsetMax = new Vector2(-40, -730);
            scrollRt.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            RectTransform viewport = Stretch(NewRect("Viewport", scrollRt));
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var column = content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 12;
            column.padding = new RectOffset(0, 0, 0, 20);
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            RectTransform row = NewRect("RowTemplate", root);
            row.sizeDelta = new Vector2(1000, 132);
            var rowImage = row.gameObject.AddComponent<Image>();
            rowImage.sprite = Frame;
            rowImage.type = Image.Type.Sliced;
            rowImage.pixelsPerUnitMultiplier = FramePixelScale;
            rowImage.color = Card;
            rowImage.raycastTarget = false;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 132;
            var title = MakeText(row, "Title", "Title", 26, _black, new Vector2(0f, 1f), new Vector2(26, -14), new Vector2(720, 40));
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.alignment = TextAlignmentOptions.Left;
            var description = MakeText(row, "Description", "Description", 30, _medium, new Vector2(0f, 1f), new Vector2(26, -58), new Vector2(760, 40));
            description.rectTransform.pivot = new Vector2(0f, 1f);
            description.alignment = TextAlignmentOptions.Left;
            description.color = Soft;
            var count = MakeText(row, "Count", "0/1", 22, _black, new Vector2(1f, 1f), new Vector2(-24, -16), new Vector2(200, 36));
            count.rectTransform.pivot = new Vector2(1f, 1f);
            count.alignment = TextAlignmentOptions.Right;
            RectTransform bar = NewRect("Bar", row);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.offsetMin = new Vector2(26, 16);
            bar.offsetMax = new Vector2(-26, 30);
            var barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = new Color(0f, 0f, 0f, 0.45f);
            barImage.raycastTarget = false;
            RectTransform fill = NewRect("Fill", bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.5f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.raycastTarget = false;
            row.gameObject.SetActive(false);

            var close = MakeButton(root, "Close", "Back", new Vector2(0, -850), new Vector2(500, 110), ButtonStyle.Secondary);
            // Ghost Challenges: send the best run, or open a challenge code from the clipboard.
            var challengeBest = MakeButton(root, "Challenge", "Challenge a friend", new Vector2(-245, -700), new Vector2(470, 100), ButtonStyle.Primary);
            var paste = MakeButton(root, "Paste", "Paste challenge", new Vector2(245, -700), new Vector2(470, 100), ButtonStyle.Secondary);
            Wire(view, "_challenge", challengeBest);
            Wire(view, "_paste", paste);
            Wire(view, "_root", root.gameObject);
            Wire(view, "_summary", summary);
            Wire(view, "_header", header);
            Wire(view, "_scroll", scroll);
            Wire(view, "_content", content);
            Wire(view, "_rowTemplate", row);
            Wire(view, "_close", close);
            root.gameObject.SetActive(false);
            return view;
        }

        static SettingsView BuildSettings(RectTransform parent)
        {
            RectTransform root = MenuPanel(parent, "Settings", "Settings");
            var view = root.gameObject.AddComponent<SettingsView>();
            var music = MakeButton(root, "Music", "Music: On", new Vector2(0, 480), new Vector2(760, 124), ButtonStyle.Secondary);
            var sfx = MakeButton(root, "Sfx", "Sound effects: On", new Vector2(0, 330), new Vector2(760, 124), ButtonStyle.Secondary);
            var haptics = MakeButton(root, "Haptics", "Haptics: On", new Vector2(0, 180), new Vector2(760, 124), ButtonStyle.Secondary);
            var glow = MakeButton(root, "Glow", "Glow: On", new Vector2(0, 30), new Vector2(760, 124), ButtonStyle.Secondary);
            foreach (var toggle in new[] { music, sfx, haptics, glow })
            {
                var label = toggle.GetComponentInChildren<TMP_Text>();
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.margin = new Vector4(36, 0, 36, 0);
            }
            // The name friends see on Ghost Challenges.
            var name = MakeButton(root, "Name", "Name", new Vector2(0, -120), new Vector2(760, 124), ButtonStyle.Secondary);
            var nameLabel = name.GetComponentInChildren<TMP_Text>();
            nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
            nameLabel.margin = new Vector4(36, 0, 36, 0);
            var privacy = MakeButton(root, "Privacy", "Privacy choices", new Vector2(0, -270), new Vector2(760, 124), ButtonStyle.Secondary);
            // Privacy policy (2.0): the Daily Haunt runs, board entry and challenges kept on the server, deleted on request.
            var deleteOnline = MakeButton(root, "DeleteOnline", "Delete online data", new Vector2(0, -410), new Vector2(760, 110), ButtonStyle.Secondary);
            Wire(view, "_deleteOnline", deleteOnline);
            var devAds = MakeButton(root, "DevAdFailure", "Dev: ads succeed", new Vector2(-265, -560), new Vector2(250, 100), ButtonStyle.Secondary);
            // Version 2.0, development builds: the newest reel replayed on the server, and the Daily Haunt's date.
            var serverCheck = MakeButton(root, "ServerCheck", "Dev: server check", new Vector2(0, -560), new Vector2(250, 100), ButtonStyle.Secondary);
            var dailyDate = MakeButton(root, "DailyDate", "Dev: daily today", new Vector2(265, -560), new Vector2(250, 100), ButtonStyle.Secondary);
            Wire(view, "_serverCheck", serverCheck);
            Wire(view, "_dailyDate", dailyDate);
            var close = MakeButton(root, "Close", "Back", new Vector2(0, -830), new Vector2(500, 120), ButtonStyle.Primary);
            Wire(view, "_root", root.gameObject);
            Wire(view, "_music", music);
            Wire(view, "_sfx", sfx);
            Wire(view, "_haptics", haptics);
            Wire(view, "_glow", glow);
            Wire(view, "_privacy", privacy);
            Wire(view, "_name", name);
            // Development builds: ads and store status, readable on a test phone.
            // Five taps on the heading show it in release builds too.
            var heading = root.Find("Heading").gameObject;
            heading.GetComponent<TMP_Text>().raycastTarget = true;
            Wire(view, "_heading", heading.AddComponent<Button>());
            var status = MakeText(root, "ServicesStatus", "", 24, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, -690), new Vector2(980, 150));
            status.alignment = TextAlignmentOptions.TopLeft;
            status.color = Soft;
            status.gameObject.SetActive(false);
            Wire(view, "_status", status);
            Wire(view, "_devAdFailure", devAds);
            Wire(view, "_close", close);
            root.gameObject.SetActive(false);
            return view;
        }

        static SkinsView BuildSkins(RectTransform parent)
        {
            RectTransform root = MenuPanel(parent, "Skins", "Skins");
            var view = root.gameObject.AddComponent<SkinsView>();

            // One scrolling list between the heading and the footer; SkinsView fills it from templates.
            RectTransform scrollRt = NewRect("Scroll", root);
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(40, 200);
            scrollRt.offsetMax = new Vector2(-40, -200);
            var dragArea = scrollRt.gameObject.AddComponent<Image>();
            dragArea.color = new Color(0f, 0f, 0f, 0f);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            RectTransform viewport = Stretch(NewRect("Viewport", scrollRt));
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var column = content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 18;
            column.padding = new RectOffset(0, 0, 0, 40);
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            // Templates, cloned into the list.
            var header = MakeText(root, "HeaderTemplate", "SECTION", 36, _black, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 90));
            header.alignment = TextAlignmentOptions.BottomLeft;
            header.color = Accent;
            header.gameObject.AddComponent<LayoutElement>().minHeight = 90;

            RectTransform characterGrid = Grid(root, "CharacterGridTemplate", new Vector2(310, 400), 3);
            RectTransform itemGrid = Grid(root, "ItemGridTemplate", new Vector2(490, 120), 2);

            var character = MakeButton(root, "CharacterTemplate", "Name", Vector2.zero, new Vector2(310, 400), ButtonStyle.Secondary);
            RectTransform preview = NewRect("Preview", (RectTransform)character.transform);
            preview.anchorMin = preview.anchorMax = new Vector2(0.5f, 1f);
            preview.pivot = new Vector2(0.5f, 1f);
            preview.anchoredPosition = new Vector2(0, -26);
            preview.sizeDelta = new Vector2(PixelRig.W * 6, PixelRig.H * 6);
            var previewImage = preview.gameObject.AddComponent<Image>();
            previewImage.preserveAspect = true;
            previewImage.raycastTarget = false;
            var characterLabel = character.GetComponentInChildren<TMP_Text>().rectTransform;
            characterLabel.anchorMin = characterLabel.anchorMax = new Vector2(0.5f, 0f);
            characterLabel.pivot = new Vector2(0.5f, 0f);
            characterLabel.anchoredPosition = new Vector2(0, 14);
            characterLabel.sizeDelta = new Vector2(286, 146); // name, then up to two lines of how to unlock
            ((TMP_Text)characterLabel.GetComponent<TMP_Text>()).fontSizeMax = 26;

            var item = MakeButton(root, "ItemTemplate", "Item", Vector2.zero, new Vector2(490, 120), ButtonStyle.Secondary);
            item.GetComponentInChildren<TMP_Text>().fontSizeMax = 26;

            var product = MakeButton(root, "ProductTemplate", "Pack", Vector2.zero, new Vector2(1000, 116), ButtonStyle.Primary);
            product.gameObject.AddComponent<LayoutElement>().preferredHeight = 116;
            product.GetComponentInChildren<TMP_Text>().fontSizeMax = 30;

            var restore = MakeButton(root, "Restore", "Restore", new Vector2(-260, -850), new Vector2(460, 110), ButtonStyle.Secondary);
            var close = MakeButton(root, "Close", "Back", new Vector2(260, -850), new Vector2(460, 110), ButtonStyle.Secondary);

            foreach (Component template in new Component[] { header, characterGrid, itemGrid, character, item, product })
                template.gameObject.SetActive(false);

            Wire(view, "_root", root.gameObject);
            Wire(view, "_scroll", scroll);
            Wire(view, "_content", content);
            Wire(view, "_headerTemplate", header);
            Wire(view, "_characterGridTemplate", characterGrid);
            Wire(view, "_itemGridTemplate", itemGrid);
            Wire(view, "_characterTemplate", character);
            Wire(view, "_itemTemplate", item);
            Wire(view, "_productTemplate", product);
            Wire(view, "_close", close);
            Wire(view, "_restore", restore);
            root.gameObject.SetActive(false);
            return view;
        }

        static RectTransform Grid(RectTransform parent, string name, Vector2 cell, int columns)
        {
            RectTransform grid = NewRect(name, parent);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = cell;
            layout.spacing = new Vector2(20, 18);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            layout.childAlignment = TextAnchor.UpperCenter;
            return grid;
        }

        static ConsentView BuildConsent(RectTransform parent)
        {
            RectTransform root = MenuPanel(parent, "Consent", "Your privacy");
            var view = root.gameObject.AddComponent<ConsentView>();
            var body = MakeText(root, "Body",
                "Echo is free. Rewarded ads only ever play when you choose to watch one, never during a run.\n\n" +
                "With your permission, ad partners may personalise those ads, and we collect anonymous gameplay " +
                "analytics to balance the game.\n\nYou can change this any time in Settings.",
                44, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(920, 760));
            body.alignment = TextAlignmentOptions.Top;
            body.color = Soft;
            var accept = MakeButton(root, "AcceptAll", "Accept all", new Vector2(0, -330), new Vector2(760, 140), ButtonStyle.Primary);
            var essential = MakeButton(root, "EssentialOnly", "Essential only", new Vector2(0, -500), new Vector2(760, 140), ButtonStyle.Secondary);
            Wire(view, "_root", root.gameObject);
            Wire(view, "_acceptAll", accept);
            Wire(view, "_essentialOnly", essential);
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>
        /// The death screen's headstone: the pixel-art stone, pivot at its foot, with four engraved lines placed as
        /// fractions of the stone (its size is set at runtime to match the zoomed world). Each line is dark grooves
        /// over a lighter copy a few units lower: the lit lower edge of a cut.
        /// </summary>
        static RectTransform BuildHeadstone(RectTransform parent, out TMP_Text[] carved, out TMP_Text[] carvedLight)
        {
            RectTransform stone = NewRect("Headstone", parent);
            // Anchored across a share of the screen at runtime (DeathScreenView); the fitter keeps the art's shape.
            stone.anchorMin = new Vector2(0.21f, 0.42f);
            stone.anchorMax = new Vector2(0.79f, 0.42f);
            stone.pivot = new Vector2(0.5f, 0f);
            stone.sizeDelta = Vector2.zero;
            var fitter = stone.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            fitter.aspectRatio = 56f / 60f;
            var image = stone.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite(EchoArt.HeadstonePath);
            image.raycastTarget = false;

            // Rows of the 60-px-tall art: the face between the slab (row 12) and the cross (row 44).
            (string name, float y, float h, float size)[] lines =
            {
                ("HereLies", 40.5f, 3f, 24), ("Name", 35f, 5f, 46), ("Line", 24f, 9f, 28), ("Footer", 15.5f, 3f, 24),
            };
            carved = new TMP_Text[lines.Length];
            carvedLight = new TMP_Text[lines.Length];
            var groove = new Color(0.24f, 0.29f, 0.33f);
            var lit = new Color(0.93f, 0.95f, 0.97f, 0.75f);
            for (int i = 0; i < lines.Length; i++)
            {
                var (name, y, h, size) = lines[i];
                carvedLight[i] = EngravedLine(stone, name + "Lit", y, h, size, lit, new Vector2(0, -4));
                carved[i] = EngravedLine(stone, name, y, h, size, groove, Vector2.zero);
            }
            stone.gameObject.SetActive(false);
            return stone;
        }

        static TMP_Text EngravedLine(RectTransform stone, string name, float row, float rows, float size, Color color, Vector2 offset)
        {
            const float artW = 56f, artH = 60f, faceLeft = 11f, faceRight = 45f;
            TextMeshProUGUI text = MakeText(stone, name, "", size, _black, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform rt = text.rectTransform;
            rt.anchorMin = new Vector2(faceLeft / artW, (row - rows / 2f) / artH);
            rt.anchorMax = new Vector2(faceRight / artW, (row + rows / 2f) / artH);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offset;
            rt.offsetMax = offset;
            text.color = color;
            return text;
        }

        // ---- building blocks ----

        static void Animate(RectTransform root)
        {
            root.gameObject.AddComponent<CanvasGroup>();
            root.gameObject.AddComponent<PanelAnimator>();
        }

        static Sprite Frame => _frameSprite != null ? _frameSprite : _frameSprite = LoadSprite(EchoArt.UiFramePath);

        static Image Pill(RectTransform parent, string name, Vector2 position, Vector2 size, Color? color = null)
        {
            RectTransform rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = Frame;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = FramePixelScale;
            image.color = color ?? SecondaryButton;
            image.raycastTarget = false;
            return image;
        }

        static TextMeshProUGUI MakeText(RectTransform parent, string name, string text, float size, TMP_FontAsset font,
                                        Vector2 anchor, Vector2 position, Vector2 box)
        {
            RectTransform rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, anchor.y >= 1f ? 1f : 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = box;
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            // The arcade face is wide; let long strings shrink rather than overflow their box.
            if (font == _black)
            {
                tmp.enableAutoSizing = true;
                tmp.fontSizeMax = size;
                tmp.fontSizeMin = Mathf.Max(14f, size * 0.4f);
            }
            return tmp;
        }

        static Button MakeButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size, ButtonStyle style)
        {
            RectTransform rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = Frame;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = FramePixelScale;
            image.color = style == ButtonStyle.Primary ? Accent : SecondaryButton;
            var button = rt.gameObject.AddComponent<Button>();
            rt.gameObject.AddComponent<ButtonFeedback>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            button.colors = colors;
            var text = MakeText(rt, "Label", label, 34, _black, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(28, 10));
            text.color = style == ButtonStyle.Primary ? Ink : Color.white;
            return button;
        }
    }
}
