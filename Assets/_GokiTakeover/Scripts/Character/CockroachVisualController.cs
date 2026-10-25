using System;
using UnityEngine;

namespace GokiTakeover
{
    /// <summary>Two bone legs use a fixed ground plane; the two tripod groups alternate stance/swing.</summary>
    public sealed class CockroachVisualController : MonoBehaviour
    {
        [Serializable]
        public sealed class Leg
        {
            public Transform hip;
            public Transform knee;
            public Transform upper;
            public Transform lower;
            public Transform toe;
            public Vector3 restFoot;
            public int side;
            public int tripod;
        }

        [SerializeField] private Transform body;
        [SerializeField] private Transform[] antennae = Array.Empty<Transform>();
        [SerializeField] private Leg[] legs = Array.Empty<Leg>();
        [SerializeField, Min(0.1f)] private float walkCyclesPerSecond = 2.3f;
        [SerializeField, Min(0.1f)] private float runCyclesPerSecond = 4.8f;
        [SerializeField, Range(0.02f, 0.3f)] private float stride = 0.22f;
        [SerializeField, Range(0.01f, 0.2f)] private float footLift = 0.10f;
        [SerializeField, Min(0.01f)] private float upperLegWidth = 0.045f;
        [SerializeField, Min(0.01f)] private float lowerLegWidth = 0.030f;
        private Quaternion[] antennaRest;
        private Vector3 bodyRest;
        private float phase;
        private float motion;
        private float runBlend;
        private bool ready;
        private float pickupUntil, hitUntil;
        private Vector3 bodyScaleRest = Vector3.one;
        public bool IsDead { get; set; }
        public void PlayPickup() { pickupUntil = Time.time + 0.35f; }
        public void PlayHit() { hitUntil = Time.time + 0.55f; }
        public void ResetReaction() { IsDead = false; pickupUntil = hitUntil = 0; }
        public Leg[] Legs => legs;
        public Transform[] Antennae => antennae;

        public void ConfigureLegWidth(float upperWidth, float lowerWidth)
        {
            upperLegWidth = Mathf.Max(0.01f, upperWidth);
            lowerLegWidth = Mathf.Max(0.01f, lowerWidth);
        }

        public void Configure(Transform bodyTransform, Transform[] antennaTransforms, Leg[] legData)
        {
            body = bodyTransform;
            antennae = antennaTransforms;
            legs = legData;
        }

        private void Awake()
        {
            if (!body || legs == null || legs.Length != 6 || antennae == null || antennae.Length != 2)
            {
                Debug.LogWarning("[GOKI] モデルの部位参照が不足しています。Generate Cockroachで再生成してください。", this);
                enabled = false;
                return;
            }
            foreach (var leg in legs)
                if (leg == null || !leg.hip || !leg.knee || !leg.upper || !leg.lower || !leg.toe)
                {
                    Debug.LogWarning("[GOKI] 脚の参照が不足しています。アニメーションを停止します。", this);
                    enabled = false;
                    return;
                }
            bodyRest = body.localPosition;
            bodyScaleRest = body.localScale;
            antennaRest = new Quaternion[antennae.Length];
            for (int i = 0; i < antennae.Length; i++)
                antennaRest[i] = antennae[i] ? antennae[i].localRotation : Quaternion.identity;
            ready = true;
        }

        public void SetMotion(float speedFraction, bool running)
        {
            motion = Mathf.Clamp01(speedFraction);
            runBlend = running ? 1f : 0f;
        }

        private void LateUpdate()
        {
            if (ready) Animate(Time.deltaTime, Time.time);
        }

        public void Animate(float deltaTime, float time)
        {
            if (!ready) return;
            phase = Mathf.Repeat(phase + deltaTime * Mathf.Lerp(walkCyclesPerSecond, runCyclesPerSecond, runBlend)
                * motion, 1f);
            // Crushed by the slipper: the body flattens and spreads, then springs back (or stays flat when dead).
            float squash = IsDead ? 0.85f : Mathf.Sin(Mathf.Clamp01((hitUntil - time) / 0.55f) * Mathf.PI * 0.5f) * 0.75f;
            body.localPosition = bodyRest + Vector3.up * (Mathf.Sin(time * 2f) * 0.008f * (1 - squash) - 0.1f * squash);
            body.localScale = Vector3.Scale(bodyScaleRest, new Vector3(1 + 0.3f * squash, 1 - 0.6f * squash, 1 + 0.12f * squash));
            float bow = Mathf.Sin(Mathf.Clamp01((pickupUntil - time) / 0.35f) * Mathf.PI) * 18f;
            body.localRotation = Quaternion.Euler(4f * runBlend * motion * (1 - squash) + bow, 0, 0);
            for (int i = 0; i < antennae.Length; i++)
                if (antennae[i]) antennae[i].localRotation = antennaRest[i]
                    * Quaternion.Euler(Mathf.Sin(time * 1.6f + i) * 4f, Mathf.Sin(time * 1.1f + i * 2f) * 9f, 0);
            foreach (var leg in legs)
            {
                float p = Mathf.Repeat(phase + leg.tripod * 0.5f, 1f);
                bool swing = p >= 0.5f;
                float t = swing ? (p - 0.5f) * 2f : p * 2f;
                Vector3 foot = leg.restFoot;
                foot.z += (swing ? Mathf.Lerp(-stride, stride, t) : Mathf.Lerp(stride, -stride, t)) * motion;
                foot.y += (swing ? Mathf.Sin(t * Mathf.PI) * footLift : 0f) * motion;
                Vector3 hip = leg.hip.localPosition;
                Vector3 knee = Vector3.Lerp(hip, foot, 0.5f) + new Vector3(leg.side * (0.12f + 0.08f * squash), 0.18f * (1 - 0.7f * squash), 0);
                // All three joints share the visual-root coordinate system through the hip/knee hierarchy.
                leg.knee.position = transform.TransformPoint(knee);
                PlaceBone(leg.upper, transform.TransformPoint(hip), transform.TransformPoint(knee), upperLegWidth);
                PlaceBone(leg.lower, transform.TransformPoint(knee), transform.TransformPoint(foot), lowerLegWidth);
                leg.toe.position = transform.TransformPoint(foot);
            }
        }

        private void PlaceBone(Transform bone, Vector3 start, Vector3 end, float width)
        {
            bone.position = (start + end) * 0.5f;
            bone.rotation = Quaternion.FromToRotation(Vector3.up, end - start);
            bone.localScale = new Vector3(width, Vector3.Distance(start, end) / transform.lossyScale.x * 0.5f, width);
        }
    }
}
