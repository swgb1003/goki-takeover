using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Chapter 1 guide, from the first step to the clear: move, pick up and deliver food, breed the first ally,
    /// open the tactics view, order a capture and take one zone; then the stage goals one at a time (the rest
    /// of the territory, the food, the resident's fear) and the final defence. Each step ends when the game
    /// state shows it was done (so a resumed run skips what it already has); the HUD draws the current step,
    /// names the next one and points at what to press. Shown until finished or skipped once, then remembered
    /// in the save; the pause menu can show it again.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public sealed class StageTutorial : MonoBehaviour
    {
        public enum Step { Move, PickUp, Deliver, Breed, OpenSwarm, Capture, Hold, Territory, Food, Fear, Defence, Done }
        public const int StepCount = 11;   // Done is the closing message, not a step
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private SwarmCommander commander;
        [SerializeField] private ColonyManager colony;
        [SerializeField] private StageProgress progress;
        private Vector3 start;
        private float doneAt;
        private bool sawSwarm;
        public bool Active { get; private set; }
        public Step Current { get; private set; }
        public int StepNumber => Mathf.Min((int)Current + 1, StepCount);

        public void Configure(PhaseTwoSession game, SwarmCommander swarm, ColonyManager nest, StageProgress stage)
        { session = game; commander = swarm; colony = nest; progress = stage; }

        private void Awake()
        {
            // guideDone, not the older tutorialDone: a save that finished the short first version sees this one once.
            Active = progress.Stage.tutorial && !progress.Save.guideDone;
            session.RunReset += Restart;
        }
        private void OnDestroy() { if (session) session.RunReset -= Restart; }

        private void Restart()
        {
            start = session.Player.position;
            Current = Step.Move;
            doneAt = 0;
            sawSwarm = false;
        }

        private void Update()
        {
            if (!Active || progress.Current == StageProgress.Phase.Briefing) return;
            if (progress.Current == StageProgress.Phase.Cleared) { Finish(); return; }
            if (commander.Mode == SwarmCommander.ViewMode.Swarm) sawSwarm = true;
            while (Current < Step.Done && Satisfied(Current)) Current++;
            if (Current != Step.Done) return;
            if (doneAt == 0) doneAt = Time.unscaledTime;
            else if (Time.unscaledTime - doneAt > 6f) Finish();
        }

        private bool Satisfied(Step step)
        {
            bool delivered = colony.TotalCollected > 0, bred = colony.Born > 0 || colony.Allies > 0;
            bool captured = progress.CapturedZones > 0;
            // A run resumed in the final defence has nothing left to do before it.
            if (step < Step.Defence && progress.Current >= StageProgress.Phase.FinalDefence) return true;
            switch (step)
            {
                case Step.Move: return Flat(session.Player.position - start).magnitude > 1.5f || delivered || bred;
                case Step.PickUp: return session.Interactor.Carried || delivered || bred;
                case Step.Deliver: return delivered || bred;
                case Step.Breed: return bred;
                case Step.OpenSwarm: return sawSwarm || CaptureOrdered || captured;
                case Step.Capture: return CaptureOrdered || captured;
                case Step.Hold: return captured;
                case Step.Territory: return progress.TerritoryGauge >= 1;
                case Step.Food: return progress.FoodGauge >= 1;
                case Step.Fear: return progress.FearGauge >= 1;
                default: return progress.Current >= StageProgress.Phase.Retreat;
            }
        }

        public bool CaptureOrdered => CaptureZone >= 0;
        /// <summary>The zone the first ally on a capture order is heading for, or -1.</summary>
        public int CaptureZone
        {
            get
            {
                foreach (var ally in commander.Allies)
                    if (ally.Alive && ally.CurrentOrder == AllyRoach.Order.Capture && ally.CaptureZone >= 0) return ally.CaptureZone;
                return -1;
            }
        }

        /// <summary>Ends the guide for good (finished or skipped from the pause menu).</summary>
        public void Finish()
        {
            if (!Active) return;
            Active = false;
            var save = SaveData.Load();
            save.tutorialDone = save.guideDone = true;
            save.Save();
        }
        /// <summary>Pause menu: show the guide again from wherever the run is (finished steps are skipped).</summary>
        public void Show()
        {
            if (Active) return;
            Active = true;
            Restart();
            var save = SaveData.Load();
            save.guideDone = false;
            save.Save();
        }
        public void Skip()
        {
            Finish();
            commander.Say("ガイドを閉じました（ポーズでまた表示できます）");
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
