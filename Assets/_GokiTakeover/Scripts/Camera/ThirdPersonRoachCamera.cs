using UnityEngine;

namespace GokiTakeover
{
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(100)]
    public sealed class ThirdPersonRoachCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 pivotOffset = new Vector3(0, 0.42f, 0);
        [SerializeField, Range(1f, 5f)] private float distance = 2.5f;
        [SerializeField, Range(0.1f, 8f)] private float sensitivity = 2f;
        [SerializeField, Range(0.05f, 0.25f)] private float collisionRadius = 0.12f;
        [SerializeField] private LayerMask obstacleMask = ~(1 << 2); // Ignore Raycast: player only; no project layer changes.
        [SerializeField, Range(-12f, 65f)] private float initialPitch = 18f;
        private float yaw;
        private float pitch = 18f;
        private float currentDistance;
        private float shake;
        // Orbit frame: world up on the floor, the (smoothed) surface normal on walls and the ceiling.
        private RoachMotor climber;
        private Quaternion frame = Quaternion.identity;
        public Transform Target => target;
        public bool ManageCursor { get; set; } = true;
        // Settings screen (Phase 4): mouse sensitivity and a 0..1 scale for impact shake.
        public float Sensitivity { get => sensitivity; set => sensitivity = Mathf.Clamp(value, 0.1f, 8f); }
        public float ShakeScale { get; set; } = 1f;
        public void Configure(Transform followTarget) { target = followTarget; }
        public void ConfigureFraming(float followDistance, Vector3 offset, float elevation)
        {
            distance = Mathf.Clamp(followDistance, 1f, 5f);
            pivotOffset = offset;
            initialPitch = Mathf.Clamp(elevation, -12f, 65f);
        }

        private void Start()
        {
            if (!target)
            {
                Debug.LogWarning("[GOKI] 追従対象が未設定です。テストシーンを再生成してください。", this);
                enabled = false;
                return;
            }
            yaw = target.eulerAngles.y;
            pitch = initialPitch;
            currentDistance = distance;
            climber = target.GetComponent<RoachMotor>();
            GetComponent<Camera>().nearClipPlane = 0.04f;
            LockCursor(true);
            Follow(0);
        }

        private void Update()
        {
            if (!ManageCursor) return;
            if (GokiInput.Escape) LockCursor(false);
            if (GokiInput.Click && Application.isFocused) LockCursor(true);
        }

        private void LateUpdate()
        {
            if (Cursor.lockState == CursorLockMode.Locked && Application.isFocused)
            {
                Vector2 look = GokiInput.Look;
                RotateView(look);
            }
            Follow(Time.deltaTime);
        }

        public void RotateView(Vector2 delta)
        {
            yaw += delta.x * sensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -12f, 65f);
        }

        public void AddShake(float amount) { shake = Mathf.Clamp(shake + amount * ShakeScale, 0, 0.6f); }

        public void ResetView()
        {
            shake = 0;
            frame = Quaternion.identity;
            if (!target) return;
            yaw = target.eulerAngles.y;
            pitch = initialPitch;
            currentDistance = distance;
            Follow(0);
        }

        public void Follow(float deltaTime)
        {
            if (!target) return;
            bool surface = climber && climber.ClimbEnabled;
            // Incremental turn: never a 180 degree flip, and identity (the old behaviour) on the floor.
            frame = Quaternion.FromToRotation(frame * Vector3.up, surface ? climber.ViewUp : Vector3.up) * frame;
            Vector3 pivot = (surface ? climber.ViewFoot : target.position) + frame * pivotOffset;
            Quaternion orbit = frame * Quaternion.Euler(pitch, yaw, 0);
            Vector3 direction = orbit * Vector3.back;
            float safeDistance = distance;
            if (Physics.SphereCast(pivot, collisionRadius, direction, out RaycastHit hit, distance,
                obstacleMask, QueryTriggerInteraction.Ignore))
                safeDistance = Mathf.Max(0, hit.distance - 0.03f);
            // Pull in immediately; ease back out. Never interpolate through the blocking surface.
            currentDistance = safeDistance < currentDistance || deltaTime <= 0 ? safeDistance
                : Mathf.Lerp(currentDistance, safeDistance, 1f - Mathf.Exp(-8f * deltaTime));
            Vector3 jolt = Vector3.zero;
            if (shake > 0.001f)
            {
                float t = Time.unscaledTime * 38f;
                jolt = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0) * (shake * 0.25f);
                shake = Mathf.MoveTowards(shake, 0, Mathf.Max(deltaTime, 0) * 1.4f);
            }
            transform.SetPositionAndRotation(pivot + direction * currentDistance + orbit * jolt, orbit);
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
        private void OnApplicationFocus(bool focused) { if (!focused) LockCursor(false); }
        private void OnDisable() { LockCursor(false); }
    }
}
