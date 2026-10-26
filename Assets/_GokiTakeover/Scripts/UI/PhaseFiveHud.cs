using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Phase 5 HUD: the Phase 4 kit (dark panel, lime edge, orange = danger, 1280x720 canvas) plus the mission
    /// briefing (screen 03), the three takeover gauges and territory markers, the final defence timer and
    /// fogger warnings, a pause menu with "back to title", the game over screen with successor / checkpoint
    /// (screen 11) and the stage clear screen with rating and unlocks (screen 12, DesignReferences/06_StageClear.png).
    /// Also the chapter 1 guide (StageTutorial): a panel with the step, the key to press drawn as a keycap,
    /// and a pulsing outline or "ここ" marker on the button or place it means.
    /// </summary>
    public sealed class PhaseFiveHud : MonoBehaviour, ITacticsHud
    {
        private enum PausePage { Main, Settings, Codex }
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private SwarmCommander commander;
        [SerializeField] private ColonyManager colony;
        [SerializeField] private StageProgress progress;
        [SerializeField] private StickyTrap[] traps;
        [SerializeField] private StageTutorial tutorial;
        [SerializeField] private CodexViewer codexViewer;
        private Font font;
        private GUIStyle title, big, text, small, tiny, huge, giant, right, keycap;
        private float elapsed, scale = 1, offsetX, offsetY;
        private int frames, codexTab, warmups;
        private PausePage page;
        private string fps = "計測中";
        private static readonly Color Lime = new Color(0.79f, 0.87f, 0.37f);
        private static readonly Color Orange = new Color(1, 0.55f, 0.21f);
        private static readonly Color Cream = new Color(0.94f, 0.92f, 0.83f);
        private static readonly Color Muted = new Color(0.62f, 0.62f, 0.57f);
        private static readonly Color PanelColor = new Color(0.025f, 0.035f, 0.035f, 0.9f);
        private static readonly Rect CommandBar = new Rect(30, 594, 1220, 96);
        // The rally (4) is gone from Phase 5: left-click moves and drawn routes do its job.
        private static readonly AllyRoach.Order[] Orders = { AllyRoach.Order.Collect, AllyRoach.Order.Explore, AllyRoach.Order.Defend, AllyRoach.Order.Capture };
        private static readonly int[] OrderKeys = { 1, 2, 3, 5 };
        private static readonly string[] OrderHelp = { "食料を集めて巣へ運ぶ", "未調査の場所を調べる", "巣のまわりで待機", "区域へ分かれて占領" };
        private static readonly string[] OrderGlyph = { "◆", "◎", "■", "□" };
        private const int CaptureButton = 3;
        private static readonly Rect LaunchPanel = new Rect(880, 500, 370, 56);
        private const float OrderWidth = 296, OrderStep = 308;
        private static Rect OrderRect(int index) => new Rect(30 + index * OrderStep, 598, OrderWidth, 88);
        // Tutorial guide: top centre in the normal view, above the order bar in the tactics view (right-aligned
        // over button 5 while it is the thing to press), between the title and the stock in the nest.
        private static readonly Rect GuideNormal = new Rect(476, 28, 408, 164), GuideTactics = new Rect(476, 394, 408, 164);
        private static readonly Rect GuideCapture = new Rect(842, 394, 408, 164), GuideNest = new Rect(600, 24, 320, 100);
        private static readonly string[] UpgradeGlyph = { "»", "■", "◎", "◆", "♥" };
        public void Configure(PhaseTwoSession game, SwarmCommander swarm, ColonyManager nest, StageProgress stage, StickyTrap[] hazards, StageTutorial guide = null)
        { session = game; commander = swarm; colony = nest; progress = stage; traps = hazards; tutorial = guide; }
        public void ConfigureCodex(CodexViewer viewer) { codexViewer = viewer; }
        /// <summary>Pause menu straight to an encyclopedia tab (used by the scenario test's screenshots).</summary>
        public void OpenCodex(int tab) { page = PausePage.Codex; codexTab = Mathf.Clamp(tab, 0, CodexData.Tabs.Length - 1); }
        private void Awake()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo" }, 24);
            foreach (int size in new[] { 12, 14, 18, 22, 28, 40, 64 }) font.RequestCharactersInTexture(HudGlyphs.Phase5, size);
        }
        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (session && session.State != PhaseTwoSession.RunState.Paused) page = PausePage.Main;
            if (progress && progress.Current == StageProgress.Phase.Briefing && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) progress.Begin();
            if (session && session.State == PhaseTwoSession.RunState.Paused && page == PausePage.Codex) CodexModelPage.Keys(CodexModelPage.ForTab(codexTab), codexViewer);
            if (elapsed < 0.5f) return;
            fps = (frames / elapsed).ToString("F0") + " FPS";
            frames = 0; elapsed = 0;
        }

        public bool BlocksPointer(Vector3 screen)
        {
            Layout();
            var p = new Vector2((screen.x - offsetX) / scale, (Screen.height - screen.y - offsetY) / scale);
            return CommandBar.Contains(p) || (commander && commander.ClickOrders && commander.PendingRoutes.Count > 0 && LaunchPanel.Contains(p)) || new Rect(30, 24, 340, 80).Contains(p) || new Rect(560, 24, 690, 70).Contains(p) || new Rect(1030, 104, 220, 104).Contains(p)
                || (Guiding && TacticsGuide.Contains(p));
        }
        private void Layout()
        {
            scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            offsetX = (Screen.width - 1280 * scale) / 2; offsetY = (Screen.height - 720 * scale) / 2;
        }

        private void OnGUI()
        {
            if (!session || !commander || !colony || !progress) return;
            if (title == null)
            {
                title = Style(28); big = Style(22); text = Style(18); small = Style(14); tiny = Style(12); huge = Style(40); giant = Style(64); right = new GUIStyle(small) { alignment = TextAnchor.UpperRight };
                keycap = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
            }
            Matrix4x4 saved = GUI.matrix;
            Color savedColor = GUI.color;
            Layout();
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY), Quaternion.identity, Vector3.one * scale);
            GUI.Label(new Rect(1170, 4, 95, 22), fps, tiny);
            if (progress.Current == StageProgress.Phase.Briefing) Briefing();
            else if (session.State == PhaseTwoSession.RunState.Paused && colony.NestMenuOpen) NestScreen();
            else if (session.State == PhaseTwoSession.RunState.Paused) Pause();
            else if (session.State == PhaseTwoSession.RunState.Cleared) Clear();
            else if (!session.IsRunning) Defeat();
            else
            {
                if (commander.Mode == SwarmCommander.ViewMode.Swarm) Tactics(); else Normal();
                if (commander.NoticeVisible)
                {
                    float y = commander.Mode == SwarmCommander.ViewMode.Swarm ? 112 : 214;
                    float w = Mathf.Min(760, 60 + commander.Notice.Length * 19);
                    Panel(new Rect(640 - w / 2, y, w, 40), Lime);
                    GUI.Label(new Rect(640 - w / 2 + 18, y + 7, w - 30, 30), commander.Notice, text);
                }
            }
            // The first time a screen is drawn, IMGUI builds its text and glyph data: draw every screen once,
            // off-screen, during the first repaints so opening the tactics view or the nest later does not hitch.
            if (warmups < 2 && Event.current.type == EventType.Repaint)
            {
                warmups++;
                GUI.matrix = Matrix4x4.TRS(new Vector3(-4000, -4000), Quaternion.identity, Vector3.one);
                Tactics(); NestScreen(); Briefing(); Clear(); Defeat(); Normal();
                if (tutorial) { for (int s = 0; s <= (int)StageTutorial.Step.Done; s++) GuidePanel(GuideNormal, (StageTutorial.Step)s, false); }
                var keep = page;
                for (int i = 0; i < 3; i++) { page = (PausePage)i; Pause(); }
                for (int tab = 0; tab < CodexData.Tabs.Length; tab++) { codexTab = tab; CodexPage(new Rect(130, 70, 1020, 580)); }
                page = keep; codexTab = 0;
            }
            GUI.matrix = saved; GUI.color = savedColor;
        }

        // ---------------- Normal (third person) ----------------
        private void Normal()
        {
            var motor = session.Motor;
            var reaction = colony.Reaction;
            // World markers first, so the panels stay readable when a marker falls behind them.
            Marker(session.Nest.position + Vector3.up * 0.5f, "◇ 巣  " + Vector3.Distance(session.Player.position, session.Nest.position).ToString("F0") + "m", Lime, true);
            for (int i = 0; i < progress.ZoneCount; i++)
                if (Vector3.Distance(progress.Stage.zones[i].centre, session.Player.position) < 7) ZoneMarker(i, Camera.main);
            FoggerMarkers(Camera.main);
            foreach (var ally in commander.Allies)
                if (ally.IsCorpse && Vector3.Distance(ally.Position, session.Player.position) < 8)
                    Marker(ally.Position + Vector3.up * 0.4f, "× 仲間の死体", Muted, false);
                else if (ally.Stunned && Vector3.Distance(ally.Position, session.Player.position) < 12)
                    Marker(ally.Position + Vector3.up * 0.5f, "! ひっくり返り", Orange, false);
                else if (ally.Alive && Time.time - ally.CalledAt < 1.2f)
                    Marker(ally.Position + Vector3.up * 0.5f, "◆", Lime, false, null, 1 - (Time.time - ally.CalledAt) / 1.2f);
            foreach (var b in reaction.Baits)
                if (b.IsArmed && b.Identified && Vector3.Distance(b.transform.position, session.Player.position) < 9)
                    Marker(b.transform.position + Vector3.up * 0.45f, "! 毒餌", Orange, false);
            var napper = Pet;
            if (napper && napper.Current == PetCat.Phase.Nap && Vector3.Distance(napper.transform.position, session.Player.position) < 9)
                Marker(napper.transform.position + Vector3.up * 1.1f, "Zzz", Muted, false, null, 0.8f);
            var house = Life;
            if (house)
                for (int i = 0; i < house.MemberCount && i < session.HumanCount; i++)
                    if (session.HumanPresent(i) && house.Dozing(i) && Vector3.Distance(session.HumanAt(i).transform.position, session.Player.position) < 12)
                        Marker(house.Current(i).seat + Vector3.up * 2.4f, "Zzz", Muted, false, null, 0.8f);   // over the head on the sofa
            var cage = Cage;
            if (cage && cage.Count > 0 && Vector3.Distance(cage.transform.position, session.Player.position) < 14)
                Marker(cage.transform.position + Vector3.up * 1.6f, "◇ 虫かご  仲間 " + cage.Count + "匹", Lime, false);
            MissionPanel(new Rect(30, 28, 430, 176));
            if (progress.Current == StageProgress.Phase.FinalDefence) DefenceTimer();
            else ReactionPanel(new Rect(900, 28, 350, 92));

            Panel(new Rect(30, 528, 330, 155), Lime);
            GUI.Label(new Rect(48, 537, 290, 30), "生命力  " + session.Hp + " / " + session.MaximumHp, text);
            GUI.Label(new Rect(48, 569, 290, 30), "人間  " + HumanLabel(), text);
            Bar(new Rect(48, 604, 290, 7), Watcher.Detection, Orange);
            GUI.Label(new Rect(48, 620, 300, 26), "仲間 " + colony.Allies + "/" + colony.AllyCap + "   食料 " + colony.Food + "   巣 Lv" + colony.NestLevel, text);
            GUI.Label(new Rect(48, 650, 300, 24), AllySummary(), small);

            Panel(new Rect(385, 647, 865, 38), Lime);
            GUI.Label(new Rect(400, 655, 845, 28), Floors ? "WASD 移動  Shift 走る  E 回収・納品・巣  壁へ進んで登る  Space 降りる  F 呼ぶ  Tab 軍団  階段・ダクト 別の階  Esc ポーズ"
                : "WASD 移動  Shift 走る  E 回収・納品・巣  壁へ進んで登る  Space 降りる  F 仲間を呼ぶ  Tab 軍団  1-5 指示  Esc ポーズ", small);

            var interactor = session.Interactor;
            var food = interactor.NearbyFood;
            var bait = reaction.NearbyBait(session.Player.position, session.Settings.interactionRange);
            var corpse = progress.NearbyCorpse;
            string prompt = interactor.Carried ? (interactor.AtNest ? "E  巣に納品する（+" + FoodValue.AmountOf(interactor.Carried) + "）" : FoodValue.NameOf(interactor.Carried) + "を巣へ  ◇") :
                food ? "E  " + FoodValue.NameOf(food) + "を回収する（+" + FoodValue.AmountOf(food) + "）" :
                bait ? (bait.Identified ? "! 毒餌 — 拾わないで" : "E  食料を回収する") :
                interactor.AtNest ? "E  巣を開く（繁殖・強化・記録）" :
                corpse ? (session.Hp < session.MaximumHp ? "E  仲間の死体を食べる（生命力 +" + progress.Stage.corpseHeal + "）" : "仲間の死体  —  生命力は満タン") :
                cage && cage.Count > 0 && cage.PlayerNear ? "E  虫かごを開けて仲間を助ける（" + cage.Count + "匹）" :
                Floors && Floors.Hint(session.Player.position) != null ? Floors.Hint(session.Player.position) : ZoneHint();
            Panel(new Rect(430, 578, 420, 48), bait && bait.Identified ? Orange : Lime);
            GUI.Label(new Rect(450, 588, 385, 35), prompt, text);

            if (motor.IsClimbing) Banner((motor.SurfaceUp.y < -0.5f ? "▼ 天井を移動中" : "▲ 壁面を移動中") + "    Space で降りる", 266, Lime);
            else if (motor.ClimbEngage > 0.05f)
            {
                Panel(new Rect(490, 266, 300, 40), Lime);
                GUI.Label(new Rect(507, 272, 180, 30), "壁に張りつく…", text);
                Bar(new Rect(650, 284, 120, 7), motor.ClimbEngage, Lime);
            }
            if (session.IsSticky) Banner("粘着シート！  外に出ると速度が戻ります", 316, Orange);
            var spray = reaction.Spray;
            var smoke = SmokeNearPlayer();
            var watcher = Watcher;
            if (FamilyBanner()) { }
            else if (NestSmokeBanner()) { }
            else if (smoke) Banner(smoke.IsArmed ? (smoke.Contains(motor.BodyCentre) ? "煙の中！  輪の外か高い所・巣へ逃げろ" : "燻煙剤の煙  —  近づかないで") : "燻煙剤！  " + Mathf.CeilToInt(smoke.Remaining) + "秒で煙が広がる", 366, Orange);
            else if (spray.Telegraphing || spray.Spraying) Banner("殺虫スプレー！  物陰か橙の扇の外へ", 366, Orange);
            else if (progress.Current == StageProgress.Phase.Retreat) Banner("人間が部屋から逃げていく…！", 366, Lime);
            else if (watcher.State == HumanAIController.HumanState.Attack && !watcher.TargetIsPlayer)
                Banner("仲間が" + watcher.WeaponName + "に狙われている！", 366, Orange);
            else if (watcher.State == HumanAIController.HumanState.Attack)
                Banner(session.PlayerStrikeable ? (session.Motor.IsClimbing || session.Motor.FootPoint.y > 0.5f ? watcher.WeaponName + "注意！  ここも届く — 上へ逃げろ" : watcher.WeaponName + "注意！  橙の枠からダッシュで逃げろ") : watcher.WeaponName + "注意！  高い所なら届かない", 366, Orange);
            else if (reaction.CurrentAction == HumanReaction.Activity.Walking || reaction.CurrentAction == HumanReaction.Activity.Placing)
                Banner("人間が罠を仕掛けに行く…", 366, Orange);
            else if (watcher.State == HumanAIController.HumanState.Chase)
                Banner(watcher.TargetIsPlayer ? (People ? watcher.Label + "に見つかった！  家具の下か壁の上へ" : "見つかった！  家具の下か壁の上へ逃げよう")
                    : People ? "仲間が見つかった！  " + watcher.Label + "が追いかけている" : "仲間が見つかった！  人間が追いかけている", 366, Orange);
            else if (StartledNearby) Banner("物音に気づかれた！  止まるか物陰へ", 366, Orange);
            else if (Floors && StaffStatusBanner()) { }
            SpeechBubble();
            if (Guiding) { GuideTargets(Camera.main); GuidePanel(GuideNormal, tutorial.Current, true); }
            if (Cursor.lockState != CursorLockMode.Locked) Banner("クリックして操作を再開", 416, Lime);
        }

        // Mission card: the three gauges of spec 9.3, each with its number (colour is never the only cue).
        private void MissionPanel(Rect r)
        {
            bool final = progress.Current >= StageProgress.Phase.FinalDefence;
            Panel(r, final ? Orange : Lime);
            GUI.Label(new Rect(r.x + 18, r.y + 8, r.width - 30, 22), "STAGE " + StageNumber + "  /  " + progress.Stage.title + (Floors ? "  ・  " + Floors.FloorName(session.Player.position) : ""), small);
            Ink(title, final ? Orange : Cream, () => GUI.Label(new Rect(r.x + 18, r.y + 30, r.width - 30, 38),
                progress.Current == StageProgress.Phase.Retreat ? "人間が逃げていく！" : final ? "最終防衛を耐えろ" : "部屋を乗っ取れ", title));
            Gauge(new Rect(r.x + 18, r.y + 76, r.width - 36, 30), "◆ 食料確保", colony.TotalCollected + " / " + progress.Stage.foodGoal, progress.FoodGauge);
            Gauge(new Rect(r.x + 18, r.y + 108, r.width - 36, 30), "■ 縄張り", progress.CapturedZones + " / " + progress.ZoneCount + " 区域", progress.TerritoryGauge);
            Gauge(new Rect(r.x + 18, r.y + 140, r.width - 36, 30), "▲ 人間の恐怖", Mathf.FloorToInt(progress.FearGauge * 100) + "%", progress.FearGauge);
        }
        private void Gauge(Rect r, string label, string value, float amount)
        {
            bool full = amount >= 1;
            GUI.Label(new Rect(r.x, r.y, 130, 24), label, small);
            Bar(new Rect(r.x + 122, r.y + 9, r.width - 250, 7), amount, full ? Lime : new Color(0.86f, 0.80f, 0.55f));
            Ink(right, full ? Lime : Cream, () => GUI.Label(new Rect(r.xMax - 124, r.y, 124, 24), (full ? "✓ " : "") + value, right));
        }
        private void DefenceTimer()
        {
            var r = new Rect(900, 28, 350, 110);
            Panel(r, Orange);
            GUI.Label(new Rect(r.x + 18, r.y + 8, 300, 22), "反応段階 4  ―  駆除業者", small);
            Ink(huge, Orange, () => GUI.Label(new Rect(r.x + 18, r.y + 28, 200, 52), Mathf.CeilToInt(progress.DefenceRemaining) + "秒", huge));
            GUI.Label(new Rect(r.x + 170, r.y + 44, 170, 26), "耐えればクリア", text);
            Bar(new Rect(r.x + 18, r.y + 88, r.width - 36, 7), 1 - progress.DefenceRemaining / progress.Stage.finalDefenceSeconds, Orange);
        }
        // The exterminator smoking the nest outranks every other warning: it is the one that sends you running.
        private bool NestSmokeBanner()
        {
            switch (progress.NestFumigation)
            {
                case StageProgress.Fumigation.Approach: Banner("駆除業者が巣に向かっている！  燻される前に外へ", 366, Orange); return true;
                case StageProgress.Fumigation.Warning: Banner("巣が燻される！  あと " + Mathf.CeilToInt(progress.NestFumigationLeft) + "秒で外へ逃げろ", 366, Orange); return true;
                case StageProgress.Fumigation.Smoke:
                    if (session.InNest(session.Player.position)) { Banner("巣の中は煙！  すぐ外へ", 366, Orange); return true; }
                    Banner("巣は煙で使えない  あと " + Mathf.CeilToInt(progress.NestFumigationLeft) + "秒  ―  外を逃げ回れ", 366, Orange); return true;
                default: return SprayBanner();
            }
        }
        // The backpack sprayer aimed at a roach up high (final defence).
        private bool SprayBanner()
        {
            var spray = progress.Sprayer;
            if (!spray || progress.Current != StageProgress.Phase.FinalDefence) return false;
            if (spray.Targeting)
            {
                Banner(spray.Inside(session.Motor.FootPoint) ? "噴霧器で狙われている！  橙の円から這って逃げろ" : "噴霧器の霧  —  橙の円に入らないで", 366, Orange);
                return true;
            }
            if (spray.Current == CeilingSprayer.Phase.Idle && spray.HighTime > 1.2f) { Banner("業者が上を見ている…  高い所に居続けると噴霧器で狙われる", 366, Orange); return true; }
            return false;
        }
        private string ZoneHint()
        {
            if (progress.Current == StageProgress.Phase.FinalDefence) return "巣は燻され、高い所には噴霧器が来る";
            if (progress.AllGaugesFull) return "まもなく最終防衛…";
            if (progress.TerritoryGauge < 1)
            {
                int zone = progress.PlayerZone;
                if (zone < 0) return colony.Allies > 0 ? "5 占拠  —  仲間が「□」区域へ向かう" : "仲間を増やすと区域を占領できる（巣で E）";
                if (progress.ZoneContested(zone)) return "人間がいる間は占領が止まる";
                int need = progress.Stage.holdPresence - progress.ZonePresence(zone);
                if (need > 0) return colony.Allies > 0 ? "あと" + need + "匹  —  5 占拠／F で近くの仲間を呼ぶ" : "あと" + need + "匹  —  巣で仲間を増やそう";
                return "占領中…  " + Mathf.FloorToInt(progress.ZoneProgress(zone) * 100) + "%  そのまま待とう";
            }
            return progress.FoodGauge < 1 ? "食料を巣へ運ぼう" : "人間をもっと怖がらせよう（見られる・増える）";
        }
        private SmokeFogger SmokeNearPlayer()
        {
            foreach (var f in progress.Foggers)
                if (f.Active && Flat(session.Motor.FootPoint - f.transform.position).magnitude < f.Radius + 1.2f) return f;
            return null;
        }
        private void ZoneMarker(int i, Camera view)
        {
            var zone = progress.Stage.zones[i];
            bool done = progress.ZoneCaptured(i), contested = progress.ZoneContested(i);
            float rate = progress.ZoneRate(i);
            string state = done ? "占領済" : contested ? "人間がいる" : Mathf.FloorToInt(progress.ZoneProgress(i) * 100) + "%  " + progress.ZonePresence(i) + "/" + progress.Stage.holdPresence + "匹" + (rate > 1.01f ? "  ×" + rate.ToString("0.#") : "");
            Marker(zone.centre + Vector3.up * 0.25f, (done ? "■ " : "□ ") + zone.name + "  " + state, contested ? Orange : done ? Lime : Cream, false, view, done ? 0.8f : 1);
        }
        private void FoggerMarkers(Camera view)
        {
            foreach (var f in progress.Foggers)
                if (f.Active) Marker(f.transform.position + Vector3.up * 0.5f, f.IsArmed ? "! 燻煙剤  " + Mathf.CeilToInt(f.Remaining) + "秒" : "! 燻煙剤  まもなく", Orange, false, view);
        }
        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        private void ReactionPanel(Rect r)
        {
            var reaction = colony.Reaction;
            int level = (int)reaction.CurrentLevel;
            Color c = level >= 3 ? Orange : level == 2 ? new Color(1, 0.8f, 0.35f) : Cream;
            Panel(r, level >= 2 ? Orange : Lime);
            GUI.Label(new Rect(r.x + 18, r.y + 8, 140, 22), "人間の反応", small);
            GUI.color = c; GUI.Label(new Rect(r.x + 18, r.y + 30, 160, 32), HumanReaction.LevelName(reaction.CurrentLevel), big); GUI.color = Color.white;
            for (int i = 0; i < 4; i++) Fill(new Rect(r.x + 170 + i * 42, r.y + 40, 36, 10), i <= level ? c : new Color(0.25f, 0.27f, 0.25f));
            GUI.Label(new Rect(r.x + 18, r.y + 64, 80, 22), "恐怖度", tiny);
            Bar(new Rect(r.x + 70, r.y + 71, r.width - 90, 6), reaction.Fear / 100f, Orange);
        }

        private void SpeechBubble()
        {
            var reaction = colony.Reaction;
            if (!colony.Options.speechBubbles || !reaction.BubbleVisible || !Camera.main) return;
            var speaker = reaction.Speaker && reaction.Speaker.gameObject.activeInHierarchy ? reaction.Speaker : session.Human;
            if (!ToGui(speaker.transform.position + Vector3.up * (speaker.EyeHeight + 1.2f), Camera.main, out Vector2 p)) return;
            float w = 40 + reaction.Bubble.Length * 19;
            p.x = Mathf.Clamp(p.x, w / 2 + 20, 1260 - w / 2); p.y = Mathf.Clamp(p.y, 140, 520);
            var r = new Rect(p.x - w / 2, p.y - 42, w, 36);
            Fill(r, new Color(0.95f, 0.93f, 0.85f, 0.95f));
            Fill(new Rect(p.x - 5, r.yMax, 10, 6), new Color(0.95f, 0.93f, 0.85f, 0.95f));
            text.normal.textColor = new Color(0.08f, 0.08f, 0.07f);
            GUI.Label(new Rect(r.x + 18, r.y + 5, w - 20, 28), reaction.Bubble, text);
            text.normal.textColor = Cream;
        }

        // ---------------- Tactics view ----------------
        private void Tactics()
        {
            Camera view = Camera.main;
            var reaction = colony.Reaction;
            if (view)
            {
                foreach (var t in traps) if (t && t.gameObject.activeInHierarchy) Marker(t.transform.position, "! 粘着", Orange, false, view);
                foreach (var b in reaction.Baits)
                    if (b.IsArmed) Marker(b.transform.position + Vector3.up * 0.3f, b.Identified ? "! 毒餌" : "◆ 食料？", b.Identified ? Orange : Lime, false, view);
                for (int i = 0; i < commander.ScoutPoints.Count; i++)
                    if (!commander.IsScouted(i)) Marker(commander.ScoutPoints[i], "？", new Color(0.7f, 0.72f, 0.66f), false, view, 0.75f);
                for (int i = 0; i < commander.Foods.Count; i++)
                {
                    var food = commander.Foods[i];
                    if (!commander.IsDiscovered(i) || food.State != FoodItem.FoodState.Available) continue;
                    Marker(food.transform.position + Vector3.up * 0.3f, "◆ " + FoodValue.NameOf(food) + (SwarmCommander.OnFloor(food) ? "" : "・高所"), Lime, false, view);
                }
                Marker(session.Nest.position + Vector3.up * 0.4f, "◇ 巣", Lime, false, view);
                for (int i = 0; i < progress.ZoneCount; i++) ZoneMarker(i, view);
                FoggerMarkers(view);
                if (commander.HasRallyPoint) Marker(commander.RallyPoint, "◎ 目標", Cream, false, view);
                for (int h = 0; h < session.HumanCount; h++)
                {
                    if (!session.HumanPresent(h)) continue;
                    var human = session.HumanAt(h);
                    bool alarm = human.State != HumanAIController.HumanState.Routine || h == 0 && reaction.CurrentAction != HumanReaction.Activity.None;
                    Marker(human.transform.position + Vector3.up * (human.EyeHeight + 0.9f), "▲ " + (People ? human.Label : "人間") + "・" + (h == 0 ? ResidentText() : StateText(human)), alarm ? Orange : Cream, false, view);
                }
                var cage = Cage;
                if (cage && cage.Count > 0) Marker(cage.transform.position + Vector3.up * 1.4f, "◇ 虫かご " + cage.Count + "匹", Lime, false, view);
                var cat = Pet;
                if (cat && cat.gameObject.activeInHierarchy && cat.Current != PetCat.Phase.Gone)
                    Marker(cat.transform.position + Vector3.up * 1.9f, "▲ 猫・" + cat.Label, cat.Stalking ? Orange : Cream, false, view);
                Marker(session.Motor.FootPoint + Vector3.up * 0.6f, "◆ あなた", Lime, false, view);
                for (int i = 0; i < commander.Allies.Count; i++) AllyMarker(i, view);
            }
            if (view) RouteLines(view);
            if (commander.Dragging)
            {
                Rect r = commander.DragScreenRect;
                var box = new Rect((r.x - offsetX) / scale, (Screen.height - r.yMax - offsetY) / scale, r.width / scale, r.height / scale);
                Fill(box, new Color(Lime.r, Lime.g, Lime.b, 0.12f));
                Outline(box, Lime, 2);
            }
            bool final = progress.Current == StageProgress.Phase.FinalDefence;
            Panel(new Rect(30, 24, 340, 80), final ? Orange : Lime);
            title.normal.textColor = Lime;
            GUI.Label(new Rect(48, 30, 300, 40), "軍団モード", title);
            title.normal.textColor = Cream;
            if (final) Ink(text, Orange, () => GUI.Label(new Rect(196, 38, 170, 30), "最終防衛 " + Mathf.CeilToInt(progress.DefenceRemaining) + "秒", text));
            GUI.Label(new Rect(50, 72, 300, 24), final ? "燻煙剤から仲間を守る：3 防衛で巣へ" : "Tab で通常モードへ   ／   時間は止まりません", small);
            var gauges = new Rect(1030, 104, 220, 104);
            Panel(gauges, Lime);
            MiniGauge(new Rect(1048, 112, 190, 28), "◆ 食料", progress.FoodGauge);
            MiniGauge(new Rect(1048, 142, 190, 28), "■ 縄張り", progress.TerritoryGauge);
            MiniGauge(new Rect(1048, 172, 190, 28), "▲ 恐怖", progress.FearGauge);

            Panel(new Rect(560, 24, 690, 70), Lime);
            Stat(new Rect(582, 30, 120, 60), "仲間", colony.Allies + " / " + colony.AllyCap, Cream);
            Stat(new Rect(712, 30, 120, 60), "食料", colony.Food.ToString(), Lime);
            string danger = commander.DangerLabel;
            Stat(new Rect(842, 30, 130, 60), "▲ 反応", HumanReaction.LevelName(reaction.CurrentLevel), reaction.CurrentLevel >= HumanReaction.Level.Alert ? Orange : Cream);
            Stat(new Rect(982, 30, 120, 60), "■ 占領", progress.CapturedZones + " / " + progress.ZoneCount, progress.TerritoryGauge >= 1 ? Lime : Cream);
            Stat(new Rect(1112, 30, 130, 60), "？ 調査", commander.ScoutedCount + " / " + commander.ScoutPoints.Count, danger == "高" ? Orange : Cream);

            int chosen = commander.SelectedCount;
            GUI.Label(new Rect(34, 566, 1220, 24), (chosen > 0 ? "選択 " + chosen + "匹" : "選択なし＝全員") + "   左クリック: 仲間＝選ぶ/外す  食料・区域・床＝指示   ドラッグ: ルート  Enter 決定   Q 全員  X 解除  F 追従  Space 視点", small);
            for (int i = 0; i < Orders.Length; i++) OrderButton(OrderRect(i), i, commander.ActiveAllyCount > 0);
            // Routes wait for 決定: one button starts them all together (the decoys and the capture team at once).
            int waiting = 0;
            foreach (var route in commander.PendingRoutes) waiting += commander.PendingCount(route);
            if (waiting > 0)
            {
                Panel(LaunchPanel, Lime);
                if (Button(new Rect(LaunchPanel.x + 8, LaunchPanel.y + 6, 240, 44), "▶ 決定  ルート開始（" + waiting + "匹）", true, true)) commander.LaunchRoutes();
                if (Secondary(new Rect(LaunchPanel.x + 256, LaunchPanel.y + 6, 106, 44), "取消")) commander.CancelRoutes();
            }
            SpeechBubble();
            if (Guiding)
            {
                if (view) GuideTargets(view);
                GuidePanel(TacticsGuide, tutorial.Current, true);
                if (tutorial.Current == StageTutorial.Step.Capture || tutorial.Current == StageTutorial.Step.Territory && colony.Allies > 0) PointAt(OrderRect(CaptureButton), "ここをクリック！", true);
                else if (tutorial.Current == StageTutorial.Step.Food && colony.Allies > 0) PointAt(OrderRect(0), "ここをクリック！", true);
            }
        }
        private bool Guiding => tutorial && tutorial.Active && progress.Current >= StageProgress.Phase.Invasion && progress.Current <= StageProgress.Phase.Retreat;
        private Rect TacticsGuide => tutorial && (tutorial.Current == StageTutorial.Step.Capture || tutorial.Current == StageTutorial.Step.Territory) ? GuideCapture : GuideTactics;

        private void MiniGauge(Rect r, string label, float amount)
        {
            GUI.Label(new Rect(r.x, r.y, 80, 24), label, small);
            Bar(new Rect(r.x + 76, r.y + 9, 70, 7), amount, amount >= 1 ? Lime : new Color(0.86f, 0.80f, 0.55f));
            Ink(right, amount >= 1 ? Lime : Cream, () => GUI.Label(new Rect(r.x + 140, r.y, 50, 24), Mathf.FloorToInt(amount * 100) + "%", right));
        }

        private void OrderButton(Rect r, int index, bool enabled)
        {
            var order = Orders[index];
            Color accent = Lime;
            bool hover = enabled && r.Contains(Event.current.mousePosition);
            Fill(r, hover ? new Color(0.09f, 0.11f, 0.08f, 0.94f) : PanelColor);
            Fill(new Rect(r.x, r.y, 4, r.height), enabled ? accent : new Color(0.35f, 0.35f, 0.33f));
            if (commander.LastOrder == order) Outline(r, accent, 2);
            Color ink = enabled ? Cream : Muted;
            GUI.color = enabled ? accent : ink;
            GUI.Label(new Rect(r.x + 16, r.y + 18, 36, 40), OrderGlyph[index], title);
            GUI.color = Color.white;
            Ink(text, ink, () => GUI.Label(new Rect(r.x + 52, r.y + 12, r.width - 60, 30), OrderKeys[index] + "  " + SwarmCommander.OrderName(order), text));
            Ink(small, enabled ? new Color(0.78f, 0.77f, 0.7f) : ink, () => GUI.Label(new Rect(r.x + 52, r.y + 46, r.width - 58, 26), enabled ? OrderHelp[index] : "仲間がいません", small));
            if (enabled && GUI.Button(r, GUIContent.none, GUIStyle.none)) commander.Command(order);
        }

        // Drawn routes as dotted lines on the floor: the one being drawn (lime) and those allies are running (cream).
        private void RouteLines(Camera view)
        {
            foreach (var route in commander.ActiveRoutes) Dotted(route.Points, view, new Color(Cream.r, Cream.g, Cream.b, 0.8f), route.EndLabel);
            foreach (var route in commander.PendingRoutes) Dotted(route.Points, view, Lime, "決定待ち " + commander.PendingCount(route) + "匹・" + route.EndLabel);
            if (commander.DrawingRoute) Dotted(commander.RouteDraft, view, Lime, null);
        }
        private void Dotted(System.Collections.Generic.IReadOnlyList<Vector3> points, Camera view, Color color, string end)
        {
            for (int i = 1; i < points.Count; i++)
            {
                if (!ToGui(points[i - 1], view, out Vector2 a) || !ToGui(points[i], view, out Vector2 b)) continue;
                int dots = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 9));
                for (int k = 0; k < dots; k++) { Vector2 p = Vector2.Lerp(a, b, k / (float)dots); Fill(new Rect(p.x - 2, p.y - 2, 4, 4), color); }
            }
            if (end != null && points.Count > 0 && ToGui(points[points.Count - 1], view, out Vector2 e))
            {
                Fill(new Rect(e.x - 5, e.y - 5, 10, 10), color);
                Shadowed(new Rect(e.x + 9, e.y - 10, 240, 20), "→ " + end, tiny, color);
            }
        }

        private void AllyMarker(int index, Camera view)
        {
            var ally = commander.Allies[index];
            if (!ally.gameObject.activeSelf || !ToGui(ally.Position + Vector3.up * 0.3f, view, out Vector2 p)) return;
            bool down = ally.State == AllyRoach.AllyState.Disabled;
            bool picked = commander.IsSelected(index);
            Color dot = down ? new Color(0.45f, 0.45f, 0.42f) : picked ? Lime : Cream;
            if (ally.IsDead) { Shadowed(new Rect(p.x - 6, p.y - 12, 90, 20), "× 死体", tiny, Muted); return; }
            if (picked) Brackets(new Rect(p.x - 11, p.y - 11, 22, 22), Lime);
            Fill(new Rect(p.x - 4, p.y - 4, 8, 8), new Color(0, 0, 0, 0.7f));
            Fill(new Rect(p.x - 3, p.y - 3, 6, 6), dot);
            if (ally.Carried) Fill(new Rect(p.x + 4, p.y - 9, 5, 5), Orange);
            Shadowed(new Rect(p.x + 12, p.y - 9, 90, 20), down ? "× " + ally.Label : ally.Label, tiny, ally.State == AllyRoach.AllyState.FleeHazard ? Orange : dot);
        }

        // ---------------- Nest screen ----------------
        private void NestScreen()
        {
            Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, 0.55f));
            Panel(new Rect(30, 24, 560, 92), Lime);
            title.normal.textColor = Lime;
            GUI.Label(new Rect(52, 32, 80, 40), "巣", title);
            title.normal.textColor = Cream;
            GUI.Label(new Rect(96, 44, 120, 24), "ネスト", small);
            GUI.Label(new Rect(52, 76, 520, 28), "食料で仲間を産み、巣を広げ、コロニーを強くしよう", text);
            Panel(new Rect(930, 24, 320, 70), Lime);
            GUI.Label(new Rect(952, 30, 120, 24), "◆ 食料", small);
            GUI.color = Lime; GUI.Label(new Rect(952, 50, 200, 40), colony.Food.ToString(), title); GUI.color = Color.white;
            GUI.Label(new Rect(1060, 62, 180, 24), "合計回収 " + colony.TotalCollected, small);

            // Colony card
            var card = new Rect(30, 132, 300, 428);
            Panel(card, Lime);
            GUI.Label(new Rect(50, 146, 260, 26), "コロニー", big);
            GUI.Label(new Rect(50, 186, 260, 22), "仲間", small);
            GUI.Label(new Rect(50, 206, 260, 44), colony.Allies + " / " + colony.AllyCap + " 匹", title);
            Bar(new Rect(50, 254, 260, 7), colony.Allies / (float)colony.AllyCap, Lime);
            GUI.Label(new Rect(50, 264, 260, 22), "仲間が多いほど占領が早く、評価も上がる", tiny);
            Row(new Rect(50, 300, 260, 28), "巣の規模", "Lv " + colony.NestLevel + " / " + colony.MaxNestLevel);
            Row(new Rect(50, 334, 260, 28), "生命力", session.Hp + " / " + session.MaximumHp);
            Row(new Rect(50, 368, 260, 28), "人間の反応", HumanReaction.LevelName(colony.Reaction.CurrentLevel));
            Row(new Rect(50, 402, 260, 28), "仲間を増やす費用", "食料 " + colony.BreedCost);
            GUI.Label(new Rect(50, 450, 270, 90), "仲間が増えるほど人間は怖がり、\n罠やスプレーで対抗してくる。\n外で F を押すと仲間を呼べる。\n進行は自動で記録される。", small);

            // Upgrades
            var ups = colony.Settings.upgrades;
            for (int i = 0; i < ups.Length; i++) UpgradeRow(new Rect(346, 132 + i * 86, 904, 80), i, ups[i]);

            // Actions
            bool full = colony.Allies >= colony.AllyCap;
            ActionCard(new Rect(30, 574, 400, 96), "仲間を増やす", full ? (colony.NestLevel < colony.MaxNestLevel ? "巣がいっぱい — 拡張が必要" : "上限に達しました") : "新しい仲間を1匹産む",
                "食料 " + colony.BreedCost, !full && colony.Food >= colony.BreedCost, !full, () => colony.TryBreed());
            bool maxNest = colony.NestLevel >= colony.MaxNestLevel;
            int nextCap = maxNest ? colony.AllyCap : colony.Settings.allyCapByNestLevel[colony.NestLevel];
            ActionCard(new Rect(440, 574, 400, 96), "巣を拡張", maxNest ? "最大まで広がっています" : "仲間の上限 " + colony.AllyCap + " → " + nextCap + "匹",
                maxNest ? "最大" : "食料 " + colony.Settings.expandCost, !maxNest && colony.Food >= colony.Settings.expandCost, !maxNest, () => colony.TryExpand());
            ActionCard(new Rect(850, 574, 400, 96), "施設の建設", "今後のステージで解放", "準備中", false, false, null);

            GUI.Label(new Rect(34, 684, 430, 26), progress.Current == StageProgress.Phase.Invasion ? "Esc  閉じて出撃（閉じると記録されます）" : "Esc  閉じて出撃（最終防衛中は記録しません）", small);
            if (GUI.Button(new Rect(30, 680, 300, 34), GUIContent.none, GUIStyle.none)) colony.CloseNestMenu();
            if (colony.MessageVisible) Shadowed(new Rect(470, 684, 780, 26), colony.Message, text, colony.Message.Contains("足りません") || colony.Message.Contains("いっぱい") ? Orange : Lime);
            if (Guiding && (tutorial.Current == StageTutorial.Step.Breed || tutorial.Current == StageTutorial.Step.Territory && colony.Allies < 2))
            {
                bool afford = colony.Food >= colony.BreedCost;
                NestGuide(afford ? "「仲間を増やす」をクリック" : "食料が足りない — Esc で閉じて集めよう",
                    afford ? "食料 " + colony.BreedCost + " で仲間が1匹生まれる" : "あと " + (colony.BreedCost - colony.Food) + " 集めよう（巣で E を押すと開く）");
                if (afford) PointAt(new Rect(30, 574, 400, 96), "ここをクリック！", false);
                else PointAt(new Rect(30, 680, 300, 34), "ここで閉じる", false);
            }
        }

        private void UpgradeRow(Rect r, int index, ColonySettings.Upgrade up)
        {
            int level = colony.LevelAt(index);
            bool maxed = colony.IsMaxed(index);
            int cost = colony.CostAt(index);
            bool afford = !maxed && colony.Food >= cost;
            Panel(r, Lime);
            GUI.color = Lime; GUI.Label(new Rect(r.x + 22, r.y + 18, 44, 44), UpgradeGlyph[Mathf.Min(index, UpgradeGlyph.Length - 1)], title); GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 74, r.y + 8, 240, 30), up.name, big);
            GUI.Label(new Rect(r.x + 214, r.y + 14, 80, 24), "Lv " + level, text);
            for (int i = 0; i < up.maxLevel; i++) Fill(new Rect(r.x + 300 + i * 40, r.y + 20, 34, 10), i < level ? Lime : new Color(0.25f, 0.27f, 0.25f));
            GUI.Label(new Rect(r.x + 74, r.y + 44, 560, 26), up.description, small);
            Ink(small, afford ? Cream : maxed ? Muted : Orange, () => GUI.Label(new Rect(r.x + 600, r.y + 28, 110, 26), maxed ? "最大" : "食料 " + cost, small));
            if (Button(new Rect(r.x + 716, r.y + 18, 170, 44), maxed ? "最大" : "強化する", afford, !maxed)) colony.TryUpgrade(index);
        }

        private void ActionCard(Rect r, string name, string detail, string cost, bool afford, bool available, System.Action act)
        {
            bool hover = available && r.Contains(Event.current.mousePosition);
            Fill(r, hover ? new Color(0.09f, 0.11f, 0.08f, 0.95f) : PanelColor);
            Fill(new Rect(r.x, r.y, 4, r.height), available ? (afford ? Lime : Orange) : new Color(0.35f, 0.35f, 0.33f));
            if (hover && Event.current.type == EventType.Repaint && Input.GetMouseButton(0)) Fill(r, new Color(0, 0, 0, 0.25f));
            Ink(big, available ? Cream : Muted, () => GUI.Label(new Rect(r.x + 22, r.y + 12, 260, 32), name, big));
            Ink(small, available ? new Color(0.78f, 0.77f, 0.7f) : Muted, () => GUI.Label(new Rect(r.x + 22, r.y + 52, 360, 26), detail, small));
            Ink(text, !available ? Muted : afford ? Lime : Orange, () => GUI.Label(new Rect(r.x + 280, r.y + 14, 110, 28), cost, text));
            if (available && act != null && Clicked(r, true)) act();
        }

        // ---------------- Pause, settings, encyclopedia ----------------
        private void Pause()
        {
            Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, 0.45f));
            var r = new Rect(330, 120, 620, Floors && page == PausePage.Main ? 510 : 480);
            Panel(r, Lime);
            if (page == PausePage.Settings) { SettingsPage(r); return; }
            if (page == PausePage.Codex) { CodexPage(new Rect(130, 70, 1020, 580)); return; }
            GUI.Label(new Rect(370, 140, 500, 45), "ポーズ", title);
            GUI.Label(new Rect(370, 188, 540, 28), "人間も仲間も止まっています。", text);
            if (Button(new Rect(370, 226, 540, 50), "探索を再開  [Esc]", true, true)) session.Resume();
            if (Button(new Rect(370, 284, 540, 50), "設定", true, true)) page = PausePage.Settings;
            if (Button(new Rect(370, 342, 540, 50), "図鑑", true, true)) page = PausePage.Codex;
            if (Button(new Rect(370, 400, 540, 50), "ステージを最初からやり直す", true, true)) progress.RestartStage();
            if (Button(new Rect(370, 458, 540, 50), "タイトルへ戻る（つづきから再開できます）", true, true)) { progress.SaveOnLeave(); GameFlow.ToTitle(); }
            if (tutorial && tutorial.Active && Secondary(new Rect(370, 516, 540, 44), "ガイドを閉じる")) { tutorial.Skip(); session.Resume(); }
            else if (tutorial && !tutorial.Active && progress.Stage.tutorial && Secondary(new Rect(370, 516, 540, 44), "ガイドを表示する（今やること・次にやること）")) { tutorial.Show(); session.Resume(); }
            GUI.Label(new Rect(370, 568, 540, 26), "Tab 軍団  1回収 2探索 3防衛 5占拠  F 呼ぶ(長押し 全員)  E(巣) 繁殖", small);
            if (Floors) GUI.Label(new Rect(370, 594, 540, 26), "階段・ダクトに入ると別の階へ（仲間も通れる）", small);
        }

        private void SettingsPage(Rect r)
        {
            var o = colony.Options;
            GUI.Label(new Rect(370, 140, 500, 45), "設定", title);
            bool changed = false;
            changed |= Stepper(new Rect(370, 194, 540, 44), "マウス感度", ref o.mouseSensitivity, 0.5f, 5f, 0.25f, o.mouseSensitivity.ToString("F2"));
            changed |= Stepper(new Rect(370, 242, 540, 44), "音量", ref o.masterVolume, 0, 1, 0.1f, Mathf.RoundToInt(o.masterVolume * 100) + "%");
            changed |= Choice(new Rect(370, 290, 540, 44), "画質", ref o.quality, "低（軽い）", "中（影あり）");
            int bubbles = o.speechBubbles ? 1 : 0, shake = o.cameraShake ? 1 : 0, screen = o.fullscreen ? 1 : 0;
            if (Choice(new Rect(370, 338, 540, 44), "人間の吹き出し", ref bubbles, "オフ", "オン")) { o.speechBubbles = bubbles == 1; changed = true; }
            if (Choice(new Rect(370, 386, 540, 44), "画面の揺れ", ref shake, "オフ", "オン")) { o.cameraShake = shake == 1; changed = true; }
            if (Choice(new Rect(370, 434, 540, 44), "画面", ref screen, "ウィンドウ", "全画面")) { o.fullscreen = screen == 1; o.ApplyScreen(); changed = true; }
            if (changed) colony.SaveOptions();
            GUI.Label(new Rect(370, 486, 540, 24), "変更はすぐ反映・保存されます。音量は効果音全体に効きます。", small);
            if (Button(new Rect(370, 522, 540, 56), "戻る", true, true)) page = PausePage.Main;
        }

        private void CodexPage(Rect r)
        {
            Fill(r, PanelColor); Fill(new Rect(r.x, r.y, 3, r.height), Lime);
            GUI.Label(new Rect(r.x + 30, r.y + 18, 300, 45), "図鑑", title);
            for (int i = 0; i < CodexData.Tabs.Length; i++)
                if (Tab(new Rect(r.x + 200 + i * 150, r.y + 24, 140, 40), CodexData.Tabs[i], codexTab == i)) codexTab = i;
            var book = CodexModelPage.ForTab(codexTab);   // the human and food tabs show models
            if (book != null) CodexModelPage.Draw(book, r, colony.Options, codexViewer, big, text, small);
            var entries = book != null ? System.Array.Empty<CodexData.Entry>() : CodexData.Entries[codexTab];
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                bool known = e.id == null || colony.Options.Knows(e.id);
                var row = new Rect(r.x + 30, r.y + 84 + i * 84, r.width - 60, 76);
                Fill(row, new Color(0.06f, 0.07f, 0.065f, 0.95f));
                Fill(new Rect(row.x, row.y, 3, row.height), known ? Lime : new Color(0.35f, 0.35f, 0.33f));
                Ink(big, known ? Cream : Muted, () => GUI.Label(new Rect(row.x + 20, row.y + 8, 300, 30), known ? e.name : "？？？", big));
                Ink(small, known ? new Color(0.8f, 0.79f, 0.72f) : Muted, () => GUI.Label(new Rect(row.x + 20, row.y + 42, row.width - 40, 26), known ? e.body : "まだ出会っていない", small));
            }
            if (Button(new Rect(r.xMax - 250, r.yMax - 70, 220, 50), "戻る", true, true)) page = PausePage.Main;
        }

        // ---------------- Mission briefing (screen 03) ----------------
        private static readonly string[] Dangers =
        {
            "スリッパ  ―  橙の枠から逃げる。台・低い壁も届く", "粘着シート  ―  踏むと大きく減速", "毒餌  ―  青緑の粒。仲間の探索で見破る",
            "殺虫スプレー  ―  パニック中。物陰に隠れる", "燻煙剤  ―  最終防衛。輪の外・高い所へ。巣も燻される"
        };
        // Stage 3 (町中華): the owner-cook's ladle swats like the slipper; the floors are joined by the stairs and a duct.
        private static readonly string[] DinerDangers =
        {
            "店主のお玉  ―  橙の枠から逃げる。台・低い壁も届く", "店主の熱湯  ―  橙の帯に流れる。帯から横へ",
            "おかみさんのお盆  ―  横長の橙の四角。前後へ逃げる", "アルコールスプレー  ―  浴びると数秒遅くなる",
            "バイトのデッキブラシ  ―  橙の四角ごと掃き飛ばす", "濡れた床（モップがけ）  ―  滑って止まれない",
            "罠・毒餌・殺虫剤・燻煙剤  ―  店主が使う"
        };
        // Stage 2: the father keeps the Stage 1 tools; the mother and son bring their own.
        private static readonly string[] HouseDangers =
        {
            "父のスリッパ  ―  橙の枠から逃げる。台・低い壁も届く", "母の掃除機  ―  橙の扇に吸い込まれる。横へ逃げる",
            "息子の虫取り網  ―  捕まったら Space 連打。仲間は虫かごへ", "罠・毒餌・スプレー  ―  父が警戒・パニックで使う",
            "飼い猫  ―  動くものに飛びかかる。止まればやり過ごせる", "家族の生活  ―  落ち着いていれば生活中。走ると気づく",
            "燻煙剤  ―  最終防衛。家族は避難、業者が来る"
        };
        private void Briefing()
        {
            var stage = progress.Stage;
            Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, 0.62f));
            var left = new Rect(50, 48, 720, 560);
            Panel(left, Lime);
            Ink(small, Lime, () => GUI.Label(new Rect(78, 64, 400, 22), "MISSION  ―  STAGE " + StageNumber, small));
            GUI.Label(new Rect(76, 88, 680, 56), stage.title, huge);
            GUI.Label(new Rect(78, 146, 680, 28), stage.description, text);
            GUI.Label(new Rect(78, 196, 400, 26), "目的  ―  3つのゲージを100%にする", big);
            Objective(new Rect(78, 234, 670, 62), "◆", "食料確保", "食料を合計 " + stage.foodGoal + " 巣へ運ぶ（種類で価値が違う）");
            Objective(new Rect(78, 302, 670, 62), "■", "縄張り", stage.zones.Length + "つの区域に " + stage.holdPresence + "匹以上で " + stage.holdSeconds.ToString("F0") + "秒（あなたも1匹）。5 占拠で仲間を送る");
            Objective(new Rect(78, 370, 670, 62), "▲", "人間の恐怖", "見られる・仲間を増やす・食料を奪う・占領するとたまる");
            Fill(new Rect(78, 446, 664, 2), new Color(0.3f, 0.32f, 0.28f));
            Ink(big, Orange, () => GUI.Label(new Rect(78, 458, 680, 30), "最終防衛  ―  駆除業者の燻煙剤を " + stage.finalDefenceSeconds.ToString("F0") + "秒 耐える", big));
            GUI.Label(new Rect(78, 490, 680, 26), stage.startAllies == 0 ? "最初はあなた1匹だけ。食料を巣へ運んで仲間を増やそう。" : "主人公がやられても、仲間がいれば後を継げる。耐えきれば人間は部屋を出ていく。", small);
            GUI.Label(new Rect(78, 514, 680, 26), stage.allyCorpseSeconds > 0 ? "やられた仲間は戻らない。死体を食べると生命力 +" + stage.corpseHeal + "。あなたがやられたら仲間が後を継ぐ。" : "進行は自動で記録される。タイトルの「つづきから」で再開できる。", small);
            if (stage.allyCorpseSeconds > 0) GUI.Label(new Rect(78, 538, 680, 26), "進行は自動で記録される。タイトルの「つづきから」で再開できる。", small);
            GUI.Label(new Rect(78, 566, 680, 26), "人間 " + stage.residents + "人   ／   難易度 " + Stars(stage.difficulty), small);

            var right = new Rect(790, 48, 440, 560);
            Panel(right, Orange);
            GUI.Label(new Rect(816, 64, 300, 30), "危険", big);
            var dangers = Floors ? DinerDangers : Household ? HouseDangers : Dangers;
            float step = dangers.Length > 6 ? 34 : dangers.Length > 5 ? 38 : 46;
            for (int i = 0; i < dangers.Length; i++)
            {
                Fill(new Rect(816, 110 + i * step, 6, 6), Orange);
                GUI.Label(new Rect(832, 100 + i * step, 390, 26), dangers[i], small);
            }
            GUI.Label(new Rect(816, 350, 300, 30), "操作", big);
            GUI.Label(new Rect(816, 388, 400, 200), "WASD 移動   Shift 走る   E 回収・納品・巣・死体\n壁へ進み続けて登る   Space 降りる\nTab 軍団モード（時間は止まらない）\n1 回収  2 探索  3 防衛  5 占拠\nF 見える仲間を呼ぶ（長押し 全員）\n軍団: 左クリック 選ぶ・指示  ドラッグ ルート（Enter 決定）\nEsc ポーズ" + (Household ? "\nSpace 連打  虫取り網から脱出" : "") + (Floors ? "\n階段・ダクトに入ると別の階へ" : ""), small);

            bool resume = GameFlow.ResumeRequested && progress.HasCheckpoint;
            if (resume)
            {
                var c = progress.Save.checkpoint;
                Panel(new Rect(50, 618, 720, 34), Lime);
                GUI.Label(new Rect(70, 624, 700, 26), "つづきから  ―  " + c.savedAt + " の記録（仲間 " + c.allies + "匹・食料 " + c.food + "・占領 " + CountCaptured(c) + "区域）", small);
            }
            if (Button(new Rect(790, 624, 290, 58), resume ? "▶  再開する  [Enter]" : "▶  出撃  [Enter]", true, true)) progress.Begin();
            if (Secondary(new Rect(1090, 624, 140, 58), "タイトルへ")) GameFlow.ToTitle();
        }
        private int CountCaptured(SaveData.Checkpoint c)
        {
            int n = 0; foreach (float z in c.zones) if (z >= progress.Stage.holdSeconds) n++; return n;
        }
        private void Objective(Rect r, string glyph, string name, string detail)
        {
            Fill(r, new Color(0.06f, 0.07f, 0.065f, 0.95f));
            Fill(new Rect(r.x, r.y, 3, r.height), Lime);
            Ink(title, Lime, () => GUI.Label(new Rect(r.x + 16, r.y + 12, 40, 40), glyph, title));
            GUI.Label(new Rect(r.x + 60, r.y + 6, 300, 28), name, big);
            GUI.Label(new Rect(r.x + 60, r.y + 34, r.width - 70, 24), detail, small);
        }
        private static string Stars(int n) { var sb = new System.Text.StringBuilder(); for (int i = 0; i < 5; i++) sb.Append(i < n ? "★" : "☆"); return sb.ToString(); }

        // ---------------- Stage clear (screen 12) ----------------
        private void Clear()
        {
            var stage = progress.Stage;
            Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, 0.35f));
            Fill(new Rect(40, 70, 690, 150), new Color(0.025f, 0.035f, 0.035f, 0.82f));
            Fill(new Rect(40, 70, 690, 5), Orange);
            Shadowed(new Rect(70, 82, 660, 80), "ステージクリア！", giant, Orange);
            Shadowed(new Rect(76, 170, 640, 34), Household ? "この家は俺たちのものだ！" : Floors ? "この店は俺たちのものだ！" : "キッチンは俺たちのものだ！", big, Cream);

            var stats = new Rect(800, 52, 440, 440);
            Panel(stats, Lime);
            ResultRow(new Rect(822, 70, 400, 56), "■", "支配率", Mathf.FloorToInt(progress.TerritoryGauge * 100) + "%");
            ResultRow(new Rect(822, 132, 400, 56), "◆", "回収した食料", colony.TotalCollected.ToString());
            ResultRow(new Rect(822, 194, 400, 56), "♥", "増えた仲間", colony.Born + "匹");
            ResultRow(new Rect(822, 256, 400, 56), "◎", "発見回数", session.Discoveries + "回");
            ResultRow(new Rect(822, 318, 400, 56), "»", "時間", Clock(session.RunSeconds));
            Fill(new Rect(822, 384, 400, 92), new Color(0.06f, 0.07f, 0.065f, 0.95f));
            GUI.Label(new Rect(842, 410, 200, 30), "★ 評価", big);
            Shadowed(new Rect(1120, 386, 90, 90), progress.Rating, giant, progress.Rating == "A" ? Lime : progress.Rating == "B" ? Cream : Orange);
            GUI.Label(new Rect(842, 444, 260, 24), "得点 " + progress.RatingPoints + "（A 6以上・B 3以上）" + (progress.Successors > 0 ? "  後継 " + progress.Successors : ""), tiny);

            var unlock = new Rect(800, 504, 440, 92);
            Panel(unlock, Orange);
            if (progress.NewStageUnlocked || progress.NewRewardUnlocked)
            {
                Ink(small, Orange, () => GUI.Label(new Rect(822, 512, 80, 22), "NEW!", small));
                if (progress.NewStageUnlocked) GUI.Label(new Rect(822, 534, 410, 28), "新ステージ解放  「" + stage.unlocksTitle + "」" + (stage.unlocks == "stage02" || stage.unlocks == "stage03" ? "" : "（制作中）"), text);
                if (progress.NewRewardUnlocked) GUI.Label(new Rect(822, 564, 410, 28), stage.rewardTitle + " ― ステージ選択で切替", small);
            }
            else
            {
                var record = progress.Save.Record(stage.id);
                GUI.Label(new Rect(822, 516, 410, 28), "ベスト評価 " + (record != null ? record.bestRating : progress.Rating) + "    ベストタイム " + Clock(record != null ? record.bestSeconds : session.RunSeconds), text);
                GUI.Label(new Rect(822, 550, 410, 28), progress.ClearSaved ? "結果を保存しました" : "結果を保存できませんでした", small);
            }

            if (Button(new Rect(40, 624, 330, 60), "▶  ステージ選択へ", true, true)) GameFlow.ToTitle(GameFlow.TitlePage.StageSelect);
            if (Secondary(new Rect(390, 624, 200, 60), "リプレイ  [R]")) progress.RestartStage();
            if (Secondary(new Rect(610, 624, 200, 60), "タイトルへ")) GameFlow.ToTitle();
        }
        private void ResultRow(Rect r, string glyph, string label, string value)
        {
            Fill(r, new Color(0.06f, 0.07f, 0.065f, 0.95f));
            Ink(big, Lime, () => GUI.Label(new Rect(r.x + 16, r.y + 12, 36, 32), glyph, big));
            GUI.Label(new Rect(r.x + 58, r.y + 14, 200, 30), label, text);
            var style = new GUIStyle(title) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(r.x + 200, r.y, r.width - 216, r.height), value, style);
        }
        private static string Clock(float seconds) { int s = Mathf.FloorToInt(seconds); return (s / 60) + ":" + (s % 60).ToString("00"); }

        // ---------------- Game over (screen 11) ----------------
        private void Defeat()
        {
            Fill(new Rect(0, 0, 1280, 720), new Color(0.05f, 0, 0, 0.55f));
            var r = new Rect(340, 110, 600, 440);
            Panel(r, Orange);
            Shadowed(new Rect(370, 128, 540, 60), "やられた…", huge, Orange);
            GUI.Label(new Rect(372, 190, 540, 28), "生き残った仲間 " + colony.Allies + "匹   占領 " + progress.CapturedZones + " / " + progress.ZoneCount + "   食料 " + colony.TotalCollected + " / " + progress.Stage.foodGoal, text);
            GUI.Label(new Rect(372, 220, 540, 26), progress.Current == StageProgress.Phase.FinalDefence ? "最終防衛中  ―  残り " + Mathf.CeilToInt(progress.DefenceRemaining) + "秒" : "時間 " + Clock(session.RunSeconds) + "   発見 " + session.Discoveries + "回", small);
            float y = 262;
            if (progress.CanSucceed)
            {
                if (Button(new Rect(370, y, 540, 56), "仲間に後を継がせる（仲間 -1、進行はそのまま）", true, true)) progress.Succeed();
                y += 66;
            }
            else { GUI.Label(new Rect(372, y + 8, 540, 26), "後を継げる仲間がいない…", small); y += 44; }
            if (progress.HasCheckpoint)
            {
                if (Secondary(new Rect(370, y, 540, 56), "記録から再開（" + progress.Save.checkpoint.savedAt + "）")) progress.RestoreCheckpoint();
                y += 66;
            }
            if (Secondary(new Rect(370, y, 540, 56), "ステージを最初から  [R]")) progress.RestartStage();
            y += 66;
            if (Secondary(new Rect(370, y, 540, 56), "タイトルへ")) GameFlow.ToTitle();
        }

        // ---------------- Tutorial guide ----------------
        private float Pulse => 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);

        // Short names for the "つぎ" line and the step list.
        private static string StepTitle(StageTutorial.Step step)
        {
            switch (step)
            {
                case StageTutorial.Step.Move: return "動いてみる";
                case StageTutorial.Step.PickUp: return "食料を拾う";
                case StageTutorial.Step.Deliver: return "巣へ運ぶ";
                case StageTutorial.Step.Breed: return "仲間を増やす";
                case StageTutorial.Step.OpenSwarm: return "軍団モードを開く";
                case StageTutorial.Step.Capture: return "占拠を命じる";
                case StageTutorial.Step.Hold: return "区域を占領する";
                case StageTutorial.Step.Territory: return "残りの区域も占領";
                case StageTutorial.Step.Food: return "食料を30集める";
                case StageTutorial.Step.Fear: return "人間を怖がらせる";
                case StageTutorial.Step.Defence: return "最終防衛を耐える";
                default: return "乗っ取り成功";
            }
        }

        // One step: header (step count and a dot per step), title, two lines of body, the key(s) to press as
        // keycaps, and the step that comes after this one.
        private void GuidePanel(Rect r, StageTutorial.Step step, bool live)
        {
            string head, body, keys = null, after = "";
            switch (step)
            {
                case StageTutorial.Step.Move: head = "まずは動いてみよう"; body = "マウスで視点、Shift で走る。\n人間に見つからないように進もう。"; keys = "W A S D"; after = "で移動"; break;
                case StageTutorial.Step.PickUp: head = "食料を拾おう"; body = "床の「◆ 食料」に近づいて E。\n高い所の食料は壁を登って取る。"; keys = "E"; after = "食料の近くで押す"; break;
                case StageTutorial.Step.Deliver: head = "巣へ運ぼう"; body = "くわえたまま「◇ 巣」へ戻り、\nE で納品。◆食料確保のゲージが増える。"; keys = "E"; after = "巣の上で押す"; break;
                case StageTutorial.Step.Breed:
                {
                    int lack = Mathf.Max(0, colony.BreedCost - colony.Food);
                    head = "仲間を増やそう";
                    body = lack > 0 ? "巣で食料 " + colony.BreedCost + " を使うと仲間が1匹生まれる。\nあと " + lack + " 集めよう。" : "巣で E を押して、\n「仲間を増やす」をクリック。";
                    if (lack == 0) { keys = "E"; after = "巣の上で押す"; }
                    break;
                }
                case StageTutorial.Step.OpenSwarm: head = "軍団モードを開こう"; body = "部屋を見下ろして仲間に指示できる。\n時間は止まらないので人間に注意。"; keys = "Tab"; after = "を押してみよう！"; break;
                case StageTutorial.Step.Capture: head = "占拠を命じよう"; body = "仲間が「□」区域へ向かってとどまる。\n区域を左クリックしても同じ。"; keys = "5"; after = commander.Mode == SwarmCommander.ViewMode.Swarm ? "を押すか、ボタンをクリック" : "を押してみよう！"; break;
                case StageTutorial.Step.Hold:
                    head = "区域を占領しよう"; body = HoldText();
                    if (HoldNeedsAllies) { keys = "F"; after = "で見える仲間を呼ぶ（長押し 全員）"; }
                    break;
                case StageTutorial.Step.Territory:
                {
                    int left = progress.ZoneCount - progress.CapturedZones;
                    head = "残りの区域も占領しよう";
                    body = colony.Allies < 2 ? "占拠には仲間が要る。巣で仲間を増やそう。\n残り " + left + " 区域（■ " + progress.CapturedZones + " / " + progress.ZoneCount + "）"
                        : "仲間が2匹ずつ「□」区域へ向かう。\n残り " + left + " 区域（■ " + progress.CapturedZones + " / " + progress.ZoneCount + "）";
                    if (colony.Allies > 0) { keys = "5"; after = "で占拠を命じる  ／  F で仲間を呼ぶ"; }
                    break;
                }
                case StageTutorial.Step.Food:
                    head = "食料を集めきろう";
                    body = "食料を巣へ運ぶか、仲間に回収を任せよう。\n◆ 食料確保  " + colony.TotalCollected + " / " + progress.Stage.foodGoal;
                    if (colony.Allies > 0) { keys = "1"; after = "で仲間に回収を命じる"; }
                    break;
                case StageTutorial.Step.Fear:
                    head = "人間を怖がらせよう";
                    body = "人間に姿を見せる・仲間を増やす・食料を奪うと\n恐怖がたまる。▲ 恐怖  " + Mathf.FloorToInt(progress.FearGauge * 100) + "%";
                    break;
                case StageTutorial.Step.Defence:
                    head = progress.Current == StageProgress.Phase.FinalDefence ? "最終防衛！  あと " + Mathf.CeilToInt(progress.DefenceRemaining) + "秒耐えろ" : "まもなく最終防衛";
                    body = "燻煙剤の輪の外へ。業者は巣も燻し、高い所には\n噴霧器を撒く。橙の円が出たらそこから逃げろ。";
                    if (colony.Allies > 0) { keys = "F"; after = "長押しで全員を呼んで一緒に逃げる"; }
                    break;
                default: head = "乗っ取り成功！"; body = "人間が部屋から逃げていく。\nこの部屋はもうあなたたちのものだ。"; break;
            }
            Panel(r, Lime);
            bool done = step == StageTutorial.Step.Done;
            int number = Mathf.Min((int)step + 1, StageTutorial.StepCount);
            Ink(tiny, Lime, () => GUI.Label(new Rect(r.x + 16, r.y + 8, 90, 18), done ? "ガイド  完了" : "ガイド  " + number + " / " + StageTutorial.StepCount, tiny));
            // One dot per step: filled = done, outlined and pulsing = now, dim = still to come.
            for (int i = 0; i < StageTutorial.StepCount; i++)
            {
                var dot = new Rect(r.x + 112 + i * 12, r.y + 13, 8, 8);
                if (i < (int)step) Fill(dot, Lime);
                else if (i == (int)step) Outline(dot, new Color(Lime.r, Lime.g, Lime.b, live ? Pulse : 1), 2);
                else Fill(dot, new Color(0.3f, 0.32f, 0.28f));
            }
            Ink(tiny, Muted, () => GUI.Label(new Rect(r.xMax - 132, r.y + 8, 124, 18), "Esc ポーズで閉じる", tiny));
            Ink(big, Cream, () => GUI.Label(new Rect(r.x + 16, r.y + 26, r.width - 30, 30), head, big));
            GUI.Label(new Rect(r.x + 16, r.y + 56, r.width - 26, 48), body, small);
            if (keys != null)
            {
                float x = r.x + 16;
                foreach (string key in keys.Split(' '))
                {
                    float w = key.Length > 1 ? 58 : 34;
                    Keycap(new Rect(x, r.y + 106, w, 26), key, live);
                    x += w + 6;
                }
                Ink(small, Lime, () => GUI.Label(new Rect(x + 6, r.y + 109, r.xMax - x - 16, 24), after, small));
            }
            Fill(new Rect(r.x + 16, r.y + 138, r.width - 32, 1), new Color(0.3f, 0.32f, 0.28f));
            string next = done ? "あとはクリア画面へ" : "つぎ ▶ " + StepTitle(step + 1);
            Ink(tiny, Muted, () => GUI.Label(new Rect(r.x + 16, r.y + 142, r.width - 32, 18), next, tiny));
        }
        // The zone to hold still lacks roaches and there are allies to call over.
        private bool HoldNeedsAllies
        {
            get
            {
                int zone = tutorial.CaptureZone;
                return colony.Allies > 0 && zone >= 0 && zone < progress.ZoneCount && !progress.ZoneContested(zone) && progress.ZonePresence(zone) < progress.Stage.holdPresence;
            }
        }
        private string HoldText()
        {
            int zone = tutorial.CaptureZone;
            if (zone < 0 || zone >= progress.ZoneCount) return "区域に " + progress.Stage.holdPresence + "匹そろうと占領が進む。\nあなたも1匹に数える。";
            string name = progress.Stage.zones[zone].name;
            if (progress.ZoneContested(zone)) return "「" + name + "」に人間がいる。\n離れるまで待とう。";
            if (progress.ZonePresence(zone) >= progress.Stage.holdPresence)
                return "「" + name + "」  " + Mathf.FloorToInt(progress.ZoneProgress(zone) * 100) + "%\nそのまま待とう。人間が来たら逃げる。";
            return "あなたも「" + name + "」に入ろう。\n" + progress.Stage.holdPresence + "匹で " + progress.Stage.holdSeconds.ToString("F0") + "秒とどまると占領。";
        }
        private void Keycap(Rect r, string label, bool live)
        {
            Fill(r, new Color(0.1f, 0.12f, 0.09f, 0.96f));
            Fill(new Rect(r.x, r.yMax - 3, r.width, 3), new Color(0, 0, 0, 0.6f));
            Outline(r, new Color(Lime.r, Lime.g, Lime.b, live ? Pulse : 1), 2);
            Ink(keycap, Lime, () => GUI.Label(new Rect(r.x, r.y - 1, r.width, r.height), label, keycap));
        }
        // A pulsing outline on a screen button with a tag above it ("ここをクリック！").
        private void PointAt(Rect target, string label, bool tagAbove)
        {
            var c = new Color(Lime.r, Lime.g, Lime.b, Pulse);
            Outline(new Rect(target.x - 3, target.y - 3, target.width + 6, target.height + 6), c, 3);
            float w = 34 + label.Length * 14;
            var tag = tagAbove ? new Rect(target.center.x - w / 2, target.y - 30, w, 24) : new Rect(target.xMax - w - 8, target.y - 13, w, 24);
            Fill(tag, Lime);
            var style = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(0.06f, 0.07f, 0.04f);
            GUI.Label(tag, "▼ " + label, style);
        }
        // World places for the step: the nearest known food, the nest, the zone to hold.
        private void GuideTargets(Camera view)
        {
            if (!view) return;
            Vector3? at = null;
            switch (tutorial.Current)
            {
                case StageTutorial.Step.PickUp: case StageTutorial.Step.Breed: case StageTutorial.Step.Food:
                    if (tutorial.Current == StageTutorial.Step.Breed && colony.Food >= colony.BreedCost) { at = session.Nest.position + Vector3.up * 0.9f; break; }
                    var food = NearestFood();
                    if (session.Interactor.Carried) at = session.Nest.position + Vector3.up * 0.9f;
                    else if (food) at = food.transform.position + Vector3.up * 0.7f;
                    break;
                case StageTutorial.Step.Deliver: at = session.Nest.position + Vector3.up * 0.9f; break;
                case StageTutorial.Step.Capture: case StageTutorial.Step.Hold:
                {
                    int zone = tutorial.CaptureZone;
                    if (zone < 0) zone = NearestOpenZone();
                    if (zone >= 0) at = progress.Stage.zones[zone].centre + Vector3.up * 0.9f;
                    break;
                }
                case StageTutorial.Step.Territory:
                {
                    int zone = NearestOpenZone();
                    if (colony.Allies < 2) at = session.Nest.position + Vector3.up * 0.9f;   // breed first
                    else if (zone >= 0) at = progress.Stage.zones[zone].centre + Vector3.up * 0.9f;
                    break;
                }
            }
            if (at.HasValue) Marker(at.Value, "▼ ここ！", new Color(Lime.r, Lime.g, Lime.b, 1), true, view, 0.5f + 0.5f * Pulse);
        }
        private FoodItem NearestFood()
        {
            FoodItem best = null; float bestDistance = float.MaxValue;
            for (int i = 0; i < commander.Foods.Count; i++)
            {
                var f = commander.Foods[i];
                if (!commander.IsDiscovered(i) || f.State != FoodItem.FoodState.Available || !SwarmCommander.OnFloor(f)) continue;
                float d = (f.transform.position - session.Player.position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = f; }
            }
            return best;
        }
        private int NearestOpenZone()
        {
            int best = -1; float bestDistance = float.MaxValue;
            for (int i = 0; i < progress.ZoneCount; i++)
            {
                if (progress.ZoneCaptured(i)) continue;
                float d = (progress.Stage.zones[i].centre - session.Player.position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }
        private void NestGuide(string head, string body)
        {
            var r = GuideNest;
            Panel(r, Lime);
            Ink(tiny, Lime, () => GUI.Label(new Rect(r.x + 16, r.y + 8, 220, 18), "ガイド  " + tutorial.StepNumber + " / " + StageTutorial.StepCount, tiny));
            Ink(text, Cream, () => GUI.Label(new Rect(r.x + 16, r.y + 28, r.width - 24, 28), head, text));
            GUI.Label(new Rect(r.x + 16, r.y + 62, r.width - 24, 26), body, small);
        }

        // ---------------- Widgets ----------------
        // States: default, hover (lighter), pressed (darker), disabled (grey), unaffordable (orange edge).
        private bool Button(Rect r, string label, bool enabled, bool available)
        {
            bool hover = enabled && r.Contains(Event.current.mousePosition);
            bool pressed = hover && Input.GetMouseButton(0);
            Color fill = !available ? new Color(0.08f, 0.08f, 0.08f, 0.9f) : !enabled ? new Color(0.1f, 0.09f, 0.07f, 0.92f)
                : pressed ? new Color(0.55f, 0.62f, 0.25f) : hover ? new Color(0.86f, 0.93f, 0.45f) : Lime;
            Fill(r, fill);
            if (available && !enabled) Outline(r, Orange, 1);
            var style = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = enabled ? new Color(0.06f, 0.07f, 0.04f) : Muted;
            GUI.Label(r, label, style);
            return Clicked(r, enabled);
        }
        private static bool Clicked(Rect r, bool enabled)
        {
            bool hit = enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (hit) GameAudio.Ui();
            return hit;
        }
        // Secondary action: dark plate, lime outline on hover, darker while pressed.
        private bool Secondary(Rect r, string label)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            bool pressed = hover && Input.GetMouseButton(0);
            Fill(r, pressed ? new Color(0.04f, 0.05f, 0.04f, 0.96f) : hover ? new Color(0.12f, 0.14f, 0.10f, 0.96f) : new Color(0.07f, 0.08f, 0.07f, 0.94f));
            Outline(r, hover ? Lime : new Color(0.35f, 0.37f, 0.32f), hover ? 2 : 1);
            var style = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Cream;
            GUI.Label(r, label, style);
            return Clicked(r, true);
        }
        private bool Tab(Rect r, string label, bool on)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, on ? Lime : hover ? new Color(0.14f, 0.16f, 0.12f) : new Color(0.08f, 0.09f, 0.08f));
            var style = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = on ? new Color(0.06f, 0.07f, 0.04f) : Cream;
            GUI.Label(r, label, style);
            return Clicked(r, true);
        }
        private bool Stepper(Rect r, string label, ref float value, float min, float max, float step, string shown)
        {
            GUI.Label(new Rect(r.x, r.y + 8, 170, 30), label, text);
            float before = value;
            if (Button(new Rect(r.x + 180, r.y + 2, 44, 40), "−", value > min + 1e-4f, true)) value = Mathf.Max(min, value - step);
            Bar(new Rect(r.x + 236, r.y + 18, 180, 8), (value - min) / (max - min), Lime);
            if (Button(new Rect(r.x + 428, r.y + 2, 44, 40), "+", value < max - 1e-4f, true)) value = Mathf.Min(max, value + step);
            GUI.Label(new Rect(r.x + 482, r.y + 8, 70, 30), shown, text);
            return !Mathf.Approximately(before, value);
        }
        private bool Choice(Rect r, string label, ref int value, string a, string b)
        {
            GUI.Label(new Rect(r.x, r.y + 8, 170, 30), label, text);
            int before = value;
            if (Tab(new Rect(r.x + 180, r.y + 2, 170, 40), a, value == 0)) value = 0;
            if (Tab(new Rect(r.x + 360, r.y + 2, 170, 40), b, value == 1)) value = 1;
            return before != value;
        }
        private void Row(Rect r, string label, string value)
        {
            GUI.Label(new Rect(r.x, r.y, 150, r.height), label, small);
            var style = new GUIStyle(text) { alignment = TextAnchor.UpperRight };
            GUI.Label(new Rect(r.x + 110, r.y - 3, r.width - 110, r.height), value, style);
        }

        // ---------------- Helpers ----------------
        private string HumanLabel()
        {
            var vacuum = Vacuum; var net = Net;
            if (vacuum && (vacuum.Current == VacuumAttack.Phase.Aim || vacuum.Sucking)) return "母・掃除機！";
            if (net && (net.Current == BugNetAttack.Phase.Windup || net.PlayerCaught)) return "息子・虫取り網！";
            foreach (var a in progress.Attacks)   // Stage 3: the staff's own attacks
            {
                if (a is HotWaterAttack hot && (hot.Aiming || hot.Hot)) return a.Human.Label + "・熱湯！";
                if (a is AlcoholSprayAttack spray && (spray.Aiming || spray.Spraying)) return a.Human.Label + "・スプレー！";
                if (a is DeckBrushAttack brush && brush.Busy && brush.Current != DeckBrushAttack.Phase.Recover) return a.Human.Label + "・デッキブラシ！";
            }
            var w = Watcher;
            if (w == session.Human) return People ? w.Label + "・" + ResidentText() : ResidentText();
            return w.Label + "・" + StateText(w);
        }
        // The resident (the father in Stage 2) also places traps and sprays.
        private string ResidentText()
        {
            var reaction = colony.Reaction;
            if (reaction.Spray.Telegraphing || reaction.Spray.Spraying) return "スプレー！";
            if (reaction.CurrentAction == HumanReaction.Activity.Walking || reaction.CurrentAction == HumanReaction.Activity.Placing) return "罠を設置中";
            return StateText(session.Human);
        }
        private string StateText(HumanAIController h)
        {
            var state = h.State;
            if (state == HumanAIController.HumanState.Routine) { string doing = LifeText(h); if (doing != null) return doing; }
            if (state == HumanAIController.HumanState.Routine && h.TryGetComponent(out MopDuty mop) && mop.Mopping) return "モップがけ";
            if (h.OnStairs) return "階段";
            return state == HumanAIController.HumanState.Routine ? "巡回中" : state == HumanAIController.HumanState.Chase ? "追跡中！" : state == HumanAIController.HumanState.Search ? "捜索中" : "攻撃予兆！";
        }
        // ---- Stage 2 household ----
        // Stage 2: the family house (its own texts: the net, the cat, the father). People: more than one named human (Stage 2 and 3).
        private bool Household => session.Family.Count > 0 && !Floors;
        private bool People => session.Family.Count > 0;
        // Stage 3: the floors (null in Stage 1 and 2).
        private FloorLinks floors;
        private bool floorsLooked;
        private FloorLinks Floors { get { if (!floorsLooked) { floorsLooked = true; floors = FindFirstObjectByType<FloorLinks>(); } return floors; } }
        private string StageNumber => progress.Stage.id.Length > 2 ? progress.Stage.id.Substring(progress.Stage.id.Length - 2).TrimStart('0') : "1";
        /// <summary>The member of the household to report: attacking first, then chasing, searching, else the most alert.</summary>
        private HumanAIController Watcher
        {
            get
            {
                HumanAIController best = session.Human; int bestRank = -1; float bestDetection = -1;
                for (int i = 0; i < session.HumanCount; i++)
                {
                    if (!session.HumanPresent(i)) continue;
                    var h = session.HumanAt(i);
                    int rank = h.State == HumanAIController.HumanState.Attack ? 3 : h.State == HumanAIController.HumanState.Chase ? 2 : h.State == HumanAIController.HumanState.Search ? 1 : 0;
                    if (rank > bestRank || rank == bestRank && h.Detection > bestDetection) { best = h; bestRank = rank; bestDetection = h.Detection; }
                }
                return best;
            }
        }
        private VacuumAttack Vacuum { get { foreach (var a in progress.Attacks) if (a is VacuumAttack v) return v; return null; } }
        private BugNetAttack Net { get { foreach (var a in progress.Attacks) if (a is BugNetAttack n) return n; return null; } }
        private BugCage Cage { get { var net = Net; return net ? net.Cage : null; } }
        private PetCat pet;
        private bool petLooked;
        // Stage 2 daily life: what each member is doing while the house is calm (null: patrolling).
        private HouseholdLife life;
        private bool lifeLooked;
        private HouseholdLife Life { get { if (!lifeLooked) { lifeLooked = true; life = FindFirstObjectByType<HouseholdLife>(); } return life; } }
        private int MemberIndex(HumanAIController h) { for (int i = 0; i < session.HumanCount; i++) if (session.HumanAt(i) == h) return i; return -1; }
        private string LifeText(HumanAIController h)
        {
            var l = Life; int i = MemberIndex(h);
            return l && i >= 0 && i < l.MemberCount ? l.Label(i) : null;
        }
        // A busy member who heard the player run close by and turned round.
        private bool StartledNearby
        {
            get
            {
                var l = Life;
                if (!l) return false;
                for (int i = 0; i < l.MemberCount && i < session.HumanCount; i++)
                    if (session.HumanPresent(i) && l.Startled(i) && Vector3.Distance(session.HumanAt(i).transform.position, session.Player.position) < 6) return true;
                return false;
            }
        }
        private PetCat Pet { get { if (!petLooked) { petLooked = true; pet = FindFirstObjectByType<PetCat>(); } return pet; } }
        // The mother's vacuum and the son's net outrank the other warnings: they are the ones to dodge right now.
        private bool FamilyBanner()
        {
            var net = Net; var vacuum = Vacuum;
            if (net && net.PlayerCaught)
            {
                Banner("網に捕まった！  Space 連打で脱出  " + net.Presses + " / " + BugNetAttack.EscapePresses, 366, Orange);
                Bar(new Rect(407, 400, 476, 4), net.Presses / (float)BugNetAttack.EscapePresses, Lime);
                return true;
            }
            if (net && net.Current == BugNetAttack.Phase.Windup && net.Inside(session.Motor.FootPoint)) { Banner("虫取り網が来る！  橙の輪から逃げろ", 366, Orange); return true; }
            var cat = Pet;
            if (cat && (cat.Current == PetCat.Phase.Crouch || cat.Current == PetCat.Phase.Leap) && cat.InRing(session.Motor.FootPoint)) { Banner("猫が飛びかかる！  橙の輪から逃げろ", 366, Orange); return true; }
            if (cat && cat.Current == PetCat.Phase.Stalk && cat.TargetIsPlayer) { Banner("猫に狙われている…  止まればやり過ごせる", 366, Orange); return true; }
            if (vacuum && (vacuum.Current == VacuumAttack.Phase.Aim || vacuum.Sucking))
            {
                bool inFan = vacuum.InFan(session.Motor.FootPoint);
                Banner(vacuum.Sucking ? (inFan ? "掃除機に吸い込まれる！  横へ走って逃げろ" : "掃除機が吸っている  ―  橙の扇に入るな")
                    : inFan ? "掃除機で狙われている！  扇から横へ逃げろ" : "母が掃除機を向けている…", 366, Orange);
                return true;
            }
            return Floors && StaffBanner();
        }
        // Stage 3: the staff's attacks on the player's spot, then the lasting effects (soaked, a wet floor).
        private bool StaffBanner()
        {
            Vector3 foot = session.Motor.FootPoint;
            foreach (var a in progress.Attacks)
            {
                if (a is HotWaterAttack hot && hot.OnStrip(foot))
                {
                    if (hot.Aiming) { Banner("熱湯が来る！  橙の帯から横へ逃げろ", 366, Orange); return true; }
                    if (hot.Hot) { Banner("熱湯！  帯の外へ逃げろ", 366, Orange); return true; }
                }
                if (a is AlcoholSprayAttack spray && (spray.Aiming || spray.Spraying) && spray.InFan(foot))
                { Banner(spray.Aiming ? "アルコールスプレー！  橙の扇から逃げろ" : "アルコールを浴びている！  扇の外へ", 366, Orange); return true; }
                if (a is DeckBrushAttack brush && brush.WindingUp && brush.InBox(foot)) { Banner("デッキブラシが来る！  橙の四角から逃げろ", 366, Orange); return true; }
            }
            return false;
        }
        // The lasting effects, below every attack warning.
        private bool StaffStatusBanner()
        {
            if (session.Slowed) { Banner("アルコールで動きが鈍っている（あと" + Mathf.CeilToInt(session.SlowRemaining) + "秒）", 366, Orange); return true; }
            var wet = Wet;
            if (wet && wet.PlayerOnWet) { Banner("濡れた床！  滑って止まれない", 366, Orange); return true; }
            return false;
        }
        private WetFloor wetFloor;
        private bool wetLooked;
        private WetFloor Wet { get { if (!wetLooked) { wetLooked = true; wetFloor = FindFirstObjectByType<WetFloor>(); } return wetFloor; } }
        private readonly int[] counts = new int[System.Enum.GetValues(typeof(AllyRoach.AllyState)).Length];
        private string AllySummary()
        {
            System.Array.Clear(counts, 0, counts.Length);
            foreach (var a in commander.Allies) if (a.Alive) counts[(int)a.State]++;
            var sb = new System.Text.StringBuilder();
            Append(sb, "回収", counts[(int)AllyRoach.AllyState.CollectFood]);
            Append(sb, "探索", counts[(int)AllyRoach.AllyState.Explore]);
            Append(sb, "追従", counts[(int)AllyRoach.AllyState.FollowLeader]);
            Append(sb, "防衛", counts[(int)AllyRoach.AllyState.DefendNest]);
            Append(sb, "移動・占拠", counts[(int)AllyRoach.AllyState.GoToTarget]);
            Append(sb, "帰巣", counts[(int)AllyRoach.AllyState.ReturnToNest]);
            Append(sb, "待機", counts[(int)AllyRoach.AllyState.Idle]);
            Append(sb, "回避", counts[(int)AllyRoach.AllyState.FleeHazard]);
            Append(sb, "ひっくり返り", counts[(int)AllyRoach.AllyState.Stunned]);
            Append(sb, "復活待ち", counts[(int)AllyRoach.AllyState.Disabled]);
            return sb.ToString();
        }
        private static void Append(System.Text.StringBuilder sb, string label, int n)
        { if (n > 0) sb.Append(sb.Length > 0 ? "  " : "").Append(label).Append(n); }
        private bool ToGui(Vector3 world, Camera view, out Vector2 p)
        {
            Vector3 s = view.WorldToScreenPoint(world);
            p = new Vector2((s.x - offsetX) / scale, (Screen.height - s.y - offsetY) / scale);
            return s.z > 0;
        }
        private void Marker(Vector3 world, string label, Color color, bool clampToEdge, Camera view = null, float alpha = 1)
        {
            view = view ? view : Camera.main;
            if (!view) return;
            // Stage 3: the other floor lies beside this one, not above it; its markers would show through the walls.
            if (Floors && Floors.FloorOf(world) != Floors.FloorOf(view.transform.position)) return;
            bool front = ToGui(world, view, out Vector2 p);
            if (clampToEdge)
            {
                if (!front) p = new Vector2(p.x > 640 ? 80 : 1060, 390);
                p = new Vector2(Mathf.Clamp(p.x, 80, 1060), Mathf.Clamp(p.y, 170, 520));
            }
            else if (!front) return;
            float w = 26 + label.Length * 14;
            Fill(new Rect(p.x - w / 2, p.y - 14, w, 26), new Color(0.025f, 0.035f, 0.035f, 0.8f * alpha));
            Fill(new Rect(p.x - w / 2, p.y - 14, 3, 26), new Color(color.r, color.g, color.b, alpha));
            GUI.color = new Color(color.r, color.g, color.b, alpha);
            GUI.Label(new Rect(p.x - w / 2 + 9, p.y - 12, w, 24), label, small);
            GUI.color = Color.white;
        }
        private void Stat(Rect r, string label, string value, Color color)
        {
            GUI.Label(new Rect(r.x, r.y, r.width, 22), label, small);
            GUI.color = color; GUI.Label(new Rect(r.x, r.y + 20, r.width, 34), value, title); GUI.color = Color.white;
        }
        private void Banner(string label, float y, Color edge)
        { Panel(new Rect(390, y, 510, 40), edge); GUI.Label(new Rect(407, y + 6, 480, 32), label, text); }
        private void Shadowed(Rect r, string label, GUIStyle style, Color color)
        {
            GUI.color = new Color(0, 0, 0, 0.85f); GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), label, style);
            GUI.color = color; GUI.Label(r, label, style); GUI.color = Color.white;
        }
        private static void Ink(GUIStyle style, Color color, System.Action draw)
        { Color before = style.normal.textColor; style.normal.textColor = color; draw(); style.normal.textColor = before; }
        private GUIStyle Style(int size)
        { var style = new GUIStyle(GUI.skin.label) { font = font, fontSize = size }; style.normal.textColor = Cream; return style; }
        private static void Panel(Rect r, Color edge) { Fill(r, PanelColor); Fill(new Rect(r.x, r.y, 3, r.height), edge); }
        private static void Bar(Rect r, float amount, Color color) { Fill(r, new Color(0.2f, 0.22f, 0.2f)); r.width *= Mathf.Clamp01(amount); Fill(r, color); }
        private static void Outline(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c); Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c); Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }
        private static void Brackets(Rect r, Color c)
        {
            const float l = 6, t = 2;
            Fill(new Rect(r.x, r.y, l, t), c); Fill(new Rect(r.x, r.y, t, l), c);
            Fill(new Rect(r.xMax - l, r.y, l, t), c); Fill(new Rect(r.xMax - t, r.y, t, l), c);
            Fill(new Rect(r.x, r.yMax - t, l, t), c); Fill(new Rect(r.x, r.yMax - l, t, l), c);
            Fill(new Rect(r.xMax - l, r.yMax - t, l, t), c); Fill(new Rect(r.xMax - t, r.yMax - l, t, l), c);
        }
        private static void Fill(Rect r, Color color) { GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
        private void OnDestroy() { if (font) Destroy(font); }
    }
}
