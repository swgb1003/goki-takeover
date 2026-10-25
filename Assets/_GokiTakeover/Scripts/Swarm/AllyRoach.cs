using System.Collections.Generic;
using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// One ally. A small state machine driven by SwarmCommander at a fixed order each frame; it follows
    /// grid paths without physics, keeps apart from its neighbours, and survival (fleeing a slipper)
    /// always overrides the player's order. Allies stay on the floor: they do not climb.
    /// Phase 5: a crushed ally may die for good (the commander sets a corpse time), leaving a body the
    /// player can eat; and the capture order sends it into a territory zone.
    /// Stage tuning may give it one spare life: the first slipper flips it on its back for a moment (Stunned,
    /// legs kicking), a second hit while wounded kills it; the wound heals at the nest or after a while.
    /// </summary>
    public sealed class AllyRoach : MonoBehaviour
    {
        public enum AllyState { Idle, FollowLeader, GoToTarget, Explore, CollectFood, ReturnToNest, DefendNest, FleeHazard, Disabled, Stunned }
        public enum Order { Follow, Collect, Explore, Defend, Rally, Move, Capture }
        [SerializeField] private CockroachVisualController visual;
        [SerializeField] private Transform carryAnchor, body;
        private SwarmCommander commander;
        private readonly List<Vector3> path = new List<Vector3>(64);
        private readonly HashSet<FoodItem> unreachable = new HashSet<FoodItem>();
        private readonly HashSet<int> skippedPoints = new HashSet<int>();
        private int pathIndex, scoutPoint = -1, slot;
        private float repathAt, disabledUntil, hideAt, corpseUntil, stunnedUntil, stunStarted, healAt;
        private Vector3 pathGoal, orderPoint, velocity;
        private bool hasPath;
        public AllyState State { get; private set; }
        public Order CurrentOrder { get; private set; }
        public FoodItem Carried { get; private set; }
        public FoodItem TargetFood { get; private set; }
        public int ScoutPoint => scoutPoint;
        public bool Active => gameObject.activeSelf && State != AllyState.Disabled;
        // Dead for good (Phase 5): the body stays as a corpse for a while, then the slot is free again.
        public bool IsDead { get; private set; }
        public bool Alive => gameObject.activeSelf && !IsDead;
        public bool IsCorpse => IsDead && gameObject.activeSelf;
        public int CaptureZone { get; private set; } = -1;
        public Vector3 Position => transform.position;
        public float Speed => velocity.magnitude;
        public Vector3 Velocity => velocity;
        public int Slot => slot;
        public int Crushes { get; private set; }
        public bool Wounded { get; private set; }
        public bool Stunned => State == AllyState.Stunned;
        public int Flips { get; private set; }
        public float CalledAt { get; set; } = -10;

        public void Configure(CockroachVisualController legs, Transform anchor, Transform model)
        { visual = legs; carryAnchor = anchor; body = model; }

        public void Initialise(SwarmCommander owner, int index)
        { commander = owner; slot = index; repathAt = index * 0.04f; }

        public void Spawn(Vector3 position, float yaw)
        {
            DropFood();
            transform.SetPositionAndRotation(new Vector3(position.x, 0, position.z), Quaternion.Euler(0, yaw, 0));
            if (body) body.gameObject.SetActive(true);
            if (visual) { visual.ResetReaction(); visual.SetMotion(0, false); }
            velocity = Vector3.zero;
            unreachable.Clear(); skippedPoints.Clear();
            TargetFood = null; scoutPoint = -1; hasPath = false;
            IsDead = false;
            Wounded = false;
            if (body) body.localRotation = Quaternion.identity;
            State = AllyState.Idle;
            SetOrder(Order.Follow, Vector3.zero);
        }

        public void SetOrder(Order order, Vector3 point, FoodItem food = null)
        {
            if (State == AllyState.Disabled || State == AllyState.Stunned) { CurrentOrder = order; orderPoint = point; CaptureZone = -1; return; }
            CurrentOrder = order;
            orderPoint = point;
            CaptureZone = -1;
            hasPath = false;
            unreachable.Clear(); skippedPoints.Clear();
            if (scoutPoint >= 0) { commander.ReleaseScout(scoutPoint, this); scoutPoint = -1; }
            TargetFood = order == Order.Collect && food && commander.CanTarget(food, this) ? food : null;
            State = StateFor(order);
        }

        /// <summary>Capture order: walk into the zone and stay there, close to its centre.</summary>
        public void SetCapture(Vector3 centre, int zone)
        {
            if (IsDead) return;
            SetOrder(Order.Capture, centre);
            CaptureZone = zone;
        }

        private static AllyState StateFor(Order order)
        {
            switch (order)
            {
                case Order.Collect: return AllyState.CollectFood;
                case Order.Explore: return AllyState.Explore;
                case Order.Defend: return AllyState.DefendNest;
                case Order.Rally: case Order.Move: case Order.Capture: return AllyState.GoToTarget;
                default: return AllyState.FollowLeader;
            }
        }

        public void Tick(float dt)
        {
            if (!gameObject.activeSelf || dt <= 0) return;
            if (State == AllyState.Disabled)
            {
                if (IsDead) { if (Time.time >= corpseUntil) gameObject.SetActive(false); return; }
                if (body && body.gameObject.activeSelf && Time.time >= hideAt) { DropFood(); body.gameObject.SetActive(false); }
                if (Time.time >= disabledUntil) Respawn();
                return;
            }
            if (State == AllyState.Stunned) { Kick(dt); return; }
            if (Wounded && (Time.time >= healAt || commander.Session.InNest(Position + Vector3.up * 0.1f))) Wounded = false;
            if (commander.Threatened(this, out Vector3 escape))
            {
                if (State != AllyState.FleeHazard) { State = AllyState.FleeHazard; hasPath = false; }
                Steer(escape, commander.Settings3.allyRunSpeed, dt, true);
                return;
            }
            if (State == AllyState.FleeHazard) { State = StateFor(CurrentOrder); hasPath = false; }
            float walk = commander.Settings3.allyWalkSpeed;
            switch (State)
            {
                case AllyState.FollowLeader:
                {
                    Vector3 goal = commander.FormationPoint(commander.LeaderGround, slot, commander.LeaderYaw, 0.9f);
                    bool far = (goal - Position).sqrMagnitude > 9;
                    Steer(goal, far ? commander.Settings3.allyRunSpeed : walk, dt, far, 0.35f);
                    break;
                }
                case AllyState.CollectFood: Collect(walk, dt); break;
                case AllyState.Explore: Explore(walk, dt); break;
                case AllyState.DefendNest:
                    Steer(commander.NestPoint(slot, 1.25f), walk, dt, false, 0.3f);
                    break;
                case AllyState.GoToTarget:
                {
                    // A capture keeps the group tight so even the outer ring stays inside the zone.
                    bool capture = CurrentOrder == Order.Capture;
                    Vector3 goal = commander.FormationPoint(orderPoint, slot, 0, capture ? 0.45f : 0.75f);
                    bool rally = CurrentOrder == Order.Rally || (capture && (goal - Position).sqrMagnitude > 9);
                    if (Steer(goal, rally ? commander.Settings3.allyRunSpeed : walk, dt, rally, 0.35f)) State = AllyState.Idle;
                    break;
                }
                case AllyState.ReturnToNest:
                    if (Steer(commander.NestPoint(slot, 1.25f), walk, dt, false, 0.35f))
                        State = AllyState.Idle;
                    break;
                default:
                    Brake(dt);
                    break;
            }
        }

        private void Collect(float walk, float dt)
        {
            if (Carried)
            {
                if (commander.NestSmoked) { Steer(commander.NestRefuge, walk, dt, false, 0.9f); return; }   // wait out the smoke with the food
                if (Steer(commander.NestGround, walk, dt, false, 0.75f))
                {
                    var food = Carried;
                    Carried = null;
                    food.Deliver();
                    commander.RecordAllyDelivery(this);
                    TargetFood = null; hasPath = false;
                }
                return;
            }
            if (!TargetFood || !commander.CanTarget(TargetFood, this) || unreachable.Contains(TargetFood))
            {
                TargetFood = commander.ClaimNearestFood(this, unreachable);
                hasPath = false;
                if (!TargetFood)
                {
                    State = AllyState.ReturnToNest;
                    commander.ReportNoFood(this);
                    return;
                }
            }
            Vector3 food2 = TargetFood.transform.position;
            if (Steer(new Vector3(food2.x, 0, food2.z), walk, dt, false, 0.45f) && TargetFood.TryCarry(carryAnchor))
            {
                Carried = TargetFood;
                TargetFood = null;
                hasPath = false;
                if (visual) visual.PlayPickup();
            }
        }

        private void Explore(float walk, float dt)
        {
            if (scoutPoint < 0 || commander.IsScouted(scoutPoint))
            {
                if (scoutPoint >= 0) commander.ReleaseScout(scoutPoint, this);
                scoutPoint = commander.ClaimScoutPoint(this, skippedPoints);
                hasPath = false;
                if (scoutPoint < 0) { State = AllyState.ReturnToNest; commander.ReportExploreDone(this); return; }
            }
            if (Steer(commander.ScoutPosition(scoutPoint), walk, dt, false, 0.5f)) commander.MarkScouted(scoutPoint);
        }

        /// <summary>Follows a grid path to goal. Returns true once within tolerance.</summary>
        private bool Steer(Vector3 goal, float speed, float dt, bool running, float tolerance = 0.3f)
        {
            goal.y = 0;
            // Never into a smoked nest, whatever the order (following the player in there included): the refuge instead.
            if (commander.NestSmoked && commander.Session.InNest(goal + Vector3.up * 0.1f)) goal = commander.NestPoint(slot, 0.9f);
            // A goal inside furniture becomes the nearest free floor, so arrival is still reachable.
            if (!commander.Grid.Walkable(goal) && commander.Grid.NearestWalkable(goal, 1.5f, out Vector3 free)) goal = free;
            Vector3 flat = Position; flat.y = 0;
            if ((goal - flat).sqrMagnitude <= tolerance * tolerance) { Brake(dt); return true; }
            if (!hasPath || Time.time >= repathAt || (goal - pathGoal).sqrMagnitude > 0.36f)
            {
                repathAt = Time.time + commander.Settings3.allyRepathSeconds;
                pathGoal = goal;
                hasPath = commander.Grid.FindPath(flat, goal, path);
                pathIndex = 0;
                if (!hasPath) { PathFailed(); Brake(dt); return false; }
            }
            while (pathIndex < path.Count - 1 && (path[pathIndex] - flat).sqrMagnitude < 0.04f) pathIndex++;
            Vector3 toward = path[Mathf.Min(pathIndex, path.Count - 1)] - flat;
            if (commander.Grid.IsSticky(flat)) speed *= commander.Session.Settings.stickySpeed;
            if (Carried) speed *= commander.Session.CarryingSpeed;
            speed *= commander.AllySpeedScale;
            Vector3 desired = toward.sqrMagnitude > 1e-4f ? toward.normalized * speed : Vector3.zero;
            desired += commander.Separation(this) * speed;
            desired = Vector3.ClampMagnitude(desired, speed);
            velocity = Vector3.MoveTowards(velocity, desired, speed * 8 * dt);
            Advance(dt, running);
            return false;
        }

        private void Brake(float dt)
        {
            velocity = Vector3.MoveTowards(velocity, commander.Separation(this) * 0.6f, 6 * dt);
            Advance(dt, false);
        }

        // Moves without physics, but never into a blocked grid cell (slides along the free axis instead).
        private void Advance(float dt, bool running)
        {
            Vector3 step = velocity * dt, from = Position, to = from + step;
            var grid = commander.Grid;
            if (!grid.Walkable(to) && grid.Walkable(from))   // from a blocked cell, any step out is allowed
            {
                Vector3 x = from + new Vector3(step.x, 0, 0), z = from + new Vector3(0, 0, step.z);
                to = grid.Walkable(x) ? x : grid.Walkable(z) ? z : from;
                velocity = (to - from) / dt;
            }
            to.y = 0;
            transform.position = to;
            if (velocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(velocity), 540 * dt);
            if (visual) visual.SetMotion(Mathf.Clamp01(velocity.magnitude / commander.Settings3.allyWalkSpeed), running && velocity.sqrMagnitude > 1);
        }

        // No route: give up on this target instead of retrying it forever.
        private void PathFailed()
        {
            switch (State)
            {
                case AllyState.CollectFood:
                    if (TargetFood) { unreachable.Add(TargetFood); commander.ReleaseFood(TargetFood, this); TargetFood = null; }
                    break;
                case AllyState.Explore:
                    if (scoutPoint >= 0) { skippedPoints.Add(scoutPoint); commander.ReleaseScout(scoutPoint, this); scoutPoint = -1; }
                    break;
                case AllyState.FleeHazard: break;
                default:
                    State = AllyState.Idle;
                    commander.ReportUnreachable(this);
                    break;
            }
        }

        /// <summary>
        /// A slipper landed on it. With a spare life (stunSeconds > 0, not wounded) it only flips over;
        /// otherwise it is crushed. Returns true when it died (or was disabled).
        /// </summary>
        public bool Hit(float stunSeconds, float healSeconds, float respawnSeconds)
        {
            if (State == AllyState.Disabled) return false;
            if (stunSeconds <= 0 || Wounded || State == AllyState.Stunned) { if (State == AllyState.Stunned) RightSideUp(); Crush(respawnSeconds); return true; }
            Wounded = true; Flips++;
            healAt = Time.time + stunSeconds + healSeconds;
            State = AllyState.Stunned;
            stunStarted = Time.time; stunnedUntil = Time.time + stunSeconds;
            velocity = Vector3.zero; hasPath = false;
            DropFood();
            if (scoutPoint >= 0) { commander.ReleaseScout(scoutPoint, this); scoutPoint = -1; }
            if (visual) visual.PlayHit();
            return false;
        }

        // On its back, legs kicking; it flips back at the end and carries on with its order.
        private void Kick(float dt)
        {
            float t = Time.time - stunStarted, left = stunnedUntil - Time.time;
            float flip = Mathf.Clamp01(t / 0.18f) * Mathf.Clamp01(left / 0.22f);
            if (body) body.localRotation = Quaternion.Euler(0, 0, 180 * flip + Mathf.Sin(t * 19) * 9 * flip);
            if (visual) visual.SetMotion(1, true);
            if (left > 0) return;
            RightSideUp();
            State = StateFor(CurrentOrder);   // a capture keeps its zone: back into it
        }
        private void RightSideUp() { if (body) body.localRotation = Quaternion.identity; }

        public void Crush(float respawnSeconds)
        {
            if (State == AllyState.Disabled) return;
            State = AllyState.Disabled;
            Crushes++;
            velocity = Vector3.zero;
            if (visual) { visual.IsDead = true; visual.SetMotion(0, false); }
            hideAt = Time.time + 1.4f;
            disabledUntil = Time.time + respawnSeconds;
            if (commander.AllyCorpseSeconds > 0)
            {
                // No respawn: what it carried goes back, the body stays where it fell.
                IsDead = true;
                corpseUntil = Time.time + commander.AllyCorpseSeconds;
                DropFood();
                commander.ReportAllyDeath(this);
            }
            if (TargetFood) { commander.ReleaseFood(TargetFood, this); TargetFood = null; }
            if (scoutPoint >= 0) { commander.ReleaseScout(scoutPoint, this); scoutPoint = -1; }
        }

        private void Respawn()
        {
            var order = CurrentOrder; var point = orderPoint;
            Vector3 home = commander.NestPoint(slot, 0.9f);
            Spawn(home, slot * 40);
            SetOrder(order, point);
        }

        // A dropped crumb returns to where it was found, available again.
        public void DropFood()
        {
            if (Carried) { Carried.ResetItem(); Carried = null; }
            if (TargetFood && commander) commander.ReleaseFood(TargetFood, this);
            TargetFood = null;
        }

        public void ForgetFood() { Carried = null; TargetFood = null; }

        /// <summary>Dragged across the floor from outside (Stage 2: the vacuum's suction); never into furniture.</summary>
        public void Pull(Vector3 delta)
        {
            if (!Active) return;
            Vector3 to = Position + new Vector3(delta.x, 0, delta.z);
            if (commander.Grid.Walkable(to)) transform.position = to;
        }

        /// <summary>The player eats the corpse: it is gone at once.</summary>
        public bool Consume()
        {
            if (!IsCorpse) return false;
            gameObject.SetActive(false);
            return true;
        }

        public string Label
        {
            get
            {
                switch (State)
                {
                    case AllyState.FollowLeader: return "追従";
                    case AllyState.GoToTarget: return CurrentOrder == Order.Rally ? "侵攻" : CurrentOrder == Order.Capture ? "占拠" : "移動";
                    case AllyState.Explore: return "探索";
                    case AllyState.CollectFood: return Carried ? "運搬" : "回収";
                    case AllyState.ReturnToNest: return "帰巣";
                    case AllyState.DefendNest: return "防衛";
                    case AllyState.FleeHazard: return "回避";
                    case AllyState.Disabled: return IsDead ? "死体" : "復活待ち";
                    case AllyState.Stunned: return "ひっくり返り";
                    default: return CurrentOrder == Order.Capture ? "占拠中" : "待機";
                }
            }
        }
    }
}
