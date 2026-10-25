using System.Collections.Generic;
using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Stage 2's house cat "ミケ". Naps on its bed, wanders the house, and reacts to roaches that MOVE on open floor:
    /// interest grows while the roach moves in sight and fades while it keeps still (stopping lets it pass). Interested,
    /// it stalks closer, crouches with an orange ring on the spot (CrouchSeconds) and pounces onto it: the player there
    /// is hurt, allies there are swatted like by a slipper (flipped first). Furniture overhead hides a roach from it;
    /// walls, the ceiling and furniture tops are out of its reach. It moves on its own floor grid (no space under the
    /// sofa, not into the nest). It leaves the house with the family when the exterminator comes.
    /// </summary>
    public sealed class PetCat : MonoBehaviour, IAllyThreat
    {
        public enum Phase { Nap, Wander, Stalk, Crouch, Leap, Recover, Leave, Gone }
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private SwarmCommander commander;
        [SerializeField] private StageProgress progress;
        [SerializeField] private AllyNavGrid grid;
        [SerializeField] private CatAnimator body;
        [SerializeField] private Transform ring, fill, skin;
        [SerializeField] private Vector3 bed;
        [SerializeField] private Rect area = new Rect(-6.5f, -5.5f, 27.5f, 11f);
        public const float SightRange = 7f, SightAngle = 150f, HearRange = 1.8f, PounceRange = 2.8f, Radius = 0.95f;
        public const float CrouchSeconds = 0.8f, LeapSeconds = 0.38f, RecoverSeconds = 1.2f, Cooldown = 3f, AllyNotice = 0.35f;
        public const float WanderSpeed = 1.3f, StalkSpeed = 0.95f, LeaveSpeed = 2.4f, MovingSpeed = 0.25f;
        private readonly List<Vector3> path = new List<Vector3>();
        private Vector3 goal, leapFrom, point, velocity;
        private int pathIndex, target = -2;   // -2 none, -1 the player, else an ally slot
        private float clock, sense, phaseUntil, cooldownUntil, repathAt, idleUntil, wanderUntil;
        private bool hasPath, missLogged;
        // Stuck watch: no headway for a while means plan again; several times over means give that goal up.
        private float watchAt;
        private Vector3 watchFrom;
        private int stuck;
        public int Unstuck { get; private set; }
        public Phase Current { get; private set; }
        public float Interest { get; private set; }
        public bool TargetIsPlayer => target == -1;
        public Vector3 Point => point;
        public Vector3 Velocity => velocity;
        public int Pounces { get; private set; }
        public int PlayerHits { get; private set; }
        public int AllyHits { get; private set; }
        public int LostInterest { get; private set; }
        public float Remaining => Current == Phase.Crouch ? CrouchSeconds - clock : 0;
        public bool Stalking => Current == Phase.Stalk || Current == Phase.Crouch || Current == Phase.Leap;
        // Sitting a moment between two wander goals (not stuck).
        public bool Resting => Current == Phase.Wander && Time.time < idleUntil;
        public Vector3 LookPoint { get; private set; }
        public string Label
        {
            get
            {
                switch (Current)
                {
                    case Phase.Nap: return "お昼寝中";
                    case Phase.Stalk: return "忍び寄り";
                    case Phase.Crouch: case Phase.Leap: return "飛びかかる！";
                    case Phase.Recover: return "毛づくろい";
                    case Phase.Leave: case Phase.Gone: return "避難";
                    default: return "うろうろ";
                }
            }
        }

        public void Configure(PhaseTwoSession game, SwarmCommander swarm, StageProgress stage, AllyNavGrid floor, CatAnimator animator, Transform warningRing, Transform warningFill, Transform model, Vector3 home)
        { session = game; commander = swarm; progress = stage; grid = floor; body = animator; ring = warningRing; fill = warningFill; skin = model; bed = home; }

        private void Awake()
        {
            grid = Instantiate(grid);
            commander.HouseholdThreats.Add(this);
            session.RunReset += ResetCat;
        }
        private void OnDestroy()
        {
            if (commander) commander.HouseholdThreats.Remove(this);
            if (session) session.RunReset -= ResetCat;
        }
        private ColonyManager colony;
        private void Start() { colony = FindFirstObjectByType<ColonyManager>(); ResetCat(); }

        public void ResetCat()
        {
            transform.SetPositionAndRotation(bed, Quaternion.Euler(0, 200, 0));
            if (skin) skin.gameObject.SetActive(true);
            Hide();
            Current = Phase.Nap; clock = 0; phaseUntil = 6f; target = -2; Interest = 0; velocity = Vector3.zero; hasPath = false;
            stuck = 0; Unstuck = 0; watchAt = 0;
            cooldownUntil = 0; Pounces = PlayerHits = AllyHits = LostInterest = 0;
        }

        private void Update()
        {
            if (!session.IsRunning || Current == Phase.Gone) { velocity = Vector3.zero; return; }
            float dt = Time.deltaTime;
            clock += dt;
            if (progress && progress.Current >= StageProgress.Phase.FinalDefence && Current != Phase.Leave) BeginLeave();
            sense += dt;
            if (sense >= 0.1f && Current <= Phase.Stalk) { Sense(sense); sense = 0; }
            switch (Current)
            {
                case Phase.Nap:
                    velocity = Vector3.zero;
                    if (clock >= phaseUntil) { Current = Phase.Wander; clock = 0; wanderUntil = Random.Range(20f, 35f); hasPath = false; }
                    break;
                case Phase.Wander: Wander(dt); break;
                case Phase.Stalk: Stalk(dt); break;
                case Phase.Crouch:
                    velocity = Vector3.zero;
                    Face(point, dt);
                    if (fill) fill.localScale = Vector3.one * Mathf.Lerp(0.05f, Radius * 2, clock / CrouchSeconds);
                    if (clock >= CrouchSeconds) { Current = Phase.Leap; clock = 0; leapFrom = transform.position; }
                    break;
                case Phase.Leap:
                {
                    float t = Mathf.Clamp01(clock / LeapSeconds);
                    Vector3 p = Vector3.Lerp(leapFrom, point, t);
                    p.y = Mathf.Sin(t * Mathf.PI) * 0.9f;
                    velocity = (point - leapFrom) / LeapSeconds;
                    transform.position = p;
                    if (t >= 1) Land();
                    break;
                }
                case Phase.Recover:
                    velocity = Vector3.zero;
                    if (clock >= RecoverSeconds) { Current = Phase.Wander; clock = 0; cooldownUntil = Time.time + Cooldown; hasPath = false; }
                    break;
                case Phase.Leave:
                    if (Steer(progress ? progress.Stage.exitPoint : bed, LeaveSpeed, dt, 0.7f) || clock > 10f) { Current = Phase.Gone; if (skin) skin.gameObject.SetActive(false); }
                    break;
            }
        }

        // ---------------- Noticing a moving roach ----------------
        private void Sense(float step)
        {
            bool napping = Current == Phase.Nap;
            int best = -2; float bestDistance = float.MaxValue;
            Consider(-1, napping, ref best, ref bestDistance);
            for (int i = 0; i < commander.Allies.Count; i++) Consider(i, napping, ref best, ref bestDistance);
            bool current = target != -2 && Visible(target, napping) && Moving(target);
            if (current) Interest = Mathf.Min(1.2f, Interest + step * 1.4f);
            else
            {
                Interest = Mathf.Max(0, Interest - step * 0.8f);
                if (Interest <= 0.05f && best != -2) { target = best; Interest = 0.1f; }
            }
            if (napping)
            {
                if (best != -2) { target = best; Interest = 0.6f; StartStalk(); }
                return;
            }
            if (Current == Phase.Wander && target != -2 && Interest >= 0.5f && Time.time >= cooldownUntil) StartStalk();
            if (Current == Phase.Stalk && Interest <= 0)
            {
                // It kept still (or hid) long enough: the cat loses interest.
                if (target == -1) { LostInterest++; commander.Say("猫が興味をなくした ― 止まっていればやり過ごせる"); }
                target = -2; Current = Phase.Wander; clock = 0; hasPath = false;
            }
        }
        private void Consider(int who, bool napping, ref int best, ref float bestDistance)
        {
            if (!Visible(who, napping) || !Moving(who)) return;
            float d = Flat(PositionOf(who) - transform.position).magnitude;
            if (d < bestDistance) { bestDistance = d; best = who; }
        }
        /// <summary>A roach the cat could see now: on open floor (not under furniture, not in the nest), in its cone and line of sight.</summary>
        public bool Visible(int who, bool napping = false)
        {
            if (!Targetable(who)) return false;
            Vector3 p = PositionOf(who), delta = Flat(p - transform.position);
            float d = delta.magnitude;
            if (napping) return d <= HearRange;
            if (d > SightRange || (d > HearRange && Vector3.Angle(transform.forward, delta) > SightAngle * 0.5f)) return false;
            return !Physics.Linecast(transform.position + Vector3.up * 1.4f, p + Vector3.up * 0.15f, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }
        public bool Targetable(int who)
        {
            if (who == -1)
            {
                var motor = session.Motor;
                return motor.FootPoint.y < 0.5f && !motor.IsClimbing && !session.InNest(session.Player.position) && !Covered(motor.FootPoint);
            }
            if (who < 0 || who >= commander.Allies.Count) return false;
            var ally = commander.Allies[who];
            return ally.Active && !session.InNest(ally.Position + Vector3.up * 0.1f) && !Covered(ally.Position);
        }
        private bool Moving(int who) => who == -1 ? session.Motor.CurrentSpeed > MovingSpeed : commander.Allies[who].Speed > MovingSpeed;
        private Vector3 PositionOf(int who) => who == -1 ? Flat(session.Motor.FootPoint) : Flat(commander.Allies[who].Position);
        /// <summary>Furniture overhead (the table, the sofa): out of the cat's sight and reach.</summary>
        public bool Covered(Vector3 position) =>
            Physics.Raycast(position + Vector3.up * 0.15f, Vector3.up, out RaycastHit roof, 3.3f, ~(1 << 2), QueryTriggerInteraction.Ignore) && !session.IsHumanBody(roof.transform);

        // ---------------- Moving ----------------
        private void Wander(float dt)
        {
            if (clock >= wanderUntil)
            {
                // Tired: back to the bed for a nap.
                if (Steer(bed, WanderSpeed, dt, 0.4f) || stuck >= 3) { Current = Phase.Nap; clock = 0; phaseUntil = Random.Range(12f, 20f); target = -2; Interest = 0; stuck = 0; }
                return;
            }
            if (Time.time < idleUntil) { velocity = Vector3.MoveTowards(velocity, Vector3.zero, 6 * dt); return; }
            if (!hasPath || Flat(goal - transform.position).magnitude < 0.5f)
            {
                if (hasPath) { idleUntil = Time.time + Random.Range(1f, 3f); hasPath = false; return; }
                for (int k = 0; k < 20; k++)
                {
                    var p = new Vector3(Random.Range(area.xMin, area.xMax), 0, Random.Range(area.yMin, area.yMax));
                    if (grid.Walkable(p) && grid.FindPath(transform.position, p, path)) { goal = p; hasPath = true; pathIndex = 0; repathAt = Time.time + 6; break; }
                }
                return;
            }
            Steer(goal, WanderSpeed, dt, 0.5f);
            if (stuck >= 2) { stuck = 0; hasPath = false; idleUntil = Time.time + 0.5f; }   // somewhere else then
        }

        private void StartStalk()
        {
            Current = Phase.Stalk; clock = 0; hasPath = false; stuck = 0; watchAt = Time.time + 1.5f; watchFrom = Flat(transform.position);
            if (target == -1) { commander.Say("猫に見つかった！  止まればやり過ごせる"); if (colony) colony.Learn("cat"); }
        }

        private void Stalk(float dt)
        {
            if (target == -2 || !Targetable(target)) { Interest = Mathf.Max(0, Interest - dt); return; }
            Vector3 p = PositionOf(target);
            LookPoint = p;
            float d = Flat(p - transform.position).magnitude;
            // It pounces on movement only: a roach that keeps still is never jumped on.
            if (d <= PounceRange && Visible(target) && Moving(target) && Time.time >= cooldownUntil)
            { BeginPounce(target == -1 ? p : p + Vector3.ClampMagnitude(commander.Allies[target].Velocity * 0.4f, 0.6f)); return; }
            Steer(p, d > 4 ? StalkSpeed * 1.6f : StalkSpeed, dt, PounceRange * 0.8f);
            // Out of its reach (no way there, or it keeps getting stuck): it gives up on that roach.
            if (stuck >= 2) { stuck = 0; target = -2; Interest = 0; Current = Phase.Wander; clock = 0; hasPath = false; cooldownUntil = Time.time + Cooldown; }
        }

        /// <summary>Follows a path on the cat's own grid; true once within tolerance.</summary>
        private bool Steer(Vector3 to, float speed, float dt, float tolerance)
        {
            to.y = 0;
            if (!grid.Walkable(to) && grid.NearestWalkable(to, 1.5f, out Vector3 free)) to = free;
            Vector3 here = Flat(transform.position);
            if ((to - here).magnitude <= tolerance) { velocity = Vector3.MoveTowards(velocity, Vector3.zero, 6 * dt); return true; }
            // Off its floor (a landing, a squeeze): step back onto the nearest free spot first.
            if (!grid.Walkable(here))
            {
                if (grid.NearestWalkable(here, 1.5f, out Vector3 back)) transform.position = Vector3.MoveTowards(here, back, speed * dt);
                velocity = Vector3.zero;
                return false;
            }
            if (!hasPath || Time.time >= repathAt || (to - goal).sqrMagnitude > 0.5f)
            {
                goal = to; repathAt = Time.time + 0.5f; pathIndex = 0;
                hasPath = grid.FindPath(here, to, path);
                if (!hasPath) { velocity = Vector3.zero; stuck++; Unstuck++; return false; }   // no way there: counts as stuck
            }
            while (pathIndex < path.Count - 1 && (path[pathIndex] - here).sqrMagnitude < 0.04f) pathIndex++;
            Vector3 toward = path[Mathf.Min(pathIndex, path.Count - 1)] - here;
            Vector3 desired = toward.sqrMagnitude > 1e-4f ? toward.normalized * speed : Vector3.zero;
            velocity = Vector3.MoveTowards(velocity, desired, speed * 6 * dt);
            Vector3 next = here + velocity * dt;
            if (!grid.Walkable(next))
            {
                // Its sway carried it off the straight line at a doorway or corner: head straight for the waypoint, else
                // slide along the edge, never stop dead against it.
                Vector3 direct = here + desired * dt, alongX = here + new Vector3(velocity.x * dt, 0, 0), alongZ = here + new Vector3(0, 0, velocity.z * dt);
                if (grid.Walkable(direct)) { next = direct; velocity = desired; }
                else if (grid.Walkable(alongX)) { next = alongX; velocity.z = 0; }
                else if (grid.Walkable(alongZ)) { next = alongZ; velocity.x = 0; }
                else { next = here; velocity = Vector3.zero; repathAt = 0; }
            }
            transform.position = next;
            if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(velocity), 300 * dt);
            // No headway for 1.5 s: plan again from here (the callers give the goal up after a few).
            if (Time.time >= watchAt)
            {
                bool still = (here - watchFrom).magnitude < 0.15f;
                watchFrom = here; watchAt = Time.time + 1.5f;
                if (still) { stuck++; Unstuck++; hasPath = false; } else stuck = 0;
            }
            return false;
        }
        private void Face(Vector3 at, float dt)
        {
            Vector3 flat = Flat(at - transform.position);
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), 400 * dt);
        }

        // ---------------- The pounce ----------------
        private void BeginPounce(Vector3 at)
        {
            point = Flat(at);
            if (!grid.Walkable(point)) grid.NearestWalkable(point, 1f, out point);
            Current = Phase.Crouch; clock = 0; Pounces++; missLogged = false;
            LookPoint = point;
            if (ring) { ring.gameObject.SetActive(true); ring.SetPositionAndRotation(point + Vector3.up * 0.045f, Quaternion.identity); ring.localScale = new Vector3(Radius * 2, 1, Radius * 2); }
            if (fill) { fill.gameObject.SetActive(true); fill.SetPositionAndRotation(point + Vector3.up * 0.04f, Quaternion.identity); fill.localScale = Vector3.one * 0.05f; }
        }
        public bool InRing(Vector3 position) => position.y < 0.6f && Flat(position - point).magnitude <= Radius;

        private void Land()
        {
            transform.position = point;
            Hide();
            if (Targetable(-1) && InRing(session.Motor.FootPoint) && session.TryDamage(1)) PlayerHits++;
            foreach (var ally in commander.Allies)
            {
                if (!ally.Active || !InRing(ally.Position) || Covered(ally.Position) || session.InNest(ally.Position + Vector3.up * 0.1f)) continue;
                ally.Hit(commander.AllyStunSeconds, commander.AllyWoundHealSeconds, commander.Settings3.allyRespawnSeconds);
                AllyHits++;
            }
            if (AllyHits > 0 && !missLogged) { missLogged = true; commander.Say("仲間が猫パンチを受けた！  F で呼んで助けよう"); }
            Interest = 0.3f;
            Current = Phase.Recover; clock = 0;
        }

        private void BeginLeave()
        {
            Hide();
            Current = Phase.Leave; clock = 0; hasPath = false; target = -2; Interest = 0;
        }
        private void Hide()
        {
            if (ring) ring.gameObject.SetActive(false);
            if (fill) fill.gameObject.SetActive(false);
        }

        // Allies notice the crouch a moment late, then run out of the ring.
        public bool Threatens(Vector3 position, out Vector3 escape)
        {
            escape = position;
            if ((Current != Phase.Crouch || clock < AllyNotice) && Current != Phase.Leap) return false;
            if (!InRing(position)) return false;
            Vector3 away = Flat(position - point);
            if (away.sqrMagnitude < 1e-3f) away = Vector3.right;
            escape = point + away.normalized * (Radius + 0.9f);
            if (!commander.Grid.Walkable(escape)) commander.Grid.NearestWalkable(escape, 1.5f, out escape);
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Verification only.
        public void DebugStalkPlayer() { target = -1; Interest = 0.6f; StartStalk(); }
        public void DebugPounce(Vector3 at) { cooldownUntil = 0; BeginPounce(at); }
        public void DebugWander() { Current = Phase.Wander; clock = 0; wanderUntil = 60; target = -2; Interest = 0; hasPath = false; stuck = 0; watchAt = Time.time + 1.5f; watchFrom = Flat(transform.position); }
        public void DebugPlace(Vector3 at, float yaw) { transform.SetPositionAndRotation(Flat(at), Quaternion.Euler(0, yaw, 0)); hasPath = false; }
#endif
        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
