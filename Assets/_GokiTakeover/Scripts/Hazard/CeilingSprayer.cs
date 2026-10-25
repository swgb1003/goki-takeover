using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    /// <summary>
    /// The exterminator's backpack sprayer (final defence): a roach that stays up high (the ceiling, high walls, the
    /// top of the fridge or shelf) gets sprayed. After a few seconds up there the exterminator stops, raises the lance
    /// and an orange ring marks the spot on that surface (Aim); then a cone of mist hits it (Spray). Inside the ring
    /// the roach takes damage; crawling out of it in time avoids it. Then a cooldown. StageProgress drives it.
    /// </summary>
    public sealed class CeilingSprayer : MonoBehaviour
    {
        public enum Phase { Idle, Aim, Spray, Recover }
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private BlenderHumanAnimator animator;
        [SerializeField] private Transform ring, fill, mist, splash;
        public const float HighAbove = 3.0f, NoticeSeconds = 3f, AimSeconds = 1.5f, SpraySeconds = 1.0f, RecoverSeconds = 0.6f,
            CooldownSeconds = 4f, Radius = 1.1f, Range = 7.5f;
        private Vector3 point, normal = Vector3.down;
        private float clock, cooldownUntil;
        private bool suspended;
        public Phase Current { get; private set; }
        public float HighTime { get; private set; }
        public int Hits { get; private set; }
        public int Shots { get; private set; }
        public Vector3 Point => point;
        public bool Targeting => Current == Phase.Aim || Current == Phase.Spray;
        public float Remaining => Current == Phase.Aim ? AimSeconds - clock : Current == Phase.Spray ? SpraySeconds - clock : 0;

        public void Configure(PhaseTwoSession game, BlenderHumanAnimator body, Transform warningRing, Transform warningFill, Transform mistCone, Transform impact)
        { session = game; animator = body; ring = warningRing; fill = warningFill; mist = mistCone; splash = impact; }

        public bool PlayerHigh => session.Motor.FootPoint.y > HighAbove;

        /// <summary>One step; allowed is false while the exterminator is busy elsewhere (walking to smoke the nest).</summary>
        public void Tick(float dt, bool allowed)
        {
            var human = session.Human;
            clock += dt;
            switch (Current)
            {
                case Phase.Idle:
                    HighTime = PlayerHigh ? HighTime + dt : Mathf.Max(0, HighTime - dt * 2);
                    if (allowed && HighTime >= NoticeSeconds && Time.time >= cooldownUntil && !human.Suspended && human.State != HumanAIController.HumanState.Attack
                        && Flat(session.Motor.FootPoint - human.transform.position).magnitude <= Range) Begin();
                    break;
                case Phase.Aim:
                    Face(dt);
                    if (fill) fill.localScale = new Vector3(1, 1, 1) * Mathf.Lerp(0.05f, Radius * 2, clock / AimSeconds);
                    if (clock >= AimSeconds) { Current = Phase.Spray; clock = 0; Shots++; if (mist) mist.gameObject.SetActive(true); if (splash) splash.gameObject.SetActive(true); if (fill) fill.gameObject.SetActive(false); }
                    break;
                case Phase.Spray:
                    Face(dt);
                    PlaceMist();
                    if (Inside(session.Motor.FootPoint) && session.TryDamage(1)) Hits++;
                    if (clock >= SpraySeconds) { Current = Phase.Recover; clock = 0; Hide(); animator.SprayAim = 0; }
                    break;
                case Phase.Recover:
                    if (clock >= RecoverSeconds) { Release(); Current = Phase.Idle; cooldownUntil = Time.time + CooldownSeconds; HighTime = 0; }
                    break;
            }
        }

        private void Begin()
        {
            var motor = session.Motor;
            point = motor.FootPoint;
            normal = motor.IsClimbing ? motor.SurfaceUp.normalized : Vector3.up;
            Current = Phase.Aim; clock = 0;
            var human = session.Human;
            human.Suspended = true; suspended = true;
            var agent = human.GetComponent<NavMeshAgent>();
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
            animator.SprayPoint = point; animator.SprayAim = 1;
            Quaternion lie = Quaternion.FromToRotation(Vector3.up, normal);
            if (ring) { ring.gameObject.SetActive(true); ring.SetPositionAndRotation(point + normal * 0.03f, lie); ring.localScale = new Vector3(Radius * 2, 1, Radius * 2); }
            if (fill) { fill.gameObject.SetActive(true); fill.SetPositionAndRotation(point + normal * 0.025f, lie); fill.localScale = Vector3.one * 0.05f; }
            if (splash) { splash.SetPositionAndRotation(point + normal * 0.04f, lie); splash.localScale = new Vector3(Radius * 2, 1, Radius * 2); }
        }

        // Within the ring on the struck surface (a roach on the far side of a wall or a shelf board is missed).
        public bool Inside(Vector3 position)
        {
            Vector3 offset = position - point;
            return Mathf.Abs(Vector3.Dot(offset, normal)) < 0.9f && Vector3.ProjectOnPlane(offset, normal).magnitude <= Radius;
        }

        private void PlaceMist()
        {
            if (!mist) return;
            Vector3 from = animator.NozzleWorld, to = point;
            Vector3 along = to - from;
            float flicker = 1 + 0.08f * Mathf.Sin(Time.time * 41) + 0.05f * Mathf.Sin(Time.time * 67);
            mist.SetPositionAndRotation(from, Quaternion.LookRotation(along.sqrMagnitude > 1e-4f ? along : Vector3.up));
            mist.localScale = new Vector3(Radius * 1.7f * flicker, Radius * 1.7f * flicker, along.magnitude);
        }

        private void Face(float dt)
        {
            var human = session.Human.transform;
            Vector3 flat = Flat(point - human.position);
            if (flat.sqrMagnitude > 0.01f) human.rotation = Quaternion.RotateTowards(human.rotation, Quaternion.LookRotation(flat), 300 * dt);
        }

        private void Hide()
        {
            if (ring) ring.gameObject.SetActive(false);
            if (fill) fill.gameObject.SetActive(false);
            if (mist) mist.gameObject.SetActive(false);
            if (splash) splash.gameObject.SetActive(false);
        }
        private void Release()
        {
            if (!suspended) return;
            suspended = false;
            var human = session.Human;
            human.Suspended = false;
            var agent = human.GetComponent<NavMeshAgent>();
            if (agent && agent.isOnNavMesh) agent.isStopped = false;
        }

        /// <summary>Back to idle at once (reset, retreat, a run restored).</summary>
        public void Stop()
        {
            Hide();
            if (animator) animator.SprayAim = 0;
            if (Current != Phase.Idle) Release();
            Current = Phase.Idle; clock = 0; HighTime = 0; cooldownUntil = 0;
        }
        public void ResetCounts() { Hits = Shots = 0; }

        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
