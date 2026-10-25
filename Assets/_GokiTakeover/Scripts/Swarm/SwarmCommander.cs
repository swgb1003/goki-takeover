using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GokiTakeover
{
    /// <summary>
    /// Phase 3 hub: the Tab view switch (one camera, two controllers, never both rendering), the ally
    /// pool and its orders, selection in the tactics view, food discovery and scouting. Game time keeps
    /// running in the tactics view: the resident still patrols while the player gives orders.
    /// </summary>
    /// <summary>A HUD whose tactics-view panels should not pass clicks through to selection.</summary>
    public interface ITacticsHud { bool BlocksPointer(Vector3 screen); }
    /// <summary>A hazard allies run from whatever their order (Phase 5 smoke). Returns false when the spot is safe.</summary>
    public interface IAllyThreat { bool Threatens(Vector3 position, out Vector3 escape); }
    /// <summary>Territory the capture order can send allies to (Phase 5 stage). Without it there is no capture order.</summary>
    public interface ICaptureZones
    {
        int ZoneCount { get; }
        int HoldPresence { get; }
        bool ZoneOpen(int index);
        Vector3 ZoneCentre(int index);
        int ZoneAt(Vector3 position);
    }

    [DefaultExecutionOrder(-100)]
    public sealed class SwarmCommander : MonoBehaviour, IHumanPrey
    {
        public enum ViewMode { ThirdPerson, Swarm }
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private PhaseThreeSettings settings;
        [SerializeField] private AllyNavGrid grid;
        [SerializeField] private AllyRoach[] allies;
        [SerializeField] private FoodItem[] foods;
        [SerializeField] private bool[] knownAtStart;
        [SerializeField] private Vector3[] scoutPoints;
        [SerializeField] private SwarmViewCamera swarmCamera;
        [SerializeField] private Renderer[] hideInSwarm, ceiling;
        [SerializeField] private MonoBehaviour hud;   // an ITacticsHud: its panels swallow clicks
        private readonly Dictionary<FoodItem, AllyRoach> claims = new Dictionary<FoodItem, AllyRoach>();
        private bool[] discovered, scouted, selected;
        private AllyRoach[] scoutClaims;
        private GameObject[] beacons;
        private float discoveryClock, noticeUntil, deathNoticeAt;
        private Vector3 dragStart;
        private bool dragging, collectWarned;
        public ViewMode Mode { get; private set; }
        public PhaseTwoSession Session => session;
        public PhaseThreeSettings Settings3 => settings;
        public AllyNavGrid Grid => grid;
        public IReadOnlyList<AllyRoach> Allies => allies;
        public IReadOnlyList<FoodItem> Foods => foods;
        public IReadOnlyList<Vector3> ScoutPoints => scoutPoints;
        public Vector3 LeaderGround { get; private set; }
        public float LeaderYaw { get; private set; }
        public Vector3 NestGround => new Vector3(session.Nest.position.x, 0, session.Nest.position.z);
        public bool HasRallyPoint { get; private set; }
        public Vector3 RallyPoint { get; private set; }
        public AllyRoach.Order? LastOrder { get; private set; }
        public string Notice { get; private set; }
        public bool NoticeVisible => Time.unscaledTime < noticeUntil;
        public int AllyDeliveries { get; private set; }
        public int AllyLosses { get; private set; }
        public int ModeSwitches { get; private set; }
        public bool Dragging => dragging;
        // Phase 4: the colony decides how many allies exist (breeding) and how fast they are.
        // Negative: the Phase 3 settings decide. Zero is a valid start (Phase 5 starts with the player alone).
        public int AllyLimitOverride { get; set; } = -1;
        public int AllyLimit => AllyLimitOverride >= 0 ? Mathf.Min(AllyLimitOverride, allies.Length) : settings.allyCount;
        public float AllySpeedScale { get; set; } = 1f;
        public IAllyThreat ExtraThreat { get; set; }
        // Stage 2: the household's own attacks (the vacuum's pull, the bug net) are threats allies run from too.
        public readonly List<IAllyThreat> HouseholdThreats = new List<IAllyThreat>();
        // Phase 5: territory for the capture order, and allies that die for good (corpse seconds; 0 = respawn at the nest).
        public ICaptureZones Zones { get; set; }
        // Phase 5 tactics view: left clicks only. Click an ally to add it to (or drop it from) the selection; click food,
        // a zone or the floor to send the chosen allies there (none chosen: every ally); drag to draw a route for them.
        // Off (Phase 3-4): left drag selects in a box, right click orders, 4 rallies.
        public bool ClickOrders { get; set; }
        public const float RoutePointSpacing = 0.5f, DragStartPixels = 14f;
        public const int MaxRoutePoints = 160;
        private readonly List<Vector3> draft = new List<Vector3>(MaxRoutePoints);
        private readonly List<AllyRoute> routes = new List<AllyRoute>();
        private bool pressing;
        private Vector3 pressMouse;
        private List<Vector3> cover;
        public IReadOnlyList<Vector3> RouteDraft => draft;
        public bool DrawingRoute { get; private set; }
        private readonly List<AllyRoute> pending = new List<AllyRoute>();
        public int RoutesIssued { get; private set; }
        public float AllyCorpseSeconds { get; set; }
        public int AllyDeaths { get; private set; }
        // Phase 5: an ally under a raised slipper freezes about this long before it runs (0 = runs at once).
        public float AllyReactionSeconds { get; set; }
        // Phase 5 balance (2026-10-24): a first slipper only flips an ally over for this long (0 = crushed at once);
        // the wound heals at the nest or after AllyWoundHealSeconds. Allies near a slipper aimed at a friend scatter
        // at once (alarm radius, 0 = off). F taps call the allies in view, a held F calls everyone.
        public float AllyStunSeconds { get; set; }
        public float AllyWoundHealSeconds { get; set; } = 30f;
        public float AllyAlarmRadius { get; set; }
        public bool CallInView { get; set; }
        public float CollectReactionScale { get; set; } = 1f;
        // Final defence (Phase 5): while the nest is smoked, allies keep out of it and wait at a refuge instead.
        public bool NestSmoked { get; set; }
        public Vector3 NestRefuge { get; set; }
        public const float CallRange = 10f, CallHoldSeconds = 0.5f;
        public int AllyFlips { get; private set; }
        private float callPressedAt = -1, flipNoticeAt;
        private bool callHeld;
        public int OrdersIssued { get; private set; }   // sound cue (Phase 6)
        public Rect DragScreenRect => ScreenRect(dragStart, Input.mousePosition);

        public void Configure(PhaseTwoSession game, PhaseThreeSettings config, AllyNavGrid floor, AllyRoach[] pool, FoodItem[] items,
            bool[] known, Vector3[] scouts, SwarmViewCamera overview, Renderer[] hidden, Renderer[] roof, MonoBehaviour display)
        {
            session = game; settings = config; grid = floor; allies = pool; foods = items; knownAtStart = known;
            scoutPoints = scouts; swarmCamera = overview; hideInSwarm = hidden; ceiling = roof; hud = display;
        }

        private void Awake()
        {
            discovered = new bool[foods.Length];
            scouted = new bool[scoutPoints.Length];
            scoutClaims = new AllyRoach[scoutPoints.Length];
            selected = new bool[allies.Length];
            beacons = new GameObject[foods.Length];
            for (int i = 0; i < foods.Length; i++)
            {
                var beacon = foods[i].transform.Find("FoodBeacon");
                beacons[i] = beacon ? beacon.gameObject : null;
            }
            grid = Instantiate(grid);   // runtime copy: placed traps may mark cells without touching the asset
            for (int i = 0; i < allies.Length; i++) allies[i].Initialise(this, i);
            swarmCamera.enabled = false;
            session.DeliveryGoalOverride = settings.deliveryGoal;
            session.RunReset += ResetSwarm;
            // Every slipper in the house (Stage 2: the mother's too); each landing knows whose it was.
            landings = new System.Action<Vector3, bool>[session.HumanCount];
            for (int i = 0; i < landings.Length; i++)
            {
                var who = session.HumanAt(i);
                landings[i] = (point, crushed) => OnSlipperLanding(who);
                who.SlipperLanding += landings[i];
            }
        }
        private System.Action<Vector3, bool>[] landings;

        private void OnDestroy()
        {
            if (session) session.RunReset -= ResetSwarm;
            if (session && landings != null) for (int i = 0; i < landings.Length; i++) if (session.HumanAt(i)) session.HumanAt(i).SlipperLanding -= landings[i];
        }

        public void ResetSwarm()
        {
            claims.Clear();
            grid.ClearOverlays();
            for (int i = 0; i < foods.Length; i++) Reveal(i, knownAtStart[i]);
            for (int i = 0; i < scouted.Length; i++) { scouted[i] = false; scoutClaims[i] = null; }
            UpdateLeader();
            for (int i = 0; i < allies.Length; i++)
            {
                selected[i] = false;
                allies[i].gameObject.SetActive(i < AllyLimit);
                allies[i].ForgetFood();
                if (i < AllyLimit) allies[i].Spawn(NestPoint(i, 0.9f), i * 40);
            }
            HasRallyPoint = false;
            LastOrder = null;
            AllyDeliveries = AllyLosses = AllyDeaths = AllyFlips = 0;
            deathNoticeAt = 0;
            collectWarned = false;
            noticeUntil = 0;
            SetMode(ViewMode.ThirdPerson);
        }

        private void Update()
        {
            swarmCamera.InputEnabled = session.IsRunning && Mode == ViewMode.Swarm;
            if (!session.IsRunning) { dragging = false; return; }
            UpdateLeader();
            if (Input.GetKeyDown(KeyCode.Tab)) SetMode(Mode == ViewMode.Swarm ? ViewMode.ThirdPerson : ViewMode.Swarm);
            float dt = Time.deltaTime;
            foreach (var ally in allies) ally.Tick(dt);
            discoveryClock += dt;
            if (discoveryClock >= 0.1f) { discoveryClock = 0; Discover(); }
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Command(AllyRoach.Order.Collect);
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Command(AllyRoach.Order.Explore);
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) Command(AllyRoach.Order.Defend);
            if (!ClickOrders && (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))) Command(AllyRoach.Order.Rally);
            if (Zones != null && (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))) Command(AllyRoach.Order.Capture);
            if (CallInView && Mode == ViewMode.ThirdPerson) CallKey();
            else if (Input.GetKeyDown(KeyCode.F)) Command(AllyRoach.Order.Follow);
            if (Mode != ViewMode.Swarm) return;
            if (Input.GetKeyDown(KeyCode.Q)) SelectAll();
            if (ClickOrders && Input.GetKeyDown(KeyCode.X)) ClearSelection();
            if (ClickOrders && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) LaunchRoutes();
            if (Input.GetKeyDown(KeyCode.Space)) swarmCamera.FocusOn(SelectedCount > 0 ? SelectionCentre : LeaderGround);
            HandlePointer();
        }

        // F: a tap calls the allies in view, holding it calls everyone (Phase 5).
        private void CallKey()
        {
            if (Input.GetKeyDown(KeyCode.F)) { callPressedAt = Time.unscaledTime; callHeld = false; }
            if (callPressedAt < 0) return;
            if (!callHeld && Input.GetKey(KeyCode.F) && Time.unscaledTime - callPressedAt >= CallHoldSeconds)
            {
                callHeld = true;
                int all = Command(AllyRoach.Order.Follow);
                if (all > 0) { MarkCalled(null); Say("全員を呼んだ（" + all + "匹）"); }
            }
            if (Input.GetKeyUp(KeyCode.F)) { if (!callHeld) CallVisible(); callPressedAt = -1; }
        }

        /// <summary>Calls every living ally on screen, within CallRange and not behind furniture or walls.</summary>
        public int CallVisible()
        {
            var view = session.FollowCamera ? session.FollowCamera.GetComponent<Camera>() : null;
            int count = 0;
            for (int i = 0; i < allies.Length; i++)
            {
                var ally = allies[i];
                if (!ally.Alive || !InView(view, ally.Position)) continue;
                ally.SetOrder(AllyRoach.Order.Follow, Vector3.zero);
                ally.CalledAt = Time.time;
                count++;
            }
            if (count > 0) { LastOrder = AllyRoach.Order.Follow; OrdersIssued++; Say("近くの仲間 " + count + "匹を呼んだ（F 長押しで全員）"); }
            else Say(ActiveAllyCount == 0 ? NoAllyText : "見える所に仲間がいない ― F 長押しで全員を呼ぶ");
            return count;
        }
        public bool InView(Camera view, Vector3 position)
        {
            Vector3 body = position + Vector3.up * 0.3f;
            if ((body - session.Player.position).sqrMagnitude > CallRange * CallRange) return false;
            if (!view) return true;
            Vector3 v = view.WorldToViewportPoint(body);
            if (v.z <= 0 || v.x < 0 || v.x > 1 || v.y < 0 || v.y > 1) return false;
            return !Physics.Linecast(view.transform.position, body, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }
        private void MarkCalled(AllyRoach only) { foreach (var ally in allies) if (ally.Alive && (!only || ally == only)) ally.CalledAt = Time.time; }

        // ---- View switch ----
        public void SetMode(ViewMode next)
        {
            Mode = next;
            bool swarm = next == ViewMode.Swarm;
            ModeSwitches++;
            dragging = false;
            pressing = false; DrawingRoute = false; draft.Clear();
            session.ExternalCursor = swarm;
            var follow = session.FollowCamera;
            // One camera object, one active controller: the two views never render at the same time.
            follow.enabled = !swarm;
            swarmCamera.enabled = swarm;
            if (swarm) swarmCamera.Begin(LeaderGround);
            else follow.Follow(0);
            // The near wall stays as a shadow caster so the room's lighting does not change between views.
            foreach (var r in hideInSwarm) if (r) r.shadowCastingMode = swarm ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (var r in ceiling) if (r) r.enabled = !swarm;
            bool locked = !swarm && session.IsRunning && Application.isFocused;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            if (swarm && SelectedCount == 0 && !ClickOrders) SelectAll();   // click orders: none chosen means everyone
        }

        // ---- Orders ----
        public int Command(AllyRoach.Order order) => Command(order, Vector3.zero, null);

        /// <summary>In the tactics view orders go to the selection; in the normal view to every ally.</summary>
        public int Command(AllyRoach.Order order, Vector3 point, FoodItem food)
        {
            if (order == AllyRoach.Order.Capture) return CommandCapture(-1);
            int count = 0;
            if (order == AllyRoach.Order.Rally && point == Vector3.zero) point = HasRallyPoint ? RallyPoint : LeaderGround;
            for (int i = 0; i < allies.Length; i++)
            {
                if (!Chosen(i)) continue;
                allies[i].SetOrder(order, point, food);
                count++;
            }
            if (count == 0) { Say(ActiveAllyCount == 0 && AllyLimitOverride >= 0 ? NoAllyText : SelectHint); return 0; }
            LastOrder = order;
            OrdersIssued++;
            if (order == AllyRoach.Order.Collect && AvailableFoodSpots == 0)
                Say("見つかっている食料がありません — 2 探索で探そう");
            else Say(OrderName(order) + "を指示  （" + count + "匹）");
            collectWarned = false;
            return count;
        }

        private const string NoAllyText = "仲間がいません — 巣で E を押して仲間を増やそう";
        private string SelectHint => ClickOrders ? "仲間をクリックして選ぼう（Q で全員）" : "仲間を選んでください（クリック・ドラッグ／Qで全員）";
        // Who an order goes to: the selection in the tactics view (with click orders, everyone when none is chosen);
        // every ally in the normal view.
        private bool Chosen(int i) => allies[i].Alive && (Mode != ViewMode.Swarm || selected[i] || ClickOrders && SelectedCount == 0);
        private readonly List<AllyRoach> recipients = new List<AllyRoach>(9);
        private readonly List<AllyRoach> unassigned = new List<AllyRoach>(9);
        private readonly List<int> openZones = new List<int>(8);

        /// <summary>
        /// Capture: the chosen allies split into groups of the hold count and each group takes one of the
        /// nearest open zones (zone >= 0 sends them all to that zone). Too few allies still go: the player
        /// makes up the count by standing in the zone too.
        /// </summary>
        public int CommandCapture(int zone)
        {
            if (Zones == null) return 0;
            recipients.Clear();
            for (int i = 0; i < allies.Length; i++)
                if (Chosen(i)) recipients.Add(allies[i]);
            if (recipients.Count == 0) { Say(ActiveAllyCount == 0 ? NoAllyText : SelectHint); return 0; }
            int zones = AssignCapture(recipients, zone);
            if (zones == 0) { Say(zone >= 0 ? "その区域は占領済みです" : "占領できる区域が残っていません"); return 0; }
            LastOrder = AllyRoach.Order.Capture;
            OrdersIssued++;
            collectWarned = false;
            int hold = Zones.HoldPresence;
            if (recipients.Count < hold)
                Say("占拠を指示（" + recipients.Count + "匹）  あと" + (hold - recipients.Count) + "匹必要 — あなたも区域に入ろう");
            else Say("占拠を指示（" + recipients.Count + "匹 → " + zones + "区域）");
            return recipients.Count;
        }

        /// <summary>A zone was taken: the allies holding it move on to the next open zones.</summary>
        public void ContinueCapture(int zone)
        {
            recipients.Clear();
            foreach (var ally in allies) if (ally.Alive && ally.CurrentOrder == AllyRoach.Order.Capture && ally.CaptureZone == zone) recipients.Add(ally);
            if (recipients.Count > 0 && AssignCapture(recipients, -1) > 0) Say("占領した仲間は次の区域へ向かう");
        }

        private int AssignCapture(List<AllyRoach> group, int zone)
        {
            if (zone >= 0)
            {
                if (!Zones.ZoneOpen(zone)) return 0;
                foreach (var ally in group) ally.SetCapture(Zones.ZoneCentre(zone), zone);
                return 1;
            }
            Vector3 centre = Vector3.zero;
            foreach (var ally in group) centre += ally.Position;
            centre /= group.Count;
            openZones.Clear();
            for (int i = 0; i < Zones.ZoneCount; i++) if (Zones.ZoneOpen(i)) openZones.Add(i);
            if (openZones.Count == 0) return 0;
            openZones.Sort((a, b) => Flat(Zones.ZoneCentre(a) - centre).sqrMagnitude.CompareTo(Flat(Zones.ZoneCentre(b) - centre).sqrMagnitude));
            int groups = Mathf.Clamp(group.Count / Mathf.Max(1, Zones.HoldPresence), 1, openZones.Count);
            // Fill the nearest zones in turn, each with its share, taking the allies closest to it first.
            unassigned.Clear();
            unassigned.AddRange(group);
            for (int g = 0; g < groups; g++)
            {
                int index = openZones[g];
                Vector3 at = Zones.ZoneCentre(index);
                int share = group.Count / groups + (g < group.Count % groups ? 1 : 0);
                unassigned.Sort((a, b) => Flat(a.Position - at).sqrMagnitude.CompareTo(Flat(b.Position - at).sqrMagnitude));
                for (int k = 0; k < share && unassigned.Count > 0; k++) { unassigned[0].SetCapture(at, index); unassigned.RemoveAt(0); }
            }
            return groups;
        }

        public void ReportAllyDeath(AllyRoach ally)
        {
            AllyDeaths++;
            if (Time.unscaledTime < deathNoticeAt) return;
            deathNoticeAt = Time.unscaledTime + 1f;
            Say("仲間がやられた…（残り " + ActiveAllyCount + "匹）  死体を食べると生命力 +1");
        }

        /// <summary>The nearest corpse within reach of a point, or null.</summary>
        public AllyRoach NearestCorpse(Vector3 position, float range)
        {
            AllyRoach best = null; float bestDistance = range * range;
            foreach (var ally in allies)
            {
                if (!ally.IsCorpse) continue;
                float d = Flat(ally.Position - position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = ally; }
            }
            return best;
        }

        public static string OrderName(AllyRoach.Order order)
        {
            switch (order)
            {
                case AllyRoach.Order.Collect: return "回収";
                case AllyRoach.Order.Explore: return "探索";
                case AllyRoach.Order.Defend: return "防衛";
                case AllyRoach.Order.Rally: return "一斉侵攻";
                case AllyRoach.Order.Move: return "移動";
                case AllyRoach.Order.Capture: return "占拠";
                default: return "追従";
            }
        }

        // ---- Selection (tactics view) ----
        public int SelectedCount
        {
            get { int n = 0; for (int i = 0; i < allies.Length; i++) if (selected[i] && allies[i].Alive) n++; return n; }
        }
        public bool IsSelected(int index) => selected[index];
        public Vector3 SelectionCentre
        {
            get
            {
                Vector3 sum = Vector3.zero; int n = 0;
                for (int i = 0; i < allies.Length; i++) if (selected[i] && allies[i].Alive) { sum += allies[i].Position; n++; }
                return n > 0 ? sum / n : LeaderGround;
            }
        }
        public void SelectAll() { for (int i = 0; i < allies.Length; i++) selected[i] = allies[i].Alive; }
        public void SelectOnly(int index) { for (int i = 0; i < allies.Length; i++) selected[i] = i == index; }
        public void ClearSelection() { for (int i = 0; i < selected.Length; i++) selected[i] = false; }

        private void HandlePointer()
        {
            var view = swarmCamera.Camera;
            Vector3 mouse = Input.mousePosition;
            bool overUi = hud is ITacticsHud panels && panels.BlocksPointer(mouse);
            if (ClickOrders)
            {
                if (Input.GetMouseButtonDown(0) && !overUi) PressAt(mouse);
                if (pressing && Input.GetMouseButton(0)) DragTo(view, mouse);
                if (pressing && Input.GetMouseButtonUp(0)) ReleaseAt(view, mouse);
                return;
            }
            if (Input.GetMouseButtonDown(0) && !overUi) { dragging = true; dragStart = mouse; }
            if (dragging && Input.GetMouseButtonUp(0))
            {
                dragging = false;
                bool add = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                Rect box = ScreenRect(dragStart, mouse);
                if (box.width < 8 && box.height < 8)
                {
                    int hit = PickAlly(view, mouse, 30);
                    if (hit >= 0) { if (add) selected[hit] = !selected[hit]; else SelectOnly(hit); }
                    else if (!add) for (int i = 0; i < selected.Length; i++) selected[i] = false;
                }
                else
                {
                    for (int i = 0; i < allies.Length; i++)
                    {
                        if (!allies[i].Alive) continue;
                        Vector3 p = view.WorldToScreenPoint(allies[i].Position + Vector3.up * 0.3f);
                        bool inside = p.z > 0 && box.Contains(new Vector2(p.x, p.y));
                        selected[i] = add ? selected[i] || inside : inside;
                    }
                }
            }
            if (Input.GetMouseButtonDown(1) && !overUi) PointOrder(view, mouse);
        }

        // Right click: on a known crumb -> collect it; on an open zone (Phase 5) -> capture it; anywhere else ->
        // move there (and remember it for 4).
        public void PointOrder(Camera view, Vector3 mouse)
        {
            int food = PickFood(view, mouse, 40);
            if (food >= 0) { Command(AllyRoach.Order.Collect, Vector3.zero, foods[food]); return; }
            // Allies live on the floor, so the order goes to the floor under the cursor (the hidden ceiling
            // and near wall keep their colliders and must not catch the click).
            Ray ray = view.ScreenPointToRay(mouse);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter)) return;
            Vector3 point = ray.GetPoint(enter);
            point.y = 0;
            int zone = Zones != null ? Zones.ZoneAt(point) : -1;
            if (zone >= 0 && Zones.ZoneOpen(zone)) { CommandCapture(zone); return; }
            if (!grid.Walkable(point) && !grid.NearestWalkable(point, 2.5f, out point)) { Say("そこへは行けません"); return; }
            RallyPoint = point; HasRallyPoint = true;
            Command(AllyRoach.Order.Move, point, null);
        }

        // ---- Click orders (Phase 5): press, then either a click or a drawn route ----
        public void PressAt(Vector3 mouse) { pressing = true; pressMouse = mouse; DrawingRoute = false; draft.Clear(); }
        public void DragTo(Camera view, Vector3 mouse)
        {
            if (!pressing) return;
            if (!DrawingRoute)
            {
                if (((Vector2)mouse - (Vector2)pressMouse).magnitude < DragStartPixels) return;
                DrawingRoute = true;
                AddRoutePoint(view, pressMouse);
            }
            AddRoutePoint(view, mouse);
        }
        public void ReleaseAt(Camera view, Vector3 mouse)
        {
            if (!pressing) return;
            pressing = false;
            if (DrawingRoute)
            {
                DrawingRoute = false;
                AddRoutePoint(view, mouse);
                int food = PickFood(view, mouse, 40);
                int zone = draft.Count > 0 && Zones != null ? Zones.ZoneAt(draft[draft.Count - 1]) : -1;
                CommandRoute(draft, food >= 0 ? foods[food] : null, zone >= 0 && Zones.ZoneOpen(zone) ? zone : -1);
                draft.Clear();
                return;
            }
            ClickAt(view, mouse);
        }
        /// <summary>A click: an ally toggles in the selection; anything else orders the chosen allies there.</summary>
        public void ClickAt(Camera view, Vector3 mouse)
        {
            int hit = PickAlly(view, mouse, 30);
            if (hit >= 0) { selected[hit] = !selected[hit]; return; }
            PointOrder(view, mouse);
        }
        // The floor under the cursor, kept on the allies' floor; spaced points so the route stays light.
        private void AddRoutePoint(Camera view, Vector3 mouse)
        {
            if (draft.Count >= MaxRoutePoints) return;
            Ray ray = view.ScreenPointToRay(mouse);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter)) return;
            Vector3 p = ray.GetPoint(enter); p.y = 0;
            if (!grid.Walkable(p) && !grid.NearestWalkable(p, 1.0f, out p)) return;
            if (draft.Count > 0 && Flat(p - draft[draft.Count - 1]).magnitude < RoutePointSpacing) return;
            draft.Add(p);
        }
        /// <summary>
        /// Gives the chosen allies (none chosen: everyone) a route along these floor points. They hold their spot until
        /// LaunchRoutes (決定): several routes can be set up and started together. Returns how many were given it.
        /// </summary>
        public int CommandRoute(IList<Vector3> points, FoodItem food, int zone)
        {
            if (points == null || points.Count < 2) { Say("ルートが短すぎます — 床をドラッグして線を引こう"); return 0; }
            var route = new AllyRoute { Food = food, Zone = zone };
            route.Points.AddRange(points);
            int count = 0;
            for (int i = 0; i < allies.Length; i++)
            {
                if (!Chosen(i)) continue;
                allies[i].HoldForRoute(route);
                count++;
            }
            if (count == 0) { Say(ActiveAllyCount == 0 ? NoAllyText : SelectHint); return 0; }
            pending.Add(route);
            RoutesIssued++;
            Say("ルートを登録（" + count + "匹）  決定（Enter）で走り出す");
            return count;
        }
        /// <summary>Routes drawn and waiting for 決定, each still held by at least one ally.</summary>
        public IReadOnlyList<AllyRoute> PendingRoutes
        {
            get
            {
                pending.RemoveAll(r => { foreach (var a in allies) if (a.Alive && a.PendingRoute == r) return false; return true; });
                return pending;
            }
        }
        public int PendingCount(AllyRoute route) { int n = 0; foreach (var a in allies) if (a.Alive && a.PendingRoute == route) n++; return n; }
        /// <summary>決定: every ally holding a route starts running it at once. Returns how many started.</summary>
        public int LaunchRoutes()
        {
            int count = 0;
            foreach (var ally in allies)
            {
                if (!ally.Alive || ally.PendingRoute == null) continue;
                var route = ally.PendingRoute;
                ally.SetRoute(route);
                if (!routes.Contains(route)) routes.Add(route);
                count++;
            }
            pending.Clear();
            if (count == 0) { Say("決定待ちのルートがありません — 仲間を選んで床をドラッグ"); return 0; }
            OrdersIssued++;
            LastOrder = AllyRoach.Order.Route;
            collectWarned = false;
            Say("ルート開始！（" + count + "匹）");
            return count;
        }
        /// <summary>取消: the waiting routes are dropped; those allies stay where they are.</summary>
        public int CancelRoutes()
        {
            int count = 0;
            foreach (var ally in allies)
                if (ally.Alive && ally.PendingRoute != null) { ally.SetOrder(AllyRoach.Order.Move, ally.Position); count++; }
            pending.Clear();
            if (count > 0) Say("ルートを取り消した（" + count + "匹）");
            return count;
        }
        /// <summary>Routes some ally is still running (for the tactics view to draw).</summary>
        public IReadOnlyList<AllyRoute> ActiveRoutes
        {
            get
            {
                routes.RemoveAll(r => { foreach (var a in allies) if (a.Alive && a.Route == r) return false; return true; });
                return routes;
            }
        }
        /// <summary>The nearest floor with furniture overhead that can be walked to (the route's end when it asks to hide).</summary>
        public Vector3 NearestCover(Vector3 from)
        {
            if (cover == null)
            {
                cover = new List<Vector3>();
                for (int z = 0; z < grid.Depth; z += 2)
                    for (int x = 0; x < grid.Width; x += 2)
                    {
                        Vector3 p = grid.ToWorld(x, z);
                        if (grid.Walkable(p) && !session.InNest(p + Vector3.up * 0.1f) && Physics.Raycast(p + Vector3.up * 0.15f, Vector3.up, out RaycastHit roof, 3.3f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                            && !session.IsHumanBody(roof.transform)) cover.Add(p);
                    }
            }
            Vector3 best = Flat(from); float bestDistance = float.MaxValue;
            foreach (var p in cover)
            {
                float d = Flat(p - from).sqrMagnitude;
                if (d < bestDistance && d < 64) { bestDistance = d; best = p; }
            }
            return best;
        }

        private int PickAlly(Camera view, Vector3 mouse, float pixels)
        {
            int best = -1; float bestDistance = pixels * pixels;
            for (int i = 0; i < allies.Length; i++)
            {
                if (!allies[i].Alive) continue;
                Vector3 p = view.WorldToScreenPoint(allies[i].Position + Vector3.up * 0.3f);
                float d = ((Vector2)p - (Vector2)mouse).sqrMagnitude;
                if (p.z > 0 && d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }
        private int PickFood(Camera view, Vector3 mouse, float pixels)
        {
            int best = -1; float bestDistance = pixels * pixels;
            for (int i = 0; i < foods.Length; i++)
            {
                if (!discovered[i] || foods[i].State != FoodItem.FoodState.Available) continue;
                Vector3 p = view.WorldToScreenPoint(foods[i].transform.position);
                float d = ((Vector2)p - (Vector2)mouse).sqrMagnitude;
                if (p.z > 0 && d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }
        private static Rect ScreenRect(Vector3 a, Vector3 b) =>
            Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        /// <summary>Activates the next pooled ally at the nest (breeding). Null when the pool is used up.</summary>
        public AllyRoach AddAlly()
        {
            // Free slots first; a lingering corpse gives up its slot only when nothing else is left.
            for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < allies.Length; i++)
            {
                if (pass == 0 ? allies[i].gameObject.activeSelf : !allies[i].IsCorpse) continue;
                allies[i].gameObject.SetActive(true);
                allies[i].ForgetFood();
                allies[i].Spawn(NestPoint(i, 0.9f), i * 40);
                if (Mode == ViewMode.Swarm && !ClickOrders) selected[i] = true;   // click orders: the newcomer joins "everyone"
                return allies[i];
            }
            return null;
        }
        /// <summary>Retires the last active ally (it becomes the player's successor). False when none is left.</summary>
        public bool RemoveAlly()
        {
            for (int i = allies.Length - 1; i >= 0; i--)
            {
                if (!allies[i].Alive) continue;
                allies[i].DropFood();
                allies[i].gameObject.SetActive(false);
                selected[i] = false;
                return true;
            }
            return false;
        }
        public int ActiveAllyCount { get { int n = 0; foreach (var a in allies) if (a.Alive) n++; return n; } }

        public void Reveal(FoodItem food, bool state)
        {
            int index = System.Array.IndexOf(foods, food);
            if (index >= 0) Reveal(index, state);
        }
        public int NearestScoutPoint(Vector3 position)
        {
            int best = -1; float bestDistance = float.MaxValue;
            for (int i = 0; i < scoutPoints.Length; i++)
            {
                float d = Flat(scoutPoints[i] - position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }
        // Something changed there (the resident placed a trap): the spot shows "?" again until scouted.
        public void Unscout(int index) { if (index >= 0 && index < scouted.Length) { scouted[index] = false; scoutClaims[index] = null; } }

        // ---- Food, scouting, discovery ----
        public bool IsDiscovered(int index) => discovered[index];
        public bool IsScouted(int index) => scouted[index];
        public int ScoutedCount { get { int n = 0; foreach (bool s in scouted) if (s) n++; return n; } }
        public Vector3 ScoutPosition(int index) => scoutPoints[index];
        public static bool OnFloor(FoodItem food) => food.transform.position.y < 0.5f;
        public int AvailableFoodSpots
        {
            get { int n = 0; for (int i = 0; i < foods.Length; i++) if (discovered[i] && foods[i].State == FoodItem.FoodState.Available) n++; return n; }
        }

        public bool CanTarget(FoodItem food, AllyRoach ally)
        {
            int index = System.Array.IndexOf(foods, food);
            if (index < 0 || !discovered[index] || food.State != FoodItem.FoodState.Available || !OnFloor(food)) return false;
            return !claims.TryGetValue(food, out var owner) || owner == ally || !owner.Active;
        }

        public FoodItem ClaimNearestFood(AllyRoach ally, HashSet<FoodItem> exclude)
        {
            ReleaseClaimsOf(ally);
            FoodItem best = null; float bestDistance = float.MaxValue;
            foreach (var food in foods)
            {
                if (exclude.Contains(food) || !CanTarget(food, ally)) continue;
                float d = (food.transform.position - ally.Position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = food; }
            }
            if (best) claims[best] = ally;
            return best;
        }
        public void ReleaseFood(FoodItem food, AllyRoach ally) { if (food && claims.TryGetValue(food, out var owner) && owner == ally) claims.Remove(food); }
        private readonly List<FoodItem> released = new List<FoodItem>(8);
        private void ReleaseClaimsOf(AllyRoach ally)
        {
            released.Clear();
            foreach (var pair in claims) if (pair.Value == ally) released.Add(pair.Key);
            foreach (var food in released) claims.Remove(food);
        }

        public int ClaimScoutPoint(AllyRoach ally, HashSet<int> skip)
        {
            int best = -1; float bestDistance = float.MaxValue;
            // First unclaimed points; when every open point is taken, help with the nearest one.
            for (int pass = 0; pass < 2 && best < 0; pass++)
                for (int i = 0; i < scoutPoints.Length; i++)
                {
                    if (scouted[i] || skip.Contains(i)) continue;
                    if (pass == 0 && scoutClaims[i] && scoutClaims[i] != ally && scoutClaims[i].Active) continue;
                    float d = (scoutPoints[i] - ally.Position).sqrMagnitude;
                    if (d < bestDistance) { bestDistance = d; best = i; }
                }
            if (best >= 0 && !scoutClaims[best]) scoutClaims[best] = ally;
            return best;
        }
        public void ReleaseScout(int index, AllyRoach ally) { if (index >= 0 && scoutClaims[index] == ally) scoutClaims[index] = null; }

        public void MarkScouted(int index)
        {
            if (scouted[index]) return;
            scouted[index] = true;
            scoutClaims[index] = null;
            float reach = settings.discoveryRadius * 1.2f;
            for (int i = 0; i < foods.Length; i++)
                if (!discovered[i] && Flat(foods[i].transform.position - scoutPoints[index]).sqrMagnitude < reach * reach) Reveal(i, true, true);
        }

        private void Discover()
        {
            Vector3 leader = session.Motor.FootPoint;
            float r = settings.discoveryRadius;
            for (int i = 0; i < foods.Length; i++)
            {
                if (discovered[i] || foods[i].State != FoodItem.FoodState.Available) continue;
                Vector3 at = foods[i].transform.position;
                bool found = (at - leader).sqrMagnitude < r * r;
                if (!found && OnFloor(foods[i]))
                    foreach (var ally in allies) if (ally.Active && Flat(at - ally.Position).sqrMagnitude < r * r) { found = true; break; }
                if (found) Reveal(i, true, true);
            }
            for (int i = 0; i < scoutPoints.Length; i++)
            {
                if (scouted[i]) continue;
                bool here = leader.y < 1 && Flat(scoutPoints[i] - leader).sqrMagnitude < 1.44f;
                if (!here) foreach (var ally in allies) if (ally.Active && Flat(scoutPoints[i] - ally.Position).sqrMagnitude < 0.49f) { here = true; break; }
                if (here) MarkScouted(i);
            }
        }

        private void Reveal(int index, bool state, bool announce = false)
        {
            if (announce && state && !discovered[index]) Say(OnFloor(foods[index]) ? "食料を発見！  1 回収で運ばせよう" : "高い所に食料を発見！  壁を登って取りに行こう");
            discovered[index] = state;
            if (beacons[index]) beacons[index].SetActive(state);
        }

        public void RecordAllyDelivery(AllyRoach ally) { AllyDeliveries++; session.RecordDelivery(); }
        public void ReportNoFood(AllyRoach ally)
        {
            if (collectWarned) return;
            collectWarned = true;
            Say(AvailableFoodSpots > 0 ? "残りの食料は高い所です — 主人公が取りに行こう" : "回収できる食料がありません — 2 探索で探そう");
        }
        public void ReportExploreDone(AllyRoach ally) { if (ScoutedCount == scoutPoints.Length) Say("探索完了 — 部屋をすべて調べました"); }
        public void ReportUnreachable(AllyRoach ally) { Say("仲間が目標に到達できません"); }
        public void Say(string text) { Notice = text; noticeUntil = Time.unscaledTime + 2.8f; }

        // ---- Movement helpers for allies ----
        private void UpdateLeader()
        {
            Vector3 foot = session.Motor.FootPoint;
            Vector3 ground = new Vector3(foot.x, 0, foot.z);
            if (!grid.Walkable(ground)) grid.NearestWalkable(ground, 3f, out ground);
            LeaderGround = ground;
            LeaderYaw = session.Player.eulerAngles.y;
        }

        /// <summary>
        /// Resting slot at the nest. Around an open nest: the formation rings. In an enclosed nest (Phase 5,
        /// inside the sink cabinet): staggered rows along its long side, so the whole pool fits inside.
        /// </summary>
        public Vector3 NestPoint(int slot, float spacing)
        {
            if (!session.HasNestArea) return FormationPoint(NestGround, slot, slot * 40, spacing);
            if (NestSmoked) return FormationPoint(NestRefuge, slot, slot * 40, 0.8f);
            Bounds area = session.NestArea;
            bool alongX = area.size.x >= area.size.z;
            float length = (alongX ? area.size.x : area.size.z) - 1.0f, depth = (alongX ? area.size.z : area.size.x) - 0.9f;
            const int perRow = 5;
            int rows = Mathf.Max(1, Mathf.CeilToInt(allies.Length / (float)perRow));
            int row = slot / perRow, column = slot % perRow;
            float u = ((column + (row % 2 == 0 ? 0.3f : 0.7f)) / perRow - 0.5f) * length;
            float v = rows > 1 ? (row / (rows - 1f) - 0.5f) * Mathf.Max(0, depth) : 0;
            Vector3 p = new Vector3(area.center.x, 0, area.center.z) + (alongX ? new Vector3(u, 0, v) : new Vector3(v, 0, u));
            if (!grid.Walkable(p)) grid.NearestWalkable(p, 1.5f, out p);
            return p;
        }

        /// <summary>Slot around a centre: an inner ring of five and an outer ring of four, turned by yaw.</summary>
        public Vector3 FormationPoint(Vector3 centre, int slot, float yaw, float spacing)
        {
            float angle = (slot < 5 ? slot * 72f : 36f + (slot - 5) * 90f) + yaw + 180f;
            float radius = spacing * (slot < 5 ? 1.1f : 2.0f);
            Vector3 p = centre + Quaternion.Euler(0, angle, 0) * Vector3.forward * radius;
            p.y = 0;
            if (!grid.Walkable(p)) grid.NearestWalkable(p, 1.5f, out p);
            return p;
        }

        public Vector3 Separation(AllyRoach me)
        {
            Vector3 push = Vector3.zero, at = me.Position;
            float range = settings.allySeparation;
            foreach (var other in allies)
            {
                if (other == me || !other.Active) continue;
                Vector3 away = Flat(at - other.Position);
                float d = away.magnitude;
                if (d < range && d > 1e-3f) push += away / d * (1 - d / range);
            }
            Vector3 leader = Flat(at - LeaderGround);
            if (!session.Motor.IsClimbing && leader.magnitude < range && leader.magnitude > 1e-3f) push += leader.normalized * (1 - leader.magnitude / range);
            return Vector3.ClampMagnitude(push, 1);
        }

        /// <summary>Inside (or close to) a telegraphed slipper footprint: run out of it, whatever the order.</summary>
        public bool Threatened(Vector3 position, out Vector3 escape) => Threatened(position, 0, out escape);
        /// <summary>The ally's own check: with a reaction time it notices the raised slipper a moment late (Phase 5).</summary>
        public bool Threatened(AllyRoach ally, out Vector3 escape)
        {
            // Busy with food, a collector neither hears the alarm nor looks up quickly (balance, 2026-10-24).
            bool collecting = ally.State == AllyRoach.AllyState.CollectFood;
            if (!collecting && Alarmed(ally, out escape)) return true;
            return Threatened(ally.Position, ReactionDelay(ally) * (collecting ? CollectReactionScale : 1), out escape);
        }

        /// <summary>
        /// Alarm (Phase 5): while a slipper is raised over a friend, the allies around it run at once, out of the
        /// alarm radius, instead of freezing. The one being aimed at still reacts late. They return to their orders
        /// as soon as the slipper is down.
        /// </summary>
        private bool Alarmed(AllyRoach ally, out Vector3 escape)
        {
            escape = ally.Position;
            for (int i = 0; i < session.HumanCount; i++)
                if (session.HumanPresent(i) && Alarmed(session.HumanAt(i), ally, out escape)) return true;
            return false;
        }
        private bool Alarmed(HumanAIController human, AllyRoach ally, out Vector3 escape)
        {
            escape = ally.Position;
            if (AllyAlarmRadius <= 0 || human.State != HumanAIController.HumanState.Attack || human.SlipperLanded) return false;
            if (!human.TargetIsPlayer && human.Prey == ally.Slot) return false;   // the target itself: see ReactionDelay
            Vector3 offset = Flat(ally.Position - human.AttackPoint);
            if (human.StrikeUp.y < 0.7f || offset.magnitude > AllyAlarmRadius) return false;
            Vector3 away = offset.sqrMagnitude > 1e-3f ? offset.normalized : Quaternion.Euler(0, ally.Slot * 40, 0) * Vector3.forward;
            for (int k = 0; k < 5; k++)
            {
                Vector3 candidate = human.AttackPoint + Quaternion.Euler(0, (k + 1) / 2 * 35f * (k % 2 == 0 ? 1 : -1), 0) * away * (AllyAlarmRadius + 0.9f);
                candidate.y = 0;
                if (grid.Walkable(candidate)) { escape = candidate; return true; }
            }
            grid.NearestWalkable(human.AttackPoint + away * (AllyAlarmRadius + 0.9f), 2f, out escape);
            return true;
        }
        private bool Threatened(Vector3 position, float reaction, out Vector3 escape)
        {
            escape = position;
            if (ExtraThreat != null && ExtraThreat.Threatens(position, out escape)) return true;
            foreach (var threat in HouseholdThreats) if (threat.Threatens(position, out escape)) return true;
            escape = position;
            for (int i = 0; i < session.HumanCount; i++)
                if (session.HumanPresent(i) && Threatened(session.HumanAt(i), position, reaction, out escape)) return true;
            escape = position;
            return false;
        }
        private bool Threatened(HumanAIController human, Vector3 position, float reaction, out Vector3 escape)
        {
            escape = position;
            if (human.State != HumanAIController.HumanState.Attack || human.SlipperLanded || human.AttackTime < reaction) return false;
            // A slap on the worktop or high on a wall does not reach the floor (Phase 5 reach); a floor strike always passes.
            Vector3 strikeUp = human.StrikeUp;
            if (Mathf.Abs(Vector3.Dot(position - human.AttackPoint, strikeUp)) > HumanAIController.StrikeDepth) return false;
            var s = session.Settings;
            if (strikeUp.y < 0.7f)
            {
                // Low on a wall: step straight away from it.
                if (!human.IsUnderSlipper(position)) return false;
                Vector3 candidate = position + Flat(strikeUp).normalized * (s.slipperHalfLength + s.crushMargin + 0.8f);
                candidate.y = 0;
                if (!grid.Walkable(candidate)) grid.NearestWalkable(candidate, 2f, out candidate);
                escape = candidate;
                return true;
            }
            Vector3 forward = human.StrikeForward, right = Vector3.Cross(Vector3.up, forward);
            Vector3 offset = Flat(position - human.AttackPoint);
            float a = s.slipperHalfLength + s.crushMargin + 0.45f, b = s.slipperHalfWidth + s.crushMargin + 0.45f;
            float along = Vector3.Dot(offset, forward) / a, across = Vector3.Dot(offset, right) / b;
            if (along * along + across * across > 1) return false;
            Vector3 away = Mathf.Abs(Vector3.Dot(offset, right)) > 0.05f ? right * Mathf.Sign(Vector3.Dot(offset, right)) : right;
            for (int i = 0; i < 2; i++)
            {
                Vector3 candidate = human.AttackPoint + away * (b + 0.7f) + forward * Vector3.Dot(offset, forward);
                candidate.y = 0;
                if (grid.Walkable(candidate)) { escape = candidate; return true; }
                away = -away;
            }
            grid.NearestWalkable(human.AttackPoint + away * (b + 0.7f), 2f, out escape);
            return true;
        }

        // 0.6-1.4 x AllyReactionSeconds, different for each ally and each swing (a hash, so tests repeat).
        private float ReactionDelay(AllyRoach ally)
        {
            if (AllyReactionSeconds <= 0) return 0;
            Vector3 p = session.Human.AttackPoint;
            for (int i = 1; i < session.HumanCount; i++)
                if (session.HumanPresent(i) && session.HumanAt(i).State == HumanAIController.HumanState.Attack) p += session.HumanAt(i).AttackPoint;
            float r = Mathf.Repeat(Mathf.Sin(ally.Slot * 12.9898f + p.x * 78.233f + p.z * 37.719f) * 43758.5453f, 1f);
            return AllyReactionSeconds * (0.6f + 0.8f * r);
        }

        // IHumanPrey (Phase 5): an ally at the nest is as safe as the player there.
        public int PreyCount => allies.Length;
        public bool PreyTargetable(int index) => allies[index].Active && (session.HasNestArea ? !session.InNest(allies[index].Position) : Flat(allies[index].Position - NestGround).magnitude >= 1.2f);
        public Vector3 PreyPosition(int index) => allies[index].Position;
        public Vector3 PreyVelocity(int index) => allies[index].Velocity;

        private void OnSlipperLanding(HumanAIController human)
        {
            int lost = 0;
            foreach (var ally in allies)
            {
                if (!ally.Active || !human.IsUnderSlipper(ally.Position) || Covered(ally.Position)) continue;
                if (!ally.Hit(AllyStunSeconds, AllyWoundHealSeconds, settings.allyRespawnSeconds))
                {
                    AllyFlips++;
                    if (Time.unscaledTime >= flipNoticeAt) { flipNoticeAt = Time.unscaledTime + 1.5f; Say("仲間がひっくり返った！  もう一度叩かれるとやられる ― F で呼んで助けよう"); }
                    continue;
                }
                AllyLosses++; lost++;
            }
            if (lost > 0 && AllyCorpseSeconds <= 0) Say("仲間" + lost + "匹がスリッパにやられた！  " + settings.allyRespawnSeconds.ToString("F0") + "秒後に巣で復活");
        }

        private bool Covered(Vector3 position)
        {
            return Physics.Raycast(position + Vector3.up * 0.15f, Vector3.up, out RaycastHit roof, 3.3f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                && !session.IsHumanBody(roof.transform);
        }

        public string DangerLabel
        {
            get
            {
                int worst = 0;
                for (int i = 0; i < session.HumanCount; i++)
                {
                    if (!session.HumanPresent(i)) continue;
                    var h = session.HumanAt(i);
                    switch (h.State)
                    {
                        case HumanAIController.HumanState.Routine: worst = Mathf.Max(worst, h.Detection > 0.3f ? 1 : 0); break;
                        case HumanAIController.HumanState.Search: worst = Mathf.Max(worst, 1); break;
                        default: worst = 2; break;
                    }
                }
                return worst == 2 ? "高" : worst == 1 ? "中" : "低";
            }
        }

        // ---- Stage 2 household attacks ----
        /// <summary>An ally sucked up by the vacuum: gone for good (no corpse).</summary>
        public void LoseAlly(AllyRoach ally)
        {
            if (!ally.Alive) return;
            ally.DropFood();
            ally.gameObject.SetActive(false);
            selected[ally.Slot] = false;
            AllyDeaths++;
            AllyLosses++;
        }
        /// <summary>An ally caught in the bug net goes into the cage: out of play until the player lets it out.</summary>
        public void CageAlly(AllyRoach ally)
        {
            if (!ally.Alive) return;
            ally.DropFood();
            ally.gameObject.SetActive(false);
            selected[ally.Slot] = false;
        }
        /// <summary>A caged ally back in play next to the cage, following the player.</summary>
        public bool ReleaseAlly(int slot, Vector3 at)
        {
            var ally = allies[slot];
            if (ally.gameObject.activeSelf) return false;   // the slot went to a newborn meanwhile
            ally.gameObject.SetActive(true);
            ally.ForgetFood();
            Vector3 p = at;
            if (!grid.Walkable(p)) grid.NearestWalkable(p, 2f, out p);
            ally.Spawn(p, slot * 40);
            ally.CalledAt = Time.time;
            return true;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
