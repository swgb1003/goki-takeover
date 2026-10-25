using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    /// <summary>
    /// Shared part of the Stage 2 family's own attacks: who is attacking, what they aim at (the player on the floor,
    /// or the ally they are chasing) and taking the member's state machine over while the attack runs.
    /// StageProgress switches them off when the family leaves the house (final defence).
    /// </summary>
    public abstract class HouseholdAttack : MonoBehaviour, IAllyThreat
    {
        [SerializeField] protected PhaseTwoSession session;
        [SerializeField] protected SwarmCommander commander;
        [SerializeField] protected HumanAIController human;
        [SerializeField] protected BlenderHumanAnimator animator;
        [SerializeField] protected string codexId;   // the encyclopedia page learnt when this member first spots the player
        protected HumanReaction reaction;
        private ColonyManager colony;
        private bool met;
        protected NavMeshAgent agent;
        protected float clock, cooldownUntil;
        protected bool holding;
        public bool Allowed { get; set; } = true;
        public int Uses { get; protected set; }
        public HumanAIController Human => human;
        public abstract bool Busy { get; }

        public void Configure(PhaseTwoSession game, SwarmCommander swarm, HumanAIController member, BlenderHumanAnimator body, string codexPage)
        { session = game; commander = swarm; human = member; animator = body; codexId = codexPage; }

        protected virtual void Awake()
        {
            agent = human.GetComponent<NavMeshAgent>();
            commander.HouseholdThreats.Add(this);
            session.RunReset += Stop;
        }
        protected virtual void Start() { colony = FindFirstObjectByType<ColonyManager>(); reaction = colony ? colony.Reaction : null; }
        protected virtual void LateUpdate()
        {
            if (met || !colony || !session.IsRunning || !human.CanSeePlayer || string.IsNullOrEmpty(codexId)) return;
            met = true; colony.Learn(codexId);
        }
        protected virtual void OnDestroy()
        {
            if (commander) commander.HouseholdThreats.Remove(this);
            if (session) session.RunReset -= Stop;
        }

        /// <summary>Back to idle at once (reset, the family leaving, a restored run).</summary>
        public abstract void Stop();
        public abstract bool Threatens(Vector3 position, out Vector3 escape);

        protected bool PlayerOnFloor => session.Motor.FootPoint.y < 0.5f && !session.Motor.IsClimbing && !session.InSafeNest;
        /// <summary>The roach to attack: the player on the floor in sight, else the ally being chased. False: none.</summary>
        protected bool PickTarget(out Vector3 point, out int ally)
        {
            point = Vector3.zero; ally = -1;
            if (human.State != HumanAIController.HumanState.Chase || !human.CanSeeTarget) return false;
            if (human.TargetIsPlayer) { if (!PlayerOnFloor) return false; point = Flat(session.Motor.FootPoint); return true; }
            ally = human.Prey;
            point = Flat(commander.Allies[ally].Position);
            return commander.Allies[ally].Active;
        }
        protected bool CanStart => Allowed && session.IsRunning && !human.Suspended && human.State != HumanAIController.HumanState.Attack && Time.time >= cooldownUntil;

        protected void Hold()
        {
            human.Suspended = true; holding = true;
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
        }
        protected void Release()
        {
            if (!holding) return;
            holding = false;
            human.Suspended = false;
            if (agent && agent.isOnNavMesh) agent.isStopped = false;
        }
        protected void Face(Vector3 point, float dt)
        {
            Vector3 flat = Flat(point - human.transform.position);
            if (flat.sqrMagnitude > 0.01f) human.transform.rotation = Quaternion.RotateTowards(human.transform.rotation, Quaternion.LookRotation(flat), 360 * dt);
        }
        protected void Speak(string line) { if (reaction) reaction.SayAs(human, line); }
        protected static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
