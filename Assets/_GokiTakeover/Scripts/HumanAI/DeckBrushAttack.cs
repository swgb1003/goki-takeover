using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Stage 3: the part-timer's deck brush. Close to a roach on the floor he sets the brush down: an orange box in front
    /// of him (WindupSeconds), then one hard sweep (SweepSeconds) down its length. Roaches in the box are swept away
    /// along it (a shove across the floor); the player also takes a hit, an ally is flipped (or killed when wounded).
    /// </summary>
    public sealed class DeckBrushAttack : HouseholdAttack
    {
        public enum Phase { Idle, Windup, Sweep, Recover }
        [SerializeField] private RectTelegraph box;
        public const float MaxRange = 2.8f, Length = 3.2f, Width = 1.6f, StartOffset = 0.5f;
        public const float WindupSeconds = 0.9f, SweepSeconds = 0.35f, RecoverSeconds = 0.7f, Cooldown = 4f, Shove = 7.5f;
        private Vector3 start, direction;
        private bool playerSwept;
        private readonly System.Collections.Generic.HashSet<AllyRoach> swept = new System.Collections.Generic.HashSet<AllyRoach>();
        public Phase Current { get; private set; }
        public int PlayerHits { get; private set; }
        public int AllyHits { get; private set; }
        public override bool Busy => Current != Phase.Idle;
        public bool WindingUp => Current == Phase.Windup;
        public Vector3 Origin => start;
        public Vector3 Direction => direction;

        public void ConfigureVisuals(RectTelegraph telegraph) { box = telegraph; }

        private void Update()
        {
            if (!session.IsRunning) { if (playerSwept) session.Motor.ExternalVelocity = Vector3.zero; playerSwept = false; return; }
            float dt = Time.deltaTime;
            clock += dt;
            switch (Current)
            {
                case Phase.Idle:
                    if (CanStart && PickTarget(out Vector3 target, out _) && Flat(target - human.transform.position).magnitude <= MaxRange) Begin(target);
                    break;
                case Phase.Windup:
                    // Brush lifted behind him, then brought down to the near end of the box.
                    animator.SprayPoint = start + Vector3.up * Mathf.Lerp(1.8f, 0.1f, Mathf.Clamp01(clock / WindupSeconds));
                    if (clock >= WindupSeconds) { Current = Phase.Sweep; clock = 0; if (box) box.Hide(); Speak("どりゃっ！"); }
                    break;
                case Phase.Sweep:
                    Sweep(dt);
                    if (clock >= SweepSeconds) End();
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
            direction = Flat(target - from);
            direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : human.transform.forward;
            human.transform.rotation = Quaternion.LookRotation(direction);
            start = from + direction * StartOffset;
            Current = Phase.Windup; clock = 0; Uses++;
            swept.Clear(); playerSwept = false;
            animator.SprayPoint = start + Vector3.up * 1.8f; animator.SprayAim = 1;
            if (box) box.Show(start, direction, Length, Width);
            Speak("デッキブラシで掃き出してやる！");
        }

        /// <summary>Inside the swept box (on the floor).</summary>
        public bool InBox(Vector3 position)
        {
            if (position.y > 0.6f) return false;
            Vector3 offset = Flat(position - start);
            float along = Vector3.Dot(offset, direction), across = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, direction)));
            return along >= -0.2f && along <= Length && across <= Width * 0.5f;
        }

        private void Sweep(float dt)
        {
            float t = Mathf.Clamp01(clock / SweepSeconds);
            animator.SprayPoint = start + direction * Length * t + Vector3.up * 0.1f;   // the head runs down the box
            if (!playerSwept && PlayerOnFloor && InBox(session.Motor.FootPoint))
            {
                playerSwept = true;
                if (session.TryDamage(1)) PlayerHits++;
                commander.Say("デッキブラシで掃き飛ばされた！");
            }
            session.Motor.ExternalVelocity = playerSwept ? direction * Shove * (1 - t) : Vector3.zero;
            foreach (var ally in commander.Allies)
            {
                if (!ally.Active) continue;
                if (!swept.Contains(ally))
                {
                    if (!InBox(ally.Position)) continue;
                    swept.Add(ally);
                    AllyHits++;
                    if (commander.StrikeAlly(ally)) continue;
                }
                if (ally.Active) ally.Pull(direction * Shove * (1 - t) * dt);
            }
        }

        private void End()
        {
            if (playerSwept) session.Motor.ExternalVelocity = Vector3.zero;
            playerSwept = false;
            Current = Phase.Recover; clock = 0;
            animator.SprayAim = 0;
            if (box) box.Hide();
        }

        public override void Stop()
        {
            if (Current != Phase.Idle) { End(); Release(); }
            Current = Phase.Idle; clock = 0; cooldownUntil = 0;
            if (animator) animator.SprayAim = 0;
        }
        public void ResetCounts() { PlayerHits = AllyHits = Uses = 0; }

        // Allies in the box step out of it sideways while he winds up.
        public override bool Threatens(Vector3 position, out Vector3 escape)
        {
            escape = position;
            if (Current != Phase.Windup || !InBox(position)) return false;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            Vector3 offset = Flat(position - start);
            float s = Vector3.Dot(offset, side) >= 0 ? 1 : -1, along = Mathf.Clamp(Vector3.Dot(offset, direction), 0, Length);
            for (int k = 0; k < 2; k++, s = -s)
            {
                Vector3 p = start + direction * along + side * s * (Width * 0.5f + 0.9f);
                if (commander.Grid.Walkable(p)) { escape = p; return true; }
            }
            commander.Grid.NearestWalkable(start + direction * along + side * s * 1.8f, 2f, out escape);
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugAim(Vector3 target) { cooldownUntil = 0; Begin(Flat(target)); }
#endif
    }
}
