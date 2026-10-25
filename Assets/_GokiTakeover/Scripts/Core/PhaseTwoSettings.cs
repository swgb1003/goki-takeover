using UnityEngine;

namespace GokiTakeover
{
    [CreateAssetMenu(menuName = "GOKI TAKEOVER/Phase 2 Settings")]
    public sealed class PhaseTwoSettings : ScriptableObject
    {
        [Min(1)] public int maximumHp = 3;
        [Range(1, 3)] public int deliveryGoal = 3;
        [Min(0.1f)] public float interactionRange = 0.85f;
        [Range(0.2f, 1)] public float carryingSpeed = 0.88f;
        [Min(0.1f)] public float invulnerabilitySeconds = 1.6f;
        [Min(1)] public float sightDistance = 7;
        [Range(30, 180)] public float sightAngle = 110;
        [Min(0.1f)] public float detectionSeconds = 0.75f;
        [Min(0.1f)] public float searchSeconds = 5;
        [Min(0.1f)] public float patrolSpeed = 1.15f;
        [Min(0.1f)] public float chaseSpeed = 2.2f;
        [Min(0.1f)] public float attackRange = 1.7f;
        [Min(0.2f)] public float attackWindup = 0.9f;
        [Min(0.1f)] public float attackRecover = 0.6f;
        [Min(0.2f)] public float attackCooldown = 1.8f;
        // Root-to-target distance the resident steps to before slamming; matches a deep squat + arm reach.
        [Min(0.5f)] public float strikeDistance = 1.45f;
        // Crush footprint: the slipper sole (half width/length) plus how much of the roach body may stick out.
        [Min(0.05f)] public float slipperHalfWidth = 0.145f;
        [Min(0.05f)] public float slipperHalfLength = 0.39f;
        [Min(0f)] public float crushMargin = 0.28f;
        [Range(0.1f, 1)] public float stickySpeed = 0.32f;
    }
}
