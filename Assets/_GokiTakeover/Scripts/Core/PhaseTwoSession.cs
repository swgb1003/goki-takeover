using System;
using System.Collections.Generic;
using UnityEngine;

namespace GokiTakeover
{
    [DefaultExecutionOrder(-150)]
    public sealed class PhaseTwoSession : MonoBehaviour
    {
        public enum RunState { Playing, Paused, Dead, Cleared }
        [SerializeField] private PhaseTwoSettings settings;
        [SerializeField] private RoachMotor motor;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private ThirdPersonRoachCamera followCamera;
        [SerializeField] private HumanAIController human;
        [SerializeField] private Transform nest;
        [SerializeField] private StickyTrap[] traps;
        // Phase 5: the nest is an enclosed hollow (inside the sink cabinet). Zero size = the old 1.2 m circle.
        [SerializeField] private Bounds nestArea;
        // Stage 2 (2026-10-25): the rest of the household besides the resident. Empty in every earlier scene.
        [SerializeField] private HumanAIController[] family = new HumanAIController[0];
        private CockroachVisualController visual;
        private Vector3 spawn;
        private Quaternion spawnRotation;
        private float invulnerableUntil, oldTimeScale;
        public PhaseTwoSettings Settings => settings;
        public Transform Player => motor.transform;
        public Transform Nest => nest;
        public PlayerInteractor Interactor => interactor;
        public HumanAIController Human => human;
        public IReadOnlyList<HumanAIController> Family => family;
        public void ConfigureFamily(HumanAIController[] members) { family = members ?? new HumanAIController[0]; }
        /// <summary>Everyone in the house: the resident first, then the family (Stage 2).</summary>
        public int HumanCount => 1 + family.Length;
        public HumanAIController HumanAt(int index) => index == 0 ? human : family[index - 1];
        /// <summary>A human that is in the house right now (a family member who left is inactive).</summary>
        public bool HumanPresent(int index) { var h = HumanAt(index); return h && h.gameObject.activeInHierarchy; }
        /// <summary>A human in the house whose own collider is (or contains) this transform.</summary>
        public bool IsHumanBody(Transform part)
        {
            for (int i = 0; i < HumanCount; i++) if (HumanAt(i) && part.IsChildOf(HumanAt(i).transform)) return true;
            return false;
        }
        // Stage 2: the son's bug net holds the player in place until it wriggles free.
        public bool Restrained { get; set; }
        public RunState State { get; private set; }
        public int Hp { get; private set; }
        public int Delivered { get; private set; }
        public int Discoveries { get; set; }
        public float RunSeconds { get; private set; }
        public bool IsSticky { get; private set; }
        public bool IsRunning => State == RunState.Playing;
        public bool CanControl => IsRunning && Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
        public bool InSafeNest => !NestSmoked && InNest(Player.position);
        // Final defence (Phase 5): the exterminator smokes the nest; for that time it is neither safe nor usable,
        // and a successor takes over at ReviveAt instead of inside it.
        public bool NestSmoked { get; set; }
        public Vector3? ReviveAt { get; set; }
        public bool HasNestArea => nestArea.size.sqrMagnitude > 0;
        public Bounds NestArea => nestArea;
        public bool InNest(Vector3 position) => HasNestArea ? nestArea.Contains(position) : Vector3.Distance(position, nest.position) < 1.2f;
        public void ConfigureNestArea(Bounds area) { nestArea = area; }
        // Later phases: a larger goal, a free cursor for the tactics view, and a notification after a reset.
        public int DeliveryGoalOverride { get; set; }
        public int DeliveryGoal => DeliveryGoalOverride > 0 ? DeliveryGoalOverride : settings.deliveryGoal;
        public bool ExternalCursor { get; set; }
        public event Action RunReset;
        // The slipper reaches only a roach on the floor; walls, ceiling and the worktop are out of reach.
        // Phase 5 sets a reach height: then the worktop, the table and walls up to that height can be hit too
        // (never the ceiling or the underside of furniture).
        public float StrikeReach { get; set; }
        public bool PlayerStrikeable => StrikeReach > 0
            ? !(motor.IsClimbing && motor.SurfaceUp.y < -0.5f) && motor.FootPoint.y <= StrikeReach
            : !motor.IsClimbing && Player.position.y < 0.5f;
        public Vector3 PlayerStrikePoint => StrikeReach > 0 ? motor.FootPoint : Player.position;
        public Vector3 PlayerSurfaceUp => motor.IsClimbing ? motor.SurfaceUp : Vector3.up;
        public RoachMotor Motor => motor;
        // Colony upgrades (Phase 4). Both reset with the run; zero keeps the settings asset values.
        public float CarrySpeedOverride { get; set; }
        public float CarryingSpeed => CarrySpeedOverride > 0 ? CarrySpeedOverride : settings.carryingSpeed;
        public int MaxHpBonus { get; private set; }
        public int MaximumHp => settings.maximumHp + MaxHpBonus;
        public void AddMaxHp(int amount) { MaxHpBonus += amount; Hp = Mathf.Min(Hp + amount, MaximumHp); }
        /// <summary>Restores health up to the maximum (Phase 5: eating a fallen ally). False when already full.</summary>
        public bool Heal(int amount)
        {
            if (!IsRunning || amount <= 0 || Hp >= MaximumHp) return false;
            Hp = Mathf.Min(Hp + amount, MaximumHp);
            return true;
        }
        public void Complete() { if (IsRunning || State == RunState.Paused) SetState(RunState.Cleared); }
        public ThirdPersonRoachCamera FollowCamera => followCamera;
        public void Configure(PhaseTwoSettings config, RoachMotor player, PlayerInteractor inventory,
            ThirdPersonRoachCamera camera, HumanAIController enemy, Transform home, StickyTrap[] hazards)
        { settings = config; motor = player; interactor = inventory; followCamera = camera; human = enemy; nest = home; traps = hazards; }
        private void Awake()
        {
            oldTimeScale = Time.timeScale;
            spawn = Player.position;
            spawnRotation = Player.rotation;
            visual = motor.GetComponentInChildren<CockroachVisualController>();
            Hp = MaximumHp;
            followCamera.ManageCursor = false;
            motor.AddPusher(human.GetComponent<CapsuleCollider>());
            human.SlipperLanding += OnSlipperLanding;
            foreach (var member in family)
            {
                motor.AddPusher(member.GetComponent<CapsuleCollider>());
                member.SlipperLanding += OnSlipperLanding;
            }
        }
        // Impact feedback scales with distance; only a crush (already applied as damage) hits hard.
        private void OnSlipperLanding(Vector3 point, bool crushed)
        {
            float near = 1 - Mathf.Clamp01(Vector3.Distance(Player.position, point) / 4f);
            followCamera.AddShake(crushed ? 0.32f : 0.14f * near);
        }
        private void Start() { ResetRun(); }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (State == RunState.Playing) Pause();
                else if (State == RunState.Paused) Resume();
            }
            if ((State == RunState.Dead || State == RunState.Cleared) && Input.GetKeyDown(KeyCode.R)) ResetRun();
            if (!IsRunning) return;
            RunSeconds += Time.deltaTime;
            IsSticky = false;
            foreach (var trap in traps) IsSticky |= trap.Contains(Player.position);
            motor.SpeedMultiplier = (interactor.Carried ? CarryingSpeed : 1) * (IsSticky ? settings.stickySpeed : 1);
            motor.MovementEnabled = CanControl && !Restrained;
            if (!ExternalCursor && Input.GetMouseButtonDown(0) && Application.isFocused) SetCursor(true);
        }
        public bool TryDamage(int amount)
        {
            if (!IsRunning || amount <= 0 || Time.time < invulnerableUntil || InSafeNest) return false;
            Hp = Mathf.Max(0, Hp - amount);
            invulnerableUntil = Time.time + settings.invulnerabilitySeconds;
            visual.PlayHit();
            if (Hp == 0)
            {
                visual.IsDead = true;
                SetState(RunState.Dead);
            }
            return true;
        }
        public void RecordDelivery()
        {
            if (!IsRunning) return;
            Delivered++;
            if (Delivered >= DeliveryGoal) SetState(RunState.Cleared);
        }
        /// <summary>Phase 5 successor: a new roach takes over at the nest with full health; the run goes on.</summary>
        public void Revive()
        {
            if (State != RunState.Dead) return;
            interactor.ResetInventory();
            var controller = motor.GetComponent<CharacterController>();
            controller.enabled = false;
            Player.SetPositionAndRotation(NestSmoked && ReviveAt.HasValue ? ReviveAt.Value : spawn, spawnRotation);
            controller.enabled = true;
            Physics.SyncTransforms();
            motor.ResetSurface();
            motor.ResetSafePosition();
            Hp = MaximumHp;
            Restrained = false;
            invulnerableUntil = Time.time + settings.invulnerabilitySeconds * 2;
            IsSticky = false;
            visual.ResetReaction();
            visual.SetMotion(0, false);
            SetState(RunState.Playing);
            followCamera.ResetView();
        }
        /// <summary>Stage 3: the player steps through to the other floor (stairs, duct). It lands on the floor there, facing yaw.</summary>
        public void TeleportPlayer(Vector3 position, float yaw)
        {
            var controller = motor.GetComponent<CharacterController>();
            controller.enabled = false;
            Player.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            controller.enabled = true;
            Physics.SyncTransforms();
            motor.ResetSurface();
            motor.ResetSafePosition();
            followCamera.ResetView();
        }
        public void RestoreRunSeconds(float seconds) { RunSeconds = Mathf.Max(0, seconds); }
        public void Pause() { if (IsRunning) SetState(RunState.Paused); }
        public void Resume() { if (State == RunState.Paused) SetState(RunState.Playing); }
        public void ResetRun()
        {
            interactor.ResetInventory();
            var controller = motor.GetComponent<CharacterController>();
            controller.enabled = false;
            Player.SetPositionAndRotation(spawn, spawnRotation);
            controller.enabled = true;
            Physics.SyncTransforms();
            motor.ResetSurface();
            motor.ResetSafePosition();
            MaxHpBonus = 0;
            CarrySpeedOverride = 0;
            Hp = MaximumHp;
            Delivered = Discoveries = 0;
            RunSeconds = 0;
            invulnerableUntil = Time.time + settings.invulnerabilitySeconds;
            IsSticky = false;
            motor.SpeedMultiplier = 1;
            visual.ResetReaction();
            visual.SetMotion(0, false);
            human.ResetHuman();
            Restrained = false;
            foreach (var member in family) { member.gameObject.SetActive(true); member.ResetHuman(); }
            SetState(RunState.Playing);
            followCamera.ResetView();
            RunReset?.Invoke();
        }
        private void SetState(RunState next)
        {
            State = next;
            Time.timeScale = next == RunState.Playing ? 1 : 0;
            motor.MovementEnabled = next == RunState.Playing;
            SetCursor(next == RunState.Playing && !ExternalCursor);
        }
        private static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
        private void OnApplicationFocus(bool focused) { if (!focused) Pause(); }
        private void OnDestroy() { Time.timeScale = oldTimeScale; SetCursor(false); }
    }
}
