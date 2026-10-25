using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    /// <summary>
    /// The son's bug net (Stage 2). Close to a roach on the floor he lifts the net and slams it down on an orange ring
    /// (larger than a slipper, a little slower). The player caught under it is held: mash Space to wriggle out before
    /// HoldSeconds or take a hit. An ally caught goes into his bug cage until the player opens it (E at the cage).
    /// </summary>
    public sealed class BugNetAttack : HouseholdAttack
    {
        public enum Phase { Idle, Windup, Hold, Recover }
        [SerializeField] private Transform ring, fill, cover;
        [SerializeField] private BugCage cage;
        public const float MinRange = 0f, MaxRange = 3.1f, WindupSeconds = 1.05f, HoldSeconds = 2.6f, RecoverSeconds = 0.9f, Cooldown = 4.5f;
        public const float Radius = 0.95f, AllyNotice = 0.5f;
        public const int EscapePresses = 7;
        private Vector3 point;
        public Phase Current { get; private set; }
        public bool PlayerCaught { get; private set; }
        public int Presses { get; private set; }
        public int Caught { get; private set; }
        public int Escapes { get; private set; }
        public int PlayerHits { get; private set; }
        public Vector3 Point => point;
        public override bool Busy => Current != Phase.Idle;
        public float Remaining => Current == Phase.Windup ? WindupSeconds - clock : Current == Phase.Hold ? HoldSeconds - clock : 0;
        public BugCage Cage => cage;

        public void ConfigureVisuals(Transform warningRing, Transform warningFill, Transform netCover, BugCage bugCage)
        { ring = warningRing; fill = warningFill; cover = netCover; cage = bugCage; }

        private void Update()
        {
            if (!session.IsRunning) return;
            float dt = Time.deltaTime;
            clock += dt;
            switch (Current)
            {
                case Phase.Idle:
                    if (CanStart && PickTarget(out Vector3 target, out int ally))
                    {
                        float d = Flat(target - human.transform.position).magnitude;
                        if (d >= MinRange && d <= MaxRange) Begin(ally >= 0 ? target + Vector3.ClampMagnitude(commander.Allies[ally].Velocity * WindupSeconds * 0.4f, 0.6f) : target);
                    }
                    break;
                case Phase.Windup:
                    Face(point, dt);
                    // Net raised high, then slammed down onto the ring in the last moment.
                    float swing = Mathf.Clamp01((clock - (WindupSeconds - 0.18f)) / 0.18f);
                    animator.SprayPoint = point + Vector3.up * Mathf.Lerp(3.4f, 0, swing * swing);
                    if (fill) fill.localScale = Vector3.one * Mathf.Lerp(0.05f, Radius * 2, clock / WindupSeconds);
                    if (clock >= WindupSeconds) Land();
                    break;
                case Phase.Hold:
                    if (Input.GetKeyDown(KeyCode.Space) && session.CanControl) Wriggle();
                    if (Current == Phase.Hold && clock >= HoldSeconds) Fail();
                    break;
                case Phase.Recover:
                    if (clock >= RecoverSeconds) { Release(); Current = Phase.Idle; cooldownUntil = Time.time + Cooldown; animator.SprayAim = 0; if (cover) cover.gameObject.SetActive(false); }
                    break;
            }
        }

        private void Begin(Vector3 target)
        {
            Hold();
            point = Flat(target);
            if (!commander.Grid.Walkable(point)) commander.Grid.NearestWalkable(point, 1f, out point);
            Current = Phase.Windup; clock = 0; Uses++;
            animator.SprayPoint = point + Vector3.up * 3.4f; animator.SprayAim = 1;
            if (ring) { ring.gameObject.SetActive(true); ring.SetPositionAndRotation(point + Vector3.up * 0.03f, Quaternion.identity); ring.localScale = new Vector3(Radius * 2, 1, Radius * 2); }
            if (fill) { fill.gameObject.SetActive(true); fill.SetPositionAndRotation(point + Vector3.up * 0.025f, Quaternion.identity); fill.localScale = Vector3.one * 0.05f; }
            Speak("虫取り網でつかまえる！");
        }

        public bool Inside(Vector3 position) => position.y < 0.6f && Flat(position - point).magnitude <= Radius;

        private void Land()
        {
            if (ring) ring.gameObject.SetActive(false);
            if (fill) fill.gameObject.SetActive(false);
            if (cover) { cover.gameObject.SetActive(true); cover.SetPositionAndRotation(point, Quaternion.identity); }
            int netted = 0;
            foreach (var ally in commander.Allies)
            {
                if (!ally.Active || !Inside(ally.Position) || (session.HasNestArea && session.InNest(ally.Position))) continue;
                commander.CageAlly(ally);
                if (cage) cage.Add(ally.Slot);
                netted++; Caught++;
            }
            if (PlayerOnFloor && Inside(session.Motor.FootPoint))
            {
                PlayerCaught = true; Presses = 0;
                session.Restrained = true;
                Current = Phase.Hold; clock = 0;
                Speak("つかまえた！  虫かごに入れちゃお");
                return;
            }
            if (netted > 0) { Speak("やった！  ゲット！"); commander.Say(netted == 1 ? "仲間が虫取り網に捕まった！  虫かごで E を押すと助けられる" : "仲間" + netted + "匹が虫取り網に捕まった！  虫かごで助けよう"); }
            else Speak("あーっ、逃げられた");
            Current = Phase.Recover; clock = 0;
        }

        /// <summary>One Space press while held: enough of them and the player slips out.</summary>
        public void Wriggle()
        {
            if (Current != Phase.Hold) return;
            Presses++;
            if (Presses < EscapePresses) return;
            PlayerCaught = false; session.Restrained = false; Escapes++;
            Speak("あっ、網から逃げた！");
            Current = Phase.Recover; clock = 0;
        }
        private void Fail()
        {
            PlayerCaught = false; session.Restrained = false;
            if (session.TryDamage(1)) PlayerHits++;
            Speak("えいっ！  …あれ、どこ行った？");
            Current = Phase.Recover; clock = 0;
        }

        public override void Stop()
        {
            if (PlayerCaught && session) session.Restrained = false;
            PlayerCaught = false;
            if (Current != Phase.Idle) Release();
            Current = Phase.Idle; clock = 0; cooldownUntil = 0;
            if (animator) animator.SprayAim = 0;
            foreach (var t in new[] { ring, fill, cover }) if (t) t.gameObject.SetActive(false);
        }
        public void ResetCounts() { Caught = Escapes = PlayerHits = Uses = 0; }

        // Allies notice the raised net a moment late, then run out of the ring.
        public override bool Threatens(Vector3 position, out Vector3 escape)
        {
            escape = position;
            if (Current != Phase.Windup || clock < AllyNotice || !Inside(position)) return false;
            Vector3 away = Flat(position - point);
            if (away.sqrMagnitude < 1e-3f) away = Vector3.right;
            escape = point + away.normalized * (Radius + 0.9f);
            if (!commander.Grid.Walkable(escape)) commander.Grid.NearestWalkable(escape, 1.5f, out escape);
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugSwing(Vector3 target) { cooldownUntil = 0; Begin(target); }
#endif
    }
}
