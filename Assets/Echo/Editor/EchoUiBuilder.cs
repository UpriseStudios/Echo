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
            // The main game's speed, once a new best has raised it (a user revision).
            var speed = MakeText(root, "Speed", "SPEED 100%", 26, _black, new Vector2(0.5f, 0.5f), new Vector2(0, 222), new Vector2(500, 44));
            speed.color = Soft;
            Wire(view, "_speed", speed);

            var tap = MakeText(root, "TapToRun", "TAP TO RUN", 60, _black, new Vector2(0.5f, 0.5f), new Vector2(0, -480), new Vector2(1000, 130));
            tap.gameObject.AddComponent<Pulse>();

            var skins = MakeButton(root, "Skins", "Skins", new Vector2(-390, -740), new Vector2(240, 112), ButtonStyle.Secondary);
            var stats = MakeButton(root, "Stats", "Stats", new Vector2(-130, -740), new Vector2(240, 112), ButtonStyle.Secondary);
            // Highlights reel (a user revision): the reel cupboard.
            var reels = MakeButton(root, "Reels", "Reels", new Vector2(130, -740), new Vector2(240, 112), ButtonStyle.Secondary);
            var settings = MakeButton(root, "Settings", "Settings", new Vector2(390, -740), new Vector2(240, 112), ButtonStyle.Secondary);
            Wire(view, "_reels", reels);
            LabelRow(root, "Menu", 32, skins, stats, reels, settings);
            foreach (var b in new[] { skins, stats, reels, settings }) b.GetComponent<Image>().color = Glass;
            // Exit (a user revision): top left, out of the way of the logo; shown on Android only.
            var exit = MakeButton(root, "Exit", "Exit", Vector2.zero, new Vector2(200, 96), ButtonStyle.Secondary);
            var exitRt = (RectTransform)exit.transform;
            exitRt.anchorMin = exitRt.anchorMax = new Vector2(0f, 1f);
            exitRt.pivot = new Vector2(0f, 1f);
            exitRt.anchoredPosition = new Vector2(36, -36);
            exit.GetComponent<Image>().color = Glass;
            Wire(view, "_exit", exit);
            // Development-server builds: a tag top right, so one is never mistaken for the store build.
            var devServer = MakeText(root, "DevServer", "DEV SERVER", 30, _black, new Vector2(1f, 1f), new Vector2(-190, -60), new Vector2(320, 60));
            devServer.color = NemesisRed;
            devServer.gameObject.SetActive(false);
            Wire(view, "_devServer", devServer.gameObject);
            // Version 2.0: the Daily Haunt, above Tap to run, with its countdown.
            var daily = MakeButton(root, "Daily", "Daily Haunt", new Vector2(-120, -196), new Vector2(620, 104), ButtonStyle.Secondary);
            daily.GetComponent<Image>().color = new Color(Accent.r, Accent.g, Accent.b, 0.85f);
            daily.GetComponentInChildren<TMP_Text>().color = Ink;
            Wire(view, "_daily", daily);
            // Modes doc: the Modes screen, beside the Daily Haunt.
            // Ghost Town (a user decision): the online hub; "Modes" in code.
            var modes = MakeButton(root, "Modes", "Ghost Town", new Vector2(330, -196), new Vector2(240, 104), ButtonStyle.Secondary);
            Wire(view, "_modes", modes);
            LabelRow(root, "Play", 32, daily, modes);
            modes.GetComponent<Image>().color = Glass;
            // Weekly Twists banner (value-adds doc): this week's twist, tap to switch to Classic and back.
            var twist = MakeButton(root, "Twist", "THIS WEEK: TWIST", new Vector2(0, 165), new Vector2(820, 120), ButtonStyle.Secondary);
            // Power-ups (a user revision): earned with rewarded ads before a run.
            var powerUps = MakeButton(root, "PowerUps", "Power-ups", new Vector2(0, -610), new Vector2(520, 104), ButtonStyle.Secondary);
            powerUps.GetComponent<Image>().color = GhostCyan;
            powerUps.GetComponentInChildren<TMP_Text>().color = Ink;
            Wire(view, "_powerUps", powerUps);
            twist.GetComponentInChildren<TMP_Text>().fontSizeMax = 30;
            twist.gameObject.SetActive(false);

            // The camera keeps the floor at 30% of the screen's height (CameraRig.FloorFromBottom), so the buttons around
            // it are placed from the floor, not from the centre: on tall phones they stayed put while the floor rose,
            // and the Daily Haunt button sat on the runner. Positions are as laid out on 16:9, where the floor is 384
            // below the centre.
            foreach (Component c in new Component[] { daily, modes, tap, powerUps, skins, stats, reels, settings })
            {
                var rt = (RectTransform)c.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, Echo.Game.Views.CameraRig.FloorFromBottom);
                rt.anchoredPosition += new Vector2(0, 1920 * (0.5f - Echo.Game.Views.CameraRig.FloorFromBottom));
            }

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
                so.FindProperty("_saveReelColor").colorValue = Glass;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var menu = MakeButton(root, "Menu", "Menu", new Vector2(375, -625), new Vector2(230, 100), ButtonStyle.Secondary);
            LabelRow(root, "Offers", 34, cont, powerUps);
            LabelRow(root, "Actions", 28, share, challenge, saveReel, menu);
            foreach (var b in new[] { share, saveReel, menu }) b.GetComponent<Image>().color = Glass;

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
            // Every slot's buttons share one label size, so the cupboard's rows match.
            var slotButtons = new System.Collections.Generic.List<Button>();
            for (int i = 0; i < slots; i++) slotButtons.AddRange(new[] { watch[i], clip[i], challenge[i], delete[i] });
            LabelRow(root, "Slots", 28, slotButtons.ToArray());
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

            var gif = MakeButton(root, "Gif", "GIF", new Vector2(-310, -440), new Vector2(290, 108), ButtonStyle.Secondary);
            var video = MakeButton(root, "Video", "Video", new Vector2(0, -440), new Vector2(290, 108), ButtonStyle.Secondary);
            // Marketing content plan: the Vertical preset (1152 x 2048, 60 fps, 9-20 s) with a hook line and an end card.
            var vertical = MakeButton(root, "Vertical", "Vertical", new Vector2(310, -440), new Vector2(290, 108), ButtonStyle.Secondary);
            var play = MakeButton(root, "Play", "Preview", new Vector2(-235, -575), new Vector2(440, 116), ButtonStyle.Secondary);
            var make = MakeButton(root, "Make", "Make GIF", new Vector2(235, -575), new Vector2(440, 116), ButtonStyle.Primary);
            var cancel = MakeButton(root, "Cancel", "Cancel", new Vector2(0, -575), new Vector2(500, 116), ButtonStyle.Secondary);
            cancel.gameObject.SetActive(false);
            RectTransform options = NewRect("VerticalOptions", root);
            options.anchorMin = options.anchorMax = new Vector2(0.5f, 0.5f);
            options.anchoredPosition = new Vector2(0, -690);
            options.sizeDelta = new Vector2(940, 100);
            GameObject hookGo = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            hookGo.name = "Hook";
            var hookRt = (RectTransform)hookGo.transform;
            hookRt.SetParent(options, false);
            hookRt.anchorMin = hookRt.anchorMax = new Vector2(0.5f, 0.5f);
            hookRt.anchoredPosition = new Vector2(-150, 0);
            hookRt.sizeDelta = new Vector2(620, 96);
            var hookBg = hookGo.GetComponent<Image>();
            hookBg.sprite = Frame;
            hookBg.type = Image.Type.Sliced;
            hookBg.pixelsPerUnitMultiplier = FramePixelScale;
            hookBg.color = SecondaryButton;
            var hook = hookGo.GetComponent<TMP_InputField>();
            hook.characterLimit = 60;
            hook.lineType = TMP_InputField.LineType.SingleLine;
            foreach (TMP_Text t in hookGo.GetComponentsInChildren<TMP_Text>(true))
            {
                t.font = _black;
                t.fontSize = 26;
                t.alignment = TextAlignmentOptions.Center;
                t.color = t == hook.placeholder ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            }
            ((TMP_Text)hook.placeholder).text = "HOOK (UP TO 8 WORDS)";
            var endCard = MakeButton(options, "EndCard", "End card: on", new Vector2(330, 0), new Vector2(270, 96), ButtonStyle.Secondary);
            options.gameObject.SetActive(false);
            // A user revision: once a clip is made, keep it in the gallery or share it (in the hook row's place).
            RectTransform ready = NewRect("Ready", root);
            ready.anchorMin = ready.anchorMax = new Vector2(0.5f, 0.5f);
            ready.anchoredPosition = new Vector2(0, -690);
            ready.sizeDelta = new Vector2(940, 100);
            var saveClip = MakeButton(ready, "Save", "Save to gallery", new Vector2(-235, 0), new Vector2(440, 100), ButtonStyle.Primary);
            var shareClip = MakeButton(ready, "Share", "Share", new Vector2(235, 0), new Vector2(440, 100), ButtonStyle.Secondary);
            shareClip.GetComponent<Image>().color = GhostCyan;
            shareClip.GetComponentInChildren<TMP_Text>().color = Ink;
            ready.gameObject.SetActive(false);

            var back = MakeButton(root, "Back", "Back", new Vector2(0, -800), new Vector2(500, 100), ButtonStyle.Secondary);
            var status = MakeText(root, "Status", "", 34, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, -890), new Vector2(1000, 56));
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
            Wire(view, "_vertical", vertical);
            Wire(view, "_verticalOptions", options.gameObject);
            Wire(view, "_hook", hook);
            Wire(view, "_endCard", endCard);
            Wire(view, "_play", play);
            Wire(view, "_make", make);
            Wire(view, "_back", back);
            Wire(view, "_cancel", cancel);
            Wire(view, "_ready", ready.gameObject);
            Wire(view, "_saveClip", saveClip);
            Wire(view, "_shareClip", shareClip);
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
        /// Ghost Town (a user decision: the online hub, "Modes" in code): its name with the logo's echo, your profile line,
        /// the Daily Haunt as a gold hero card, then up to four event cards (LIVE NOW, then COMING UP), the status line and
        /// Back. Laid out from the top of the design frame.
        /// </summary>
        static ModesView BuildModes(RectTransform parent)
        {
            RectTransform root = MenuPanel(parent, "Modes", "Ghost Town");
            var view = root.gameObject.AddComponent<ModesView>();
            var heading = root.Find("Heading").GetComponent<TMP_Text>();
            // The name echoes itself, as the logo does on the title.
            var echo = MakeText(root, "HeadingEcho", heading.text, 64, _black, new Vector2(0.5f, 1f), new Vector2(-10, -80), new Vector2(1000, 130));
            echo.color = new Color(GhostCyan.r, GhostCyan.g, GhostCyan.b, 0.45f);
            echo.transform.SetSiblingIndex(heading.transform.GetSiblingIndex());
            var tagline = MakeText(root, "Tagline", "Where every runner's ghost ends up", 30, _medium, new Vector2(0.5f, 1f), new Vector2(0, -196), new Vector2(980, 48));
            tagline.color = Soft;

            Pill(root, "ProfileCard", new Vector2(0, 664), new Vector2(940, 92), Card);
            var profile = MakeText(root, "Profile", "", 30, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, 664), new Vector2(880, 84));
            profile.spriteAsset = _badges;
            profile.textWrappingMode = TextWrappingModes.NoWrap;
            profile.enableAutoSizing = true;
            profile.fontSizeMin = 18;
            profile.fontSizeMax = 30;

            // The Daily Haunt: the hero card, gold, its lines in ink.
            var hero = MakeButton(root, "Hero", "", new Vector2(0, 418), new Vector2(940, 360), ButtonStyle.Primary);
            var heroRt = (RectTransform)hero.transform;
            Object.DestroyImmediate(hero.GetComponentInChildren<TMP_Text>().gameObject);
            TMP_Text HeroLine(string name, float y, float size, TMP_FontAsset font, float alpha)
            {
                var t = MakeText(heroRt, name, "", size, font, new Vector2(0f, 1f), new Vector2(44, y), new Vector2(852, size * 1.6f));
                t.rectTransform.pivot = new Vector2(0f, 1f);
                t.alignment = TextAlignmentOptions.TopLeft;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.overflowMode = TextOverflowModes.Ellipsis;
                t.color = new Color(Ink.r, Ink.g, Ink.b, alpha);
                return t;
            }
            var heroTitle = HeroLine("Title", -34, 44, _black, 1f);
            heroTitle.text = "DAILY HAUNT";
            var heroRunners = HeroLine("Runners", -104, 30, _bold, 0.85f);
            var heroLeader = HeroLine("Leader", -158, 30, _bold, 0.85f);
            var heroYou = HeroLine("You", -212, 30, _bold, 0.85f);
            var heroCountdown = HeroLine("Countdown", -282, 26, _medium, 0.7f);
            var enter = MakeText(heroRt, "Enter", "ENTER >", 30, _black, new Vector2(1f, 0f), new Vector2(-44, 36), new Vector2(300, 50));
            enter.rectTransform.pivot = new Vector2(1f, 0f);
            enter.alignment = TextAlignmentOptions.BottomRight;
            enter.color = Ink;
            RectTransform dot = NewRect("Dot", heroRt);
            dot.anchorMin = dot.anchorMax = dot.pivot = new Vector2(1f, 1f);
            dot.anchoredPosition = new Vector2(-44, -40);
            dot.sizeDelta = new Vector2(30, 30);
            var dotImage = dot.gameObject.AddComponent<Image>();
            dotImage.sprite = Frame;
            dotImage.type = Image.Type.Sliced;
            dotImage.pixelsPerUnitMultiplier = FramePixelScale * 3f;
            dotImage.color = NemesisRed;
            dotImage.raycastTarget = false;

            // The events, in a column under the hero card: section headers and cards, reordered by ModesView.
            RectTransform list = NewRect("Events", root);
            list.anchorMin = list.anchorMax = new Vector2(0.5f, 0.5f);
            list.pivot = new Vector2(0.5f, 1f);
            list.anchoredPosition = new Vector2(0, 210);
            list.sizeDelta = new Vector2(940, 900);
            var column = list.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 12;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            var liveHeader = SectionHeader(list, "LiveHeader", "Live now");
            var soonHeader = SectionHeader(list, "SoonHeader", "Coming up");
            var events = new Button[4];
            for (int i = 0; i < events.Length; i++) events[i] = EventCard(list, "Event" + i);

            var status = MakeText(root, "Status", "", 30, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, -712), new Vector2(980, 60));
            status.color = Soft;
            var back = MakeButton(root, "Back", "Back", new Vector2(0, -830), new Vector2(500, 120), ButtonStyle.Secondary);

            Wire(view, "_root", root.gameObject);
            Wire(view, "_profile", profile);
            Wire(view, "_hero", hero);
            Wire(view, "_heroRunners", heroRunners);
            Wire(view, "_heroLeader", heroLeader);
            Wire(view, "_heroYou", heroYou);
            Wire(view, "_heroCountdown", heroCountdown);
            Wire(view, "_heroDot", dot.gameObject);
            Wire(view, "_liveHeader", liveHeader.gameObject);
            Wire(view, "_soonHeader", soonHeader.gameObject);
            WireArray(view, "_events", events);
            Wire(view, "_status", status);
            Wire(view, "_back", back);
            root.gameObject.SetActive(false);
            return view;
        }

        // A Ghost Town event card: a stripe in the event's colour, its name and a LIVE/SOON chip, the pitch, and its live
        // numbers with the countdown along the bottom. ModesView fills it.
        static Button EventCard(RectTransform list, string name)
        {
            RectTransform rt = NewRect(name, list);
            rt.sizeDelta = new Vector2(940, 168);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 168;
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = Frame;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = FramePixelScale;
            image.color = RowSurface;
            var button = rt.gameObject.AddComponent<Button>();
            rt.gameObject.AddComponent<ButtonFeedback>();
            button.targetGraphic = image;

            RectTransform stripe = NewRect("Stripe", rt);
            stripe.anchorMin = new Vector2(0f, 0f);
            stripe.anchorMax = new Vector2(0f, 1f);
            stripe.pivot = new Vector2(0f, 0.5f);
            stripe.offsetMin = new Vector2(10, 14);
            stripe.offsetMax = new Vector2(22, -14);
            var stripeImage = stripe.gameObject.AddComponent<Image>();
            stripeImage.raycastTarget = false;

            TMP_Text Line(string n, Vector2 anchor, Vector2 at, Vector2 box, float size, TMP_FontAsset font, TextAlignmentOptions align)
            {
                var t = MakeText(rt, n, "", size, font, anchor, at, box);
                t.rectTransform.pivot = anchor;
                t.alignment = align;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.overflowMode = TextOverflowModes.Ellipsis;
                return t;
            }
            Line("Name", new Vector2(0f, 1f), new Vector2(48, -20), new Vector2(640, 52), 34, _black, TextAlignmentOptions.TopLeft);
            var pitch = Line("Pitch", new Vector2(0f, 1f), new Vector2(48, -76), new Vector2(860, 40), 28, _medium, TextAlignmentOptions.TopLeft);
            pitch.color = Soft;
            Line("Players", new Vector2(0f, 0f), new Vector2(48, 16), new Vector2(520, 40), 28, _bold, TextAlignmentOptions.BottomLeft);
            var countdown = Line("Countdown", new Vector2(1f, 0f), new Vector2(-28, 16), new Vector2(340, 40), 26, _medium, TextAlignmentOptions.BottomRight);
            countdown.color = Soft;

            RectTransform chip = NewRect("Chip", rt);
            chip.anchorMin = chip.anchorMax = chip.pivot = new Vector2(1f, 1f);
            chip.anchoredPosition = new Vector2(-24, -18);
            chip.sizeDelta = new Vector2(150, 52);
            var chipImage = chip.gameObject.AddComponent<Image>();
            chipImage.sprite = Frame;
            chipImage.type = Image.Type.Sliced;
            chipImage.pixelsPerUnitMultiplier = FramePixelScale;
            chipImage.raycastTarget = false;
            var chipLabel = MakeText(chip, "Label", "LIVE", 22, _black, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 52));
            chipLabel.color = Ink;
            rt.gameObject.SetActive(false);
            return button;
        }

        static DailyLobbyView BuildDailyLobby(RectTransform parent)
        {
            RectTransform root = MenuPanel(parent, "DailyLobby", "Daily Haunt");
            var view = root.gameObject.AddComponent<DailyLobbyView>();
            var countdown = MakeText(root, "Countdown", "", 34, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, 750), new Vector2(980, 56));
            countdown.color = GhostCyan;
            var you = MakeText(root, "You", "", 40, _black, new Vector2(0.5f, 0.5f), new Vector2(0, 650), new Vector2(980, 70));
            you.color = Accent;
            // A user report: "Your best today: 488 m · #1" ran off a 1080 x 2340 screen. Header lines shrink to fit.
            FitOneLine(countdown, 20);
            FitOneLine(you, 22);

            Pill(root, "BoardCard", new Vector2(0, 160), new Vector2(980, 820), Card);
            // A user decision: any run on the board can be watched (a rewarded ad, unless it's your own).
            var watchHint = MakeText(root, "WatchHint", "TAP A RUN TO WATCH IT", 24, _black, new Vector2(0.5f, 0.5f), new Vector2(0, 584), new Vector2(900, 36));
            watchHint.color = GhostCyan;
            var rows = new TMP_Text[10];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = MakeText(root, "Row" + i, "", 38, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, 520 - i * 78), new Vector2(900, 70));
                rows[i].spriteAsset = _badges;
                rows[i].alignment = TextAlignmentOptions.MidlineLeft;
                FitOneLine(rows[i], 18);
                rows[i].raycastTarget = true;
                var tap = rows[i].gameObject.AddComponent<Button>();
                tap.targetGraphic = rows[i];
                tap.transition = Selectable.Transition.ColorTint;
            }
            // The loading runner (a user revision): the player's character runs and hops on the board until it loads.
            RectTransform loader = NewRect("Loader", root);
            loader.anchorMin = loader.anchorMax = new Vector2(0.5f, 0.5f);
            loader.anchoredPosition = new Vector2(0, 120);
            loader.sizeDelta = new Vector2(24 * 6, 34 * 6 + 140);
            Image loaderShadow = Pill(loader, "Shadow", new Vector2(0, -(34 * 6 + 140) / 2f + 10), new Vector2(110, 18), new Color(0f, 0f, 0f, 0.35f));
            loaderShadow.raycastTarget = false;
            RectTransform figure = NewRect("Figure", loader);
            figure.anchorMin = figure.anchorMax = new Vector2(0.5f, 0.5f);
            figure.anchoredPosition = new Vector2(0, -70);
            figure.sizeDelta = new Vector2(24 * 6, 34 * 6);
            var loaderFigure = figure.gameObject.AddComponent<Image>();
            loaderFigure.preserveAspect = true;
            loaderFigure.raycastTarget = false;
            var yesterday = MakeText(root, "Yesterday", "", 32, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, -300), new Vector2(980, 56));
            FitOneLine(yesterday, 18);
            // Modes doc, Naughty vs Nice: the tally bar (Nice from the left), the totals and your side.
            RectTransform tally = NewRect("Tally", root);
            tally.anchorMin = tally.anchorMax = new Vector2(0.5f, 0.5f);
            tally.anchoredPosition = new Vector2(0, -375);
            tally.sizeDelta = new Vector2(940, 110);
            var track = Pill(tally, "Track", new Vector2(0, 38), new Vector2(900, 18), NemesisRed);
            track.raycastTarget = false;
            RectTransform niceFill = NewRect("Nice", (RectTransform)track.transform);
            niceFill.anchorMin = Vector2.zero;
            niceFill.anchorMax = new Vector2(0.5f, 1f);
            niceFill.offsetMin = niceFill.offsetMax = Vector2.zero;
            var niceImage = niceFill.gameObject.AddComponent<Image>();
            niceImage.color = new Color(1f, 0.91f, 0.63f);
            niceImage.raycastTarget = false;
            var tallyText = MakeText(tally, "Text", "", 24, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, -16), new Vector2(940, 70));
            tally.gameObject.SetActive(false);
            Wire(view, "_tally", tally.gameObject);
            // Modes doc, the Community Graveyard: today's board or the week's, where the watch hint would be.
            var boardToggle = MakeButton(root, "BoardToggle", "Show this week", new Vector2(0, 590), new Vector2(560, 64), ButtonStyle.Secondary);
            boardToggle.GetComponentInChildren<TMP_Text>().fontSize = 24;
            boardToggle.gameObject.SetActive(false);
            Wire(view, "_boardToggle", boardToggle);
            Wire(view, "_tallyNice", niceFill);
            Wire(view, "_tallyText", tallyText);
            yesterday.color = Soft;
            var status = MakeText(root, "Status", "", 32, _medium, new Vector2(0.5f, 0.5f), new Vector2(0, -478), new Vector2(980, 90));
            status.color = Soft;
            var play = MakeButton(root, "Play", "Play today's course", new Vector2(0, -600), new Vector2(760, 132), ButtonStyle.Primary);
            var back = MakeButton(root, "Back", "Back", new Vector2(0, -830), new Vector2(500, 120), ButtonStyle.Secondary);

            Wire(view, "_root", root.gameObject);
            Wire(view, "_countdown", countdown);
            Wire(view, "_you", you);
            WireArray(view, "_rows", rows);
            Wire(view, "_watchHint", watchHint);
            Wire(view, "_yesterday", yesterday);
            Wire(view, "_status", status);
            Wire(view, "_play", play);
            Wire(view, "_back", back);
            Wire(view, "_loader", loader);
            Wire(view, "_heading", root.Find("Heading").GetComponent<TMP_Text>());
            Wire(view, "_loaderFigure", loaderFigure);
            Wire(view, "_loaderShadow", loaderShadow.rectTransform);
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

        /// <summary>
        /// How to play (a user revision): the overlay on the tutorial clips. The caption card sits high over the world,
        /// the topic and its dots above it, Skip beside them, and "Tap for next" low down.
        /// </summary>
        static HowToPlayView BuildHowToPlay(RectTransform parent)
        {
            RectTransform root = Stretch(NewRect("HowToPlay", parent));
            var view = root.gameObject.AddComponent<HowToPlayView>();
            var topic = MakeText(root, "Topic", "HOW TO PLAY", 34, _black, new Vector2(0.5f, 0.5f), new Vector2(-90, 650), new Vector2(760, 70));
            topic.color = GhostCyan;
            var dots = new Image[4];
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i] = Pill(root, "Dot" + i, new Vector2(-90 + (i - 1.5f) * 56, 592), new Vector2(36, 36), Color.white);
            }
            var skip = MakeButton(root, "Skip", "Skip", new Vector2(400, 650), new Vector2(210, 96), ButtonStyle.Secondary);

            Image card = Pill(root, "Caption", new Vector2(0, 400), new Vector2(980, 250), new Color(Ink.r, Ink.g, Ink.b, 0.88f));
            var caption = MakeText((RectTransform)card.transform, "Text", "", 40, _black, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 210));
            caption.color = Color.white;
            var hint = MakeText(root, "Hint", "TAP FOR NEXT", 30, _black, new Vector2(0.5f, 0.5f), new Vector2(0, -470), new Vector2(900, 60));
            hint.color = Soft;
            hint.gameObject.AddComponent<Pulse>();

            Wire(view, "_root", root.gameObject);
            Wire(view, "_captionCard", card.gameObject);
            Wire(view, "_caption", caption);
            Wire(view, "_topic", topic);
            Wire(view, "_hint", hint);
            WireArray(view, "_dots", dots);
            Wire(view, "_skip", skip);
            {
                var so = new UnityEditor.SerializedObject(view);
                so.FindProperty("_dotOn").colorValue = Accent;
                so.FindProperty("_dotOff").colorValue = new Color(1f, 1f, 1f, 0.3f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            card.gameObject.SetActive(false);
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

        // A menu on the 1080 x 1920 design frame (DesignFrame): everything in it keeps its place relative to everything
        // else on any screen. The background is a child reaching far past the frame, so it still fills the screen and
        // blocks taps to what's behind.
        static RectTransform MenuPanel(RectTransform parent, string name, string heading)
        {
            RectTransform root = NewRect(name, parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = DesignFrame.Size;
            root.gameObject.AddComponent<DesignFrame>();
            Animate(root);
            RectTransform backdrop = Stretch(NewRect("Backdrop", root));
            backdrop.offsetMin = new Vector2(-4000, -4000);
            backdrop.offsetMax = new Vector2(4000, 4000);
            var bg = backdrop.gameObject.AddComponent<Image>();
            bg.color = MenuTint;
            var title = MakeText(root, "Heading", heading.ToUpperInvariant(), 64, _black, new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(1000, 130));
            return root;
        }

        static StatsView BuildStats(RectTransform parent)
        {
            RectTransform root = MenuPanel(parent, "Stats", "Stats");
            var view = root.gameObject.AddComponent<StatsView>();
            Pill(root, "SummaryCard", new Vector2(0, 515), new Vector2(940, 360), Card);
            var summary = MakeText(root, "Summary", "", 40, _bold, new Vector2(0.5f, 1f), new Vector2(0, -285), new Vector2(840, 340));
            summary.alignment = TextAlignmentOptions.TopLeft;
            summary.lineSpacing = 8;
            var header = MakeText(root, "AchievementsHeader", "ACHIEVEMENTS", 30, _black, new Vector2(0.5f, 1f), new Vector2(0, -660), new Vector2(980, 60));
            // Modes doc (a user decision): the online profile at the top, between the heading and the summary.
            var profile = MakeText(root, "Profile", "", 30, _medium, new Vector2(0.5f, 1f), new Vector2(0, -192), new Vector2(980, 70));
            profile.spriteAsset = _badges;
            Wire(view, "_profile", profile);
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
            LabelRow(root, "Challenges", 30, challengeBest, paste);
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

        // Settings (a user revision: it looked cluttered): one scrolling list of sections, each row a name on the left and
        // its value or an ON/OFF switch on the right, with Back fixed below the list. Rows that don't apply (Play Games
        // off Android, the developer tools in release builds) are hidden and the list closes up.
        static SettingsView BuildSettings(RectTransform parent)
        {
            RectTransform root = MenuPanel(parent, "Settings", "Settings");
            var view = root.gameObject.AddComponent<SettingsView>();
            RectTransform list = ScrollList(root, "Scroll", top: 230, bottom: 220, spacing: 14);

            SectionHeader(list, "SoundHeader", "Sound and feel");
            var music = SettingRow(list, "Music", "Music", toggle: true);
            var sfx = SettingRow(list, "Sfx", "Sound effects", toggle: true);
            var haptics = SettingRow(list, "Haptics", "Haptics", toggle: true);
            var glow = SettingRow(list, "Glow", "Glow", toggle: true);
            // A user revision (worlds): whether seasons' looks show over your world.
            var seasonal = SettingRow(list, "SeasonalLooks", "Seasonal looks", toggle: true);

            SectionHeader(list, "GameHeader", "Game");
            // The name friends see on Ghost Challenges.
            var name = SettingRow(list, "Name", "Challenge name", toggle: false);
            // How to play (a user revision): the tutorial clips again.
            var howTo = SettingRow(list, "HowToPlay", "How to play", toggle: false, opens: true);
            // Google Play Games (Android, once set up), shown by SettingsView.SetPlayGames.
            var playGames = SettingRow(list, "PlayGames", "Google Play Games", toggle: false);
            playGames.gameObject.SetActive(false);

            SectionHeader(list, "PrivacyHeader", "Privacy and data");
            var privacy = SettingRow(list, "Privacy", "Privacy choices", toggle: false, opens: true);
            // Privacy policy (2.0): the Daily Haunt runs, board entry and challenges kept on the server, deleted on request.
            var deleteOnline = SettingRow(list, "DeleteOnline", "Delete online data", toggle: false, opens: true);

            // Development builds (and release builds after five taps on the heading): tools and the services status.
            var devHeader = SectionHeader(list, "DevHeader", "Developer");
            var devAds = SettingRow(list, "DevAdFailure", "Dev: ads succeed", toggle: false);
            // Version 2.0: the newest reel replayed on the server, and the Daily Haunt's date.
            var serverCheck = SettingRow(list, "ServerCheck", "Dev: server check", toggle: false);
            var dailyDate = SettingRow(list, "DailyDate", "Dev: daily today", toggle: false);
            // Modes doc: every competitive mode forced open (the date is the Daily Haunt's).
            var devModes = SettingRow(list, "DevModes", "Dev: modes live", toggle: false);
            // Seasons doc: force any season (development builds and the recording APK).
            var season = SettingRow(list, "Season", "Dev: season auto", toggle: false);
            // Marketing content plan: recording mode for screen recordings.
            var recording = SettingRow(list, "Recording", "Recording: off", toggle: false);
            var recordingHud = SettingRow(list, "RecordingHud", "HUD: shown", toggle: false);
            var status = MakeText(list, "ServicesStatus", "", 24, _medium, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940, 60));
            status.alignment = TextAlignmentOptions.TopLeft;
            status.margin = new Vector4(12, 8, 12, 8);
            status.color = Soft;
            foreach (var row in new Component[] { devHeader, devAds, serverCheck, dailyDate, devModes, season, recording, recordingHud, status })
                row.gameObject.SetActive(false);

            var close = MakeButton(root, "Close", "Back", new Vector2(0, -830), new Vector2(500, 120), ButtonStyle.Primary);
            // Development builds: ads and store status, readable on a test phone. Five taps on the heading show it in
            // release builds too.
            var heading = root.Find("Heading").gameObject;
            heading.GetComponent<TMP_Text>().raycastTarget = true;
            Wire(view, "_heading", heading.AddComponent<Button>());

            Wire(view, "_root", root.gameObject);
            Wire(view, "_music", music);
            Wire(view, "_sfx", sfx);
            Wire(view, "_haptics", haptics);
            Wire(view, "_glow", glow);
            Wire(view, "_seasonalLooks", seasonal);
            Wire(view, "_name", name);
            Wire(view, "_howToPlay", howTo);
            Wire(view, "_playGames", playGames);
            Wire(view, "_privacy", privacy);
            Wire(view, "_deleteOnline", deleteOnline);
            Wire(view, "_devHeader", devHeader.gameObject);
            Wire(view, "_devAdFailure", devAds);
            Wire(view, "_serverCheck", serverCheck);
            Wire(view, "_dailyDate", dailyDate);
            Wire(view, "_devModes", devModes);
            Wire(view, "_season", season);
            Wire(view, "_recording", recording);
            Wire(view, "_recordingHud", recordingHud);
            Wire(view, "_status", status);
            Wire(view, "_close", close);
            root.gameObject.SetActive(false);
            return view;
        }

        // A vertical list that scrolls between the heading and the buttons fixed below it (top and bottom are distances
        // from the design frame's edges). Rows size themselves (LayoutElement); hidden rows take no space.
        static RectTransform ScrollList(RectTransform root, string name, float top, float bottom, float spacing)
        {
            RectTransform scrollRt = NewRect(name, root);
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(70, bottom);
            scrollRt.offsetMax = new Vector2(-70, -top);
            scrollRt.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
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
            column.spacing = spacing;
            column.padding = new RectOffset(0, 0, 0, 24);
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        static TextMeshProUGUI SectionHeader(RectTransform list, string name, string text)
        {
            var header = MakeText(list, name, text.ToUpperInvariant(), 24, _black, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940, 72));
            header.alignment = TextAlignmentOptions.BottomLeft;
            header.margin = new Vector4(8, 0, 8, 6);
            header.color = Accent;
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 72;
            return header;
        }

        // A settings row: the name on the left; on the right its value (Value) or, for a toggle, an ON/OFF switch
        // (Switch, with Value inside). SettingsView fills them in.
        static Button SettingRow(RectTransform list, string name, string label, bool toggle, bool opens = false)
        {
            RectTransform rt = NewRect(name, list);
            rt.sizeDelta = new Vector2(940, 104);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 104;
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = Frame;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = FramePixelScale;
            image.color = RowSurface;
            var button = rt.gameObject.AddComponent<Button>();
            rt.gameObject.AddComponent<ButtonFeedback>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;

            var text = MakeText(rt, "Label", label, 30, _black, new Vector2(0f, 0.5f), new Vector2(36, 0), new Vector2(toggle ? 680 : 560, 80));
            text.rectTransform.pivot = new Vector2(0f, 0.5f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontSizeMin = 18;

            TextMeshProUGUI value;
            if (toggle)
            {
                RectTransform sw = NewRect("Switch", rt);
                sw.anchorMin = sw.anchorMax = sw.pivot = new Vector2(1f, 0.5f);
                sw.anchoredPosition = new Vector2(-24, 0);
                sw.sizeDelta = new Vector2(150, 64);
                var swImage = sw.gameObject.AddComponent<Image>();
                swImage.sprite = Frame;
                swImage.type = Image.Type.Sliced;
                swImage.pixelsPerUnitMultiplier = FramePixelScale;
                swImage.color = Accent;
                swImage.raycastTarget = false;
                value = MakeText(sw, "Value", "ON", 26, _black, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 64));
                value.color = Ink;
            }
            else
            {
                value = MakeText(rt, "Value", opens ? ">" : "", 26, _black, new Vector2(1f, 0.5f), new Vector2(-36, 0), new Vector2(320, 80));
                value.rectTransform.pivot = new Vector2(1f, 0.5f);
                value.alignment = TextAlignmentOptions.MidlineRight;
                value.overflowMode = TextOverflowModes.Ellipsis;
                value.color = Accent;
            }
            value.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }

        static SkinsView BuildSkins(RectTransform parent)
        {
            RectTransform root = MenuPanel(parent, "Skins", "Skins");
            var view = root.gameObject.AddComponent<SkinsView>();

            // A user revision: the store in tabs (SkinsTab order), a bar under the heading.
            RectTransform tabBar = NewRect("Tabs", root);
            tabBar.anchorMin = new Vector2(0f, 1f);
            tabBar.anchorMax = new Vector2(1f, 1f);
            tabBar.pivot = new Vector2(0.5f, 1f);
            tabBar.offsetMin = new Vector2(40, -300);
            tabBar.offsetMax = new Vector2(-40, -206);
            var tabRow = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabRow.spacing = 8;
            tabRow.childControlWidth = tabRow.childControlHeight = true;
            tabRow.childForceExpandWidth = tabRow.childForceExpandHeight = true;
            string[] tabNames = System.Enum.GetNames(typeof(SkinsTab));
            var tabs = new Button[tabNames.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i] = MakeButton(tabBar, tabNames[i], tabNames[i], Vector2.zero, new Vector2(160, 94), ButtonStyle.Secondary);
            }
            LabelRow(root, "Tabs", 24, tabs);

            // One scrolling list between the tabs and the footer; SkinsView fills it from templates.
            RectTransform scrollRt = NewRect("Scroll", root);
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(40, 200);
            scrollRt.offsetMax = new Vector2(-40, -320);
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
            characterLabel.anchorMin = new Vector2(0f, 0f);
            characterLabel.anchorMax = new Vector2(1f, 0f);
            characterLabel.pivot = new Vector2(0.5f, 0f);
            characterLabel.anchoredPosition = new Vector2(0, 14);
            characterLabel.sizeDelta = new Vector2(-24, 146); // the card's width: name, then up to two lines of how to unlock
            // A user revision: a skin pack's four characters in one row.
            RectTransform packGrid = Grid(root, "PackGridTemplate", new Vector2(235, 400), 4);
            CardLabel(characterLabel.GetComponent<TMP_Text>(), 26);

            var item = MakeButton(root, "ItemTemplate", "Item", Vector2.zero, new Vector2(490, 120), ButtonStyle.Secondary);
            CardLabel(item.GetComponentInChildren<TMP_Text>(), 26);

            // A world's card: a little picture (sky, far layer, near layer) over its name and price.
            RectTransform worldGrid = Grid(root, "WorldGridTemplate", new Vector2(490, 340), 2);
            var world = MakeButton(root, "WorldTemplate", "World", Vector2.zero, new Vector2(490, 340), ButtonStyle.Secondary);
            RectTransform picture = NewRect("Preview", (RectTransform)world.transform);
            picture.anchorMin = picture.anchorMax = new Vector2(0.5f, 1f);
            picture.pivot = new Vector2(0.5f, 1f);
            picture.anchoredPosition = new Vector2(0, -16);
            picture.sizeDelta = new Vector2(456, 200);
            picture.gameObject.AddComponent<Image>().raycastTarget = false;
            picture.gameObject.AddComponent<RectMask2D>();
            foreach (var (layer, height) in new[] { ("Far", 199f), ("Near", 100f) })
            {
                RectTransform rt = NewRect(layer, picture);
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(0, height);
                rt.gameObject.AddComponent<Image>().raycastTarget = false;
            }
            var worldLabel = world.GetComponentInChildren<TMP_Text>().rectTransform;
            worldLabel.anchorMin = worldLabel.anchorMax = new Vector2(0.5f, 0f);
            worldLabel.pivot = new Vector2(0.5f, 0f);
            worldLabel.anchoredPosition = new Vector2(0, 12);
            worldLabel.sizeDelta = new Vector2(466, 112);
            CardLabel(worldLabel.GetComponent<TMP_Text>(), 26);

            var product = MakeButton(root, "ProductTemplate", "Pack", Vector2.zero, new Vector2(1000, 116), ButtonStyle.Primary);
            product.gameObject.AddComponent<LayoutElement>().preferredHeight = 116;
            CardLabel(product.GetComponentInChildren<TMP_Text>(), 30);

            var restore = MakeButton(root, "Restore", "Restore", new Vector2(-260, -850), new Vector2(460, 110), ButtonStyle.Secondary);
            var close = MakeButton(root, "Close", "Back", new Vector2(260, -850), new Vector2(460, 110), ButtonStyle.Secondary);
            LabelRow(root, "Footer", 34, restore, close);

            foreach (Component template in new Component[] { header, characterGrid, itemGrid, character, item, product, worldGrid, world, packGrid })
                template.gameObject.SetActive(false);
            WireArray(view, "_tabs", tabs);
            Wire(view, "_worldGridTemplate", worldGrid);
            Wire(view, "_packGridTemplate", packGrid);
            Wire(view, "_worldTemplate", world);
            Wire(view, "_normalFar", UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(EchoArt.Dir + "/bg_mountains.png"));
            Wire(view, "_normalNear", UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(EchoArt.Dir + "/bg_graveyard.png"));

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

        // One line that shrinks (down to minSize) rather than wrapping or running off the screen.
        static void FitOneLine(TMP_Text text, float minSize)
        {
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = minSize;
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
            var text = MakeText(rt, "Label", label, 34, _black, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(40, 16));
            text.color = style == ButtonStyle.Primary ? Ink : Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontSizeMin = 16;
            return button;
        }

        // A card's text: a name over a line or two about it, wrapping within the card, with room between the lines.
        static void CardLabel(TMP_Text label, float maxSize)
        {
            label.textWrappingMode = TextWrappingModes.Normal;
            label.fontSizeMax = maxSize;
            label.fontSizeMin = 14;
            label.lineSpacing = 18;
        }

        // A row of buttons whose labels share one size (LabelGroup): the largest that fits all of them on one line.
        static void LabelRow(RectTransform parent, string name, float maxSize, params Button[] buttons)
        {
            var group = NewRect("Labels" + name, parent).gameObject.AddComponent<LabelGroup>();
            var labels = new Object[buttons.Length];
            for (int i = 0; i < buttons.Length; i++) labels[i] = buttons[i].GetComponentInChildren<TMP_Text>(true);
            WireArray(group, "_labels", labels);
            var so = new UnityEditor.SerializedObject(group);
            so.FindProperty("_maxSize").floatValue = maxSize;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
