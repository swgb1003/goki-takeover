using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Common surface for hazards added from Phase 4 (spec 7): what it is called, whether it is live,
    /// where it hurts, and what the HUD warns. Older hazards (slipper, sticky sheet) keep their own code.
    /// </summary>
    public abstract class HazardBase : MonoBehaviour
    {
        public abstract string DisplayName { get; }
        public abstract string Warning { get; }
        public abstract string CodexId { get; }
        public virtual bool IsArmed => isActiveAndEnabled;
        public abstract bool Contains(Vector3 position);
    }
}
