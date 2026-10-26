using System;
using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    /// <summary>
    /// Stage 3: the diner's day, round and round (about seven minutes): prepping, lunch, the afternoon break, dinner,
    /// closed. Each part sets where the staff go and how much they notice, opens the doors to customers, puts food out
    /// (the gyoza being made, the shumai steamer at dinner) and, between services, sets fresh traps. After closing the
    /// lights go down, the landlady and the part-timer go home and the cook walks the shop with a torch; they are back for
    /// the next morning's prep. The day stops at the final defence.
    /// </summary>
    public sealed class DinerBusiness : MonoBehaviour
    {
        public enum Phase { Prep, Lunch, Break, Dinner, Closed }
        public static readonly float[] Seconds = { 60, 120, 60, 120, 60 };
        public static readonly string[] Names = { "仕込み", "ランチ", "中休み", "ディナー", "閉店" };
        [Serializable] public sealed class Rounds { public Transform[] prep, lunch, rest, dinner, closed; }
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private StageProgress progress;
        [SerializeField] private SwarmCommander commander;
        [SerializeField] private ColonyManager colony;
        [SerializeField] private DinerCustomers customers;
        [SerializeField] private MopDuty mop;
        [SerializeField] private Rounds chefRounds, okamiRounds, baitoRounds;
        [SerializeField] private FoodItem[] prepFood = new FoodItem[0];      // gyoza laid out on the work table
        [SerializeField] private FoodItem[] dinnerFood = new FoodItem[0];    // the shumai steamer upstairs
        [SerializeField] private Light[] lights = new Light[0];
        [SerializeField] private Light torch;
        [SerializeField] private Vector3[] trapSpots = new Vector3[0];
        [SerializeField] private Vector3 backDoor;
        public const float BusyRange = 0.6f, BusyAngle = 0.8f, TorchAngle = 0.55f, DimTo = 0.18f;
        public const int LunchMin = 3, LunchMax = 6, DinnerMin = 2, DinnerMax = 5;
        private float[] brightness;
        private float clock;
        private bool stopped;
        private readonly bool[] home = new bool[2];   // the landlady, the part-timer: sent home after closing
        public bool WentHome(int member) => home[member];
        private int days, nextTrap;
        private readonly System.Random dice = new System.Random(5);
        public Phase Current { get; private set; }
        public float Remaining => Seconds[(int)Current] - clock;
        public string Name => Names[(int)Current];
        public int Days => days;
        public bool Dark => Current == Phase.Closed && !stopped;
        public int TrapsSet { get; private set; }
        public event Action<Phase> Changed;

        public void Configure(PhaseTwoSession game, StageProgress stage, SwarmCommander swarm, ColonyManager nest, DinerCustomers guests, MopDuty mopping,
            Rounds chef, Rounds okami, Rounds baito, FoodItem[] prep, FoodItem[] dinner, Light[] roomLights, Light cookTorch, Vector3[] traps, Vector3 exit)
        {
            session = game; progress = stage; commander = swarm; colony = nest; customers = guests; mop = mopping;
            chefRounds = chef; okamiRounds = okami; baitoRounds = baito; prepFood = prep; dinnerFood = dinner; lights = roomLights; torch = cookTorch; trapSpots = traps; backDoor = exit;
        }

        private HumanAIController Chef => session.Human;
        private HumanAIController Okami => session.Family.Count > 0 ? session.Family[0] : null;
        private HumanAIController Baito => session.Family.Count > 1 ? session.Family[1] : null;

        private void Awake()
        {
            brightness = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) brightness[i] = lights[i] ? lights[i].intensity : 0;
            session.RunReset += Restart;
        }
        private void OnDestroy() { if (session) session.RunReset -= Restart; }
        private void Start() { Restart(); }

        /// <summary>The morning again: prepping, everyone in, the lights on (a new run or a replay).</summary>
        public void Restart()
        {
            stopped = false; days = 0; TrapsSet = 0; nextTrap = 0;
            foreach (var f in dinnerFood) if (f && f.State == FoodItem.FoodState.Available) f.Hide();
            Enter(Phase.Prep);
        }

        private void Update()
        {
            if (!session.IsRunning) return;
            if (progress.Current >= StageProgress.Phase.FinalDefence) { if (!stopped) Stop(); return; }
            if (progress.Current != StageProgress.Phase.Invasion) return;
            clock += Time.deltaTime;
            if (clock >= Seconds[(int)Current]) Enter((Phase)(((int)Current + 1) % Seconds.Length));
            if (Current == Phase.Closed) SendHome();
        }

        /// <summary>Into a part of the day (verification may jump straight to one).</summary>
        public void Enter(Phase next)
        {
            var before = Current;
            Current = next; clock = 0;
            if (next == Phase.Prep && before == Phase.Closed) days++;
            SetLights(next != Phase.Closed);
            switch (next)
            {
                case Phase.Prep:
                    BackToWork();
                    Busy(1, 1);
                    foreach (var f in prepFood) if (f && f.State == FoodItem.FoodState.Hidden) colony.DropFood(f);
                    SetRounds(r => r.prep);
                    customers.Empty();
                    Say("仕込みの時間。作業台に餃子が並んだ");
                    break;
                case Phase.Lunch:
                    BackToWork();
                    Busy(BusyRange, BusyAngle);
                    SetRounds(r => r.lunch);
                    customers.OpenDoors(dice.Next(LunchMin, LunchMax + 1), Seconds[(int)Phase.Lunch] * 0.65f, false);
                    Say("ランチタイム！  客が来る（店員は忙しくて見落としがち）");
                    break;
                case Phase.Break:
                    BackToWork();
                    Busy(1, 1);
                    SetRounds(r => r.rest);
                    customers.CloseDoors();
                    if (mop && Baito && Baito.gameObject.activeInHierarchy) mop.Begin();
                    SetTraps(2);
                    Say("中休み。床そうじと罠の補充");
                    break;
                case Phase.Dinner:
                    BackToWork();
                    Busy(BusyRange, BusyAngle);
                    SetRounds(r => r.dinner);
                    foreach (var f in dinnerFood) if (f && f.State == FoodItem.FoodState.Hidden) colony.DropFood(f);
                    customers.OpenDoors(dice.Next(DinnerMin, DinnerMax + 1), Seconds[(int)Phase.Dinner] * 0.65f, true);
                    Say("ディナータイム！  2Fの座敷にも客が入る");
                    break;
                case Phase.Closed:
                    customers.Empty();
                    foreach (var f in dinnerFood) if (f && f.State == FoodItem.FoodState.Available) f.Hide();   // the steamer cleared away
                    Busy(1, 1);
                    if (Chef) { Chef.BusyAngleScale = TorchAngle; Chef.SetPatrol(chefRounds.closed); }
                    SetTraps(2);
                    Say("閉店。明かりが消えた…  店主が懐中電灯で見回る");
                    break;
            }
            Changed?.Invoke(next);
        }

        private void Stop()
        {
            stopped = true;
            SetLights(true);
            Busy(1, 1);
            customers.Empty();
        }

        private void SetRounds(Func<Rounds, Transform[]> pick)
        {
            if (Chef) Chef.SetPatrol(pick(chefRounds));
            if (Okami) Okami.SetPatrol(pick(okamiRounds));
            if (Baito) Baito.SetPatrol(pick(baitoRounds));
        }
        private void Busy(float range, float angle)
        {
            for (int i = 0; i < session.HumanCount; i++) { var h = session.HumanAt(i); h.BusyRangeScale = range; h.BusyAngleScale = angle; }
        }
        private void SetLights(bool on)
        {
            for (int i = 0; i < lights.Length; i++) if (lights[i]) lights[i].intensity = brightness[i] * (on ? 1 : DimTo);
            if (torch) torch.enabled = !on;
        }

        // After closing the landlady and the part-timer walk out by the back door and are gone till morning.
        private void SendHome()
        {
            var staff = new[] { Okami, Baito };
            for (int i = 0; i < staff.Length; i++)
            {
                var h = staff[i];
                if (!h || !h.gameObject.activeSelf) continue;
                var agent = h.Agent;
                if (!home[i])
                {
                    home[i] = true;
                    foreach (var a in h.GetComponents<HouseholdAttack>()) a.Stop();
                    h.Suspended = true;
                    if (agent.isOnNavMesh && NavMesh.SamplePosition(backDoor, out NavMeshHit exit, 1.5f, agent.areaMask)) { agent.isStopped = false; agent.SetDestination(exit.position); }
                }
                if (agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance < 0.8f && !agent.isOnOffMeshLink) h.gameObject.SetActive(false);
            }
        }
        // Morning: whoever went home comes in at the back door.
        private void BackToWork()
        {
            var staff = new[] { Okami, Baito };
            for (int i = 0; i < staff.Length; i++)
            {
                var h = staff[i];
                if (!h || !home[i]) continue;
                home[i] = false;
                if (!h.gameObject.activeSelf) h.gameObject.SetActive(true);
                h.ResetHuman();
                h.Agent.Warp(backDoor);
            }
        }

        // Fresh traps where nothing is in the way, taking turns round the list of spots (poison bait and sticky sheets alternate).
        private void SetTraps(int count)
        {
            var reaction = colony.Reaction;
            if (!reaction || trapSpots.Length == 0) return;
            for (int k = 0, tries = 0; k < count && tries < trapSpots.Length; tries++)
            {
                Vector3 p = trapSpots[nextTrap]; nextTrap = (nextTrap + 1) % trapSpots.Length;
                if (!reaction.SpotAllowed(p)) continue;
                if (reaction.Place(p)) { k++; TrapsSet++; }
            }
        }

        private void Say(string text) => commander.Say(text);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Verification: as if this part of the day had only that many seconds left.</summary>
        public void DebugLeft(float seconds) { clock = Mathf.Max(0, Seconds[(int)Current] - seconds); }
#endif
    }
}
