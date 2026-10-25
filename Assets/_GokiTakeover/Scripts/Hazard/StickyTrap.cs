using UnityEngine;

namespace GokiTakeover
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StickyTrap : MonoBehaviour
    {
        private BoxCollider area;
        private void Awake() { area = GetComponent<BoxCollider>(); }
        public bool Contains(Vector3 position)
        {
            if (!isActiveAndEnabled) return false;   // pooled traps (Phase 4) wait inactive
            if (!area) area = GetComponent<BoxCollider>();
            Vector3 local = transform.InverseTransformPoint(position) - area.center;
            Vector3 half = area.size * 0.5f;
            return Mathf.Abs(local.x) < half.x && Mathf.Abs(local.z) < half.z && Mathf.Abs(local.y) < half.y;
        }
    }
}
