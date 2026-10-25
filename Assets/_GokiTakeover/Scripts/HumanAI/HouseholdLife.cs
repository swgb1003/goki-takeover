using System;
using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    /// <summary>
    /// Stage 2 daily life (2026-10-25). While the house is calm (「平穏」, and between short patrols at 「発見」) the family
    /// do not patrol but live: the father reads the paper at the table, watches TV and nods off on the sofa, works at his
    /// desk; the mother cooks (dropping crumbs on the floor), washes up, folds the laundry and vacuums the floor; the son
    /// plays video games, does his homework and plays in the toy corner. Now and then the mother cooks dinner and calls
    /// everyone to the kitchen table: the other rooms are left empty, and leftovers stay behind.
    /// A busy person sees a narrower, shorter cone (the dozing father nothing). A roach running close by makes them turn
    /// round and look (and wakes the father). A sighting ends it at once: they need a moment to get up, then the usual
    /// chase takes over. From 「警戒」 they patrol as before. Drives HumanAIController through IDailyLife only.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class HouseholdLife : MonoBehaviour
    {
        public enum Kind { Paper, Tv, Doze, Desk, Cook, Wash, Laundry, Vacuum, Game, Homework, Toys, Dinner }
        public enum Meal { None, Cooking, Gathering, Eating }
        [Serializable]
        public sealed class Spot
        {
            public Kind kind;
            public int member;                        // 0 father, 1 mother, 2 son
            public Vector3 stand;                     // where they stand, on the NavMesh
            public float yaw;                         // the way they face while busy
            public BlenderHumanAnimator.LifePose pose;
            public Vector3 seat;                      // seated poses: the seat surface under the hips (zero: standing)
            public Vector3 hands;                     // the worktop, desk, table or bed the hands work on
            public Vector3 look;                      // what they watch
            public float sightAngle = 0.5f, sightRange = 0.6f;
        }
        private enum Doing { Choose, Walk, Busy, Startled, Patrol }
        private sealed class Member : IDailyLife
        {
            public int index;
            public HumanAIController human;
            public BlenderHumanAnimator body;
            public NavMeshAgent agent;
            public CapsuleCollider capsule;
            public Vector3 capsuleRest;
            public System.Random random;
            public Doing doing;
            public Spot spot, forced;
            public Kind last = Kind.Dinner;
            public float until, total, left, riseUntil, giveUp;
            public int route, dropped;
            public bool settled, cleaning;
            public Vector3 noise;
            public bool leads;
            public Vector3 destination;
            public float angle = 1, range = 1;
            public Vector3 eye;
            public bool Leads => leads;
            public Vector3 Destination => destination;
            public float SightAngleScale => angle;
            public float SightRangeScale => range;
            public Vector3 EyeShift => eye;
        }

        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private StageProgress progress;
        [SerializeField] private ColonyManager colony;
        [SerializeField] private Spot[] spots = new Spot[0];
        [SerializeField] private Vector3[] vacuumRoute = new Vector3[0];
        [SerializeField] private FoodItem[] crumbs = new FoodItem[0];
        [SerializeField] private FoodItem[] leftovers = new FoodItem[0];
        [SerializeField] private Transform newspaper, gamepad;
        // Running this close turns a busy person round; the dozing father wakes to running (or walking right by him).
        public const float HearRunning = 2.8f, WakeRunning = 2.2f, WakeWalking = 0.9f, StartleSeconds = 3f;
        // Getting up from a chair or the floor before giving chase; the dozing father takes longer.
        public const float StandUpSeconds = 0.6f, WakeUpSeconds = 1.2f;
        public const float DinnerFirst = 100f, DinnerEvery = 150f, CookSeconds = 24f, DinnerSeconds = 30f, GatherSeconds = 25f, NoticedPatrolSeconds = 15f;
        private Member[] members = new Member[0];
        private HumanReaction reaction;
        private VacuumAttack vacuum;
        private bool hidePending = true;
        private float dinnerClock, mealUntil;
        public Meal Dinner { get; private set; }
        public int Dinners { get; private set; }
        public int CrumbsDropped { get; private set; }
        public int LeftoversDropped { get; private set; }
        public int Startles { get; private set; }
        public int MemberCount => members.Length;
        public float NextDinnerIn => Dinner == Meal.None ? Mathf.Max(0, DinnerEvery - dinnerClock) : 0;

        public void Configure(PhaseTwoSession game, StageProgress stage, ColonyManager nest, Spot[] activities, Vector3[] route, FoodItem[] dropped, FoodItem[] left, Transform paper, Transform pad)
        { session = game; progress = stage; colony = nest; spots = activities; vacuumRoute = route; crumbs = dropped; leftovers = left; newspaper = paper; gamepad = pad; }

        private void Awake()
        {
            members = new Member[session.HumanCount];
            for (int i = 0; i < members.Length; i++)
            {
                var h = session.HumanAt(i);
                var m = new Member { index = i, human = h, agent = h.GetComponent<NavMeshAgent>(), body = h.GetComponentInChildren<BlenderHumanAnimator>(true), capsule = h.GetComponent<CapsuleCollider>() };
                if (m.capsule) m.capsuleRest = m.capsule.center;
                h.Life = m;
                members[i] = m;
                if (!vacuum) vacuum = h.GetComponent<VacuumAttack>();
            }
            session.RunReset += ResetLife;
        }
        private void Start() { reaction = colony.Reaction; ResetLife(); }
        private void OnDestroy() { if (session) session.RunReset -= ResetLife; }
        // Switched off (verification of the older attacks): everyone stands up and the patrol leads again.
        private void OnDisable()
        {
            foreach (var m in members)
            {
                if (!m.human) continue;
                Unsettle(m, 0); StopCleaning(m);
                m.riseUntil = 0; m.leads = false; m.doing = Doing.Choose; m.forced = null;
            }
            if (Dinner != Meal.None) { Dinner = Meal.None; dinnerClock = DinnerEvery * 0.6f; }
        }

        public void ResetLife()
        {
            foreach (var m in members)
            {
                Unsettle(m, 0);
                StopCleaning(m);
                m.riseUntil = 0;
                m.doing = Doing.Choose; m.spot = m.forced = null; m.last = Kind.Dinner;
                m.random = new System.Random(17 + m.index * 31);
                m.leads = false;
            }
            Dinner = Meal.None; dinnerClock = DinnerEvery - DinnerFirst; Dinners = 0;
            CrumbsDropped = LeftoversDropped = Startles = 0;
            hidePending = true;   // after every other reset handler has put the food back
        }

        private void Update()
        {
            if (hidePending)
            {
                hidePending = false;
                foreach (var f in crumbs) if (f && f.State == FoodItem.FoodState.Available) f.Hide();
                foreach (var f in leftovers) if (f && f.State == FoodItem.FoodState.Available) f.Hide();
            }
            if (!session.IsRunning || !reaction) return;
            float dt = Time.deltaTime;
            bool calm = Calm;
            Meals(dt, calm);
            foreach (var m in members) Tick(m, dt, calm);
        }
        private bool Calm => progress.Current == StageProgress.Phase.Invasion && reaction.CurrentLevel <= HumanReaction.Level.Noticed;
        private static bool Present(Member m) => m.human && m.human.isActiveAndEnabled;
        private bool Free(Member m, bool calm) => calm && Present(m) && !m.human.Suspended && m.human.State == HumanAIController.HumanState.Routine;

        // ---------------- One person ----------------
        private void Tick(Member m, float dt, bool calm)
        {
            var h = m.human;
            Rising(m);
            bool vacuuming = m.doing == Doing.Busy && m.spot != null && m.spot.kind == Kind.Vacuum;
            if (!Free(m, calm))
            {
                m.leads = false;
                bool spotted = Present(m) && h.State != HumanAIController.HumanState.Routine;
                if (m.settled || m.doing == Doing.Startled) Unsettle(m, spotted ? (m.spot != null && m.spot.kind == Kind.Doze ? WakeUpSeconds : StandUpSeconds) : 0);
                StopCleaning(m);
                // After a chase they pick something new; a house that is no longer calm forgets its plans.
                if (!calm || !Present(m)) { m.doing = Doing.Choose; m.forced = null; }
                else if (m.doing != Doing.Patrol) m.doing = Doing.Choose;
                return;
            }
            if (!vacuuming) StopCleaning(m);
            switch (m.doing)
            {
                case Doing.Choose:
                    Pick(m);
                    break;
                case Doing.Patrol:
                    m.leads = false;
                    if (Time.time >= m.until || m.forced != null) m.doing = Doing.Choose;
                    break;
                case Doing.Walk:
                    m.leads = true; Sight(m, 1, 1);
                    m.destination = m.spot.stand;
                    if (m.spot.kind == Kind.Vacuum) { m.doing = Doing.Busy; m.route = 0; m.until = float.MaxValue; break; }
                    if (Arrived(m, m.spot.stand, 0.5f)) Settle(m, m.left > 0 ? m.left : Duration(m));
                    else if (Time.time >= m.giveUp) { m.last = m.spot.kind; m.doing = Doing.Choose; }   // could not get there
                    break;
                case Doing.Busy:
                    m.leads = true;
                    if (m.spot.kind == Kind.Vacuum) Vacuuming(m);
                    else { Face(m, Quaternion.Euler(0, m.spot.yaw, 0), dt); Sight(m, m.spot.sightAngle, m.spot.sightRange); }
                    if (Hears(m.index, session.Motor.FootPoint, session.Motor.IsRunning && session.Motor.CurrentSpeed > 1f, session.Motor.CurrentSpeed > 0.25f) && PlayerAudible)
                    { Startle(m, session.Motor.FootPoint); break; }
                    if (m.spot.kind == Kind.Cook) Crumbs(m);
                    if (Time.time >= m.until) Finish(m);
                    break;
                case Doing.Startled:
                    m.leads = true; Sight(m, 1, 1);
                    m.destination = h.transform.position;
                    if (m.agent.isOnNavMesh) m.agent.isStopped = true;
                    m.agent.updateRotation = false;
                    Vector3 to = Flat(m.noise - h.transform.position);
                    if (to.sqrMagnitude > 0.01f) Face(m, Quaternion.LookRotation(to), dt);
                    if (Time.time < m.until) break;
                    m.agent.updateRotation = true;
                    if (m.spot.kind == Kind.Doze || m.spot.kind == Kind.Vacuum) Finish(m);
                    else { m.doing = Doing.Walk; m.giveUp = Time.time + 20; }   // back to it for the time that was left
                    break;
            }
        }
        private bool PlayerAudible => !session.InSafeNest && session.Motor.FootPoint.y < 0.5f && !session.Motor.IsClimbing;

        private void Pick(Member m)
        {
            Spot next = m.forced;
            m.forced = null;
            if (next == null)
            {
                // Something other than the last activity (dinner and dozing off only come on their own).
                int count = 0;
                foreach (var s in spots) if (Choosable(m, s)) count++;
                if (count == 0) { m.leads = false; return; }
                int pick = m.random.Next(count);
                foreach (var s in spots) if (Choosable(m, s) && pick-- == 0) { next = s; break; }
            }
            m.spot = next; m.left = 0; m.dropped = 0;
            m.doing = Doing.Walk; m.giveUp = Time.time + 40;
            m.leads = true; m.destination = next.stand;
        }
        private static bool Choosable(Member m, Spot s) => s.member == m.index && s.kind != Kind.Dinner && s.kind != Kind.Doze && s.kind != m.last;
        private float Duration(Member m)
        {
            switch (m.spot.kind)
            {
                case Kind.Doze: return 45 + (float)m.random.NextDouble() * 15;
                case Kind.Cook: return Dinner == Meal.Cooking ? CookSeconds : 20 + (float)m.random.NextDouble() * 8;
                case Kind.Dinner: return float.MaxValue;   // until the meal ends
                default: return 22 + (float)m.random.NextDouble() * 16;
            }
        }

        private void Settle(Member m, float seconds)
        {
            var s = m.spot;
            m.settled = true; m.doing = Doing.Busy; m.until = Time.time + seconds; m.total = seconds; m.left = 0;
            if (m.agent.isOnNavMesh) m.agent.isStopped = true;
            m.agent.updateRotation = false;
            bool seated = s.seat.sqrMagnitude > 1e-4f;
            Vector3 shift = seated ? Flat(s.seat - s.stand) : Vector3.zero;
            if (m.capsule) m.capsule.center = m.capsuleRest + Quaternion.Inverse(Quaternion.Euler(0, s.yaw, 0)) * shift;
            m.eye = seated && m.body ? shift + Vector3.down * Mathf.Max(0, m.body.HipHeight - s.seat.y) : Vector3.zero;
            if (m.body)
            {
                m.body.Pose = s.pose; m.body.SeatPoint = s.seat; m.body.HandPoint = s.hands; m.body.LookPoint = s.look;
                m.body.HeldProp = s.kind == Kind.Paper ? newspaper : s.kind == Kind.Game ? gamepad : null;
            }
        }
        // Up from the activity. rise > 0: spotted a roach and still getting up (the agent stays stopped that long).
        private void Unsettle(Member m, float rise)
        {
            if (m.body) { m.body.Pose = BlenderHumanAnimator.LifePose.None; m.body.HeldProp = null; }
            m.eye = Vector3.zero; m.angle = m.range = 1;
            if (m.capsule) m.capsule.center = m.capsuleRest;
            bool wasHeld = m.settled || m.doing == Doing.Startled;
            m.settled = false;
            if (!m.agent || !wasHeld) return;
            if (m.human.State != HumanAIController.HumanState.Attack) m.agent.updateRotation = true;
            if (rise > 0) m.riseUntil = Time.time + rise;
            else if (!m.human.Suspended && m.human.State != HumanAIController.HumanState.Attack && m.agent.isOnNavMesh) m.agent.isStopped = false;
        }
        private void Rising(Member m)
        {
            if (m.riseUntil <= 0) return;
            if (Time.time < m.riseUntil) { if (m.agent.isOnNavMesh && m.human.State != HumanAIController.HumanState.Attack) m.agent.isStopped = true; return; }
            m.riseUntil = 0;
            if (!m.human.Suspended && m.human.State != HumanAIController.HumanState.Attack && m.agent.isOnNavMesh) m.agent.isStopped = false;
        }
        private void Finish(Member m)
        {
            var s = m.spot;
            Unsettle(m, 0);
            StopCleaning(m);
            m.last = s.kind;
            m.doing = Doing.Choose;
            if (s.kind == Kind.Cook && Dinner == Meal.Cooking) { CallDinner(m); return; }
            // The TV sends the father to sleep now and then.
            if (s.kind == Kind.Tv && m.forced == null && m.random.NextDouble() < 0.5) { m.forced = SpotOf(m.index, Kind.Doze); return; }
            if (m.forced == null && reaction.CurrentLevel == HumanReaction.Level.Noticed) { m.doing = Doing.Patrol; m.until = Time.time + NoticedPatrolSeconds; m.leads = false; }
        }
        private void Startle(Member m, Vector3 at)
        {
            float rest = m.until - Time.time;
            bool doze = m.spot.kind == Kind.Doze;
            Unsettle(m, 0);
            m.settled = false;
            m.doing = Doing.Startled; m.noise = at; m.until = Time.time + StartleSeconds;
            m.left = Mathf.Max(3, rest);
            if (m.agent.isOnNavMesh) m.agent.isStopped = true;
            Startles++;
            if (reaction) reaction.SayAs(m.human, doze ? "ふがっ…？ 何の音だ" : "ん？ 今の音…");
        }
        /// <summary>Would this member hear a roach at a point (the player's footsteps)? Only while busy at an activity.</summary>
        public bool Hears(int member, Vector3 at, bool running, bool moving)
        {
            var m = members[member];
            if (m.doing != Doing.Busy || m.spot == null) return false;
            float d = Flat(at - m.human.transform.position).magnitude;
            if (m.spot.kind == Kind.Doze) return running ? d < WakeRunning : moving && d < WakeWalking;
            return running && d < HearRunning;
        }

        // ---- Vacuuming: the route across the floor, the head pushed ahead (VacuumAttack takes over on a sighting) ----
        private void Vacuuming(Member m)
        {
            Sight(m, 1, 1);
            if (vacuumRoute.Length == 0) { m.until = Time.time; return; }
            m.destination = vacuumRoute[m.route];
            if (Arrived(m, vacuumRoute[m.route], 0.9f) && ++m.route >= vacuumRoute.Length) { m.until = Time.time; return; }
            if (m.route < vacuumRoute.Length) m.destination = vacuumRoute[m.route];
            if (m.body && (!vacuum || !vacuum.Busy))
            {
                m.cleaning = true;
                m.body.SprayAim = 0.55f;
                m.body.SprayPoint = Flat(m.human.transform.position + m.human.transform.forward * 1.5f);
            }
        }
        private void StopCleaning(Member m)
        {
            if (!m.cleaning) return;
            m.cleaning = false;
            if (m.body && (!vacuum || !vacuum.Busy)) m.body.SprayAim = 0;
        }
        public bool Vacuuming(int member) => members[member].cleaning;

        // ---- Food the family leaves about ----
        private void Crumbs(Member m)
        {
            if (crumbs.Length == 0 || m.dropped >= crumbs.Length) return;
            float total = m.total, done = total - (m.until - Time.time);
            if (done < total * (m.dropped + 1) / (crumbs.Length + 1)) return;
            var crumb = crumbs[m.dropped++];
            if (colony.DropFood(crumb)) { CrumbsDropped++; colony.Say("母が料理中に食べこぼした！ コンロの前に食料"); }
        }

        // ---------------- Dinner ----------------
        private void Meals(float dt, bool calm)
        {
            bool all = members.Length >= 3;
            foreach (var m in members) all &= Free(m, calm);
            switch (Dinner)
            {
                case Meal.None:
                    if (!calm) return;
                    dinnerClock += dt;
                    if (dinnerClock >= DinnerEvery && all) StartCooking();
                    break;
                case Meal.Cooking:
                    if (!all) CancelDinner();
                    break;
                case Meal.Gathering:
                    if (!all) { CancelDinner(); break; }
                    bool seated = true;
                    foreach (var m in members) seated &= m.doing == Doing.Busy && m.spot != null && m.spot.kind == Kind.Dinner;
                    if (seated || Time.time >= mealUntil) { Dinner = Meal.Eating; mealUntil = Time.time + DinnerSeconds; }
                    break;
                case Meal.Eating:
                    if (!all) { CancelDinner(); break; }
                    if (Time.time >= mealUntil) EndDinner();
                    break;
            }
        }
        private void StartCooking()
        {
            Dinner = Meal.Cooking;
            Interrupt(members[1], SpotOf(1, Kind.Cook));
        }
        private void CallDinner(Member mother)
        {
            Dinner = Meal.Gathering; mealUntil = Time.time + GatherSeconds;
            foreach (var m in members) Interrupt(m, SpotOf(m.index, Kind.Dinner));
            if (reaction) reaction.SayAs(mother.human, "ごはんよー！ みんな来てー！");
            colony.Say("家族が夕食に集まる！ 他の部屋が手薄だ");
        }
        private void EndDinner()
        {
            Dinner = Meal.None; dinnerClock = 0; Dinners++;
            foreach (var f in leftovers) if (colony.DropFood(f)) LeftoversDropped++;
            foreach (var m in members) if (m.spot != null && m.spot.kind == Kind.Dinner && m.doing == Doing.Busy) m.until = Time.time;
            if (reaction) reaction.SayAs(members[0].human, "ごちそうさま。さて…");
            if (LeftoversDropped > 0) colony.Say("夕食の食べ残しがある！ 食卓を探せ");
        }
        private void CancelDinner()
        {
            Dinner = Meal.None; dinnerClock = DinnerEvery * 0.6f;
            foreach (var m in members)
            {
                if (m.forced != null && (m.forced.kind == Kind.Dinner || m.forced.kind == Kind.Cook)) m.forced = null;
                if (m.spot != null && m.spot.kind == Kind.Dinner && m.doing == Doing.Busy) m.until = Time.time;
            }
        }
        // Drop what they are doing (not a startled look round) and go to this activity next.
        private void Interrupt(Member m, Spot spot)
        {
            if (spot == null) return;
            m.forced = spot;
            if (m.doing == Doing.Startled) return;
            Unsettle(m, 0); StopCleaning(m);
            m.doing = Doing.Choose;
        }
        private Spot SpotOf(int member, Kind kind) { foreach (var s in spots) if (s.member == member && s.kind == kind) return s; return null; }

        // ---------------- Helpers ----------------
        private void Sight(Member m, float angle, float range) { m.angle = angle; m.range = range; }
        private static void Face(Member m, Quaternion facing, float dt) => m.human.transform.rotation = Quaternion.RotateTowards(m.human.transform.rotation, facing, 260 * dt);
        private static bool Arrived(Member m, Vector3 point, float within) => !m.agent.pathPending && Flat(m.human.transform.position - point).magnitude < within;
        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        // ---------------- What the HUD, the sounds and the tests read ----------------
        public bool BusyAt(int member, out Kind kind)
        {
            var m = members[member];
            kind = m.spot != null ? m.spot.kind : Kind.Dinner;
            return m.leads && m.doing == Doing.Busy && m.spot != null && Present(m);
        }
        public bool Dozing(int member) => BusyAt(member, out Kind k) && k == Kind.Doze;
        public bool Startled(int member) => members[member].leads && members[member].doing == Doing.Startled;
        public bool Leading(int member) => members[member].leads;
        public bool Rising(int member) => members[member].riseUntil > 0;
        public Spot Current(int member) => members[member].spot;
        public static string Name(Kind kind)
        {
            switch (kind)
            {
                case Kind.Paper: return "新聞";
                case Kind.Tv: return "テレビ";
                case Kind.Doze: return "うたた寝";
                case Kind.Desk: return "パソコン";
                case Kind.Cook: return "料理中";
                case Kind.Wash: return "皿洗い";
                case Kind.Laundry: return "洗濯物";
                case Kind.Vacuum: return "掃除中";
                case Kind.Game: return "ゲーム中";
                case Kind.Homework: return "宿題中";
                case Kind.Toys: return "おもちゃ";
                default: return "夕食";
            }
        }
        /// <summary>The HUD's word for what this member is doing (null: not living, so patrolling or hunting).</summary>
        public string Label(int member)
        {
            var m = members[member];
            if (!m.leads || m.spot == null) return null;
            if (m.doing == Doing.Startled) return "物音に気づいた";
            return m.doing == Doing.Busy ? Name(m.spot.kind) : m.spot.kind == Kind.Dinner ? "食卓へ" : "移動中";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Verification: this member goes straight to an activity (placed at it, so tests do not wait for walks).</summary>
        public void DebugStart(int member, Kind kind, bool place = true)
        {
            var m = members[member];
            var s = SpotOf(member, kind);
            Unsettle(m, 0); StopCleaning(m);
            m.forced = null; m.spot = s; m.left = 0; m.dropped = 0; m.giveUp = Time.time + 40;
            m.doing = Doing.Walk; m.leads = true; m.destination = s.stand;
            if (place && m.agent.isOnNavMesh) { m.agent.Warp(s.stand); m.human.transform.rotation = Quaternion.Euler(0, s.yaw, 0); }
        }
        public void DebugFinish(int member) { if (members[member].doing == Doing.Busy) members[member].until = Time.time; }
        public void DebugBusyFor(int member, float seconds) { if (members[member].doing == Doing.Busy) members[member].until = Time.time + seconds; }
        public void DebugDinnerNow() { dinnerClock = DinnerEvery; }
        public void DebugEndMeal() { if (Dinner == Meal.Eating || Dinner == Meal.Gathering) { Dinner = Meal.Eating; mealUntil = Time.time; } }
        public void DebugHear(int member, Vector3 at) { var m = members[member]; if (m.doing == Doing.Busy) Startle(m, at); }
#endif
    }
}
