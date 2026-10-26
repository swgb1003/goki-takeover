using System.Collections.Generic;
using UnityEngine;

namespace GokiTakeover
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class RoachMotor : MonoBehaviour
    {
        [SerializeField] private Transform movementCamera;
        [SerializeField] private CockroachVisualController visual;
        [SerializeField, Min(0.1f)] private float walkSpeed = 1.65f;
        [SerializeField, Min(0.1f)] private float runSpeed = 3.3f;
        [SerializeField, Min(1f)] private float turnSpeed = 540f;
        [SerializeField, Min(1f)] private float gravity = 18f;
        [SerializeField] private float fallRecoveryHeight = -0.6f;
        // Optional (Phase 3+): the pivot that tilts the body onto walls and the ceiling. Without it the
        // roach stays a floor-only character exactly as in earlier phases.
        [SerializeField] private Transform surfaceAlign;
        [SerializeField, Range(0.3f, 1.2f)] private float climbSpeedScale = 0.85f;
        [SerializeField, Min(0.05f)] private float climbEngageSeconds = 0.18f;
        private const int ObstacleMask = ~(1 << 2);
        private const float Skin = 0.015f, StickDistance = 0.32f;
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private readonly List<CapsuleCollider> pushers = new List<CapsuleCollider>();
        private readonly Collider[] overlaps = new Collider[8];
        private CharacterController controller;
        private float verticalVelocity;
        private Vector3 lastSafePosition;
        private bool hasSafePosition;
        private bool climbing, hasSafeClimb;
        private Vector3 surfaceUp = Vector3.up, viewUp = Vector3.up, climbHeading = Vector3.forward;
        private Vector3 safeClimbCentre, safeClimbUp, safeClimbHeading;
        private float pushTime;
        public int Recoveries { get; private set; }
        public float CurrentSpeed { get; private set; }
        public bool IsRunning { get; private set; }
        public bool MovementEnabled { get; set; } = true;
        public float SpeedMultiplier { get; set; } = 1f;
        // Stage 2: a drag from outside on the floor (the vacuum's suction), added to the roach's own walking. Zero by default.
        public Vector3 ExternalVelocity { get; set; }
        // Colony upgrade (Phase 4); stays 1 elsewhere.
        public float UpgradeSpeed { get; set; } = 1f;
        // Stage 3: grip on the floor. Below 1 (a freshly mopped floor) the walk speeds up and slows down gradually:
        // the roach slides on when the keys are let go. 1 (every earlier scene): the walk follows the keys at once.
        public float Traction { get; set; } = 1f;
        private Vector3 slide;
        public Vector3 SlideVelocity => slide;
        public bool ClimbEnabled => surfaceAlign;
        public bool IsClimbing => climbing;
        public Vector3 SurfaceUp => surfaceUp;
        // Smoothed surface normal for the camera and body: corners turn over a few frames, not one.
        public Vector3 ViewUp => viewUp;
        public Vector3 ViewFoot => Centre - viewUp * Radius;
        public Vector3 FootPoint => Centre - surfaceUp * Radius;
        public Vector3 BodyCentre => Centre;
        // Fraction of the push needed to start climbing; the HUD shows it as a hint.
        public float ClimbEngage => climbing ? 1 : Mathf.Clamp01(pushTime / climbEngageSeconds);
        public void ConfigureClimbing(Transform alignPivot) { surfaceAlign = alignPivot; }

        public void Configure(Transform cameraTransform, CockroachVisualController visualController)
        {
            movementCamera = cameraTransform;
            visual = visualController;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (!visual) visual = GetComponentInChildren<CockroachVisualController>();
            if (!movementCamera && Camera.main) movementCamera = Camera.main.transform;
            if (!movementCamera) Debug.LogWarning("[GOKI] 移動用カメラが未設定です。ワールド方向で移動します。", this);
        }

        private void Update()
        {
            bool active = Cursor.lockState == CursorLockMode.Locked && Application.isFocused;
            if (active && MovementEnabled && climbing && Input.GetKeyDown(KeyCode.Space)) Detach();
            SimulateMove(active ? GokiInput.Move : Vector2.zero, active && GokiInput.Run, Time.deltaTime);
        }

        // Moving bodies (the resident) must not shove the controller with PhysX overlap recovery: that
        // depenetration ignores walls and could push the roach through them into the void. They are
        // excluded from the controller and pushed out here through Move(), which still respects walls.
        public void AddPusher(CapsuleCollider body)
        {
            if (!body || pushers.Contains(body)) return;
            if (!controller) controller = GetComponent<CharacterController>();
            pushers.Add(body);
            Physics.IgnoreCollision(controller, body, true);
        }
        public void ResetSafePosition() { lastSafePosition = transform.position; hasSafePosition = true; verticalVelocity = 0; }

        public void SimulateMove(Vector2 input, bool run, float deltaTime)
        {
            if (!controller || deltaTime <= 0) return;
            if (!MovementEnabled) input = Vector2.zero;
            input = Vector2.ClampMagnitude(input, 1f);
            if (climbing) { ClimbMove(input, run, deltaTime); return; }
            if (!controller.enabled) return;
            Vector3 forward = movementCamera ? Vector3.ProjectOnPlane(movementCamera.forward, Vector3.up).normalized : Vector3.forward;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            Vector3 direction = forward * input.y + Vector3.Cross(Vector3.up, forward) * input.x;
            IsRunning = run && input.sqrMagnitude > 0.01f;
            if (direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * deltaTime);
            if (controller.isGrounded && verticalVelocity < 0) verticalVelocity = -2f;
            verticalVelocity = Mathf.Max(verticalVelocity - gravity * deltaTime, -20f);
            Vector3 previous = transform.position;
            Vector3 walk = direction * (IsRunning ? runSpeed : walkSpeed) * Mathf.Clamp(SpeedMultiplier * UpgradeSpeed, 0, 2);
            if (Traction < 1) { slide = Vector3.MoveTowards(slide, walk, (1.2f + 10f * Traction) * deltaTime); walk = slide; }
            else slide = walk;
            controller.Move((walk + ExternalVelocity + Vector3.up * verticalVelocity) * deltaTime + PusherOffset());
            KeepInPlayableSpace();
            CurrentSpeed = Vector3.ProjectOnPlane(transform.position - previous, Vector3.up).magnitude / deltaTime;
            if (visual) visual.SetMotion(Mathf.Clamp01(CurrentSpeed / (IsRunning ? runSpeed : walkSpeed)), IsRunning);
            if (!ClimbEnabled) return;
            TryBeginClimb(direction, deltaTime);
            AlignBody(deltaTime);
        }

        // ---- Walls and ceiling (Phase 3) ----
        // The controller is a sphere (height = 2 x radius), so the body is the same in every orientation.
        // On a surface the CharacterController is switched off and the sphere is moved with sweeps:
        // slide along the surface, then stick to it. Sweep normals at an edge point from the edge to the
        // sphere centre, so the body rolls around convex corners instead of falling off them.
        private void TryBeginClimb(Vector3 direction, float deltaTime)
        {
            if (direction.sqrMagnitude < 0.25f || !controller.enabled) { pushTime = 0; return; }
            Vector3 along = direction.normalized;
            if (Cast(Centre, Radius * 0.92f, along, Radius * 0.08f + 0.06f, out RaycastHit hit) && Mathf.Abs(hit.normal.y) < 0.5f
                && IsClimbable(hit.collider) && Vector3.Dot(along, -hit.normal) > 0.5f)
            {
                pushTime += deltaTime;
                if (pushTime >= climbEngageSeconds) BeginClimb(hit.normal);
            }
            else pushTime = 0;
        }

        public bool BeginClimb(Vector3 normal)
        {
            if (!ClimbEnabled || climbing) return false;
            Vector3 centre = Centre;
            climbing = true;
            controller.enabled = false;
            verticalVelocity = 0;
            pushTime = 0;
            climbHeading = transform.forward;
            Turn(normal, false);
            SetCentre(centre);
            hasSafeClimb = false;
            return true;
        }

        // Space: let go of a wall or the ceiling and drop.
        public void Detach()
        {
            if (!climbing) return;
            LeaveSurface(Centre);
        }

        public void ResetSurface()
        {
            climbing = hasSafeClimb = false;
            pushTime = 0;
            slide = Vector3.zero;
            surfaceUp = viewUp = Vector3.up;
            if (controller) controller.enabled = true;
            if (surfaceAlign) surfaceAlign.localRotation = Quaternion.identity;
        }

        private void ClimbMove(Vector2 input, bool run, float deltaTime)
        {
            Vector3 right = movementCamera ? Vector3.ProjectOnPlane(movementCamera.right, surfaceUp) : Vector3.zero;
            if (right.sqrMagnitude < 0.01f) right = Vector3.Cross(surfaceUp, climbHeading);
            right.Normalize();
            Vector3 forward = Vector3.Cross(right, surfaceUp);
            Vector3 direction = forward * input.y + right * input.x;
            IsRunning = run && input.sqrMagnitude > 0.01f;
            float speed = (IsRunning ? runSpeed : walkSpeed) * climbSpeedScale * Mathf.Clamp(SpeedMultiplier * UpgradeSpeed, 0, 2);
            Vector3 start = Centre, centre = start, delta = direction * speed * deltaTime;
            if (direction.sqrMagnitude > 0.01f) climbHeading = direction.normalized;
            for (int i = 0; i < 3 && delta.sqrMagnitude > 1e-10f; i++)
            {
                float length = delta.magnitude;
                Vector3 along = delta / length;
                if (!Cast(centre, Radius - Skin, along, length + Skin, out RaycastHit hit)) { centre += delta; break; }
                float travel = Mathf.Max(0, hit.distance - Skin);
                centre += along * travel;
                Vector3 rest = delta - along * travel;
                if (CanStand(hit) && Vector3.Angle(hit.normal, surfaceUp) > 30f)
                {
                    Turn(hit.normal, false);   // inside corner: wall -> ceiling, wall -> floor
                    if (!climbing) { FinishFloorLanding(centre); return; }
                    delta = Vector3.ProjectOnPlane(rest, surfaceUp);
                }
                else delta = Vector3.ProjectOnPlane(rest, hit.normal);
            }
            if (Cast(centre, Radius - Skin, -surfaceUp, StickDistance, out RaycastHit ground) && CanStand(ground))
            {
                centre -= surfaceUp * Mathf.Max(0, ground.distance - Skin);
                Turn(ground.normal, true);
            }
            else if (!WrapEdge(ref centre, direction.sqrMagnitude > 0.01f ? direction : climbHeading)) { LeaveSurface(centre); return; }
            if (!climbing) { FinishFloorLanding(centre); return; }
            SetCentre(centre);
            if (Blocked(Radius * 0.45f)) { RestoreClimb(); return; }
            if (!Blocked(Radius * 0.92f))
            { safeClimbCentre = Centre; safeClimbUp = surfaceUp; safeClimbHeading = climbHeading; hasSafeClimb = true; }
            CurrentSpeed = (Centre - start).magnitude / deltaTime;
            if (visual) visual.SetMotion(Mathf.Clamp01(CurrentSpeed / ((IsRunning ? runSpeed : walkSpeed) * climbSpeedScale)), IsRunning);
            AlignBody(deltaTime);
        }

        // Fallback for a sharp convex edge the stick sweep missed: look back under the body for the face
        // beyond the edge and continue on it.
        private bool WrapEdge(ref Vector3 centre, Vector3 moving)
        {
            Vector3 origin = centre - surfaceUp * (Radius + 0.12f);
            if (!Ray(origin, -moving.normalized, Radius + 0.6f, out RaycastHit hit) || !CanStand(hit)) return false;
            Turn(hit.normal, true);
            centre = hit.point + hit.normal * (Radius + Skin);
            return true;
        }

        // New surface normal. The heading is carried over the corner: up a wall becomes away from it on the
        // ceiling; over a convex edge it continues onto the face beyond.
        private void Turn(Vector3 normal, bool convex)
        {
            Vector3 previous = surfaceUp;
            surfaceUp = normal.normalized;
            Vector3 heading = Vector3.ProjectOnPlane(climbHeading, surfaceUp);
            if (heading.sqrMagnitude < 0.04f) heading = Vector3.ProjectOnPlane(convex ? -previous : previous, surfaceUp);
            if (heading.sqrMagnitude > 1e-4f) climbHeading = heading.normalized;
            // Floor or worktop: back to the normal controller, but only on the face itself. A rounded-edge
            // normal is never exactly up; switching there would leave the controller balanced on the edge.
            if (surfaceUp.y > 0.99f) climbing = false;
        }

        private void FinishFloorLanding(Vector3 centre)
        {
            climbing = true;
            LeaveSurface(centre);
        }

        private void LeaveSurface(Vector3 centre)
        {
            Quaternion body = surfaceAlign ? surfaceAlign.rotation : transform.rotation;
            climbing = false;
            surfaceUp = Vector3.up;
            Vector3 flat = Vector3.ProjectOnPlane(climbHeading, Vector3.up);
            Quaternion yaw = flat.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(flat) : Quaternion.Euler(0, transform.eulerAngles.y, 0);
            transform.SetPositionAndRotation(centre - Vector3.up * Half, yaw);
            if (surfaceAlign) surfaceAlign.rotation = body;   // keep the pose; AlignBody eases it level
            controller.enabled = true;
            verticalVelocity = 0;
            pushTime = 0;
            Physics.SyncTransforms();
        }

        private void RestoreClimb()
        {
            Recoveries++;
            if (!hasSafeClimb) { LeaveSurface(lastSafePosition + Vector3.up * Half); return; }
            surfaceUp = safeClimbUp; climbHeading = safeClimbHeading;
            SetCentre(safeClimbCentre);
        }

        private void AlignBody(float deltaTime)
        {
            float k = 1 - Mathf.Exp(-14f * deltaTime);
            viewUp = Vector3.Slerp(viewUp, surfaceUp, 1 - Mathf.Exp(-7f * deltaTime)).normalized;
            if (!surfaceAlign) return;
            if (climbing) surfaceAlign.rotation = Quaternion.Slerp(surfaceAlign.rotation, Quaternion.LookRotation(climbHeading, surfaceUp), k);
            else surfaceAlign.localRotation = Quaternion.Slerp(surfaceAlign.localRotation, Quaternion.identity, k);
        }

        private float Half => controller.center.y * transform.lossyScale.y;
        private void SetCentre(Vector3 centre) { transform.position = centre - Vector3.up * Half; }
        private static bool IsClimbable(Collider body) => body && body.TryGetComponent(out ClimbableSurface _);
        private static bool CanStand(RaycastHit hit) => hit.normal.y > 0.75f || IsClimbable(hit.collider);

        private bool Ignored(RaycastHit hit)
        {
            if (hit.distance <= 0 && hit.point == Vector3.zero) return true;   // started inside: recovery handles it
            return hit.collider.transform.IsChildOf(transform) || (hit.collider is CapsuleCollider capsule && pushers.Contains(capsule));
        }
        private bool Cast(Vector3 origin, float radius, Vector3 direction, float distance, out RaycastHit best)
        {
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, hits, distance, ObstacleMask, QueryTriggerInteraction.Ignore);
            return Nearest(count, out best);
        }
        private bool Ray(Vector3 origin, Vector3 direction, float distance, out RaycastHit best)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, ObstacleMask, QueryTriggerInteraction.Ignore);
            return Nearest(count, out best);
        }
        private bool Nearest(int count, out RaycastHit best)
        {
            best = default;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
                if (!Ignored(hits[i]) && hits[i].distance < nearest) { nearest = hits[i].distance; best = hits[i]; }
            return nearest < float.MaxValue;
        }

        public bool IsEmbedded => controller && Blocked(Radius * 0.45f);
        private float Radius => controller.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        private Vector3 Centre => transform.TransformPoint(controller.center);

        private Vector3 PusherOffset()
        {
            Vector3 push = Vector3.zero, centre = Centre;
            float half = controller.height * transform.lossyScale.y * 0.5f;
            foreach (var body in pushers)
            {
                if (!body || !body.enabled || !body.gameObject.activeInHierarchy) continue;
                Vector3 axis = body.transform.TransformPoint(body.center);
                float bodyHalf = body.height * body.transform.lossyScale.y * 0.5f;
                if (centre.y - half > axis.y + bodyHalf || centre.y + half < axis.y - bodyHalf) continue;
                Vector3 away = Vector3.ProjectOnPlane(centre - axis, Vector3.up);
                float clearance = Radius + body.radius * Mathf.Max(body.transform.lossyScale.x, body.transform.lossyScale.z);
                if (away.sqrMagnitude >= clearance * clearance) continue;
                Vector3 normal = away.sqrMagnitude > 1e-6f ? away.normalized : -transform.forward;
                push += normal * (clearance - away.magnitude);
            }
            return push;
        }

        // Last line of defence: a roach that ends up inside furniture or below the floor returns to the
        // last place where it stood clear, instead of being stuck in an unlit, unescapable volume.
        private void KeepInPlayableSpace()
        {
            if (transform.position.y < fallRecoveryHeight || Blocked(Radius * 0.45f))
            {
                if (hasSafePosition) Warp(lastSafePosition);
                Recoveries++;
                return;
            }
            if (controller.isGrounded && !Blocked(Radius * 0.92f)) ResetSafePosition();
        }

        private bool Blocked(float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(Centre, radius, overlaps, ObstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = overlaps[i];
                if (hit.transform.IsChildOf(transform) || (hit is CapsuleCollider capsule && pushers.Contains(capsule))) continue;
                return true;
            }
            return false;
        }

        private void Warp(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            verticalVelocity = 0;
            Physics.SyncTransforms();
        }
    }
}
