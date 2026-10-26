using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Stage 3: an orange rectangle on the floor (translucent fill and a solid outline) for the attacks that strike a
    /// strip or a box: the landlady's tray, the cook's hot water, the part-timer's deck brush. The root's scale is the
    /// rectangle (x across, z along); the outline keeps the same thickness whatever the scale.
    /// </summary>
    public sealed class RectTelegraph : MonoBehaviour
    {
        [SerializeField] private Transform fill;
        [SerializeField] private Transform[] edges = new Transform[4];   // -z, +z, -x, +x
        public const float Edge = 0.07f;
        public void Configure(Transform fillPart, Transform[] edgeParts) { fill = fillPart; edges = edgeParts; }

        /// <summary>A rectangle from start forward for length, width across, just above floor coverings (y ≥ 0.04).</summary>
        public void Show(Vector3 start, Vector3 forward, float length, float width)
        {
            forward.y = 0;
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
            transform.SetPositionAndRotation(new Vector3(start.x, 0.045f, start.z) + forward.normalized * length * 0.5f, Quaternion.LookRotation(forward));
            transform.localScale = new Vector3(width, 1, length);
            gameObject.SetActive(true);
            LateUpdate();
        }
        public void Hide() { gameObject.SetActive(false); }

        private void LateUpdate()
        {
            Vector3 s = transform.localScale;
            if (s.x < 1e-4f || s.z < 1e-4f) return;
            float ex = Edge / s.x, ez = Edge / s.z;
            if (fill) fill.localScale = new Vector3(1, 0.01f, 1);
            if (edges == null || edges.Length < 4) return;
            // Each edge: full length along its side, a fixed width in world units.
            edges[0].localPosition = new Vector3(0, 0.002f, -0.5f + ez / 2); edges[0].localScale = new Vector3(1, 0.012f, ez);
            edges[1].localPosition = new Vector3(0, 0.002f, 0.5f - ez / 2); edges[1].localScale = new Vector3(1, 0.012f, ez);
            edges[2].localPosition = new Vector3(-0.5f + ex / 2, 0.002f, 0); edges[2].localScale = new Vector3(ex, 0.012f, 1);
            edges[3].localPosition = new Vector3(0.5f - ex / 2, 0.002f, 0); edges[3].localScale = new Vector3(ex, 0.012f, 1);
        }
    }
}
