using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    /// <summary>
    /// The mother's cordless vacuum (Stage 2). Seeing a roach on the floor at mid range she points the floor head
    /// at it: an orange fan on the floor for AimSeconds, then the suction. Inside the fan roaches are dragged toward
    /// the head, harder the closer they get; the player can still run out of it sideways (or straight back at a run).
    /// Reaching the head: the player is hurt and the vacuum clogs; an ally is sucked up for good.
    /// </summary>
    public sealed class VacuumAttack : HouseholdAttack
    {
        public enum Phase { Idle, Aim, Suck, Recover }
        [SerializeField] private Transform fan, air;
        public const float MinRange = 1.4f, MaxRange = 5.2f, AimSeconds = 1.1f, SuckSeconds = 2.6f, RecoverSeconds = 0.8f, Cooldown = 7f;
        public const float Length = 5.4f, HalfAngle = 24f, MouthRadius = 0.7f, PullFar = 1.3f, PullNear = 2.9f;
        private Vector3 mouth, direction;
        public Phase Current { get; private set; }
        public int Sucked { get; private set; }
        public int PlayerHits { get; private set; }
        public Vector3 Mouth => mouth;
        public Vector3 Direction => direction;
        public override bool Busy => Current != Phase.Idle;
        public bool Sucking => Current == Phase.Suck;
        public float Remaining => Current == Phase.Aim ? AimSeconds - clock : Current == Phase.Suck ? SuckSeconds - clock : 0;

        public void ConfigureVisuals(Transform warningFan, Transform airCone) { fan = warningFan; air = airCone; }

        private void Update()
        {
            if (!session.IsRunning) { session.Motor.ExternalVelocity = Vector3.zero; return; }
            float dt = Time.deltaTime;
            clock += dt;
            switch (Current)
            {
                case Phase.Idle:
                    if (CanStart && PickTarget(out Vector3 target, out _))
                    {
                        float d = Flat(target - human.transform.position).magnitude;
                        if (d >= MinRange && d <= MaxRange) Begin(target);
                    }
                    break;
                case Phase.Aim:
                    if (clock >= AimSeconds) { Current = Phase.Suck; clock = 0; if (air) air.gameObject.SetActive(true); Speak("吸い込んじゃえ！"); }
                    break;
                case Phase.Suck:
                    Suck(dt);
                    if (Current == Phase.Suck && clock >= SuckSeconds) End();
                    break;
                case Phase.Recover:
                    if (clock >= RecoverSeconds) { Release(); Current = Phase.Idle; cooldownUntil = Time.time + Cooldown; }
                    break;
            }
        }

        private void Begin(Vector3 target)
        {
            Hold();
            Vector3 from = Flat(human.transform.position);
            direction = Flat(target - from).normalized;
            human.transform.rotation = Quaternion.LookRotation(direction);
            mouth = from + direction * 0.95f;
            Current = Phase.Aim; clock = 0; Uses++;
            animator.SprayPoint = mouth + direction * 0.6f; animator.SprayAim = 1;
            if (fan) { fan.gameObject.SetActive(true); fan.SetPositionAndRotation(mouth + Vector3.up * 0.045f, Quaternion.LookRotation(direction)); fan.localScale = new Vector3(Length, 1, Length); }   // just above rugs and mats
            Speak("そこね！");
        }

        /// <summary>Inside the suction fan on the floor (the player up a wall or on furniture is out of reach).</summary>
        public bool InFan(Vector3 position)
        {
            if (position.y > 0.6f) return false;
            Vector3 offset = Flat(position - mouth);
            float d = offset.magnitude;
            return d <= Length && (d < MouthRadius || Vector3.Angle(direction, offset) <= HalfAngle);
        }
        private Vector3 PullOf(Vector3 position)
        {
            Vector3 toMouth = Flat(mouth - position);
            float d = toMouth.magnitude;
            return d > 1e-3f ? toMouth / d * Mathf.Lerp(PullNear, PullFar, d / Length) : Vector3.zero;
        }

        private void Suck(float dt)
        {
            if (air)
            {
                float flicker = 1 + 0.1f * Mathf.Sin(Time.time * 47);
                // The cone's tip at the head, opening over the fan: the air rushing in.
                float width = 2 * Length * Mathf.Tan(HalfAngle * Mathf.Deg2Rad);
                air.SetPositionAndRotation(mouth + Vector3.up * 0.3f, Quaternion.LookRotation(direction));
                air.localScale = new Vector3(width * flicker, 0.6f * flicker, Length);
            }
            Vector3 foot = session.Motor.FootPoint;
            if (PlayerOnFloor && InFan(foot))
            {
                session.Motor.ExternalVelocity = PullOf(foot);
                if (Flat(foot - mouth).magnitude < MouthRadius * 0.75f)
                {
                    session.Motor.ExternalVelocity = Vector3.zero;
                    if (session.TryDamage(1)) PlayerHits++;
                    Speak("詰まった！？  ゴキブリ吸っちゃった…！");
                    End();
                    return;
                }
            }
            else session.Motor.ExternalVelocity = Vector3.zero;
            foreach (var ally in commander.Allies)
            {
                if (!ally.Active || !InFan(ally.Position)) continue;
                ally.Pull(PullOf(ally.Position) * dt);
                if (Flat(ally.Position - mouth).magnitude < MouthRadius * 0.75f) { commander.LoseAlly(ally); Sucked++; commander.Say("仲間が掃除機に吸い込まれた！"); }
            }
        }

        private void End()
        {
            session.Motor.ExternalVelocity = Vector3.zero;
            Current = Phase.Recover; clock = 0;
            animator.SprayAim = 0;
            if (fan) fan.gameObject.SetActive(false);
            if (air) air.gameObject.SetActive(false);
        }

        public override void Stop()
        {
            if (Current != Phase.Idle) { End(); Release(); }
            Current = Phase.Idle; clock = 0; cooldownUntil = 0;
            if (animator) animator.SprayAim = 0;
            if (session && session.Motor) session.Motor.ExternalVelocity = Vector3.zero;
        }
        public void ResetCounts() { Sucked = PlayerHits = Uses = 0; }

        // Allies run out of the fan sideways, the shorter way.
        public override bool Threatens(Vector3 position, out Vector3 escape)
        {
            escape = position;
            if ((Current != Phase.Aim && Current != Phase.Suck) || !InFan(position)) return false;
            Vector3 offset = Flat(position - mouth);
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            float s = Vector3.Dot(offset, side) >= 0 ? 1 : -1;
            float along = Mathf.Max(0.5f, Vector3.Dot(offset, direction));
            for (int k = 0; k < 2; k++, s = -s)
            {
                Vector3 p = mouth + direction * along + side * s * (along * Mathf.Tan(HalfAngle * Mathf.Deg2Rad) + 1.1f);
                if (commander.Grid.Walkable(p)) { escape = p; return true; }
            }
            commander.Grid.NearestWalkable(mouth + direction * along + side * s * 2.2f, 2f, out escape);
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Verification: start the attack at a floor point now, whatever the mother sees.</summary>
        public void DebugAim(Vector3 target) { cooldownUntil = 0; Begin(Flat(target)); }
#endif
    }
}
