using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Stage 3: the landlady's alcohol spray (the restaurant's disinfectant). At mid range she aims the bottle: an
    /// orange fan on the floor (AimSeconds), then a cloud of mist down the fan (SpraySeconds). A roach in the mist is
    /// soaked: the player walks at half speed for SlowSeconds, an ally too. It does not hurt; the slowness is what
    /// gets roaches caught by her tray. Close up she swats with the tray instead (HumanAIController's strike).
    /// </summary>
    public sealed class AlcoholSprayAttack : HouseholdAttack
    {
        public enum Phase { Idle, Aim, Spray, Recover }
        [SerializeField] private Transform fan, mist;
        public const float MinRange = 2.2f, MaxRange = 4.6f, Length = 4.4f, HalfAngle = VacuumAttack.HalfAngle;   // the same fan as the vacuum's
        public const float AimSeconds = 0.8f, SpraySeconds = 1.4f, RecoverSeconds = 0.6f, Cooldown = 8f, SlowSeconds = 4f;
        private Vector3 nozzle, direction;
        public Phase Current { get; private set; }
        public int PlayerSoaked { get; private set; }
        public int AlliesSoaked { get; private set; }
        public override bool Busy => Current != Phase.Idle;
        public bool Spraying => Current == Phase.Spray;
        public bool Aiming => Current == Phase.Aim;

        public void ConfigureVisuals(Transform warningFan, Transform mistCone) { fan = warningFan; mist = mistCone; }

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
                    if (clock >= AimSeconds) { Current = Phase.Spray; clock = 0; if (mist) mist.gameObject.SetActive(true); Speak("シュッ！  消毒、消毒！"); }
                    break;
                case Phase.Spray:
                    Soak();
                    if (mist)
                    {
                        float puff = Mathf.Clamp01(clock / 0.3f) * (1 + 0.08f * Mathf.Sin(Time.time * 31));
                        float width = 2 * Length * Mathf.Tan(HalfAngle * Mathf.Deg2Rad);
                        mist.SetPositionAndRotation(nozzle + Vector3.up * 0.35f, Quaternion.LookRotation(direction));
                        mist.localScale = new Vector3(width * puff, 0.7f * puff, Length * puff);
                    }
                    if (clock >= SpraySeconds) End();
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
            nozzle = from + direction * 0.7f;
            Current = Phase.Aim; clock = 0; Uses++;
            animator.SprayPoint = nozzle + direction * 1.5f + Vector3.up * 0.6f; animator.SprayAim = 1;
            if (fan) { fan.gameObject.SetActive(true); fan.SetPositionAndRotation(nozzle + Vector3.up * 0.045f, Quaternion.LookRotation(direction)); fan.localScale = new Vector3(Length, 1, Length); }
            Speak("ゴキブリ！？  アルコールで消毒よ！");
        }

        /// <summary>Inside the spray fan on the floor.</summary>
        public bool InFan(Vector3 position)
        {
            if (position.y > 0.8f) return false;
            Vector3 offset = Flat(position - nozzle);
            float d = offset.magnitude;
            return d <= Length && (d < 0.6f || Vector3.Angle(direction, offset) <= HalfAngle);
        }

        private void Soak()
        {
            if (PlayerOnFloor && InFan(session.Motor.FootPoint))
            {
                if (!session.Slowed) { PlayerSoaked++; commander.Say("アルコールを浴びた！  " + SlowSeconds.ToString("F0") + "秒間 動きが鈍る"); }
                session.SlowDown(SlowSeconds);
            }
            foreach (var ally in commander.Allies)
            {
                if (!ally.Active || !InFan(ally.Position)) continue;
                if (Time.time >= ally.SlowUntil) AlliesSoaked++;
                ally.SlowUntil = Time.time + SlowSeconds;
            }
        }

        private void End()
        {
            Current = Phase.Recover; clock = 0;
            animator.SprayAim = 0;
            if (fan) fan.gameObject.SetActive(false);
            if (mist) mist.gameObject.SetActive(false);
        }

        public override void Stop()
        {
            if (Current != Phase.Idle) { End(); Release(); }
            Current = Phase.Idle; clock = 0; cooldownUntil = 0;
            if (animator) animator.SprayAim = 0;
        }
        public void ResetCounts() { PlayerSoaked = AlliesSoaked = Uses = 0; }

        // Allies run out of the fan sideways.
        public override bool Threatens(Vector3 position, out Vector3 escape)
        {
            escape = position;
            if (Current != Phase.Aim && Current != Phase.Spray || !InFan(position)) return false;
            Vector3 offset = Flat(position - nozzle);
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            float s = Vector3.Dot(offset, side) >= 0 ? 1 : -1;
            float along = Mathf.Max(0.5f, Vector3.Dot(offset, direction));
            for (int k = 0; k < 2; k++, s = -s)
            {
                Vector3 p = nozzle + direction * along + side * s * (along * Mathf.Tan(HalfAngle * Mathf.Deg2Rad) + 1.0f);
                if (commander.Grid.Walkable(p)) { escape = p; return true; }
            }
            commander.Grid.NearestWalkable(nozzle + direction * along + side * s * 2.0f, 2f, out escape);
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugAim(Vector3 target) { cooldownUntil = 0; Begin(Flat(target)); }
#endif
    }
}
