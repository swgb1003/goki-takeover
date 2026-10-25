using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Phase 3 HUD on the same IMGUI kit as PhaseTwoHud (dark panel, lime edge, orange for danger),
    /// laid out on a 1280x720 canvas. Normal view: mission, vitals, allies, climbing state.
    /// Tactics view: allies / danger / food at the top, world markers, four order buttons at the bottom.
    /// Meaning is never colour only: every marker and state also carries a word or a glyph.
    /// </summary>
    public sealed class PhaseThreeHud : MonoBehaviour, ITacticsHud
    {
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private SwarmCommander commander;
        [SerializeField] private StickyTrap[] traps;
        private Font font;
        private GUIStyle title, text, small, tiny, button, centred;
        private float elapsed, scale = 1, offsetX, offsetY;
        private int frames;
        private string fps = "計測中";
        private static readonly Color Lime = new Color(0.79f, 0.87f, 0.37f);
        private static readonly Color Orange = new Color(1, 0.55f, 0.21f);
        private static readonly Color Cream = new Color(0.94f, 0.92f, 0.83f);
        private static readonly Color PanelColor = new Color(0.025f, 0.035f, 0.035f, 0.88f);
        private static readonly Rect CommandBar = new Rect(30, 594, 1220, 96);
        private static readonly AllyRoach.Order[] Orders = { AllyRoach.Order.Collect, AllyRoach.Order.Explore, AllyRoach.Order.Defend, AllyRoach.Order.Rally };
        private static readonly string[] OrderHelp = { "食料を集めて巣へ運ぶ", "未調査の場所を調べる", "巣のまわりで待機", "選択した仲間が目標へ集結" };
        private static readonly string[] OrderGlyph = { "◆", "◎", "■", "▲" };
        public void Configure(PhaseTwoSession game, SwarmCommander swarm, StickyTrap[] hazards) { session = game; commander = swarm; traps = hazards; }
        private void Awake() { font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo" }, 24); }
        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed < 0.5f) return;
            fps = (frames / elapsed).ToString("F0") + " FPS";
            frames = 0; elapsed = 0;
        }

        /// <summary>True when a screen point (pixels, origin bottom-left) is over a tactics-view panel.</summary>
        public bool BlocksPointer(Vector3 screen)
        {
            Layout();
            var p = new Vector2((screen.x - offsetX) / scale, (Screen.height - screen.y - offsetY) / scale);
            return CommandBar.Contains(p) || new Rect(30, 24, 340, 80).Contains(p) || new Rect(700, 24, 550, 70).Contains(p);
        }
        private void Layout()
        {
            scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            offsetX = (Screen.width - 1280 * scale) / 2; offsetY = (Screen.height - 720 * scale) / 2;
        }

        private void OnGUI()
        {
            if (!session || !commander) return;
            if (title == null)
            {
                title = Style(28); text = Style(18); small = Style(14); tiny = Style(12);
                centred = Style(18); centred.alignment = TextAnchor.MiddleCenter;
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 20 };
            }
            Matrix4x4 saved = GUI.matrix;
            Color savedColor = GUI.color;
            Layout();
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY), Quaternion.identity, Vector3.one * scale);
            GUI.Label(new Rect(1170, 4, 95, 22), fps, tiny);
            if (!session.IsRunning) Results();
            else if (commander.Mode == SwarmCommander.ViewMode.Swarm) Tactics();
            else Normal();
            if (session.IsRunning && commander.NoticeVisible)
            {
                float w = Mathf.Min(760, 60 + commander.Notice.Length * 19);
                Panel(new Rect(640 - w / 2, commander.Mode == SwarmCommander.ViewMode.Swarm ? 112 : 160, w, 40), Lime);
                GUI.Label(new Rect(640 - w / 2 + 18, (commander.Mode == SwarmCommander.ViewMode.Swarm ? 112 : 160) + 7, w - 30, 30), commander.Notice, text);
            }
            GUI.matrix = saved; GUI.color = savedColor;
        }

        // ---------------- Normal (third person) ----------------
        private void Normal()
        {
            var motor = session.Motor;
            Panel(new Rect(30, 28, 400, 122), Lime);
            GUI.Label(new Rect(48, 38, 360, 25), "02  /  夜のキッチン  ―  仲間と潜入", small);
            GUI.Label(new Rect(48, 66, 360, 38), "食料を巣へ集めよう", title);
            GUI.Label(new Rect(48, 110, 370, 28), "納品  " + session.Delivered + " / " + session.DeliveryGoal + "    所持  " + (session.Interactor.Carried ? "食料 1個" : "なし"), text);

            Panel(new Rect(30, 528, 330, 155), Lime);
            GUI.Label(new Rect(48, 537, 290, 30), "生命力  " + session.Hp + " / " + session.Settings.maximumHp, text);
            GUI.Label(new Rect(48, 569, 290, 30), "人間  " + HumanLabel(session.Human.State), text);
            Bar(new Rect(48, 604, 290, 7), session.Human.Detection, Orange);
            GUI.Label(new Rect(48, 620, 300, 26), "仲間  " + ActiveAllies + " / " + commander.Settings3.allyCount + "匹", text);
            GUI.Label(new Rect(48, 650, 300, 24), AllySummary(), small);

            Panel(new Rect(385, 647, 865, 38), Lime);
            GUI.Label(new Rect(400, 655, 845, 28), "WASD 移動   Shift 走る   E 回収・納品   壁へ進み続けて登る   Space 降りる   Tab 軍団モード   1-4 指示   Esc ポーズ", small);

            string prompt = session.Interactor.Carried ? (session.Interactor.AtNest ? "E  巣に納品する" : "巣へ戻ろう  ◇") :
                session.Interactor.NearbyFood ? "E  食料を回収する" : session.Interactor.AtNest ? "巣の周りは安全です" : "黄緑の目印が巣と食料です";
            Panel(new Rect(430, 578, 420, 48), Lime);
            GUI.Label(new Rect(450, 588, 385, 35), prompt, text);

            if (motor.IsClimbing)
            {
                bool ceiling = motor.SurfaceUp.y < -0.5f;
                Banner((ceiling ? "▼ 天井を移動中" : "▲ 壁面を移動中") + "    Space で降りる", 196, Lime);
            }
            else if (motor.ClimbEngage > 0.05f)
            {
                Panel(new Rect(490, 196, 300, 40), Lime);
                GUI.Label(new Rect(507, 202, 180, 30), "壁に張りつく…", text);
                Bar(new Rect(650, 214, 120, 7), motor.ClimbEngage, Lime);
            }
            if (session.IsSticky) Banner("粘着シート！  外に出ると速度が戻ります", 246, Orange);
            if (session.Human.State == HumanAIController.HumanState.Attack)
                Banner(session.PlayerStrikeable ? "スリッパ注意！  橙の枠からダッシュで逃げろ" : "スリッパ注意！  高い所なら届かない", 296, Orange);
            else if (session.Human.State == HumanAIController.HumanState.Chase) Banner("見つかった！  家具の下か壁の上へ逃げよう", 296, Orange);
            Marker(session.Nest.position + Vector3.up * 0.5f, "◇ 巣  " + Vector3.Distance(session.Player.position, session.Nest.position).ToString("F0") + "m", Lime, true);
            if (Cursor.lockState != CursorLockMode.Locked) Banner("クリックして操作を再開", 346, Lime);
        }

        // ---------------- Tactics view ----------------
        private void Tactics()
        {
            Camera view = Camera.main;
            if (view)
            {
                for (int i = 0; i < traps.Length; i++) if (traps[i]) Marker(traps[i].transform.position, "! 粘着", Orange, false, view);
                for (int i = 0; i < commander.ScoutPoints.Count; i++)
                    if (!commander.IsScouted(i)) Marker(commander.ScoutPoints[i], "？", new Color(0.7f, 0.72f, 0.66f), false, view, 0.75f);
                for (int i = 0; i < commander.Foods.Count; i++)
                {
                    var food = commander.Foods[i];
                    if (!commander.IsDiscovered(i) || food.State != FoodItem.FoodState.Available) continue;
                    Marker(food.transform.position + Vector3.up * 0.3f, SwarmCommander.OnFloor(food) ? "◆ 食料" : "◆ 食料・高所", Lime, false, view);
                }
                Marker(session.Nest.position + Vector3.up * 0.4f, "◇ 巣", Lime, false, view);
                if (commander.HasRallyPoint) Marker(commander.RallyPoint, "◎ 目標", Cream, false, view);
                var human = session.Human;
                bool alarm = human.State != HumanAIController.HumanState.Routine;
                Marker(human.transform.position + Vector3.up * 4.4f, "▲ 人間・" + HumanLabel(human.State), alarm ? Orange : Cream, false, view);
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

            Panel(new Rect(700, 24, 550, 70), Lime);
            Stat(new Rect(722, 30, 120, 60), "仲間", ActiveAllies + " / " + commander.Settings3.allyCount, Cream);
            string danger = commander.DangerLabel;
            Stat(new Rect(852, 30, 120, 60), "▲ 危険度", danger, danger == "高" ? Orange : danger == "中" ? new Color(1, 0.8f, 0.35f) : Cream);
            Stat(new Rect(982, 30, 120, 60), "◆ 食料地点", commander.AvailableFoodSpots.ToString(), Lime);
            Stat(new Rect(1112, 30, 130, 60), "？ 調査", commander.ScoutedCount + " / " + commander.ScoutPoints.Count, Cream);

            int chosen = commander.SelectedCount;
            GUI.Label(new Rect(34, 566, 1220, 24), "選択 " + chosen + "匹   左クリック・ドラッグ 選択   右クリック 移動先・食料   Q 全員   F 追従   Space 視点を戻す   WASD・ホイール 視点", small);
            for (int i = 0; i < 4; i++) OrderButton(new Rect(30 + i * 307, 598, 296, 88), i, chosen > 0);
        }

        private void OrderButton(Rect r, int index, bool enabled)
        {
            var order = Orders[index];
            bool hover = enabled && r.Contains(Event.current.mousePosition);
            bool active = commander.LastOrder == order;
            Color accent = index == 3 ? Orange : Lime;
            Fill(r, hover ? new Color(0.09f, 0.11f, 0.08f, 0.94f) : PanelColor);
            Fill(new Rect(r.x, r.y, 4, r.height), enabled ? accent : new Color(0.35f, 0.35f, 0.33f));
            if (active) Outline(r, accent, 2);
            Color ink = enabled ? Cream : new Color(0.5f, 0.5f, 0.47f);
            GUI.color = enabled ? accent : ink;
            GUI.Label(new Rect(r.x + 20, r.y + 18, 40, 40), OrderGlyph[index], title);
            GUI.color = Color.white;
            text.normal.textColor = ink;
            GUI.Label(new Rect(r.x + 64, r.y + 12, 200, 30), (index + 1) + "  " + SwarmCommander.OrderName(order), text);
            text.normal.textColor = Cream;
            small.normal.textColor = enabled ? new Color(0.78f, 0.77f, 0.7f) : ink;
            GUI.Label(new Rect(r.x + 64, r.y + 46, 225, 26), enabled ? OrderHelp[index] : "仲間を選択してください", small);
            small.normal.textColor = Cream;
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
            string label = down ? "× " + ally.Label : ally.Label;
            Shadowed(new Rect(p.x + 12, p.y - 9, 90, 20), label, tiny, ally.State == AllyRoach.AllyState.FleeHazard ? Orange : dot);
        }

        // ---------------- Results / pause ----------------
        private void Results()
        {
            Panel(new Rect(350, 210, 580, 300), Lime);
            string heading = session.State == PhaseTwoSession.RunState.Paused ? "ポーズ" : session.State == PhaseTwoSession.RunState.Dead ? "つかまってしまった！" : "食料集め成功！";
            GUI.Label(new Rect(390, 232, 500, 45), heading, title);
            if (session.State == PhaseTwoSession.RunState.Paused)
            {
                GUI.Label(new Rect(390, 289, 500, 35), "人間も仲間も止まっています。Escで再開。", text);
                GUI.Label(new Rect(390, 322, 500, 26), "Tab 軍団モード／1 回収  2 探索  3 防衛  4 一斉侵攻  F 追従", small);
                if (GUI.Button(new Rect(390, 372, 500, 56), "探索を再開  [Esc]", button)) session.Resume();
            }
            else
            {
                GUI.Label(new Rect(390, 289, 520, 35), "納品 " + session.Delivered + "個（うち仲間 " + commander.AllyDeliveries + "個）  /  " + session.RunSeconds.ToString("F0") + "秒", text);
                GUI.Label(new Rect(390, 322, 520, 30), "発見 " + session.Discoveries + "回   仲間の被害 " + commander.AllyLosses + "匹", text);
                if (GUI.Button(new Rect(390, 372, 500, 56), "最初からやり直す  [R]", button)) session.ResetRun();
            }
            GUI.Label(new Rect(390, 466, 500, 26), "GOKI TAKEOVER  /  Phase 3  壁登り・軍団指揮", small);
        }

        // ---------------- Helpers ----------------
        private int ActiveAllies
        {
            get { int n = 0; foreach (var a in commander.Allies) if (a.Active) n++; return n; }
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
        private static string HumanLabel(HumanAIController.HumanState state)
        { return state == HumanAIController.HumanState.Routine ? "巡回中" : state == HumanAIController.HumanState.Chase ? "追跡中！" : state == HumanAIController.HumanState.Search ? "捜索中" : "攻撃予兆！"; }
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
