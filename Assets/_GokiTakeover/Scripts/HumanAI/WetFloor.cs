using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Stage 3: the floor the part-timer has just mopped. Wet patches (glossy blue discs with a light edge) dry after
    /// DrySeconds. The player walking on one loses grip (RoachMotor.Traction): it slides on when the keys are let go
    /// and is slow to turn. Allies keep their grip (they are lighter). Also drives the part-timer's mopping (MopDuty).
    /// </summary>
    public sealed class WetFloor : MonoBehaviour
    {
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private Transform[] patches = new Transform[0];   // pooled visuals, each a disc and an edge ring
        public const float Radius = 0.85f, DrySeconds = 30f, Fade = 3f, Grip = 0.06f;
        private Vector3[] centres;
        private float[] wetUntil;
        private int next;
        public int Count { get { int n = 0; if (wetUntil != null) foreach (float t in wetUntil) if (t > Time.time) n++; return n; } }
        public int Capacity => patches.Length;
        public bool PlayerOnWet { get; private set; }
        public int Slides { get; private set; }

        public void Configure(PhaseTwoSession game, Transform[] pool) { session = game; patches = pool; }

        private void Awake()
        {
            centres = new Vector3[patches.Length];
            wetUntil = new float[patches.Length];
            foreach (var p in patches) p.gameObject.SetActive(false);
            session.RunReset += DryAll;
        }
        private void OnDestroy() { if (session) session.RunReset -= DryAll; }

        /// <summary>A new wet patch here (the oldest one is reused when all are in use). Ignored if one is close already.</summary>
        public void Drop(Vector3 at)
        {
            at.y = 0;
            for (int i = 0; i < patches.Length; i++)
                if (wetUntil[i] > Time.time && (centres[i] - at).sqrMagnitude < Radius * Radius * 0.8f) { wetUntil[i] = Time.time + DrySeconds; return; }
            int slot = next; next = (next + 1) % patches.Length;
            centres[slot] = at;
            wetUntil[slot] = Time.time + DrySeconds;
            var p = patches[slot];
            p.position = new Vector3(at.x, 0.042f, at.z);   // above rugs and mats
            p.localScale = Vector3.one;
            p.gameObject.SetActive(true);
        }
        public void DryAll()
        {
            if (wetUntil == null) return;
            for (int i = 0; i < patches.Length; i++) { wetUntil[i] = 0; patches[i].gameObject.SetActive(false); }
            if (session && session.Motor) session.Motor.Traction = 1;
            PlayerOnWet = false;
        }
        public bool Wet(Vector3 position)
        {
            if (position.y > 0.5f) return false;
            position.y = 0;
            for (int i = 0; i < patches.Length; i++)
                if (wetUntil[i] > Time.time && (centres[i] - position).sqrMagnitude <= Radius * Radius) return true;
            return false;
        }

        private void Update()
        {
            for (int i = 0; i < patches.Length; i++)
            {
                if (!patches[i].gameObject.activeSelf) continue;
                float left = wetUntil[i] - Time.time;
                if (left <= 0) { patches[i].gameObject.SetActive(false); continue; }
                float s = left < Fade ? Mathf.Lerp(0.5f, 1, left / Fade) : 1;   // drying from the edge
                patches[i].localScale = new Vector3(s, 1, s);
            }
            var motor = session.Motor;
            bool wet = session.IsRunning && !motor.IsClimbing && Wet(motor.FootPoint);
            if (wet && !PlayerOnWet) Slides++;
            PlayerOnWet = wet;
            motor.Traction = wet ? Grip : 1;
        }
    }
}
