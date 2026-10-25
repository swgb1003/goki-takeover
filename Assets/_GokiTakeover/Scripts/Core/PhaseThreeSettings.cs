using UnityEngine;

namespace GokiTakeover
{
    [CreateAssetMenu(menuName = "GOKI TAKEOVER/Phase 3 Settings")]
    public sealed class PhaseThreeSettings : ScriptableObject
    {
        // Active allies. The scene holds a pool of 9; only this many are enabled.
        [Range(4, 9)] public int allyCount = 6;
        [Range(1, 8)] public int deliveryGoal = 6;
        [Min(0.1f)] public float allyWalkSpeed = 1.45f;
        [Min(0.1f)] public float allyRunSpeed = 2.7f;
        // Path refresh interval per ally; each ally is offset so they never all search in one frame.
        [Range(0.2f, 1f)] public float allyRepathSeconds = 0.35f;
        [Min(0.1f)] public float allySeparation = 0.8f;
        [Min(1f)] public float allyRespawnSeconds = 8f;
        // A food or scout point counts as found when the leader or an ally comes this close.
        [Min(0.5f)] public float discoveryRadius = 2.6f;
        [Min(1f)] public float swarmPanSpeed = 6f;
        [Range(6f, 20f)] public float swarmDistance = 13f;
    }
}
