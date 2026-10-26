using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    /// <summary>
    /// Stage 3: one customer of the diner (a salaryman, a student or a grandma). They come in at the door, walk to a free
    /// seat, sit and eat, then go. They never attack. If they see a roach (the player or an ally) they scream: the fear
    /// jumps (FearPerScream, three ordinary sightings), the nearest member of staff comes to look, and they run out
    /// leaving their food (leftovers on the table). The body is a copy of the cook's with its AI held still; this drives
    /// the agent and the sitting and eating poses itself.
    /// </summary>
    public sealed class DinerCustomer : MonoBehaviour
    {
        public enum Phase { Away, Enter, Seated, Leave, Flee }
        [SerializeField] private string kind = "客";
        [SerializeField] private float speed = 1f;
        public const float SightRange = 4.5f, SightHalfAngle = 70f, FearPerScream = 8f, EatMin = 28f, EatMax = 45f;
        private DinerCustomers house;
        private HumanAIController body;
        private NavMeshAgent agent;
        private BlenderHumanAnimator animator;
        private CapsuleCollider capsule;
        private Vector3 capsuleRest;
        private DinerCustomers.Seat seat;
        private int seatIndex = -1;
        private float until, senseClock;
        public Phase Current { get; private set; }
        public string Kind => kind;
        public int SeatIndex => seatIndex;
        public bool Present => Current != Phase.Away;
        public bool Scared { get; private set; }
        public int Screams { get; private set; }
        public HumanAIController Body => body;
        public Vector3 Eye => transform.position + (Current == Phase.Seated && seat != null ? new Vector3(seat.seat.x - transform.position.x, seat.seat.y + 1.5f, seat.seat.z - transform.position.z) : Vector3.up * 3.5f);

        public void Configure(string label, float walk) { kind = label; speed = walk; }

        public void Bind(DinerCustomers owner)
        {
            house = owner;
            body = GetComponent<HumanAIController>();
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<BlenderHumanAnimator>(true);
            capsule = GetComponent<CapsuleCollider>();
            if (capsule) capsuleRest = capsule.center;
            body.Suspended = true;   // its own AI never runs: this drives it
            agent.autoTraverseOffMeshLink = false;
            GoAway();
        }

        /// <summary>In at the door, off to this seat.</summary>
        public void Arrive(int index, DinerCustomers.Seat target, Vector3 door)
        {
            seatIndex = index; seat = target; Scared = false;
            gameObject.SetActive(true);
            body.Suspended = true;
            agent.Warp(door);
            agent.isStopped = false; agent.updateRotation = true;
            agent.speed = 1.9f * speed;
            agent.SetDestination(seat.stand);
            Current = Phase.Enter;
        }

        /// <summary>Out at once (closing time, a reset).</summary>
        public void GoAway()
        {
            Stand();
            seatIndex = -1; seat = null;
            Current = Phase.Away;
            gameObject.SetActive(false);
        }

        /// <summary>Finish up soon (the lunch hour is over).</summary>
        public void Hurry() { if (Current == Phase.Seated) until = Mathf.Min(until, Time.time + Random.Range(3f, 9f)); }

        private void Update()
        {
            if (!house || !house.Running) return;
            float dt = Time.deltaTime;
            if (agent.isOnNavMesh && agent.isOnOffMeshLink && house.Floors) { house.Floors.Traverse(body, dt); return; }
            switch (Current)
            {
                case Phase.Enter:
                    if (Arrived(0.5f)) Sit();
                    break;
                case Phase.Seated:
                    if (Time.time >= until) Leave(false);
                    break;
                case Phase.Leave:
                case Phase.Flee:
                    if (Arrived(0.8f)) { house.Gone(this); GoAway(); return; }
                    break;
            }
            if (Current == Phase.Enter || Current == Phase.Seated || Current == Phase.Leave)
            {
                senseClock += dt;
                if (senseClock >= 0.2f) { senseClock = 0; Sense(); }
            }
        }
        private bool Arrived(float within) => agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= within && !agent.isOnOffMeshLink;

        private void Sit()
        {
            Current = Phase.Seated;
            until = Time.time + Random.Range(EatMin, EatMax);
            agent.isStopped = true; agent.updateRotation = false;
            transform.rotation = Quaternion.Euler(0, seat.yaw, 0);
            Vector3 shift = seat.seat - seat.stand; shift.y = 0;
            if (capsule) capsule.center = capsuleRest + Quaternion.Inverse(transform.rotation) * shift;
            if (animator) { animator.Pose = seat.pose; animator.SeatPoint = seat.seat; animator.HandPoint = seat.hands; animator.LookPoint = seat.look; }
            house.Plate(seat.table, true);
        }
        private void Stand()
        {
            if (animator) { animator.Pose = BlenderHumanAnimator.LifePose.None; animator.HeldProp = null; }
            if (capsule) capsule.center = capsuleRest;
            if (agent && agent.isOnNavMesh) { agent.isStopped = false; agent.updateRotation = true; }
        }
        private void Leave(bool scared)
        {
            bool wasSeated = Current == Phase.Seated;
            Stand();
            Scared = scared;
            Current = scared ? Phase.Flee : Phase.Leave;
            agent.speed = (scared ? 4.2f : 1.9f) * speed;
            agent.SetDestination(house.Door);
            if (wasSeated) house.Plate(seat.table, false);
            house.Left(this, seat, wasSeated || scared);
        }

        // A roach in sight (cover and walls hide it, like for the staff); the player is preferred.
        private void Sense()
        {
            Vector3 eye = Eye;
            var session = house.Session;
            if (!session.InSafeNest && Sees(eye, session.Player.position)) { Scream(session.Player.position, true); return; }
            foreach (var ally in house.Commander.Allies)
                if (ally.Active && !session.InNest(ally.Position + Vector3.up * 0.1f) && Sees(eye, ally.Position)) { Scream(ally.Position, false); return; }
        }
        private bool Sees(Vector3 eye, Vector3 target)
        {
            Vector3 delta = target - transform.position; delta.y = 0;
            if (delta.sqrMagnitude > SightRange * SightRange || Vector3.Angle(transform.forward, delta) > SightHalfAngle) return false;
            Vector3 at = target + Vector3.up * 0.15f;
            if (target.y < 0.5f && Physics.Raycast(at, Vector3.up, out RaycastHit roof, 3.3f, ~(1 << 2), QueryTriggerInteraction.Ignore)) return false;   // under furniture
            return !Physics.Linecast(eye, at, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }
        private void Scream(Vector3 where, bool player)
        {
            Screams++;
            house.Scream(this, where, player);
            Leave(true);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Verification: sit at once (no walk), eating for this long.</summary>
        public void DebugSeat(float eating) { agent.Warp(seat.stand); Sit(); until = Time.time + eating; }
        public void DebugScream(Vector3 where, bool player) => Scream(where, player);
#endif
    }
}
