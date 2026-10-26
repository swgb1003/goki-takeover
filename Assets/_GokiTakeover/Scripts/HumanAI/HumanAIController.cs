using System;
using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    /// <summary>Other roaches the resident notices, chases and swats besides the player (Phase 5 allies). They stay on the floor.</summary>
    public interface IHumanPrey
    {
        int PreyCount { get; }
        bool PreyTargetable(int index);
        Vector3 PreyPosition(int index);
        Vector3 PreyVelocity(int index);
    }

    /// <summary>
    /// Stage 2 daily life (HouseholdLife): while it leads, the calm routine walks to an activity instead of the patrol,
    /// and a busy person sees a narrower, shorter cone from where their eyes really are (seated, on the floor).
    /// </summary>
    public interface IDailyLife
    {
        bool Leads { get; }
        Vector3 Destination { get; }
        float SightAngleScale { get; }
        float SightRangeScale { get; }
        Vector3 EyeShift { get; }
    }

    /// <summary>
    /// Stage 3 floors (FloorLinks): the stairs are NavMesh links between the two floors. While the agent stands on one,
    /// this walks the person through (up the steps, out of sight, down onto the other floor) instead of the agent
    /// sliding straight across. Returns true while it is still moving them.
    /// </summary>
    public interface IFloorTraveller
    {
        bool Traverse(HumanAIController human, float dt);
    }

    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class HumanAIController : MonoBehaviour
    {
        public enum HumanState { Routine, Chase, Search, Attack }
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private Transform warning, slipper, leftLeg, rightLeg;
        // Stage 2 household: a name for the HUD, whether it swats with a slipper (the son only nets roaches) and eye height.
        [SerializeField] private string label = "";
        [SerializeField] private bool slaps = true;
        [SerializeField] private float eyeHeight = 3.5f;
        [SerializeField] private float speedFactor = 1f;
        public string Label => string.IsNullOrEmpty(label) ? "人間" : label;
        public bool Slaps => slaps;
        public float EyeHeight => eyeHeight;
        public void ConfigureMember(string name, bool slipper, float eyes, float speed) { label = name; slaps = slipper; eyeHeight = eyes; speedFactor = speed; }
        private NavMeshAgent agent;
        private Vector3 spawn, lastSeen, attackPoint;
        private Quaternion spawnRotation, slipperRest;
        private float sensorClock, lostTime, attackTime, cooldownUntil;
        private int waypoint;
        private bool landed;
        public HumanState State { get; private set; }
        public float Detection { get; private set; }
        public bool CanSeePlayer { get; private set; }
        // Null: the resident only ever targets the player (Phase 2-4). Otherwise the nearest roach in sight is the
        // target, the player preferred unless an ally is clearly closer; the current target is kept while it stays close.
        public IHumanPrey ExtraPrey { get; set; }
        public const float PlayerPreference = 0.6f, KeepTarget = 0.8f;
        // -1: the player; otherwise an index into ExtraPrey.
        public int Prey { get; private set; } = -1;
        public bool TargetIsPlayer => Prey < 0;
        public bool CanSeeTarget { get; private set; }
        public Vector3 AttackPoint => attackPoint;
        public float AttackProgress => State == HumanState.Attack ? Mathf.Clamp01(attackTime / session.Settings.attackWindup) : 0;
        // Seconds since the windup began; the slipper lands at attackWindup and is lifted during attackRecover.
        public float AttackTime => State == HumanState.Attack ? attackTime : 0;
        public bool SlipperLanded => State == HumanState.Attack && landed;
        // Flat direction from the resident to the strike point: the slipper's toe points this way on impact.
        public Vector3 StrikeForward { get; private set; } = Vector3.forward;
        // The face the sole meets: up for the floor and the worktop, the wall normal for a wall (Phase 5 reach).
        public Vector3 StrikeUp { get; private set; } = Vector3.up;
        // Toe direction on that face: away from the resident on a flat face, upward on a wall.
        public Vector3 StrikeAlong => StrikeUp.y > 0.99f ? StrikeForward : Vector3.ProjectOnPlane(Vector3.up, StrikeUp).normalized;
        // A roach farther than this from the struck face (the floor under the table, a wall's other side) is missed.
        public const float StrikeDepth = 0.6f;
        // Raised once per swing when the sole reaches the floor; crushed is true only if the roach was under it.
        public event Action<Vector3, bool> SlipperLanding;
        // The roach is big next to the slipper: a sole that crushes it rests on its flattened back, not inside it.
        public const float CrushedRoachHeight = 0.15f;
        public float StrikeSurfaceHeight { get; private set; }
        public PhaseTwoSettings Settings => session.Settings;
        // Phase 4 reaction levels and the stealth upgrade scale these; another component may suspend the
        // state machine while it drives the resident itself (placing traps, spraying). Defaults: unchanged.
        public bool Suspended { get; set; }
        public float DetectionTimeScale { get; set; } = 1f;
        public float SpeedScale { get; set; } = 1f;
        public float SearchTimeScale { get; set; } = 1f;
        // Phase 5: a lost roach is searched for on the move (short walks around where it was last seen) instead
        // of standing still at that spot until the search times out. Off: the old behaviour.
        public bool SearchWander { get; set; }
        // Phase 5: the patrol wanders the whole room, the next stop picked at random among the nearer ones
        // (never the one just left), instead of walking the points in a fixed loop.
        public bool PatrolRandom { get; set; }
        private int previousWaypoint = -1;
        public float SearchSecondsOverride { get; set; }
        private bool wandering;
        // An ally it cannot get close enough to slap (against a wall, behind furniture): give up on it for a while
        // instead of standing there staring (with SearchWander).
        private int ignoredPrey = -1;
        private float ignoreUntil, blockedTime;
        private static readonly float[] WanderTurns = { 55, -70, 35, -45, 95, -100, 20, -25 };
        private int wanderStep;
        public NavMeshAgent Agent => agent;
        // Null: no daily life (every scene before Stage 2's family); the sight and the routine are unchanged.
        public IDailyLife Life { get; set; }
        // Null: one floor (every scene before Stage 3); the agent is never on a link.
        public IFloorTraveller Floors { get; set; }
        public bool OnStairs => Floors != null && agent && agent.isOnOffMeshLink;
        private bool Living => Life != null && State == HumanState.Routine && Life.Leads;
        public float SightAngle => Settings.sightAngle * (Living ? Life.SightAngleScale : 1);
        public float SightRange => Settings.sightDistance * (Living ? Life.SightRangeScale : 1);
        public Vector3 Eye => transform.position + Vector3.up * eyeHeight + (Living ? Life.EyeShift : Vector3.zero);
        public void Configure(PhaseTwoSession game, Transform[] route, Transform telegraph, Transform weapon, Transform left, Transform right)
        { session = game; patrolPoints = route; warning = telegraph; slipper = weapon; leftLeg = left; rightLeg = right; }
        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.enabled = true;
            spawn = transform.position;
            spawnRotation = transform.rotation;
            slipperRest = slipper.localRotation;
        }
        private void Update()
        {
            if (!session.IsRunning) return;
            Tick(Time.deltaTime);
            float gait = Mathf.Sin(Time.time * 7) * Mathf.Clamp01(agent.velocity.magnitude) * 15;
            leftLeg.localRotation = Quaternion.Euler(gait, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-gait, 0, 0);
        }
        public bool HasLineOfSight()
        {
            if (session.InSafeNest) return false;
            Vector3 eye = Eye;
            Vector3 target = session.Player.position + Vector3.up * 0.15f;
            // Low furniture is a readable hiding place even when its open side permits a diagonal ray.
            // Only on the floor: a roach high on a wall or on the worktop would otherwise count the ceiling as cover.
            if (session.Player.position.y < 0.5f && Physics.Raycast(target, Vector3.up, out RaycastHit roof, 3.3f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                && !session.IsHumanBody(roof.transform)) return false;
            return !Physics.Linecast(eye, target, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }
        public bool SensePlayer()
        {
            Vector3 delta = Vector3.ProjectOnPlane(session.Player.position - transform.position, Vector3.up);
            return delta.sqrMagnitude < SightRange * SightRange &&
                Vector3.Angle(transform.forward, delta) < SightAngle * 0.5f && HasLineOfSight();
        }
        // Same cone and cover rules as the player on the floor: furniture overhead hides an ally too.
        public bool SensePrey(Vector3 position)
        {
            Vector3 delta = Vector3.ProjectOnPlane(position - transform.position, Vector3.up);
            if (delta.sqrMagnitude >= SightRange * SightRange || Vector3.Angle(transform.forward, delta) >= SightAngle * 0.5f) return false;
            Vector3 target = position + Vector3.up * 0.15f;
            if (Physics.Raycast(target, Vector3.up, out RaycastHit roof, 3.3f, ~(1 << 2), QueryTriggerInteraction.Ignore) && !session.IsHumanBody(roof.transform)) return false;
            return !Physics.Linecast(Eye, target, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }
        private bool SelectTarget()
        {
            int previous = Prey;
            Prey = -1;
            if (ExtraPrey == null) return CanSeePlayer;
            float best = CanSeePlayer ? FlatDistance(session.Player.position) * PlayerPreference * (previous < 0 ? KeepTarget : 1) : float.MaxValue;
            for (int i = 0; i < ExtraPrey.PreyCount; i++)
            {
                if (!ExtraPrey.PreyTargetable(i) || (i == ignoredPrey && Time.time < ignoreUntil)) continue;
                Vector3 position = ExtraPrey.PreyPosition(i);
                float score = FlatDistance(position) * (i == previous ? KeepTarget : 1);
                if (score < best && SensePrey(position)) { best = score; Prey = i; }
            }
            return Prey >= 0 || CanSeePlayer;
        }
        private float FlatDistance(Vector3 position) => Vector3.ProjectOnPlane(position - transform.position, Vector3.up).magnitude;
        private Vector3 TargetPosition => Prey < 0 ? session.Player.position : ExtraPrey.PreyPosition(Prey);
        // Sensor/path refresh is limited to 10 Hz; the agent handles smooth motion between ticks.
        public void Tick(float dt)
        {
            if (!session.IsRunning || Suspended || !agent.isOnNavMesh || dt <= 0) return;
            if (Floors != null && agent.isOnOffMeshLink && State != HumanState.Attack && Floors.Traverse(this, dt)) return;
            sensorClock += dt;
            if (sensorClock >= 0.1f)
            {
                float step = sensorClock;
                sensorClock = 0;
                CanSeePlayer = SensePlayer();
                // The strike point is fixed during a swing: keep the target it was aimed at.
                if (State != HumanState.Attack) CanSeeTarget = SelectTarget();
                else CanSeeTarget = Prey < 0 ? CanSeePlayer : ExtraPrey.PreyTargetable(Prey) && SensePrey(ExtraPrey.PreyPosition(Prey));
                Detection = Mathf.Clamp01(Detection + (CanSeeTarget ? step / (Settings.detectionSeconds * DetectionTimeScale) : -step * 0.65f));
                if (CanSeeTarget) { lastSeen = TargetPosition; lostTime = 0; wandering = false; }
                else lostTime += step;
                if (State == HumanState.Routine && Detection >= 1)
                { State = HumanState.Chase; if (Prey < 0) session.Discoveries++; }   // discoveries rate the player's stealth only
                else if (State == HumanState.Search && CanSeeTarget)
                    State = HumanState.Chase;
                if (State == HumanState.Chase && !CanSeeTarget && lostTime >= 0.6f)
                    State = HumanState.Search;
                if (SearchWander && State == HumanState.Chase && Prey >= 0 && CanSeeTarget && !InPreyRange() && agent.velocity.sqrMagnitude < 0.04f)
                    blockedTime += step;
                else blockedTime = 0;
                if (blockedTime >= 0.7f)
                {
                    ignoredPrey = Prey; ignoreUntil = Time.time + 5f; blockedTime = 0;
                    lastSeen = transform.position; State = HumanState.Search; wandering = true; wanderStep = 0; lostTime = 0.6f;
                }
                if (State == HumanState.Search && lostTime >= (SearchSecondsOverride > 0 ? SearchSecondsOverride : Settings.searchSeconds) * SearchTimeScale)
                { State = HumanState.Routine; Detection = 0; }
                if (State != HumanState.Attack) RefreshDestination();
            }
            if (slaps && State == HumanState.Chase && CanSeeTarget && Time.time >= cooldownUntil && (Prey < 0 ? session.PlayerStrikeable && InStrikeRange() : InPreyRange()))
                BeginAttack();
            if (State != HumanState.Attack) return;
            attackTime += dt;
            if (!landed) AimStrike(dt);
            if (slipper) slipper.localRotation = landed ? slipperRest : slipperRest * Quaternion.Euler(-80 * Mathf.Sin(AttackProgress * Mathf.PI), 0, 0);
            if (!landed && attackTime >= Settings.attackWindup) Land();
            if (attackTime < Settings.attackWindup + Settings.attackRecover) return;
            cooldownUntil = Time.time + Settings.attackCooldown;
            State = CanSeeTarget ? HumanState.Chase : HumanState.Search;
            agent.isStopped = false;
            agent.updateRotation = true;
            RefreshDestination();
        }
        private bool InStrikeRange()
        {
            if (session.StrikeReach <= 0) return Vector3.Distance(transform.position, session.Player.position) < Settings.attackRange;
            // Reach mode: the arm covers the height, so only the distance across the floor counts.
            Vector3 flat = Vector3.ProjectOnPlane(session.PlayerStrikePoint - transform.position, Vector3.up);
            Vector3 face = session.PlayerSurfaceUp;
            // A wall face turned away from the resident (the far side of a cabinet) cannot be slapped.
            if (face.y < 0.7f && Vector3.Dot(Vector3.ProjectOnPlane(face, Vector3.up), flat) >= 0) return false;
            return flat.magnitude < Settings.attackRange;
        }
        private bool InPreyRange() => ExtraPrey.PreyTargetable(Prey) && FlatDistance(ExtraPrey.PreyPosition(Prey)) < Settings.attackRange;
        private void BeginAttack()
        {
            State = HumanState.Attack;
            agent.isStopped = true;
            agent.updateRotation = false;
            if (Prey >= 0)
            {
                // An ally on the floor: aim a little ahead of where it is walking.
                StrikeUp = Vector3.up;
                attackPoint = ExtraPrey.PreyPosition(Prey) + Vector3.ClampMagnitude(ExtraPrey.PreyVelocity(Prey) * Settings.attackWindup * 0.5f, 0.6f);
                attackPoint.y = 0.025f;
            }
            else if (session.StrikeReach > 0)
            {
                Vector3 face = session.PlayerSurfaceUp;
                StrikeUp = face.y > 0.7f ? Vector3.up : face.normalized;   // the floor and the worktop are struck flat
                attackPoint = session.PlayerStrikePoint + StrikeUp * 0.025f;
            }
            else
            {
                StrikeUp = Vector3.up;
                attackPoint = session.Player.position;
                attackPoint.y = 0.025f;
            }
            attackTime = 0;
            landed = false;
            AimStrike(0);
            warning.gameObject.SetActive(true);
        }
        // The strike point is fixed at windup start. The resident turns to it and steps to slamming distance.
        private void AimStrike(float dt)
        {
            Vector3 flat = Vector3.ProjectOnPlane(attackPoint - transform.position, Vector3.up);
            if (flat.sqrMagnitude > 1e-4f)
            {
                StrikeForward = flat.normalized;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(StrikeForward), 540 * dt);
                float gap = flat.magnitude - Settings.strikeDistance;
                if (dt > 0 && attackTime < Settings.attackWindup * 0.75f && Mathf.Abs(gap) > 0.03f && agent.isOnNavMesh)
                    agent.Move(StrikeForward * Mathf.Clamp(gap, -Settings.patrolSpeed * dt, Settings.chaseSpeed * dt));
            }
            StrikeSurfaceHeight = SurfaceLift + (session.PlayerStrikeable && IsUnderSlipper(session.PlayerStrikePoint) ? CrushedRoachHeight : 0);
            warning.SetPositionAndRotation(attackPoint, Quaternion.LookRotation(StrikeAlong, StrikeUp));
            warning.localScale = new Vector3((Settings.slipperHalfWidth + Settings.crushMargin) * 2, 0.015f,
                (Settings.slipperHalfLength + Settings.crushMargin) * 2);
        }
        public bool IsUnderSlipper(Vector3 position)
        {
            Vector3 offset = position - attackPoint;
            if (session.StrikeReach > 0 && Mathf.Abs(Vector3.Dot(offset, StrikeUp)) > StrikeDepth) return false;
            offset = Vector3.ProjectOnPlane(offset, StrikeUp);
            Vector3 toe = StrikeAlong;
            float along = Vector3.Dot(offset, toe) / (Settings.slipperHalfLength + Settings.crushMargin);
            float across = Vector3.Dot(offset, Vector3.Cross(StrikeUp, toe)) / (Settings.slipperHalfWidth + Settings.crushMargin);
            return along * along + across * across <= 1;
        }
        // Height of a flat struck face above the resident's feet (the worktop); 0 on the floor and for walls.
        private float SurfaceLift => session.StrikeReach > 0 && StrikeUp.y > 0.99f ? Mathf.Max(0, attackPoint.y - 0.025f - transform.position.y) : 0;
        // Damage happens only here, when the sole meets the floor; furniture overhead still shields the roach.
        private void Land()
        {
            landed = true;
            warning.gameObject.SetActive(false);
            bool crushed = session.PlayerStrikeable && IsUnderSlipper(session.PlayerStrikePoint) && HasLineOfSight() && session.TryDamage(1);
            StrikeSurfaceHeight = SurfaceLift + (crushed ? CrushedRoachHeight : 0);
            SlipperLanding?.Invoke(attackPoint, crushed);
        }
        private void RefreshDestination()
        {
            agent.speed = (State == HumanState.Chase ? Settings.chaseSpeed : State == HumanState.Search && SearchWander ? (Settings.patrolSpeed + Settings.chaseSpeed) * 0.5f : Settings.patrolSpeed) * SpeedScale * speedFactor;
            if (State == HumanState.Routine)
            {
                if (Living) { SetDestination(Life.Destination); return; }
                if (patrolPoints.Length == 0) return;
                if (!agent.pathPending && agent.hasPath && agent.remainingDistance < 0.35f) waypoint = NextWaypoint();
                SetDestination(patrolPoints[waypoint].position);
            }
            else if (State == HumanState.Search && SearchWander) Wander();
            else SetDestination(lastSeen);
        }
        // Close enough to pick the next spot before the agent brakes to a stop.
        private bool Arrived => !agent.pathPending && (!agent.hasPath || agent.remainingDistance < 0.9f);
        // To the last sighting first, then short looks around it: never standing still while searching.
        private void Wander()
        {
            if (!wandering)
            {
                SetDestination(lastSeen);
                if (Arrived && FlatDistance(lastSeen) < 1.4f) { wandering = true; wanderStep = 0; }
                else return;
            }
            if (!Arrived) return;
            // Mostly ahead (no U-turns, which make the agent brake to a halt), staying near the last sighting.
            for (int k = 0; k < 8; k++)
            {
                wanderStep++;
                float turn = WanderTurns[wanderStep % WanderTurns.Length];
                Vector3 look = transform.position + Quaternion.Euler(0, transform.eulerAngles.y + turn, 0) * Vector3.forward * 2.2f;
                if (Vector3.ProjectOnPlane(look - lastSeen, Vector3.up).magnitude > 3.2f) continue;
                if (NavMesh.SamplePosition(look, out NavMeshHit hit, 1f, agent.areaMask) && FlatDistance(hit.position) > 1f) { agent.SetDestination(hit.position); return; }
            }
            Vector3 back = lastSeen + Quaternion.Euler(0, wanderStep * 97f, 0) * Vector3.forward * 1.5f;   // drifted off: back toward the sighting
            if (NavMesh.SamplePosition(back, out NavMeshHit home, 1.2f, agent.areaMask)) agent.SetDestination(home.position);
        }
        public int Waypoint => waypoint;
        private int NextWaypoint()
        {
            if (!PatrolRandom || patrolPoints.Length < 3) return (waypoint + 1) % patrolPoints.Length;
            // The stop it has not seen for longest wins, nearer ones a little favoured, plus some chance:
            // it sweeps the whole room in an order the player cannot memorise.
            if (visitedAt == null || visitedAt.Length != patrolPoints.Length) { visitedAt = new float[patrolPoints.Length]; for (int i = 0; i < visitedAt.Length; i++) visitedAt[i] = -1000; }
            visitedAt[waypoint] = Time.time;
            int best = (waypoint + 1) % patrolPoints.Length; float bestScore = float.MinValue;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (i == waypoint || i == previousWaypoint) continue;
                float score = Mathf.Min(Time.time - visitedAt[i], 120) - FlatDistance(patrolPoints[i].position) * 2.5f + UnityEngine.Random.Range(0f, 8f);
                if (score > bestScore) { bestScore = score; best = i; }
            }
            previousWaypoint = waypoint;
            return best;
        }
        private float[] visitedAt;
        private void SetDestination(Vector3 target)
        {
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 1.2f, agent.areaMask)) agent.SetDestination(hit.position);
            // Reach mode: a roach up on the worktop or a wall is chased to the floor beneath it.
            else if (session.StrikeReach > 0 && NavMesh.SamplePosition(new Vector3(target.x, transform.position.y, target.z), out hit, 2f, agent.areaMask))
                agent.SetDestination(hit.position);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Verification only: lose sight of a target at a point and start searching.
        public void DebugSearch(Vector3 lastSighting) { State = HumanState.Search; lastSeen = lastSighting; lostTime = 0.6f; wandering = false; Detection = 0.5f; }
#endif
        public void ResetHuman()
        {
            if (!agent) agent = GetComponent<NavMeshAgent>();
            State = HumanState.Routine;
            Suspended = false;
            Detection = sensorClock = lostTime = attackTime = 0;
            wandering = false;
            ignoredPrey = -1; blockedTime = 0;
            CanSeePlayer = CanSeeTarget = false;
            Prey = -1;
            waypoint = 0; previousWaypoint = -1; visitedAt = null;
            cooldownUntil = Time.time + 1;
            landed = false;
            StrikeForward = Vector3.forward;
            StrikeUp = Vector3.up;
            warning.gameObject.SetActive(false);
            slipper.localRotation = slipperRest;
            agent.updateRotation = true;
            if (agent.Warp(spawn)) { agent.ResetPath(); agent.isStopped = false; }
            transform.rotation = spawnRotation;
        }
    }
}
