using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Stage 3: the cook's hot water. Seeing a roach on the floor at mid range he tips his saucepan toward it: an orange
    /// strip on the floor from his feet past the roach (AimSeconds), then the scalding water runs down the strip
    /// (FlowSeconds) and lies there steaming (PoolSeconds). On the strip while the water is there: the player is hurt,
    /// an ally flipped (or killed when already wounded) as by a slipper. The way out is sideways.
    /// </summary>
    public sealed class HotWaterAttack : HouseholdAttack
    {
        public enum Phase { Idle, Aim, Flow, Pool, Recover }
        [SerializeField] private RectTelegraph strip;
        [SerializeField] private Transform water;
        public const float MinRange = 2.0f, MaxRange = 5.0f, Length = 5.6f, Width = 1.1f, StartOffset = 0.7f;
        public const float AimSeconds = 1.1f, FlowSeconds = 0.5f, PoolSeconds = 1.6f, RecoverSeconds = 0.7f, Cooldown = 9f;
        private Vector3 start, direction;
        private readonly System.Collections.Generic.HashSet<AllyRoach> scalded = new System.Collections.Generic.HashSet<AllyRoach>();
        public Phase Current { get; private set; }
        public int PlayerHits { get; private set; }
        public int AllyHits { get; private set; }
        public override bool Busy => Current != Phase.Idle;
        public bool Hot => Current == Phase.Flow || Current == Phase.Pool;
        public bool Aiming => Current == Phase.Aim;
        public Vector3 Origin => start;
        public Vector3 Direction => direction;
        /// <summary>How far down the strip the water has run (0..Length).</summary>
        public float Reach => Current == Phase.Flow ? Length * Mathf.Clamp01(clock / FlowSeconds) : Current == Phase.Pool ? Length : 0;

        public void ConfigureVisuals(RectTelegraph telegraph, Transform hotWater) { strip = telegraph; water = hotWater; }

        private void Update()
        {
            if (!session.IsRunning) return;
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
                    if (clock >= AimSeconds) { Current = Phase.Flow; clock = 0; if (strip) strip.Hide(); Speak("熱湯をくらえ！"); }
                    break;
                case Phase.Flow:
                case Phase.Pool:
                    ShowWater();
                    Scald();
                    if (Current == Phase.Flow && clock >= FlowSeconds) { Current = Phase.Pool; clock = 0; }
                    else if (Current == Phase.Pool && clock >= PoolSeconds) End();
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
            start = from + direction * StartOffset;
            Current = Phase.Aim; clock = 0; Uses++;
            scalded.Clear();
            animator.SprayPoint = start + direction * 0.4f; animator.SprayAim = 1;   // the pot tipped at the floor
            if (strip) strip.Show(start, direction, Length, Width);
            Speak("そこか！  湯をかけてやる");
        }

        /// <summary>On the strip (on the floor): where the water runs.</summary>
        public bool OnStrip(Vector3 position, float reach = Length)
        {
            if (position.y > 0.6f) return false;
            Vector3 offset = Flat(position - start);
            float along = Vector3.Dot(offset, direction), across = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, direction)));
            return along >= -0.2f && along <= reach && across <= Width * 0.5f;
        }

        private void ShowWater()
        {
            if (!water) return;
            float reach = Mathf.Max(0.05f, Reach);
            water.gameObject.SetActive(true);
            water.SetPositionAndRotation(new Vector3(start.x, 0.05f, start.z) + direction * reach * 0.5f, Quaternion.LookRotation(direction));
            float ripple = 1 + 0.04f * Mathf.Sin(Time.time * 23);
            water.localScale = new Vector3(Width * 0.92f * ripple, 0.03f, reach);
        }

        private void Scald()
        {
            float reach = Reach;
            if (PlayerOnFloor && OnStrip(session.Motor.FootPoint, reach) && session.TryDamage(1)) { PlayerHits++; Speak("熱っちっち…  ゴキブリには効くだろう"); }
            foreach (var ally in commander.Allies)
            {
                if (!ally.Active || scalded.Contains(ally) || !OnStrip(ally.Position, reach)) continue;
                scalded.Add(ally);   // once per pour
                commander.StrikeAlly(ally);
                AllyHits++;
            }
        }

        private void End()
        {
            Current = Phase.Recover; clock = 0;
            animator.SprayAim = 0;
            if (strip) strip.Hide();
            if (water) water.gameObject.SetActive(false);
        }

        public override void Stop()
        {
            if (Current != Phase.Idle) { End(); Release(); }
            Current = Phase.Idle; clock = 0; cooldownUntil = 0;
            if (animator) animator.SprayAim = 0;
        }
        public void ResetCounts() { PlayerHits = AllyHits = Uses = 0; }

        // Allies on the strip run off it sideways (while it is announced and while the water lies there).
        public override bool Threatens(Vector3 position, out Vector3 escape)
        {
            escape = position;
            if (Current != Phase.Aim && !Hot || !OnStrip(position)) return false;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            Vector3 offset = Flat(position - start);
            float s = Vector3.Dot(offset, side) >= 0 ? 1 : -1, along = Mathf.Clamp(Vector3.Dot(offset, direction), 0, Length);
            for (int k = 0; k < 2; k++, s = -s)
            {
                Vector3 p = start + direction * along + side * s * (Width * 0.5f + 0.9f);
                if (commander.Grid.Walkable(p)) { escape = p; return true; }
            }
            commander.Grid.NearestWalkable(start + direction * along + side * s * 1.6f, 2f, out escape);
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Verification: start the pour at a floor point now.</summary>
        public void DebugAim(Vector3 target) { cooldownUntil = 0; Begin(Flat(target)); }
#endif
    }
}
