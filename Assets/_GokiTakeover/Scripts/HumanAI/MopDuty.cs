using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Stage 3: the part-timer's floor mopping. While nothing is going on (patrolling, not chasing) he mops every
    /// Interval seconds for MopSeconds as he walks his round, leaving wet patches behind him (WetFloor). The mop head
    /// is pushed along the floor ahead of his feet.
    /// </summary>
    public sealed class MopDuty : MonoBehaviour
    {
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private HumanAIController human;
        [SerializeField] private BlenderHumanAnimator animator;
        [SerializeField] private WetFloor floor;
        [SerializeField] private DeckBrushAttack brush;
        public const float Interval = 26f, MopSeconds = 12f, Spacing = 0.9f, FirstAfter = 12f;
        private float nextAt, until;
        private Vector3 lastDrop;
        public bool Mopping { get; private set; }
        public bool Allowed { get; set; } = true;
        public int Drops { get; private set; }

        public void Configure(PhaseTwoSession game, HumanAIController member, BlenderHumanAnimator body, WetFloor wet, DeckBrushAttack deckBrush)
        { session = game; human = member; animator = body; floor = wet; brush = deckBrush; }

        private void Awake() { session.RunReset += ResetDuty; ResetDuty(); }
        private void OnDestroy() { if (session) session.RunReset -= ResetDuty; }
        private void ResetDuty() { nextAt = Time.time + FirstAfter; Mopping = false; Drops = 0; }

        private void Update()
        {
            if (!session.IsRunning) return;
            bool calm = Allowed && human.isActiveAndEnabled && !human.Suspended && human.State == HumanAIController.HumanState.Routine && !human.OnStairs && (!brush || !brush.Busy);
            if (!Mopping)
            {
                if (calm && Time.time >= nextAt) Begin();
                return;
            }
            if (!calm || Time.time >= until) { End(); return; }
            Vector3 feet = human.transform.position; feet.y = 0;
            animator.SprayPoint = feet + human.transform.forward * 0.9f; animator.SprayAim = 1;   // the mop on the floor ahead
            Vector3 behind = feet - human.transform.forward * 0.4f;
            if ((behind - lastDrop).sqrMagnitude >= Spacing * Spacing) { floor.Drop(behind); lastDrop = behind; Drops++; }
        }

        /// <summary>Mop now (verification, or the routine's turn).</summary>
        public void Begin()
        {
            Mopping = true; until = Time.time + MopSeconds;
            lastDrop = new Vector3(9999, 0, 9999);
            var reaction = FindFirstObjectByType<ColonyManager>();
            if (reaction && reaction.Reaction) reaction.Reaction.SayAs(human, "床そうじ、床そうじ…");
        }
        private void End()
        {
            Mopping = false;
            nextAt = Time.time + Interval;
            if (!brush || !brush.Busy) animator.SprayAim = 0;
        }
    }
}
