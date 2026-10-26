using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    /// <summary>
    /// Stage 3 (町中華, 2026-10-26): the ground floor and the first floor lie apart on the same plane (floor y = 0), so
    /// every floor check, the allies' grid and the humans' NavMesh work as on one floor. The stairs and the duct join
    /// them: the player walks into a stairwell or a duct mouth, the screen dips to black and they come out on the other
    /// floor; allies follow grid links (AllyNavGrid) the same way; humans take the stairs as NavMesh links, walking up
    /// the steps out of sight and down onto the other floor (IFloorTraveller).
    /// </summary>
    // After KitchenNavigation (-300) has added the NavMesh, and after the session (-150) re-enables movement each frame but
    // before the motor moves: the player stands still while the screen is dark.
    [DefaultExecutionOrder(-100)]
    public sealed class FloorLinks : MonoBehaviour, IFloorTraveller
    {
        [Serializable] public sealed class End
        {
            public Vector3 ground;     // on the floor at this end: the NavMesh link point and the allies' link cell
            public Vector3 hidden;     // where a human going this way is out of sight (up the steps, down the well)
            public Bounds trigger;     // the player stepping in here goes through
            public Vector3 arrive;     // where the player comes out at this end, facing arriveYaw
            public float arriveYaw;
        }
        [Serializable] public sealed class Link
        {
            public string name = "階段";
            public bool humans = true;   // the stairs; the duct is for roaches only
            public End a = new End(), b = new End();
        }
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private Link[] links = new Link[0];
        [SerializeField] private float secondFloorX = 1000;   // x at or beyond this is the first floor (2F)
        public const float FadeOut = 0.22f, FadeIn = 0.32f, HumanLeave = 1.1f, HumanArrive = 1.1f;

        private readonly List<NavMeshLinkInstance> instances = new List<NavMeshLinkInstance>();
        private bool[] inside;
        private int phase;   // 0 none, 1 fading out, 2 fading in
        private float clock;
        private End destination;
        private sealed class Trip { public End from, to; public float t, seen; }
        private readonly Dictionary<HumanAIController, Trip> trips = new Dictionary<HumanAIController, Trip>();
        private Texture2D black;

        public IReadOnlyList<Link> Links => links;
        public float SecondFloorX => secondFloorX;
        public int FloorOf(Vector3 position) => position.x >= secondFloorX ? 2 : 1;
        public string FloorName(Vector3 position) => FloorOf(position) == 2 ? "2F" : "1F";
        public bool Travelling => phase != 0;
        public float FadeAlpha => phase == 1 ? Mathf.Clamp01(clock / FadeOut) : phase == 2 ? 1 - Mathf.Clamp01(clock / FadeIn) : 0;
        public int Trips { get; private set; }
        public event Action<string> PlayerTravelled;

        public void Configure(PhaseTwoSession game, Link[] all, float firstFloorX)
        { session = game; links = all; secondFloorX = firstFloorX; }

        private void OnEnable()
        {
            foreach (var link in links)
            {
                if (!link.humans) continue;
                var data = new NavMeshLinkData
                {
                    startPosition = link.a.ground, endPosition = link.b.ground, width = 0.9f,
                    bidirectional = true, costModifier = 1, area = 0, agentTypeID = 0
                };
                var instance = NavMesh.AddLink(data);
                if (NavMesh.IsLinkValid(instance)) instances.Add(instance);
            }
        }
        private void OnDisable()
        {
            foreach (var instance in instances) if (NavMesh.IsLinkValid(instance)) NavMesh.RemoveLink(instance);
            instances.Clear();
        }
        private void Start()
        {
            for (int i = 0; i < session.HumanCount; i++) Register(session.HumanAt(i));
        }
        /// <summary>A human who may use the stairs (everyone in the scene at the start; later arrivals register themselves).</summary>
        public void Register(HumanAIController human)
        {
            if (!human) return;
            human.Floors = this;
            var agent = human.GetComponent<NavMeshAgent>();
            if (agent) agent.autoTraverseOffMeshLink = false;
        }

        private void Update()
        {
            if (inside == null || inside.Length != links.Length * 2) inside = new bool[links.Length * 2];
            float dt = Time.deltaTime;
            if (phase != 0 && session.State == PhaseTwoSession.RunState.Dead) phase = 0;
            if (phase != 0)
            {
                clock += dt;
                session.Motor.MovementEnabled = false;
                if (phase == 1 && clock >= FadeOut)
                {
                    session.TeleportPlayer(destination.arrive, destination.arriveYaw);
                    phase = 2; clock = 0;
                }
                else if (phase == 2 && clock >= FadeIn) phase = 0;
                Track();   // arriving inside the far end's trigger must not send the player straight back
                return;
            }
            if (!session.IsRunning || session.Restrained) { Track(); return; }
            Vector3 body = session.Motor.BodyCentre;
            for (int i = 0; i < links.Length; i++)
                for (int e = 0; e < 2; e++)
                {
                    var end = e == 0 ? links[i].a : links[i].b;
                    bool now = end.trigger.Contains(body);
                    if (now && !inside[i * 2 + e]) { Go(links[i], e == 0 ? links[i].b : links[i].a); Track(); return; }
                }
            Track();
        }
        private void Track()
        {
            Vector3 body = session.Motor.BodyCentre;
            for (int i = 0; i < links.Length; i++)
            {
                inside[i * 2] = links[i].a.trigger.Contains(body);
                inside[i * 2 + 1] = links[i].b.trigger.Contains(body);
            }
        }
        private void Go(Link link, End to)
        {
            destination = to;
            phase = 1; clock = 0;
            Trips++;
            PlayerTravelled?.Invoke(link.name);
        }
        /// <summary>The prompt near a way up or down (the HUD shows it when nothing more urgent is there), else null.</summary>
        public string Hint(Vector3 position)
        {
            foreach (var link in links)
                for (int e = 0; e < 2; e++)
                {
                    var end = e == 0 ? link.a : link.b;
                    if (Flat(position - end.ground) > (link.humans ? 2.2f : 1.4f)) continue;
                    string to = FloorOf((e == 0 ? link.b : link.a).ground) == 2 ? "2F" : "1F";
                    if (!link.humans) return "ダクトに入ると " + to + " へ（ゴキブリだけの道）";
                    return FloorOf(end.ground) == 1 ? "階段を登ると " + to + " へ" : "階段を下りると " + to + " へ";
                }
            return null;
        }
        /// <summary>Verification: send the player through a link end at once (no walking into the trigger).</summary>
        public void TravelNow(int link, bool toB)
        {
            var l = links[link];
            session.TeleportPlayer((toB ? l.b : l.a).arrive, (toB ? l.b : l.a).arriveYaw);
            Track();
        }

        // ---- Humans on the stairs ----
        public bool Traverse(HumanAIController human, float dt)
        {
            var agent = human.Agent;
            if (!trips.TryGetValue(human, out var trip) || Time.time - trip.seen > 0.5f)
            {
                var data = agent.currentOffMeshLinkData;
                trip = Begin(data.startPos);
                if (trip == null) { agent.CompleteOffMeshLink(); return false; }
                trips[human] = trip;
            }
            trip.seen = Time.time;
            trip.t += dt;
            Vector3 position, toward;
            if (trip.t < HumanLeave) { position = Vector3.Lerp(trip.from.ground, trip.from.hidden, trip.t / HumanLeave); toward = trip.from.hidden - trip.from.ground; }
            else if (trip.t < HumanLeave + HumanArrive) { position = Vector3.Lerp(trip.to.hidden, trip.to.ground, (trip.t - HumanLeave) / HumanArrive); toward = trip.to.ground - trip.to.hidden; }
            else
            {
                trips.Remove(human);
                agent.CompleteOffMeshLink();
                return false;
            }
            human.transform.position = position;
            toward.y = 0;
            if (toward.sqrMagnitude > 1e-4f) human.transform.rotation = Quaternion.LookRotation(toward);
            return true;
        }
        private Trip Begin(Vector3 start)
        {
            float best = 2f; Trip trip = null;
            foreach (var link in links)
            {
                if (!link.humans) continue;
                float da = Flat(start - link.a.ground), db = Flat(start - link.b.ground);
                if (da < best) { best = da; trip = new Trip { from = link.a, to = link.b }; }
                if (db < best) { best = db; trip = new Trip { from = link.b, to = link.a }; }
            }
            return trip;
        }
        private static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

        private void OnGUI()
        {
            float alpha = FadeAlpha;
            if (alpha <= 0.001f) return;
            if (!black) { black = new Texture2D(1, 1); black.SetPixel(0, 0, Color.black); black.Apply(); }
            GUI.depth = -1000;
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);
            GUI.color = old;
        }
        private void OnDestroy() { if (black) Destroy(black); }
    }
}
