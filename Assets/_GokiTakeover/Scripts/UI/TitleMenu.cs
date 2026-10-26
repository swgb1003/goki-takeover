using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Title (screen 01, DesignReferences/01_Title.png) and stage select (screen 02, 02_StageSelect.png),
    /// with the shared settings and encyclopedia, on the same 1280x720 IMGUI canvas as the HUDs. The kitchen
    /// behind it is a static copy of the stage. Mouse, or arrows + Enter / Esc.
    /// </summary>
    public sealed class TitleMenu : MonoBehaviour
    {
        public enum Page { Title, StageSelect, Settings, Codex }
        [SerializeField] private StageDefinition[] stages;
        [SerializeField] private PolishedPresentation presentation;
        [SerializeField] private CodexViewer codexViewer;
        private Font font;
        private GUIStyle logo, subtitle, startStyle, startSub, tapeStyle, tapeLeft, quitStyle, keyStyle, title, big, text, small, tiny, centred, centredBig, centredTitle;
        private float scale = 1, offsetX, offsetY, messageUntil;
        private int focus, codexTab, warmups;
        private Page back = Page.Title;
        private string message, freshArmed;
        private float freshUntil;
        public bool FreshArmed => freshArmed != null && Time.unscaledTime < freshUntil;
        public Page Current { get; private set; }
        public int Selected { get; private set; }
        public SaveData Save { get; private set; }
        public GameSettings Options { get; private set; }
        public StageDefinition SelectedStage => stages[Selected];
        public int StageCount => stages.Length;
        public bool CanContinue => Save.HasCheckpoint(null) && StageFor(Save.checkpoint.stage) != null;
        private static readonly string[] MainItems = { "はじめる", "つづきから", "設定", "図鑑", "終了" };

        public void Configure(StageDefinition[] list, PolishedPresentation look) { stages = list; presentation = look; }
        public void ConfigureCodex(CodexViewer viewer) { codexViewer = viewer; }
        public int CodexTab { get => codexTab; set => codexTab = value; }

        private void Awake()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo" }, 24);
            foreach (int size in new[] { 12, 14, 18, 22, 28, 40, 64, 88 }) font.RequestCharactersInTexture(HudGlyphs.Phase5, size);
            Reload();
            if (SaveData.LastLoadMessage != null) Say(SaveData.LastLoadMessage);
            if (GameSettings.LastLoadMessage != null) Say(GameSettings.LastLoadMessage);
            ApplyOptions();
            Current = GameFlow.ReturnPage == GameFlow.TitlePage.StageSelect ? Page.StageSelect : Page.Title;
            GameFlow.ConsumeReturnPage();
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        public void Reload() { Save = SaveData.Load(); Options = GameSettings.Load(); }
        private void OnDestroy() { if (font) Destroy(font); }

        // ---------------- Actions (also used by the scenario test) ----------------
        public void Open(Page page) { if (page == Page.Settings || page == Page.Codex) back = Current; Current = page; focus = 0; }
        public void Back() { Current = Current == Page.Settings || Current == Page.Codex ? back : Page.Title; focus = 0; }
        public void Select(int index) { Selected = Mathf.Clamp(index, 0, stages.Length - 1); }
        public bool IsOpen(StageDefinition stage) => stage.playable && Save.IsUnlocked(stage.id);
        /// <summary>
        /// Starts the selected stage. A fresh start over an existing record needs a second press (or confirmed),
        /// because the new run's start checkpoint replaces that record.
        /// </summary>
        public bool Sortie(bool resume, bool confirmed = false)
        {
            var stage = SelectedStage;
            if (!IsOpen(stage)) { Say(Save.IsUnlocked(stage.id) ? "このステージは制作中です" : "前のステージをクリアすると解放されます"); return false; }
            if (resume && !Save.HasCheckpoint(stage.id)) return false;
            if (!resume && Save.HasCheckpoint(stage.id) && !confirmed && !(freshArmed == stage.id && Time.unscaledTime < freshUntil))
            {
                freshArmed = stage.id; freshUntil = Time.unscaledTime + 4f;
                Say("記録があります ― もう一度押すと記録を消して最初から始めます");
                return false;
            }
            freshArmed = null;
            GameFlow.StartStage(stage.sceneName, stage.id, resume);
            return true;
        }
        public bool Continue()
        {
            if (!CanContinue) { Say("記録がありません — 「はじめる」から出撃しよう"); return false; }
            Selected = System.Array.IndexOf(stages, StageFor(Save.checkpoint.stage));
            return Sortie(true);
        }
        public void ChooseSkin(string skin)
        {
            Save.skin = Save.HasReward(skin) ? skin : "";
            if (!Save.Save()) Say("保存できませんでした");
        }
        private StageDefinition StageFor(string id)
        {
            foreach (var s in stages) if (s.id == id && s.playable) return s;
            return null;
        }
        private void Say(string text) { message = text; messageUntil = Time.unscaledTime + 3f; }

        private void ApplyOptions()
        {
            AudioListener.volume = Options.masterVolume;
            if (!presentation) return;
            presentation.enabled = false; presentation.enabled = true;
            if (Options.quality == 0)
            {
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.antiAliasing = 0;
                QualitySettings.pixelLightCount = 1;
            }
        }

        // ---------------- Keyboard ----------------
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && Current != Page.Title) Back();
            if (Current == Page.Codex) CodexModelPage.Keys(CodexModelPage.ForTab(codexTab), codexViewer);
            int items = Current == Page.Title ? MainItems.Length : 0;
            if (pending >= 0)
            {
                // The press animation plays out before the page changes.
                if (Time.unscaledTime >= pendingAt) { int item = pending; pending = -1; if (Current == Page.Title) Activate(item); }
            }
            else if (Current == Page.Title)
            {
                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) { focus = (focus + 1) % items; GameAudio.Ui(GameAudio.Sfx.Focus); }
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { focus = (focus + items - 1) % items; GameAudio.Ui(GameAudio.Sfx.Focus); }
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Press(focus);
            }
            else if (Current == Page.StageSelect)
            {
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) { Select((Selected + 1) % stages.Length); GameAudio.Ui(GameAudio.Sfx.Focus); }
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) { Select((Selected + stages.Length - 1) % stages.Length); GameAudio.Ui(GameAudio.Sfx.Focus); }
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { GameAudio.Ui(); Sortie(IsOpen(SelectedStage) && Save.HasCheckpoint(SelectedStage.id)); }
            }
        }
        private void Activate(int item)
        {
            switch (item)
            {
                case 0: Open(Page.StageSelect); break;
                case 1: Continue(); break;
                case 2: Open(Page.Settings); break;
                case 3: Open(Page.Codex); break;
                default: Application.Quit(); break;
            }
        }

        // ---------------- Drawing ----------------
        private void OnGUI()
        {
            if (stages == null || stages.Length == 0) return;
            if (title == null)
            {
                logo = new GUIStyle(Style(88, FontStyle.Bold)) { alignment = TextAnchor.UpperCenter }; title = Style(28); big = Style(22); text = Style(18); small = Style(14); tiny = Style(12);
                centred = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
                centredBig = new GUIStyle(big) { alignment = TextAnchor.MiddleCenter };
                centredTitle = new GUIStyle(title) { alignment = TextAnchor.MiddleCenter };
                subtitle = new GUIStyle(Style(28, FontStyle.Bold)) { alignment = TextAnchor.MiddleCenter };
                startStyle = new GUIStyle(Style(40, FontStyle.Bold)) { alignment = TextAnchor.MiddleCenter };
                startSub = new GUIStyle(Style(14, FontStyle.Bold)) { alignment = TextAnchor.UpperCenter };
                tapeStyle = new GUIStyle(Style(18, FontStyle.Bold)) { alignment = TextAnchor.MiddleCenter };
                tapeLeft = new GUIStyle(tapeStyle) { alignment = TextAnchor.MiddleLeft };
                quitStyle = new GUIStyle(Style(14, FontStyle.Bold)) { alignment = TextAnchor.MiddleCenter };
                keyStyle = Style(12, FontStyle.Bold);
            }
            Matrix4x4 saved = GUI.matrix;
            scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            offsetX = (Screen.width - 1280 * scale) / 2; offsetY = (Screen.height - 720 * scale) / 2;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY), Quaternion.identity, Vector3.one * scale);
            Draw(Current);
            if (Time.unscaledTime < messageUntil)
            {
                float w = Mathf.Min(900, 60 + message.Length * 19);
                if (Current == Page.Title)
                {
                    // On the paper title screen the notice is the HUD's orange gum tape.
                    PaperKit.TornTape(new Rect(640 - w / 2, 300, w, 46), -1.2f, PaperKit.Orange);
                    PaperKit.Rotated(new Rect(640 - w / 2, 300, w, 46), -1.2f, () => HudKit.Ink(centred, PaperKit.Hex(0x1C1005), () => GUI.Label(new Rect(640 - w / 2, 300, w, 46), message, centred)));
                }
                else
                {
                    HudKit.Panel(new Rect(640 - w / 2, 22, w, 40), HudKit.Orange);
                    GUI.Label(new Rect(640 - w / 2 + 18, 29, w - 30, 30), message, text);
                }
            }
            // First repaints: draw every page off-screen once so opening a page later does not hitch.
            if (warmups < 2 && Event.current.type == EventType.Repaint)
            {
                warmups++;
                GUI.matrix = Matrix4x4.TRS(new Vector3(-4000, -4000), Quaternion.identity, Vector3.one);
                int keepTab = codexTab;
                for (int p = 0; p < 4; p++) Draw((Page)p);
                for (int tab = 0; tab < CodexData.Tabs.Length; tab++) { codexTab = tab; CodexPage(); }
                codexTab = keepTab;
            }
            GUI.matrix = saved;
        }
        private void Draw(Page page)
        {
            switch (page)
            {
                case Page.StageSelect: StageSelect(); break;
                case Page.Settings: Dim(); SettingsPage(); break;
                case Page.Codex: Dim(); CodexPage(); break;
                default: TitlePage(); break;
            }
        }
        private static void Dim() => HudKit.Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, 0.5f));

        // The title screen as 「人間の落とし物」 (canvas 案2b, 2026-10-26): the menu is paper tape and a round sticker on a
        // cardboard scrap at the right edge, the focus is a red-pen loop with a small cockroach, and one grade
        // (warm tint, vignette, grain) lies over the scene and the UI alike so the paper does not float.
        private static readonly Rect[] TitleItems =
        {
            new Rect(1006, 290, 186, 186), new Rect(984, 502, 244, 40), new Rect(980, 552, 150, 40), new Rect(1080, 600, 150, 40), new Rect(1000, 652, 112, 36),
        };
        private static readonly float[] TitleTilt = { -7, 1.5f, -2, 1, -1 };
        private Vector2 lastMouse = new Vector2(-1, -1);
        private const float LoopMove = 0.18f, PressTime = 0.32f, PressDelay = 0.26f, ShakeTime = 0.32f;
        private Rect loopFrom, loopShown, roachShown;
        private int loopTarget = -1, pending = -1, pressItem = -1;
        private float loopSince, pendingAt, pressSince = -9, shakeSince = -9;

        /// <summary>
        /// Press an item: the paper is pushed flat and springs back, the red pen circles it a second time and the
        /// cockroach hops at it; the action runs when that is over. A disabled item shakes its head instead.
        /// </summary>
        public bool Pressing => pending >= 0;
        public int Focus { get => focus; set => focus = Mathf.Clamp(value, 0, MainItems.Length - 1); }
        public void Press(int item)
        {
            if (pending >= 0) return;
            focus = item;
            if (item == 1 && !CanContinue) { shakeSince = Time.unscaledTime; GameAudio.Ui(GameAudio.Sfx.Focus); Activate(item); return; }
            GameAudio.Ui();
            pressItem = item; pressSince = Time.unscaledTime;
            pending = item; pendingAt = Time.unscaledTime + PressDelay;
        }
        private Rect LoopRect(int i)
        {
            var r = TitleItems[i];
            return i == 0 ? new Rect(r.x - 24, r.y - 24, r.width + 48, r.height + 48) : new Rect(r.x - 16, r.y - 12, r.width + 32, r.height + 24);
        }
        // Pushed flat (0.9) in the first third, then a small overshoot (1.05) and back to 1.
        private float PressScale(int item)
        {
            if (item != pressItem) return 1;
            float t = (Time.unscaledTime - pressSince) / PressTime;
            if (t < 0 || t >= 1) return 1;
            if (t < 0.3f) return Mathf.Lerp(1, 0.9f, Mathf.SmoothStep(0, 1, t / 0.3f));
            float k = (t - 0.3f) / 0.7f;
            return 1 + (0.9f - 1) * Mathf.Cos(k * Mathf.PI * 1.5f) * (1 - k) + 0.05f * Mathf.Sin(k * Mathf.PI) * (1 - k);
        }
        private float Shake(int item)
        {
            float t = (Time.unscaledTime - shakeSince) / ShakeTime;
            return item == 1 && t >= 0 && t < 1 ? Mathf.Sin(t * Mathf.PI * 6) * 5 * (1 - t) : 0;
        }

        private void TitlePage()
        {
            // The logo 「ゴキの惑星」 keeps its two-line form; the subtitle sits on a strip of paper tape below it.
            Logo(new Rect(240, 46, 800, 100), "ゴキの", HudKit.Cream);
            Logo(new Rect(240, 134, 800, 100), "惑星", HudKit.Orange);
            PaperKit.TornTape(new Rect(460, 244, 360, 48), -1.5f, new Color(PaperKit.Tape.r, PaperKit.Tape.g, PaperKit.Tape.b, 0.86f));
            PaperKit.Rotated(new Rect(460, 244, 360, 48), -1.5f, () => Heavy(new Rect(460, 244, 360, 48), "ゴキブリ侵略サバイバル", subtitle, PaperKit.Hex(0x586E0E)));

            float kx = 1052;
            kx = PaperKit.KeyHint(kx, 24, "↑↓", "えらぶ", keyStyle);
            PaperKit.KeyHint(kx, 24, "Enter", "決定", keyStyle);
            PaperKit.Lit(new Rect(30, 22, 120, 20), "ver 1.0", tiny, PaperKit.Light);

            // The mouse moves the focus too, so the red-pen loop always marks what Enter or a click will do.
            Vector2 mouse = Event.current.mousePosition;
            if (mouse != lastMouse && pending < 0)
            {
                for (int i = 0; i < TitleItems.Length; i++)
                    if (TitleItems[i].Contains(mouse) && focus != i && lastMouse.x >= 0) { focus = i; GameAudio.Ui(GameAudio.Sfx.Focus); }
                lastMouse = mouse;
            }

            PaperKit.Cardboard(new Rect(950, 262, 360, 480), -2);
            bool pressed = Input.GetMouseButton(0) && pending < 0;
            var start = TitleItems[0];
            bool overStart = start.Contains(mouse);
            PaperKit.Scaled(start.center, PressScale(0), () =>
            {
                PaperKit.StickerDisc(start, TitleTilt[0], focus == 0 ? 0.6f : 0, overStart && pressed);
                PaperKit.Rotated(start, TitleTilt[0], () =>
                {
                    var top = new Rect(start.x, start.y + 58 + (overStart && pressed ? 2 : 0), start.width, 48);
                    Heavy(top, "はじめる", startStyle, PaperKit.StickerInk);
                    HudKit.Ink(startSub, PaperKit.StickerInk, () => GUI.Label(new Rect(start.x, top.y + 50, start.width, 22), "ステージをえらぶ", startSub));
                });
            });
            for (int i = 1; i < TitleItems.Length; i++)
            {
                bool enabled = i != 1 || CanContinue;
                float alpha = i == 1 && !enabled ? 0.62f : i == 4 ? 0.74f : 0.88f;
                if (focus == i && enabled) alpha = Mathf.Min(1, alpha + 0.08f);
                var r = TitleItems[i];
                if (pressed && r.Contains(mouse) && enabled) r.y += 2;
                r.x += Shake(i);
                PaperKit.Scaled(r.center, PressScale(i), () =>
                {
                    PaperKit.TapeStrip(r, TitleTilt[i], alpha, null, null, PaperKit.Ink);
                    PaperKit.Rotated(r, TitleTilt[i], () =>
                    {
                        if (i != 1) { HudKit.Ink(i == 4 ? quitStyle : tapeStyle, i == 4 ? PaperKit.InkSoft : PaperKit.Ink, () => GUI.Label(r, MainItems[i], i == 4 ? quitStyle : tapeStyle)); return; }
                        // つづきから shows which record it would load, or that there is none.
                        string note = CanContinue ? "（" + StageFor(Save.checkpoint.stage).title + " " + ShortDate(Save.checkpoint.savedAt) + "）" : "（記録なし）";
                        float w1 = tapeStyle.CalcSize(new GUIContent(MainItems[1])).x, w2 = tiny.CalcSize(new GUIContent(note)).x, x = r.center.x - (w1 + 4 + w2) / 2;
                        HudKit.Ink(tapeStyle, PaperKit.InkSoft, () => GUI.Label(new Rect(x, r.y, w1 + 2, r.height), MainItems[1], tapeLeft));
                        HudKit.Ink(tiny, PaperKit.InkSoft, () => GUI.Label(new Rect(x + w1 + 4, r.y + 13, w2 + 2, 20), note, tiny));
                    });
                });
            }
            FocusMark();

            for (int i = 0; i < TitleItems.Length; i++)
                if (pending < 0 && GUI.Button(TitleItems[i], GUIContent.none, GUIStyle.none)) Press(i);

            PaperKit.Grade();
        }
        // The red-pen loop slides to the newly focused item (ease-out, 0.18 s) and the cockroach scurries after it,
        // turned the way it runs; on a press the pen circles the item once more and the cockroach hops at it.
        private void FocusMark()
        {
            float now = Time.unscaledTime;
            var target = LoopRect(focus);
            if (loopTarget < 0) { loopShown = target; roachShown = RoachRest(target); }
            if (focus != loopTarget)
            {
                loopFrom = loopShown; loopTarget = focus; loopSince = now;
            }
            float t = Mathf.Clamp01((now - loopSince) / LoopMove), e = 1 - (1 - t) * (1 - t) * (1 - t);
            loopShown = Lerp(loopFrom, target, e);
            float press = pressItem == focus ? Mathf.Clamp01((now - pressSince) / PressTime) : 1;
            float pulse = press < 1 ? 1 + 0.06f * Mathf.Sin(press * Mathf.PI) : 1;
            var size = new Vector2((int)target.width, (int)target.height);
            PaperKit.Scaled(loopShown.center, pulse, () => PaperKit.Rotated(loopShown, TitleTilt[focus] * 0.5f, () =>
            {
                PaperKit.PenLoop(loopShown, size);
                // The second, slightly smaller loop: 「これに決めた」.
                if (press < 1) PaperKit.Rotated(loopShown, 9, () => PaperKit.PenLoop(new Rect(loopShown.x + 6, loopShown.y + 5, loopShown.width - 12, loopShown.height - 10), size, Mathf.Clamp01(press * 4)));
            }));

            // The cockroach follows a little behind, faces where it runs and wiggles while it moves.
            var rest = RoachRest(loopShown);
            Vector2 before = roachShown.center;
            if (Event.current.type == EventType.Repaint) roachShown = Lerp(roachShown, rest, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 18));   // once per frame
            Vector2 step = roachShown.center - before;
            bool running = (rest.center - roachShown.center).sqrMagnitude > 4;
            float facing = running && step.sqrMagnitude > 0.01f ? Mathf.Atan2(step.y, step.x) * Mathf.Rad2Deg : 8;
            if (running) facing += Mathf.Sin(now * 55) * 7;
            var drawn = roachShown;
            if (press < 1) drawn.x += 12 * Mathf.Sin(Mathf.Clamp01(press * 1.6f) * Mathf.PI);   // the hop at the pressed item
            PaperKit.Roach(drawn, facing);
        }
        private static Rect RoachRest(Rect loop) => new Rect(loop.x - 62, loop.center.y - 28, 70, 56);
        private static Rect Lerp(Rect a, Rect b, float t) => new Rect(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.width, b.width, t), Mathf.Lerp(a.height, b.height, t));
        private static string ShortDate(string savedAt) => savedAt != null && savedAt.Length >= 16 ? savedAt.Substring(5) : savedAt;   // yyyy/MM/dd HH:mm -> MM/dd HH:mm
        // The OS font has no heavy weight: drawing the label twice, 1 px apart, thickens it.
        private static void Heavy(Rect r, string label, GUIStyle style, Color color)
        {
            HudKit.Ink(style, color, () => { GUI.Label(r, label, style); GUI.Label(new Rect(r.x + 1, r.y, r.width, r.height), label, style); });
        }
        // The OS font has no heavy weight: a hard shadow plus a doubled stroke gives the logo its mass.
        private void Logo(Rect r, string label, Color color) => Logo(r, label, color, logo, 5);
        private void Logo(Rect r, string label, Color color, GUIStyle style, float shadow)
        {
            HudKit.Shadowed(r, label, style, color, shadow);
            GUI.color = color; GUI.Label(new Rect(r.x + 2, r.y, r.width, r.height), label, style); GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), label, style); GUI.color = Color.white;
        }

        private void StageSelect()
        {
            HudKit.Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, 0.42f));
            HudKit.Shadowed(new Rect(34, 14, 400, 50), "ゴキの惑星", title, HudKit.Orange);
            GUI.Label(new Rect(36, 58, 400, 24), "どこに侵略する？", text);

            // Colony status (best results so far).
            var status = new Rect(30, 100, 330, 330);
            HudKit.Panel(status, HudKit.Lime);
            GUI.Label(new Rect(50, 112, 290, 30), "巣の状況", big);
            int clears = 0; foreach (var s in stages) if (Save.Record(s.id) != null) clears++;
            int open = 0; foreach (var s in stages) if (Save.IsUnlocked(s.id)) open++;
            StatusRow(152, "クリアしたステージ", clears + " / " + stages.Length);
            StatusRow(194, "解放したステージ", open + " / " + stages.Length);
            var first = Save.Record(stages[0].id);
            StatusRow(236, "ベスト評価", first != null ? first.bestRating : "―");
            StatusRow(278, "最多の食料", first != null ? first.bestFood.ToString() : "―");
            GUI.Label(new Rect(50, 326, 290, 22), "見た目", small);
            bool gold = Save.HasReward("skin_gold");
            if (gold)
            {
                if (HudKit.Tab(new Rect(50, 350, 140, 40), "通常", centred, Save.skin != "skin_gold")) ChooseSkin("");
                if (HudKit.Tab(new Rect(200, 350, 140, 40), "金色の甲殻", centred, Save.skin == "skin_gold")) ChooseSkin("skin_gold");
            }
            else HudKit.Ink(small, HudKit.Muted, () => GUI.Label(new Rect(50, 352, 300, 50), "？？？  ―  ステージ1クリアで解放", small));

            // Selected stage detail.
            var stage = SelectedStage;
            bool unlocked = Save.IsUnlocked(stage.id), playable = IsOpen(stage);
            var detail = new Rect(380, 100, 870, 330);
            HudKit.Panel(detail, playable ? HudKit.Lime : HudKit.Orange);
            HudKit.Ink(small, playable ? HudKit.Lime : HudKit.Orange, () => GUI.Label(new Rect(404, 112, 400, 22), "STAGE " + (Selected + 1) + "  ―  " + StatusText(stage), small));
            GUI.Label(new Rect(402, 134, 820, 56), stage.title, Style(40));
            GUI.Label(new Rect(404, 192, 820, 28), stage.subtitle + "   ／   人間 " + stage.residents + "人   ／   難易度 " + Stars(stage.difficulty), text);
            GUI.Label(new Rect(404, 226, 820, 54), stage.description, text);
            if (playable)
            {
                GUI.Label(new Rect(404, 286, 820, 26), "目的  食料 " + stage.foodGoal + "・縄張り " + stage.zones.Length + "区域・人間の恐怖 を100%に  →  最終防衛 " + stage.finalDefenceSeconds.ToString("F0") + "秒", small);
                var record = Save.Record(stage.id);
                GUI.Label(new Rect(404, 314, 820, 26), record != null ? "クリア " + record.clears + "回   ベスト評価 " + record.bestRating + "   ベストタイム " + Clock(record.bestSeconds) : "まだクリアしていません", small);
                HudKit.Ink(small, HudKit.Orange, () => GUI.Label(new Rect(404, 380, 820, 26), stage.id == "stage03" ? "危険  店主のお玉・熱湯 ・ おかみさんのお盆・スプレー ・ バイトのブラシ・濡れた床 ・ 客の悲鳴 ・ 燻煙剤" :
                    stage.residents > 1 ? "危険  父のスリッパ・罠 ・ 母の掃除機 ・ 息子の虫取り網 ・ 飼い猫 ・ 燻煙剤（最終防衛）" : "危険  スリッパ ・ 粘着シート ・ 毒餌 ・ 殺虫スプレー ・ 燻煙剤（最終防衛）", small));
                if (Save.HasCheckpoint(stage.id))
                    HudKit.Ink(small, HudKit.Lime, () => GUI.Label(new Rect(404, 342, 820, 26), "記録あり  " + Save.checkpoint.savedAt + "（仲間 " + Save.checkpoint.allies + "匹・食料 " + Save.checkpoint.food + "）", small));
            }
            else HudKit.Ink(text, HudKit.Muted, () => GUI.Label(new Rect(404, 300, 820, 60), unlocked ? "解放済み  ―  このステージは制作中です（今後のアップデートで遊べます）" : "ロック中  ―  前のステージをクリアすると解放", text));

            // Stage cards.
            for (int i = 0; i < stages.Length; i++) StageCard(new Rect(30 + i * 300, 452, 286, 150), i);

            // With a record, continuing is the main action; starting over asks for a second press.
            bool canResume = playable && Save.HasCheckpoint(stage.id);
            if (canResume)
            {
                if (HudKit.Primary(new Rect(950, 452, 300, 72), "▶  つづきから", centredTitle, true)) Sortie(true);
                if (HudKit.Secondary(new Rect(950, 534, 300, 52), FreshArmed ? "もう一度押すと最初から" : "最初から出撃（記録を消す）", centred, true, FreshArmed)) Sortie(false);
            }
            else
            {
                if (HudKit.Primary(new Rect(950, 452, 300, 72), "▶  出撃", centredTitle, playable)) Sortie(false);
                HudKit.Secondary(new Rect(950, 534, 300, 52), "つづきから（記録なし）", centred, false);
            }
            if (HudKit.Secondary(new Rect(950, 596, 145, 46), "図鑑", centred)) Open(Page.Codex);
            if (HudKit.Secondary(new Rect(1105, 596, 145, 46), "設定", centred)) Open(Page.Settings);
            if (HudKit.Secondary(new Rect(30, 620, 200, 46), "◀  タイトルへ", centred)) Back();
            HudKit.Ink(tiny, HudKit.Muted, () => GUI.Label(new Rect(250, 634, 600, 22), Save.HasCheckpoint(stage.id) && playable ? "←→ ステージを選ぶ   Enter つづきから   Esc 戻る" : "←→ ステージを選ぶ   Enter 出撃   Esc 戻る", tiny));
        }
        private void StatusRow(float y, string label, string value)
        {
            HudKit.Fill(new Rect(46, y, 300, 36), HudKit.Row);
            GUI.Label(new Rect(60, y + 7, 180, 24), label, small);
            var style = new GUIStyle(big) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(180, y, 152, 36), value, style);
        }
        private void StageCard(Rect r, int index)
        {
            var stage = stages[index];
            bool picked = index == Selected, playable = IsOpen(stage), unlocked = Save.IsUnlocked(stage.id);
            bool hover = r.Contains(Event.current.mousePosition);
            HudKit.Fill(r, hover ? new Color(0.09f, 0.11f, 0.08f, 0.95f) : HudKit.PanelColor);
            HudKit.Fill(new Rect(r.x, r.y, 4, r.height), playable ? HudKit.Lime : unlocked ? HudKit.Orange : HudKit.Off);
            if (picked) HudKit.Outline(r, HudKit.Lime, 3);
            HudKit.Ink(small, playable ? HudKit.Lime : HudKit.Muted, () => GUI.Label(new Rect(r.x + 20, r.y + 12, 240, 22), "◇ STAGE " + (index + 1), small));
            HudKit.Ink(big, unlocked ? HudKit.Cream : HudKit.Muted, () => GUI.Label(new Rect(r.x + 20, r.y + 36, 260, 30), stage.title, big));
            HudKit.Ink(small, unlocked ? new Color(0.78f, 0.77f, 0.7f) : HudKit.Muted, () => GUI.Label(new Rect(r.x + 20, r.y + 70, 256, 24), stage.subtitle, small));
            HudKit.Ink(text, HudKit.Lime, () => GUI.Label(new Rect(r.x + 20, r.y + 96, 150, 26), Stars(stage.difficulty), text));
            var record = Save.Record(stage.id);
            HudKit.Ink(small, playable ? HudKit.Cream : unlocked ? HudKit.Orange : HudKit.Muted, () =>
                GUI.Label(new Rect(r.x + 20, r.y + 120, 260, 24), record != null ? "クリア済み  評価 " + record.bestRating : StatusText(stage), small));
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { Select(index); GameAudio.Ui(GameAudio.Sfx.Focus); }
        }
        private string StatusText(StageDefinition stage) => IsOpen(stage) ? "出撃できます" : Save.IsUnlocked(stage.id) ? "解放済み・制作中" : "ロック中";

        // ---------------- Settings & encyclopedia ----------------
        private void SettingsPage()
        {
            var r = new Rect(330, 120, 620, 480);
            HudKit.Panel(r, HudKit.Lime);
            var o = Options;
            GUI.Label(new Rect(370, 140, 500, 45), "設定", title);
            bool changed = false;
            changed |= Stepper(new Rect(370, 194, 540, 44), "マウス感度", ref o.mouseSensitivity, 0.5f, 5f, 0.25f, o.mouseSensitivity.ToString("F2"));
            changed |= Stepper(new Rect(370, 242, 540, 44), "音量", ref o.masterVolume, 0, 1, 0.1f, Mathf.RoundToInt(o.masterVolume * 100) + "%");
            changed |= Choice(new Rect(370, 290, 540, 44), "画質", ref o.quality, "低（軽い）", "中（影あり）");
            int bubbles = o.speechBubbles ? 1 : 0, shake = o.cameraShake ? 1 : 0, screen = o.fullscreen ? 1 : 0;
            if (Choice(new Rect(370, 338, 540, 44), "人間の吹き出し", ref bubbles, "オフ", "オン")) { o.speechBubbles = bubbles == 1; changed = true; }
            if (Choice(new Rect(370, 386, 540, 44), "画面の揺れ", ref shake, "オフ", "オン")) { o.cameraShake = shake == 1; changed = true; }
            if (Choice(new Rect(370, 434, 540, 44), "画面", ref screen, "ウィンドウ", "全画面")) { o.fullscreen = screen == 1; o.ApplyScreen(); changed = true; }
            if (changed) { ApplyOptions(); if (!o.Save()) Say("設定を保存できませんでした"); }
            GUI.Label(new Rect(370, 486, 540, 24), "変更はすぐ反映・保存されます。音量は効果音全体に効きます。", small);
            if (HudKit.Primary(new Rect(370, 522, 540, 56), "戻る  [Esc]", centred, true)) Back();
        }
        private bool Stepper(Rect r, string label, ref float value, float min, float max, float step, string shown)
        {
            GUI.Label(new Rect(r.x, r.y + 8, 170, 30), label, text);
            float before = value;
            if (HudKit.Primary(new Rect(r.x + 180, r.y + 2, 44, 40), "−", centred, value > min + 1e-4f)) value = Mathf.Max(min, value - step);
            HudKit.Bar(new Rect(r.x + 236, r.y + 18, 180, 8), (value - min) / (max - min), HudKit.Lime);
            if (HudKit.Primary(new Rect(r.x + 428, r.y + 2, 44, 40), "+", centred, value < max - 1e-4f)) value = Mathf.Min(max, value + step);
            GUI.Label(new Rect(r.x + 482, r.y + 8, 70, 30), shown, text);
            return !Mathf.Approximately(before, value);
        }
        private bool Choice(Rect r, string label, ref int value, string a, string b)
        {
            GUI.Label(new Rect(r.x, r.y + 8, 170, 30), label, text);
            int before = value;
            if (HudKit.Tab(new Rect(r.x + 180, r.y + 2, 170, 40), a, centred, value == 0)) value = 0;
            if (HudKit.Tab(new Rect(r.x + 360, r.y + 2, 170, 40), b, centred, value == 1)) value = 1;
            return before != value;
        }
        private void CodexPage()
        {
            var r = new Rect(130, 70, 1020, 580);
            HudKit.Fill(r, HudKit.PanelColor); HudKit.Fill(new Rect(r.x, r.y, 3, r.height), HudKit.Lime);
            GUI.Label(new Rect(r.x + 30, r.y + 18, 300, 45), "図鑑", title);
            for (int i = 0; i < CodexData.Tabs.Length; i++)
                if (HudKit.Tab(new Rect(r.x + 200 + i * 150, r.y + 24, 140, 40), CodexData.Tabs[i], centred, codexTab == i)) codexTab = i;
            var book = CodexModelPage.ForTab(codexTab);   // the human and food tabs show models
            if (book != null) CodexModelPage.Draw(book, r, Options, codexViewer, big, text, small);
            var entries = book != null ? System.Array.Empty<CodexData.Entry>() : CodexData.Entries[codexTab];
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                bool known = e.id == null || Options.Knows(e.id);
                var row = new Rect(r.x + 30, r.y + 84 + i * 84, r.width - 60, 76);
                HudKit.Fill(row, HudKit.Row);
                HudKit.Fill(new Rect(row.x, row.y, 3, row.height), known ? HudKit.Lime : new Color(0.35f, 0.35f, 0.33f));
                HudKit.Ink(big, known ? HudKit.Cream : HudKit.Muted, () => GUI.Label(new Rect(row.x + 20, row.y + 8, 300, 30), known ? e.name : "？？？", big));
                HudKit.Ink(small, known ? new Color(0.8f, 0.79f, 0.72f) : HudKit.Muted, () => GUI.Label(new Rect(row.x + 20, row.y + 42, row.width - 40, 26), known ? e.body : "まだ出会っていない", small));
            }
            if (HudKit.Primary(new Rect(r.xMax - 250, r.yMax - 70, 220, 50), "戻る  [Esc]", centred, true)) Back();
        }

        private static string Stars(int n) { var sb = new System.Text.StringBuilder(); for (int i = 0; i < 5; i++) sb.Append(i < n ? "★" : "☆"); return sb.ToString(); }
        private static string Clock(float seconds) { int s = Mathf.FloorToInt(seconds); return (s / 60) + ":" + (s % 60).ToString("00"); }
        private readonly System.Collections.Generic.Dictionary<int, GUIStyle> sized = new System.Collections.Generic.Dictionary<int, GUIStyle>();
        private GUIStyle Style(int size, FontStyle weight = FontStyle.Normal)
        {
            int key = size * 4 + (int)weight;
            if (sized.TryGetValue(key, out var cached)) return cached;
            var style = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, fontStyle = weight };
            style.normal.textColor = HudKit.Cream;
            sized[key] = style;
            return style;
        }
    }
}
