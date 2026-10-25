using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    /// <summary>
    /// The son's bug cage (Stage 2): allies caught in the net wait inside, wriggling. The player opens it with E next to
    /// it and they come out following the player.
    /// </summary>
    public sealed class BugCage : MonoBehaviour
    {
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private SwarmCommander commander;
        [SerializeField] private Transform[] prisoners;
        public const float OpenRange = 1.5f;
        private readonly List<int> slots = new List<int>();
        public int Count => slots.Count;
        public int Freed { get; private set; }
        public void Configure(PhaseTwoSession game, SwarmCommander swarm, Transform[] shown) { session = game; commander = swarm; prisoners = shown; }

        private void Awake() { session.RunReset += Clear; Show(); }
        private void OnDestroy() { if (session) session.RunReset -= Clear; }

        public void Add(int slot) { if (!slots.Contains(slot)) slots.Add(slot); Show(); }
        public void Clear() { slots.Clear(); Freed = 0; Show(); }
        public bool PlayerNear => session.Motor.FootPoint.y < 0.6f && Flat(session.Motor.FootPoint - transform.position).magnitude <= OpenRange;

        private void Update()
        {
            if (!session.IsRunning) return;
            // A caged slot reused by breeding is no longer in here.
            for (int i = slots.Count - 1; i >= 0; i--) if (commander.Allies[slots[i]].gameObject.activeSelf) { slots.RemoveAt(i); Show(); }
            if (slots.Count > 0 && session.CanControl && Input.GetKeyDown(KeyCode.E) && PlayerNear && !session.Interactor.Carried) Open();
            for (int i = 0; i < prisoners.Length; i++)
                if (prisoners[i].gameObject.activeSelf) prisoners[i].localRotation = Quaternion.Euler(0, i * 77 + Mathf.Sin(Time.time * 3 + i) * 40, Mathf.Sin(Time.time * 11 + i * 2) * 8);
        }

        /// <summary>Lets every caged ally out in front of the cage. Returns how many came out.</summary>
        public int Open()
        {
            int n = 0;
            Vector3 front = transform.position + transform.forward * 1.1f;
            foreach (int slot in slots) if (commander.ReleaseAlly(slot, front + Quaternion.Euler(0, n * 50, 0) * Vector3.right * 0.4f)) n++;
            slots.Clear(); Freed += n; Show();
            if (n > 0) commander.Say("虫かごから仲間 " + n + "匹を助け出した！");
            return n;
        }

        private void Show() { if (prisoners != null) for (int i = 0; i < prisoners.Length; i++) prisoners[i].gameObject.SetActive(i < slots.Count); }
        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
