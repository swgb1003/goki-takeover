using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Fogger (spec 7, Phase 5): a can set off on the floor. First an orange ring shows the area for a few
    /// seconds, then low smoke fills it for a while. The smoke stays low: the worktop, high walls and the
    /// nest are safe. Allies run out of the ring as soon as it appears.
    /// </summary>
    public sealed class SmokeFogger : HazardBase
    {
        public enum Phase { Idle, Warning, Smoking }
        [SerializeField] private Transform ring, fill, smoke, can;
        private float radius = 3f, height = 2.4f, warning = 3.5f, duration = 9f, clock;
        public Phase Current { get; private set; }
        public float Radius => radius;
        public float Progress => Current == Phase.Warning ? Mathf.Clamp01(clock / warning) : Current == Phase.Smoking ? Mathf.Clamp01(clock / duration) : 0;
        public float Remaining => Current == Phase.Warning ? warning - clock : Current == Phase.Smoking ? duration - clock : 0;
        public override string DisplayName => "燻煙剤";
        public override string Warning => "燻煙剤！  橙の輪の外か、高い所・巣へ逃げろ";
        public override string CodexId => "fogger";
        public override bool IsArmed => Current == Phase.Smoking;
        public bool Active => Current != Phase.Idle;

        public void Configure(Transform warningRing, Transform warningFill, Transform smokeDome, Transform canister)
        { ring = warningRing; fill = warningFill; smoke = smokeDome; can = canister; }

        public void Arm(Vector3 at, float size, float warnSeconds, float smokeSeconds, float smokeHeight)
        {
            at.y = 0;
            transform.position = at;
            radius = size; warning = warnSeconds; duration = smokeSeconds; height = smokeHeight;
            clock = 0;
            Current = Phase.Warning;
            gameObject.SetActive(true);
            ring.gameObject.SetActive(true); fill.gameObject.SetActive(true); can.gameObject.SetActive(true);
            smoke.gameObject.SetActive(false);
            ring.localScale = new Vector3(radius * 2, 1, radius * 2);   // unit meshes are 1 m across
            Animate();
        }

        public void Stop()
        {
            Current = Phase.Idle;
            clock = 0;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Current == Phase.Idle) return;
            clock += Time.deltaTime;
            if (Current == Phase.Warning && clock >= warning) { Current = Phase.Smoking; clock = 0; fill.gameObject.SetActive(false); smoke.gameObject.SetActive(true); }
            else if (Current == Phase.Smoking && clock >= duration) { Stop(); return; }
            Animate();
        }

        private void Animate()
        {
            if (Current == Phase.Warning)
            {
                float k = Mathf.Max(0.05f, Progress);
                fill.localScale = new Vector3(radius * 2 * k, 1, radius * 2 * k);
            }
            else if (Current == Phase.Smoking)
            {
                // Billows out in the first second, breathes while it lasts, sinks at the end.
                float grow = Mathf.Clamp01(clock / 0.9f), fade = Mathf.Clamp01((duration - clock) / 0.8f);
                float breathe = 1 + Mathf.Sin(clock * 2.4f) * 0.04f;
                smoke.localScale = new Vector3(radius * 2 * grow * breathe, height * 1.6f * grow * fade, radius * 2 * grow * breathe);
            }
        }

        public override bool Contains(Vector3 position)
        {
            if (Current != Phase.Smoking) return false;
            Vector3 d = position - transform.position; d.y = 0;
            return d.magnitude <= radius && position.y < height;
        }

        /// <summary>On the floor near or inside the ring while it is live; escape is a point just outside it.</summary>
        public bool Threatens(Vector3 position, out Vector3 escape)
        {
            escape = position;
            if (Current == Phase.Idle) return false;
            Vector3 d = position - transform.position; d.y = 0;
            if (d.magnitude > radius + 0.5f) return false;
            Vector3 away = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.right;
            escape = transform.position + away * (radius + 1.0f);
            escape.y = 0;
            return true;
        }
    }
}
