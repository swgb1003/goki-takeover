using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Phase 4 HUD, same IMGUI kit as Phase 2/3 (dark panel, lime edge, orange = danger) on a 1280x720 canvas.
    /// Adds the colony stock and the resident's reaction level, the nest screen (breeding, expansion,
    /// upgrades; modelled on DesignReferences/05_NestUpgrade.png), pause with settings and encyclopedia,
    /// and speech bubbles. Every button has default / hover / pressed / disabled states.
    /// </summary>
    public sealed class PhaseFourHud : MonoBehaviour, ITacticsHud
    {
        private enum PausePage { Main, Settings, Codex }
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private SwarmCommander commander;
        [SerializeField] private ColonyManager colony;
        [SerializeField] private StickyTrap[] traps;
        private Font font;
        private GUIStyle title, big, text, small, tiny;
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
        private static readonly AllyRoach.Order[] Orders = { AllyRoach.Order.Collect, AllyRoach.Order.Explore, AllyRoach.Order.Defend, AllyRoach.Order.Rally };
        private static readonly string[] OrderHelp = { "食料を集めて巣へ運ぶ", "未調査の場所・怪しい物を調べる", "巣のまわりで待機", "選択した仲間が目標へ集結" };
        private static readonly string[] OrderGlyph = { "◆", "◎", "■", "▲" };
        private static readonly string[] UpgradeGlyph = { "»", "■", "◎", "◆", "♥" };
        public void Configure(PhaseTwoSession game, SwarmCommander swarm, ColonyManager nest, StickyTrap[] hazards)
        { session = game; commander = swarm; colony = nest; traps = hazards; }
        private void Awake()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo" }, 24);
            foreach (int size in new[] { 12, 14, 18, 22, 28 }) font.RequestCharactersInTexture(HudGlyphs.All, size);
        }
        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (session && session.State != PhaseTwoSession.RunState.Paused) page = PausePage.Main;
            if (elapsed < 0.5f) return;
            fps = (frames / elapsed).ToString("F0") + " FPS";
            frames = 0; elapsed = 0;
        }

        public bool BlocksPointer(Vector3 screen)
        {
            Layout();
            var p = new Vector2((screen.x - offsetX) / scale, (Screen.height - screen.y - offsetY) / scale);
            return CommandBar.Contains(p) || new Rect(30, 24, 340, 80).Contains(p) || new Rect(560, 24, 690, 70).Contains(p);
        }
        private void Layout()
        {
            scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            offsetX = (Screen.width - 1280 * scale) / 2; offsetY = (Screen.height - 720 * scale) / 2;
        }

        private void OnGUI()
        {
            if (!session || !commander || !colony) return;
            if (title == null)
            {
                title = Style(28); big = Style(22); text = Style(18); small = Style(14); tiny = Style(12);
            }
            Matrix4x4 saved = GUI.matrix;
            Color savedColor = GUI.color;
            Layout();
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY), Quaternion.identity, Vector3.one * scale);
            GUI.Label(new Rect(1170, 4, 95, 22), fps, tiny);
            if (session.State == PhaseTwoSession.RunState.Paused && colony.NestMenuOpen) NestScreen();
            else if (session.State == PhaseTwoSession.RunState.Paused) Pause();
            else if (!session.IsRunning) Results();
            else
            {
                if (commander.Mode == SwarmCommander.ViewMode.Swarm) Tactics(); else Normal();
                if (commander.NoticeVisible)
                {
                    float y = commander.Mode == SwarmCommander.ViewMode.Swarm ? 112 : 160;
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
                Tactics(); NestScreen(); Results();
                var keep = page;
                for (int i = 0; i < 3; i++) { page = (PausePage)i; Pause(); }
                for (int tab = 0; tab < CodexTabs.Length; tab++) { codexTab = tab; CodexPage(new Rect(130, 70, 1020, 580)); }
                page = keep; codexTab = 0;
            }
            GUI.matrix = saved; GUI.color = savedColor;
        }

        // ---------------- Normal (third person) ----------------
        private void Normal()
        {
            var motor = session.Motor;
            var reaction = colony.Reaction;
            Panel(new Rect(30, 28, 430, 122), Lime);
            GUI.Label(new Rect(48, 38, 400, 25), "03  /  夜のキッチン  ―  コロニーを育てよう", small);
            GUI.Label(new Rect(48, 66, 400, 38), "仲間を" + colony.Settings.clearAllies + "匹に増やそう", title);
            GUI.Label(new Rect(48, 110, 400, 28), "食料 " + colony.Food + "    仲間 " + colony.Allies + " / " + colony.AllyCap + "    巣 Lv" + colony.NestLevel, text);
            ReactionPanel(new Rect(900, 28, 350, 92));

            Panel(new Rect(30, 528, 330, 155), Lime);
            GUI.Label(new Rect(48, 537, 290, 30), "生命力  " + session.Hp + " / " + session.MaximumHp, text);
            GUI.Label(new Rect(48, 569, 290, 30), "人間  " + HumanLabel(), text);
            Bar(new Rect(48, 604, 290, 7), session.Human.Detection, Orange);
            GUI.Label(new Rect(48, 620, 300, 26), "仲間  " + colony.Allies + "匹", text);
            GUI.Label(new Rect(48, 650, 300, 24), AllySummary(), small);

            Panel(new Rect(385, 647, 865, 38), Lime);
            GUI.Label(new Rect(400, 655, 845, 28), "WASD 移動   Shift 走る   E 回収・納品・巣   壁へ進み続けて登る   Space 降りる   Tab 軍団   1-4 指示   Esc ポーズ", small);

            var interactor = session.Interactor;
            var food = interactor.NearbyFood;
            var bait = reaction.NearbyBait(session.Player.position, session.Settings.interactionRange);
            string prompt = interactor.Carried ? (interactor.AtNest ? "E  巣に納品する（+" + FoodValue.AmountOf(interactor.Carried) + "）" : FoodValue.NameOf(interactor.Carried) + "を巣へ  ◇") :
                food ? "E  " + FoodValue.NameOf(food) + "を回収する（+" + FoodValue.AmountOf(food) + "）" :
                bait ? (bait.Identified ? "! 毒餌 — 拾わないで" : "E  食料を回収する") :
                interactor.AtNest ? "E  巣を開く（仲間を増やす・強化）" : "黄緑の目印が巣と食料です";
            Panel(new Rect(430, 578, 420, 48), bait && bait.Identified ? Orange : Lime);
            GUI.Label(new Rect(450, 588, 385, 35), prompt, text);

            if (motor.IsClimbing) Banner((motor.SurfaceUp.y < -0.5f ? "▼ 天井を移動中" : "▲ 壁面を移動中") + "    Space で降りる", 196, Lime);
            else if (motor.ClimbEngage > 0.05f)
            {
                Panel(new Rect(490, 196, 300, 40), Lime);
                GUI.Label(new Rect(507, 202, 180, 30), "壁に張りつく…", text);
                Bar(new Rect(650, 214, 120, 7), motor.ClimbEngage, Lime);
            }
            if (session.IsSticky) Banner("粘着シート！  外に出ると速度が戻ります", 246, Orange);
            var spray = reaction.Spray;
            if (spray.Telegraphing || spray.Spraying) Banner("殺虫スプレー！  物陰か橙の扇の外へ", 296, Orange);
            else if (session.Human.State == HumanAIController.HumanState.Attack)
                Banner(session.PlayerStrikeable ? "スリッパ注意！  橙の枠からダッシュで逃げろ" : "スリッパ注意！  高い所なら届かない", 296, Orange);
            else if (reaction.CurrentAction == HumanReaction.Activity.Walking || reaction.CurrentAction == HumanReaction.Activity.Placing)
                Banner("人間が罠を仕掛けに行く…", 296, Orange);
            else if (session.Human.State == HumanAIController.HumanState.Chase) Banner("見つかった！  家具の下か壁の上へ逃げよう", 296, Orange);
            Marker(session.Nest.position + Vector3.up * 0.5f, "◇ 巣  " + Vector3.Distance(session.Player.position, session.Nest.position).ToString("F0") + "m", Lime, true);
            foreach (var b in reaction.Baits)
                if (b.IsArmed && b.Identified && Vector3.Distance(b.transform.position, session.Player.position) < 9)
                    Marker(b.transform.position + Vector3.up * 0.45f, "! 毒餌", Orange, false);
            SpeechBubble();
            if (Cursor.lockState != CursorLockMode.Locked) Banner("クリックして操作を再開", 346, Lime);
        }

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
            if (!ToGui(session.Human.transform.position + Vector3.up * 4.7f, Camera.main, out Vector2 p)) return;
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
                if (commander.HasRallyPoint) Marker(commander.RallyPoint, "◎ 目標", Cream, false, view);
                var human = session.Human;
                bool alarm = human.State != HumanAIController.HumanState.Routine || reaction.CurrentAction != HumanReaction.Activity.None;
                Marker(human.transform.position + Vector3.up * 4.4f, "▲ 人間・" + HumanLabel(), alarm ? Orange : Cream, false, view);
                Marker(session.Motor.FootPoint + Vector3.up * 0.6f, "◆ あなた", Lime, false, view);
                for (int i = 0; i < commander.Allies.Count; i++) AllyMarker(i, view);
            }
            if (commander.Dragging)
            {
                Rect r = commander.DragScreenRect;
                var box = new Rect((r.x - offsetX) / scale, (Screen.height - r.yMax - offsetY) / scale, r.width / scale, r.height / scale);
                Fill(box, new Color(Lime.r, Lime.g, Lime.b, 0.12f));
                Outline(box, Lime, 2);
            }
            Panel(new Rect(30, 24, 340, 80), Lime);
            title.normal.textColor = Lime;
            GUI.Label(new Rect(48, 30, 300, 40), "軍団モード", title);
            title.normal.textColor = Cream;
            GUI.Label(new Rect(50, 72, 300, 24), "Tab で通常モードへ   ／   時間は止まりません", small);

            Panel(new Rect(560, 24, 690, 70), Lime);
            Stat(new Rect(582, 30, 120, 60), "仲間", colony.Allies + " / " + colony.AllyCap, Cream);
            Stat(new Rect(712, 30, 120, 60), "食料", colony.Food.ToString(), Lime);
            string danger = commander.DangerLabel;
            Stat(new Rect(842, 30, 130, 60), "▲ 反応", HumanReaction.LevelName(reaction.CurrentLevel), reaction.CurrentLevel >= HumanReaction.Level.Alert ? Orange : Cream);
            Stat(new Rect(982, 30, 120, 60), "◆ 食料地点", commander.AvailableFoodSpots.ToString(), Lime);
            Stat(new Rect(1112, 30, 130, 60), "？ 調査", commander.ScoutedCount + " / " + commander.ScoutPoints.Count, danger == "高" ? Orange : Cream);

            int chosen = commander.SelectedCount;
            GUI.Label(new Rect(34, 566, 1220, 24), "選択 " + chosen + "匹   左クリック・ドラッグ 選択   右クリック 移動先・食料   Q 全員   F 追従   Space 視点を戻す   WASD・ホイール 視点", small);
            for (int i = 0; i < 4; i++) OrderButton(new Rect(30 + i * 307, 598, 296, 88), i, chosen > 0);
            SpeechBubble();
        }

        private void OrderButton(Rect r, int index, bool enabled)
        {
            var order = Orders[index];
            Color accent = index == 3 ? Orange : Lime;
            bool hover = enabled && r.Contains(Event.current.mousePosition);
            Fill(r, hover ? new Color(0.09f, 0.11f, 0.08f, 0.94f) : PanelColor);
            Fill(new Rect(r.x, r.y, 4, r.height), enabled ? accent : new Color(0.35f, 0.35f, 0.33f));
            if (commander.LastOrder == order) Outline(r, accent, 2);
            Color ink = enabled ? Cream : Muted;
            GUI.color = enabled ? accent : ink;
            GUI.Label(new Rect(r.x + 20, r.y + 18, 40, 40), OrderGlyph[index], title);
            GUI.color = Color.white;
            Ink(text, ink, () => GUI.Label(new Rect(r.x + 64, r.y + 12, 200, 30), (index + 1) + "  " + SwarmCommander.OrderName(order), text));
            Ink(small, enabled ? new Color(0.78f, 0.77f, 0.7f) : ink, () => GUI.Label(new Rect(r.x + 64, r.y + 46, 225, 26), enabled ? OrderHelp[index] : "仲間を選択してください", small));
            if (enabled && GUI.Button(r, GUIContent.none, GUIStyle.none)) commander.Command(order);
        }

        private void AllyMarker(int index, Camera view)
        {
            var ally = commander.Allies[index];
            if (!ally.gameObject.activeSelf || !ToGui(ally.Position + Vector3.up * 0.3f, view, out Vector2 p)) return;
            bool down = ally.State == AllyRoach.AllyState.Disabled;
            bool picked = commander.IsSelected(index);
            Color dot = down ? new Color(0.45f, 0.45f, 0.42f) : picked ? Lime : Cream;
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
            Bar(new Rect(50, 254, 260, 7), colony.Allies / (float)colony.Settings.clearAllies, Lime);
            GUI.Label(new Rect(50, 264, 260, 22), "目標 " + colony.Settings.clearAllies + "匹でクリア", tiny);
            Row(new Rect(50, 300, 260, 28), "巣の規模", "Lv " + colony.NestLevel + " / " + colony.MaxNestLevel);
            Row(new Rect(50, 334, 260, 28), "生命力", session.Hp + " / " + session.MaximumHp);
            Row(new Rect(50, 368, 260, 28), "人間の反応", HumanReaction.LevelName(colony.Reaction.CurrentLevel));
            Row(new Rect(50, 402, 260, 28), "仲間を増やす費用", "食料 " + colony.BreedCost);
            GUI.Label(new Rect(50, 450, 270, 90), "仲間が増えるほど人間は怖がり、\n罠やスプレーで対抗してくる。\n巣の周りは安全地帯。", small);

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
            ActionCard(new Rect(850, 574, 400, 96), "施設の建設", "Phase 5 で解放", "準備中", false, false, null);

            GUI.Label(new Rect(34, 684, 400, 26), "Esc  閉じて出撃（ゲームは止まっています）", small);
            if (GUI.Button(new Rect(30, 680, 300, 34), GUIContent.none, GUIStyle.none)) colony.CloseNestMenu();
            if (colony.MessageVisible) Shadowed(new Rect(470, 684, 780, 26), colony.Message, text, colony.Message.Contains("足りません") || colony.Message.Contains("いっぱい") ? Orange : Lime);
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
            if (available && act != null && GUI.Button(r, GUIContent.none, GUIStyle.none)) act();
        }

        // ---------------- Pause, settings, encyclopedia ----------------
        private void Pause()
        {
            Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, 0.45f));
            var r = new Rect(330, 120, 620, 480);
            Panel(r, Lime);
            if (page == PausePage.Settings) { SettingsPage(r); return; }
            if (page == PausePage.Codex) { CodexPage(new Rect(130, 70, 1020, 580)); return; }
            GUI.Label(new Rect(370, 146, 500, 45), "ポーズ", title);
            GUI.Label(new Rect(370, 196, 540, 28), "人間も仲間も止まっています。", text);
            if (Button(new Rect(370, 250, 540, 56), "探索を再開  [Esc]", true, true)) session.Resume();
            if (Button(new Rect(370, 318, 540, 56), "設定", true, true)) page = PausePage.Settings;
            if (Button(new Rect(370, 386, 540, 56), "図鑑", true, true)) page = PausePage.Codex;
            if (Button(new Rect(370, 454, 540, 56), "最初からやり直す", true, true)) session.ResetRun();
            GUI.Label(new Rect(370, 540, 540, 26), "Tab 軍団  1 回収  2 探索  3 防衛  4 一斉侵攻  F 追従  E(巣) 繁殖・強化", small);
        }

        private void SettingsPage(Rect r)
        {
            var o = colony.Options;
            GUI.Label(new Rect(370, 140, 500, 45), "設定", title);
            bool changed = false;
            changed |= Stepper(new Rect(370, 200, 540, 44), "マウス感度", ref o.mouseSensitivity, 0.5f, 5f, 0.25f, o.mouseSensitivity.ToString("F2"));
            changed |= Stepper(new Rect(370, 252, 540, 44), "音量", ref o.masterVolume, 0, 1, 0.1f, Mathf.RoundToInt(o.masterVolume * 100) + "%");
            changed |= Choice(new Rect(370, 304, 540, 44), "画質", ref o.quality, "低（軽い）", "中（影あり）");
            int bubbles = o.speechBubbles ? 1 : 0, shake = o.cameraShake ? 1 : 0;
            if (Choice(new Rect(370, 356, 540, 44), "人間の吹き出し", ref bubbles, "オフ", "オン")) { o.speechBubbles = bubbles == 1; changed = true; }
            if (Choice(new Rect(370, 408, 540, 44), "画面の揺れ", ref shake, "オフ", "オン")) { o.cameraShake = shake == 1; changed = true; }
            if (changed) colony.SaveOptions();
            GUI.Label(new Rect(370, 462, 540, 24), "変更はすぐ反映・保存されます。効果音は今後追加予定。", small);
            if (Button(new Rect(370, 502, 540, 56), "戻る", true, true)) page = PausePage.Main;
        }

        private static readonly string[] CodexTabs = { "ゴキブリ", "人間", "食料", "駆除グッズ" };
        private struct Entry { public string id, name, body; public Entry(string i, string n, string b) { id = i; name = n; body = b; } }
        private static readonly Entry[][] CodexEntries =
        {
            new[] { new Entry(null, "主人公", "壁へ進み続けると張りつき、天井まで歩ける。Spaceで離れる。高い所はスリッパが届かない。"),
                    new Entry(null, "仲間", "1 回収・2 探索・3 防衛・4 一斉侵攻・F 追従。床を歩き、危険からは命令より先に逃げる。"),
                    new Entry(null, "巣", "E で開く。食料で仲間を産み、巣を広げ、強化する。周りは安全地帯。") },
            new[] { new Entry("resident", "住人", "反応は 平和 → 発見 → 警戒（罠や毒餌を置く）→ パニック（殺虫スプレー）。発見・仲間の数・食料の被害で恐怖度が上がる。") },
            new[] { new Entry(null, "パンくず  +1", "いちばん多い食料。"), new Entry("food_Rice", "米粒  +1", "小さく拾いやすい。"),
                    new Entry("food_Sweets", "お菓子  +2", "見つかりやすい場所にあることが多い。"), new Entry("food_Leftovers", "食べ残し  +3", "大きくて価値が高い。"),
                    new Entry("food_Garbage", "生ごみ  +2", "人間の動線の近くに出やすい。") },
            new[] { new Entry("slipper", "スリッパ", "橙の枠が出たら着弾前に外へ。家具の下と高い所には届かない。"),
                    new Entry("sticky", "粘着シート", "踏むと大きく減速。人間が警戒すると増える。仲間は避けて歩く。"),
                    new Entry("spray", "殺虫スプレー", "パニック中の人間が使う。橙の扇の中で浴びるとダメージ。家具や壁の陰なら防げる。"),
                    new Entry("bait", "毒餌", "食料に似ているが拾うと生命力が減る。青緑の粒が目印。仲間の探索で見破れる。") },
        };

        private void CodexPage(Rect r)
        {
            Fill(r, PanelColor); Fill(new Rect(r.x, r.y, 3, r.height), Lime);
            GUI.Label(new Rect(r.x + 30, r.y + 18, 300, 45), "図鑑", title);
            for (int i = 0; i < CodexTabs.Length; i++)
                if (Tab(new Rect(r.x + 200 + i * 150, r.y + 24, 140, 40), CodexTabs[i], codexTab == i)) codexTab = i;
            var entries = CodexEntries[codexTab];
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

        private void Results()
        {
            Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, 0.35f));
            Panel(new Rect(350, 190, 580, 330), Lime);
            bool clear = session.State == PhaseTwoSession.RunState.Cleared;
            GUI.Label(new Rect(390, 212, 500, 45), clear ? "コロニー完成！" : "つかまってしまった！", title);
            GUI.Label(new Rect(390, 268, 520, 30), "仲間 " + colony.Allies + "匹（産んだ数 " + colony.Born + "）   巣 Lv" + colony.NestLevel, text);
            GUI.Label(new Rect(390, 300, 520, 30), "回収した食料 " + colony.TotalCollected + "   強化 " + colony.UpgradesBought + "回   " + session.RunSeconds.ToString("F0") + "秒", text);
            GUI.Label(new Rect(390, 332, 520, 30), "発見 " + session.Discoveries + "回   人間の反応 最大「" + HumanReaction.LevelName(colony.Reaction.HighestLevel) + "」   仲間の被害 " + commander.AllyLosses, text);
            if (Button(new Rect(390, 384, 500, 56), "最初からやり直す  [R]", true, true)) session.ResetRun();
            GUI.Label(new Rect(390, 476, 500, 26), "GOKI TAKEOVER  /  Phase 4  育成・パニック", small);
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
            return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
        }
        private bool Tab(Rect r, string label, bool on)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, on ? Lime : hover ? new Color(0.14f, 0.16f, 0.12f) : new Color(0.08f, 0.09f, 0.08f));
            var style = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = on ? new Color(0.06f, 0.07f, 0.04f) : Cream;
            GUI.Label(r, label, style);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
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
            var reaction = colony.Reaction;
            if (reaction.Spray.Telegraphing || reaction.Spray.Spraying) return "スプレー！";
            if (reaction.CurrentAction == HumanReaction.Activity.Walking || reaction.CurrentAction == HumanReaction.Activity.Placing) return "罠を設置中";
            var state = session.Human.State;
            return state == HumanAIController.HumanState.Routine ? "巡回中" : state == HumanAIController.HumanState.Chase ? "追跡中！" : state == HumanAIController.HumanState.Search ? "捜索中" : "攻撃予兆！";
        }
        private readonly int[] counts = new int[9];
        private string AllySummary()
        {
            System.Array.Clear(counts, 0, counts.Length);
            foreach (var a in commander.Allies) if (a.gameObject.activeSelf) counts[(int)a.State]++;
            var sb = new System.Text.StringBuilder();
            Append(sb, "回収", counts[(int)AllyRoach.AllyState.CollectFood]);
            Append(sb, "探索", counts[(int)AllyRoach.AllyState.Explore]);
            Append(sb, "追従", counts[(int)AllyRoach.AllyState.FollowLeader]);
            Append(sb, "防衛", counts[(int)AllyRoach.AllyState.DefendNest]);
            Append(sb, "移動", counts[(int)AllyRoach.AllyState.GoToTarget]);
            Append(sb, "帰巣", counts[(int)AllyRoach.AllyState.ReturnToNest]);
            Append(sb, "待機", counts[(int)AllyRoach.AllyState.Idle]);
            Append(sb, "回避", counts[(int)AllyRoach.AllyState.FleeHazard]);
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
