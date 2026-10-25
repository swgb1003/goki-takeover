using UnityEngine;

namespace GokiTakeover
{
    /// <summary>Title background life: a few roaches wander slow loops on the kitchen floor; the hero idles.</summary>
    public sealed class TitleBackdrop : MonoBehaviour
    {
        [SerializeField] private Transform[] walkers;
        [SerializeField] private Vector3[] centres;
        [SerializeField] private float[] radii, speeds;
        private CockroachVisualController[] legs;
        private float[] angles;

        public void Configure(Transform[] roaches, Vector3[] loopCentres, float[] loopRadii, float[] loopSpeeds)
        { walkers = roaches; centres = loopCentres; radii = loopRadii; speeds = loopSpeeds; }

        private void Awake()
        {
            legs = new CockroachVisualController[walkers.Length];
            angles = new float[walkers.Length];
            for (int i = 0; i < walkers.Length; i++) { legs[i] = walkers[i].GetComponentInChildren<CockroachVisualController>(); angles[i] = i * 2.1f; }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < walkers.Length; i++)
            {
                // Speed along the loop is constant; the sign of speed sets the direction of travel.
                angles[i] += speeds[i] * dt / radii[i];
                Vector3 offset = new Vector3(Mathf.Sin(angles[i]), 0, Mathf.Cos(angles[i])) * radii[i];
                Vector3 tangent = new Vector3(Mathf.Cos(angles[i]), 0, -Mathf.Sin(angles[i])) * Mathf.Sign(speeds[i]);
                walkers[i].SetPositionAndRotation(centres[i] + offset, Quaternion.LookRotation(tangent));
                if (legs[i]) legs[i].SetMotion(Mathf.Clamp01(Mathf.Abs(speeds[i]) / 0.9f), false);
            }
        }
    }
}
