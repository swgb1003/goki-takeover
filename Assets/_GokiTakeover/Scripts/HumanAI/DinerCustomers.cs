using System;
using System.Collections.Generic;
using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Stage 3: the diner's customers. During lunch and dinner (DinerBusiness opens and closes the doors) customers come
    /// in one by one at the entrance and take free seats: the tables and the counter, at dinner also the cushions in the
    /// banquet room upstairs. A scream (DinerCustomer) adds fear, sends the nearest member of staff to look and empties
    /// that seat. A customer leaving a table may leave food: always when scared, otherwise sometimes. The landlady clears
    /// leftovers away after LeftoverSeconds.
    /// </summary>
    public sealed class DinerCustomers : MonoBehaviour
    {
        [Serializable] public sealed class Seat
        {
            public Vector3 stand;      // on the NavMesh beside the chair or cushion
            public float yaw;          // facing the table
            public Vector3 seat;       // under the hips
            public Vector3 hands;      // on the table
            public Vector3 look;
            public BlenderHumanAnimator.LifePose pose = BlenderHumanAnimator.LifePose.Eat;
            public int table;          // which table's leftovers and plate
            public bool upstairs;      // the banquet room: dinner only
        }
        [Serializable] public sealed class Table
        {
            public FoodItem leftovers;   // hidden until a customer leaves food here
            public GameObject plate;     // the dish in front of an eating customer
        }
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private SwarmCommander commander;
        [SerializeField] private ColonyManager colony;
        [SerializeField] private FloorLinks floors;
        [SerializeField] private DinerCustomer[] pool = new DinerCustomer[0];
        [SerializeField] private Seat[] seats = new Seat[0];
        [SerializeField] private Table[] tables = new Table[0];
        [SerializeField] private Vector3 door;
        public const float LeftoverSeconds = 45f, LeaveFoodChance = 0.4f;
        private readonly List<float> arrivals = new List<float>();
        private bool open, upstairsOpen;
        private float[] clearAt;
        private int[] plates;
        private System.Random dice = new System.Random(11);
        public PhaseTwoSession Session => session;
        public SwarmCommander Commander => commander;
        public FloorLinks Floors => floors;
        public Vector3 Door => door;
        public bool Running => session.IsRunning;
        public bool Open => open;
        public IReadOnlyList<DinerCustomer> Pool => pool;
        public IReadOnlyList<Seat> Seats => seats;
        public IReadOnlyList<Table> Tables => tables;
        public int Inside { get { int n = 0; foreach (var c in pool) if (c.Present) n++; return n; } }
        public int Served { get; private set; }
        public int Screams { get; private set; }
        public int LeftoversLeft { get; private set; }
        public event Action<DinerCustomer, bool> Screamed;

        public void Configure(PhaseTwoSession game, SwarmCommander swarm, ColonyManager nest, FloorLinks links, DinerCustomer[] people, Seat[] chairs, Table[] tops, Vector3 entrance)
        { session = game; commander = swarm; colony = nest; floors = links; pool = people; seats = chairs; tables = tops; door = entrance; }

        private void Awake()
        {
            clearAt = new float[tables.Length];
            plates = new int[tables.Length];
            foreach (var c in pool) c.Bind(this);
            session.RunReset += ResetAll;
        }
        private void Start() { ResetAll(); }
        private void OnDestroy() { if (session) session.RunReset -= ResetAll; }

        public void ResetAll()
        {
            open = false; arrivals.Clear();
            dice = new System.Random(11);
            foreach (var c in pool) c.GoAway();
            for (int i = 0; i < tables.Length; i++)
            {
                clearAt[i] = 0; plates[i] = 0;
                if (tables[i].plate) tables[i].plate.SetActive(false);
                if (tables[i].leftovers && tables[i].leftovers.State == FoodItem.FoodState.Available) tables[i].leftovers.Hide();
            }
            Served = Screams = LeftoversLeft = 0;
        }

        /// <summary>Doors open: count customers arrive spread over the next seconds (upstairs too at dinner).</summary>
        public void OpenDoors(int count, float over, bool upstairs)
        {
            open = true; upstairsOpen = upstairs;
            arrivals.Clear();
            for (int i = 0; i < count; i++) arrivals.Add(Time.time + 2f + over * i / Mathf.Max(1, count) + (float)dice.NextDouble() * 4f);
        }
        /// <summary>Doors closed: nobody new; those eating finish soon.</summary>
        public void CloseDoors()
        {
            open = false; arrivals.Clear();
            foreach (var c in pool) c.Hurry();
        }
        /// <summary>Everyone out at once (closing time).</summary>
        public void Empty()
        {
            CloseDoors();
            foreach (var c in pool) if (c.Present) { if (c.SeatIndex >= 0) Plate(seats[c.SeatIndex].table, false); c.GoAway(); }
        }

        private void Update()
        {
            if (!session.IsRunning) return;
            if (open)
                for (int i = arrivals.Count - 1; i >= 0; i--)
                    if (Time.time >= arrivals[i] && Admit() != null) arrivals.RemoveAt(i);
            for (int i = 0; i < tables.Length; i++)
                if (clearAt[i] > 0 && Time.time >= clearAt[i])
                {
                    clearAt[i] = 0;
                    var food = tables[i].leftovers;
                    if (food && food.State == FoodItem.FoodState.Available) food.Hide();   // the landlady cleared the table
                }
        }

        /// <summary>The next free customer to the next free seat (none free: wait). Verification may name both.</summary>
        public DinerCustomer Admit(int customer = -1, int seatIndex = -1)
        {
            var who = customer >= 0 ? pool[customer] : null;
            if (!who) foreach (var c in pool) if (!c.Present) { who = c; break; }
            if (!who || who.Present) return null;
            if (seatIndex < 0)
            {
                var free = new List<int>();
                for (int s = 0; s < seats.Length; s++) if (SeatFree(s) && (upstairsOpen || !seats[s].upstairs)) free.Add(s);
                if (free.Count == 0) return null;
                seatIndex = free[dice.Next(free.Count)];
            }
            who.Arrive(seatIndex, seats[seatIndex], door);
            Served++;
            return who;
        }
        private bool SeatFree(int s) { foreach (var c in pool) if (c.Present && c.SeatIndex == s) return false; return true; }

        public void Plate(int table, bool on)
        {
            if (table < 0 || table >= tables.Length) return;
            plates[table] = Mathf.Max(0, plates[table] + (on ? 1 : -1));
            if (tables[table].plate) tables[table].plate.SetActive(plates[table] > 0);
        }

        /// <summary>A customer got up from a seat: food left on the table, always when scared, sometimes otherwise.</summary>
        public void Left(DinerCustomer who, Seat seat, bool atTable)
        {
            if (seat == null || !atTable) return;
            bool leave = who.Scared || dice.NextDouble() < LeaveFoodChance;
            var food = seat.table >= 0 && seat.table < tables.Length ? tables[seat.table].leftovers : null;
            if (!leave || !food || food.State == FoodItem.FoodState.Available || food.State == FoodItem.FoodState.Carried) return;
            if (colony.DropFood(food)) { LeftoversLeft++; clearAt[seat.table] = Time.time + LeftoverSeconds; }
        }
        public void Gone(DinerCustomer who) { }

        /// <summary>A customer saw a roach: fear, a sighting if it was the player, staff on their way.</summary>
        public void Scream(DinerCustomer who, Vector3 where, bool player)
        {
            Screams++;
            var reaction = colony.Reaction;
            if (reaction) { reaction.AddFear(DinerCustomer.FearPerScream); reaction.SayAs(who.Body, player ? "キャー！  ゴキブリ！！" : "ひっ、ゴキブリが歩いてる！"); }
            if (player) session.Discoveries++;
            // The nearest member of staff on the same floor comes over to look.
            HumanAIController best = null; float bestDistance = float.MaxValue;
            for (int i = 0; i < session.HumanCount; i++)
            {
                if (!session.HumanPresent(i)) continue;
                var h = session.HumanAt(i);
                if (floors && floors.FloorOf(h.transform.position) != floors.FloorOf(where)) continue;
                float d = (h.transform.position - where).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = h; }
            }
            if (best) best.Alert(where);
            commander.Say(player ? "客に見つかった！  悲鳴で店員が駆けつける" : "客が仲間を見つけて悲鳴！  店員が来る");
            Screamed?.Invoke(who, player);
        }
    }
}
