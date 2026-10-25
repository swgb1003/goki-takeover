using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    /// <summary>
    /// Phase 5 stage flow (spec 9.3, 6.2 levels 4-5, 7.1): mission briefing, the three takeover gauges
    /// (food secured, territory held, the resident's fear), the final defence against the exterminator's
    /// foggers, the resident moving out, the rating and the save. A dead roach can hand over to an ally
    /// (successor); otherwise the run returns to the last checkpoint or the title.
    /// The stage also sets the run's rules: the player starts alone, fallen allies stay dead (their corpse
    /// can be eaten for health), the slipper reaches the worktop and low walls, and the capture order.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class StageProgress : MonoBehaviour, IAllyThreat, ICaptureZones
    {
        public enum Phase { Briefing, Invasion, FinalDefence, Retreat, Cleared }
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private SwarmCommander commander;
        [SerializeField] private ColonyManager colony;
        [SerializeField] private StageDefinition stage;
        [SerializeField] private SmokeFogger[] foggers;
        [SerializeField] private Transform[] flags;
        [SerializeField] private SmokeFogger nestFogger;
        [SerializeField] private GameObject nestSmoke;
        [SerializeField] private CeilingSprayer sprayer;
        public CeilingSprayer Sprayer => sprayer;
        public void ConfigureSprayer(CeilingSprayer backpack) { sprayer = backpack; }
        [SerializeField] private Vector3 nestDoor, nestApproach, nestRefuge;
        public enum Fumigation { None, Approach, Warning, Smoke }
        public Fumigation NestFumigation { get; private set; }
        public float NestFumigationLeft => NestFumigation == Fumigation.Warning ? stage.nestFumigationWarning - fumigationClock
            : NestFumigation == Fumigation.Smoke ? stage.nestFumigationSeconds - fumigationClock : 0;
        public SmokeFogger NestFogger => nestFogger;
        public Vector3 NestRefuge => nestRefuge;
        private int fumigationsDone;
        private float fumigationClock, nestDamageClock;
        private HumanReaction reaction;
        private ExterminatorLook exterminator;
        private HumanAIController human;
        private NavMeshAgent agent;
        // Stage 2: the family's own attacks, and the members on their way out when the exterminator arrives.
        private readonly List<HouseholdAttack> attacks = new List<HouseholdAttack>();
        private readonly List<HumanAIController> leaving = new List<HumanAIController>();
        private float leaveClock;
        public IReadOnlyList<HouseholdAttack> Attacks => attacks;
        public bool FamilyGone { get { foreach (var m in session.Family) if (m.gameObject.activeSelf) return false; return true; } }
        private Renderer[] residentRenderers;
        private bool[] residentShown;
        private float[] zoneTime;
        private bool[] captured;
        private int[] presence;
        private float zoneClock, defenceClock, foggerClock, fearClock, retreatClock;
        private int waves;
        private bool hidden, pendingStartSave;
        private float autosaveClock;
        private int savedCollected, savedCaptured, savedAllies, savedNestLevel;
        // Progress is kept without visiting the nest: on each delivery, capture or colony change and every half
        // minute of play (positions are never saved, a resumed run starts at the nest).
        private const float AutosaveSeconds = 30f;
        public Phase Current { get; private set; }
        public StageDefinition Stage => stage;
        public SaveData Save { get; private set; }
        public float FoodGauge => Mathf.Clamp01(colony.TotalCollected / (float)stage.foodGoal);
        public float FearGauge => Mathf.Clamp01(reaction.FearGained / stage.fearGoal);
        public float TerritoryGauge
        {
            get { float sum = 0; for (int i = 0; i < zoneTime.Length; i++) sum += ZoneProgress(i); return zoneTime.Length == 0 ? 1 : sum / zoneTime.Length; }
        }
        public bool AllGaugesFull => FoodGauge >= 1 && FearGauge >= 1 && TerritoryGauge >= 1;
        public int ZoneCount => stage.zones.Length;
        public float ZoneProgress(int index) => captured[index] ? 1 : Mathf.Clamp01(zoneTime[index] / stage.holdSeconds);
        public bool ZoneCaptured(int index) => captured[index];
        public int ZonePresence(int index) => presence[index];
        public bool ZoneContested(int index) => !captured[index] && ResidentIn(index);
        public int CapturedZones { get { int n = 0; foreach (bool c in captured) if (c) n++; return n; } }
        public float DefenceRemaining => Current == Phase.FinalDefence ? Mathf.Max(0, stage.finalDefenceSeconds - defenceClock) : 0;
        public float NextFoggerIn => Mathf.Max(0, (waves == 0 ? 3f : stage.foggerInterval) - foggerClock);
        public SmokeFogger[] Foggers => foggers;
        public int Successors { get; private set; }
        public int SmokeLosses { get; private set; }
        public int SmokeHits { get; private set; }
        public int CorpsesEaten { get; private set; }
        public bool CanSucceed => session.State == PhaseTwoSession.RunState.Dead && colony.Allies > 0;
        public bool HasCheckpoint => Save.HasCheckpoint(stage.id);
        public string Notice { get; private set; }
        public bool ResidentHidden => hidden;
        public int FoggerWaves => waves;
        // Result of the last clear.
        public string Rating { get; private set; } = "";
        public int RatingPoints { get; private set; }
        public bool NewStageUnlocked { get; private set; }
        public bool NewRewardUnlocked { get; private set; }
        public bool ClearSaved { get; private set; }
        public event Action<string> Announce;

        public void Configure(PhaseTwoSession game, SwarmCommander swarm, ColonyManager nest, StageDefinition definition, SmokeFogger[] cans, Transform[] zoneFlags)
        { session = game; commander = swarm; colony = nest; stage = definition; foggers = cans; flags = zoneFlags; }
        /// <summary>The final defence's nest smoking: the fogger set in the cabinet door, the smoke filling it, where the
        /// exterminator stands to set it, and the refuge allies (and a successor) use meanwhile.</summary>
        public void ConfigureNestSmoking(SmokeFogger fogger, GameObject smoke, Vector3 door, Vector3 approach, Vector3 refuge)
        { nestFogger = fogger; nestSmoke = smoke; nestDoor = door; nestApproach = approach; nestRefuge = refuge; }

        private void Awake()
        {
            reaction = colony.Reaction;
            human = session.Human;
            agent = human.GetComponent<NavMeshAgent>();
            exterminator = human.GetComponentInChildren<ExterminatorLook>(true);
            residentRenderers = human.GetComponentsInChildren<Renderer>(true);
            residentShown = new bool[residentRenderers.Length];
            for (int i = 0; i < residentRenderers.Length; i++) residentShown[i] = residentRenderers[i].enabled;
            zoneTime = new float[stage.zones.Length];
            captured = new bool[stage.zones.Length];
            presence = new int[stage.zones.Length];
            colony.ClearsOnColonySize = false;
            colony.MaxNestNote = "";
            colony.NestClosed += () => { if (Current == Phase.Invasion) SaveCheckpoint(true); };
            commander.ExtraThreat = this;
            commander.Zones = this;
            commander.AllyCorpseSeconds = stage.allyCorpseSeconds;
            commander.AllyReactionSeconds = stage.allyReactionSeconds;
            commander.AllyStunSeconds = stage.allyStunSeconds;
            commander.AllyWoundHealSeconds = stage.allyWoundHealSeconds;
            commander.AllyAlarmRadius = stage.allyAlarmRadius;
            commander.CallInView = true;
            commander.CollectReactionScale = stage.allyCollectReactionScale;
            commander.NestRefuge = nestRefuge;
            session.ReviveAt = nestRefuge;
            human.SearchWander = true;
            human.PatrolRandom = true;
            if (stage.residentHuntsAllies) human.ExtraPrey = commander;
            // Stage 2: the rest of the household hunts the same way; a harder house notices roaches sooner.
            foreach (var member in session.Family)
            {
                member.SearchWander = true; member.PatrolRandom = true;
                if (stage.residentHuntsAllies) member.ExtraPrey = commander;
                var attack = member.GetComponent<HouseholdAttack>();
                if (attack) attacks.Add(attack);
            }
            reaction.StageDetectionScale = stage.humanDetectionScale;
            reaction.StageSpeedScale = stage.humanSpeedScale;
            reaction.HeatArea = stage.heatArea;
            reaction.CalmAfterSeconds = stage.calmSeconds;
            reaction.CalmBelowFear = stage.calmBelowFear;
            reaction.FearDecayOverride = stage.fearDecayPerSecond;
            reaction.AllyFearCap = stage.allyFearCap;
            reaction.ApplyLevel();
            colony.StartAlliesOverride = stage.startAllies;
            session.StrikeReach = stage.slipperReach;
            session.RunReset += ResetProgress;
            Save = SaveData.Load();
            if (SaveData.LastLoadMessage != null) Say(SaveData.LastLoadMessage);
        }
        private void OnDestroy() { if (session) session.RunReset -= ResetProgress; }

        private void Start()
        {
            // The session has already reset the run; hold it on the mission briefing until the sortie.
            ApplySkin();
            Current = Phase.Briefing;
            session.Pause();
        }

        /// <summary>Briefing -> play. Resumes from the saved checkpoint when the title asked for it.</summary>
        public void Begin()
        {
            if (Current != Phase.Briefing) return;
            bool resume = GameFlow.ResumeRequested && GameFlow.PendingStage == stage.id && HasCheckpoint;
            GameFlow.ConsumeResume();
            if (resume) { RestoreCheckpoint(); Say("前回、巣で記録したところから再開"); return; }
            Current = Phase.Invasion;
            if (session.State == PhaseTwoSession.RunState.Paused) session.Resume();
            pendingStartSave = true;
        }

        public void ResetProgress()
        {
            Array.Clear(zoneTime, 0, zoneTime.Length);
            Array.Clear(captured, 0, captured.Length);
            Array.Clear(presence, 0, presence.Length);
            foreach (var f in flags) if (f) f.gameObject.SetActive(false);
            foreach (var f in foggers) f.Stop();
            EndFumigation(); fumigationsDone = 0;
            if (sprayer) { sprayer.Stop(); sprayer.ResetCounts(); }
            zoneClock = defenceClock = foggerClock = fearClock = retreatClock = 0;
            waves = 0;
            Successors = SmokeLosses = SmokeHits = CorpsesEaten = 0;
            Rating = ""; RatingPoints = 0; NewStageUnlocked = NewRewardUnlocked = ClearSaved = false;
            reaction.enabled = true;
            leaving.Clear();
            foreach (var a in attacks) { a.Allowed = true; a.Stop(); }
            ShowResident(true);
            if (exterminator) exterminator.Show(false);
            if (Current != Phase.Briefing) Current = Phase.Invasion;
            pendingStartSave = true;   // a fresh run (retry, replay) gets a new stage start checkpoint
        }

        private void Update()
        {
            if (Current == Phase.Briefing) { if (session.IsRunning) Begin(); return; }
            if (!session.IsRunning) return;
            if (pendingStartSave) { pendingStartSave = false; SaveCheckpoint(false); }   // stage start checkpoint (spec 7.1)
            else if (Current == Phase.Invasion) Autosave(Time.deltaTime);
            if (session.CanControl && Input.GetKeyDown(KeyCode.E)) TryEatCorpse();
            float dt = Time.deltaTime;
            zoneClock += dt;
            if (zoneClock >= 0.25f) { UpdateZones(zoneClock); zoneClock = 0; }
            switch (Current)
            {
                case Phase.Invasion:
                    if (AllGaugesFull) BeginFinalDefence();
                    break;
                case Phase.FinalDefence: Defend(dt); FamilyLeaving(dt); break;
                case Phase.Retreat: Retreat(dt); break;
            }
        }

        // ---------------- Territory ----------------
        private void UpdateZones(float step)
        {
            Vector3 player = session.Motor.FootPoint;
            bool playerOnFloor = player.y < 0.5f && !session.Motor.IsClimbing;
            for (int i = 0; i < stage.zones.Length; i++)
            {
                var zone = stage.zones[i];
                int n = 0;
                // Roaches hiding inside an enclosed nest hold nothing outside it.
                if (playerOnFloor && !InsideNest(player) && Flat(player - zone.centre).magnitude <= zone.radius) n++;
                foreach (var ally in commander.Allies) if (ally.Active && !InsideNest(ally.Position) && Flat(ally.Position - zone.centre).magnitude <= zone.radius) n++;
                presence[i] = n;
                if (captured[i]) continue;
                bool resident = ResidentIn(i);
                // Holding needs enough roaches and no resident in the zone; the resident alone slowly clears it.
                if (n >= stage.holdPresence && !resident) zoneTime[i] += step;
                else if (resident && n == 0) zoneTime[i] = Mathf.Max(0, zoneTime[i] - stage.residentClearRate * step);
                if (zoneTime[i] < stage.holdSeconds) continue;
                captured[i] = true;
                if (i < flags.Length && flags[i]) flags[i].gameObject.SetActive(true);
                reaction.AddFear(stage.fearPerZone);
                Say("「" + zone.name + "」を占領した！  （" + CapturedZones + " / " + stage.zones.Length + "）");
                reaction.Say(CapturedZones == stage.zones.Length ? "どこを見てもゴキブリが…" : "あそこ、巣になってない…？");
                commander.ContinueCapture(i);
            }
        }

        // Capture order targets (ICaptureZones).
        public int HoldPresence => stage.holdPresence;
        public bool ZoneOpen(int index) => !captured[index];
        public Vector3 ZoneCentre(int index) => stage.zones[index].centre;
        public int ZoneAt(Vector3 position)
        {
            for (int i = 0; i < stage.zones.Length; i++)
                if (Flat(position - stage.zones[i].centre).magnitude <= stage.zones[i].radius) return i;
            return -1;
        }
        /// <summary>The open zone the player stands in on the floor, or -1.</summary>
        public int PlayerZone
        {
            get
            {
                Vector3 foot = session.Motor.FootPoint;
                if (foot.y >= 0.5f || session.Motor.IsClimbing) return -1;
                int zone = ZoneAt(foot);
                return zone >= 0 && !captured[zone] ? zone : -1;
            }
        }

        // ---------------- Fallen allies ----------------
        /// <summary>A corpse next to the player on the floor (E eats it), or null.</summary>
        public AllyRoach NearbyCorpse
        {
            get
            {
                var interactor = session.Interactor;
                if (interactor.Carried || session.Motor.FootPoint.y > 0.5f) return null;
                return commander.NearestCorpse(session.Motor.FootPoint, session.Settings.interactionRange + 0.3f);
            }
        }
        public bool TryEatCorpse()
        {
            var interactor = session.Interactor;
            if (!session.IsRunning || interactor.NearbyFood || interactor.AtNest) return false;
            var corpse = NearbyCorpse;
            if (!corpse || reaction.NearbyBait(session.Player.position, session.Settings.interactionRange)) return false;
            if (session.Hp >= session.MaximumHp) { Say("生命力は満タン — 傷ついた時に食べよう"); return false; }
            corpse.Consume();
            session.Heal(stage.corpseHeal);
            CorpsesEaten++;
            Say("仲間の死体を食べた…  生命力 +" + stage.corpseHeal + "（" + session.Hp + " / " + session.MaximumHp + "）");
            return true;
        }
        private bool ResidentIn(int index)
        {
            var zone = stage.zones[index];
            if (!hidden && Flat(human.transform.position - zone.centre).magnitude <= zone.radius + 0.5f) return true;
            foreach (var member in session.Family)
                if (member.gameObject.activeInHierarchy && !leaving.Contains(member) && Flat(member.transform.position - zone.centre).magnitude <= zone.radius + 0.5f) return true;
            return false;
        }
        /// <summary>The member of the household in a zone that holds it up (HUD), or null.</summary>
        public HumanAIController HumanInZone(int index)
        {
            var zone = stage.zones[index];
            for (int i = 0; i < session.HumanCount; i++)
            {
                var h = session.HumanAt(i);
                if (!session.HumanPresent(i) || (i == 0 && hidden) || leaving.Contains(h)) continue;
                if (Flat(h.transform.position - zone.centre).magnitude <= zone.radius + 0.5f) return h;
            }
            return null;
        }

        // ---------------- The family leaves (Stage 2) ----------------
        private void SendFamilyOut()
        {
            if (!stage.familyLeavesForDefence || session.Family.Count == 0) return;
            leaveClock = 0;
            foreach (var a in attacks) { a.Allowed = false; a.Stop(); }
            foreach (var member in session.Family)
            {
                if (!member.gameObject.activeSelf) continue;
                leaving.Add(member);
                member.Suspended = true;
                var walker = member.GetComponent<NavMeshAgent>();
                if (walker.isOnNavMesh && NavMesh.SamplePosition(stage.exitPoint, out NavMeshHit exit, 1.5f, walker.areaMask))
                { walker.isStopped = false; walker.speed = session.Settings.chaseSpeed; walker.SetDestination(exit.position); }
            }
            reaction.SayAs(session.Family[0], "あとはプロに任せて、みんな避難よ！");
        }
        private void FamilyLeaving(float dt)
        {
            if (leaving.Count == 0) return;
            leaveClock += dt;
            for (int i = leaving.Count - 1; i >= 0; i--)
            {
                var walker = leaving[i].GetComponent<NavMeshAgent>();
                bool arrived = walker.isOnNavMesh && !walker.pathPending && walker.remainingDistance < 0.7f;
                if (arrived || leaveClock > 12f) { leaving[i].gameObject.SetActive(false); leaving.RemoveAt(i); }
            }
        }

        // ---------------- Final defence ----------------
        public void BeginFinalDefence()
        {
            Current = Phase.FinalDefence;
            defenceClock = foggerClock = fearClock = 0;
            waves = 0;
            reaction.AddFear(100);
            reaction.Say(exterminator ? "駆除業者だ。…どこにいる？" : "もう限界…  駆除業者を呼んだ！");
            if (exterminator) exterminator.Show(true);   // the resident's call brings the professional in
            Say("最終防衛！  駆除業者の燻煙剤を " + stage.finalDefenceSeconds.ToString("F0") + "秒 耐えろ");
            colony.Learn("fogger");
            SendFamilyOut();
        }

        private void Defend(float dt)
        {
            defenceClock += dt; foggerClock += dt; fearClock += dt;
            if (fearClock >= 5) { fearClock = 0; reaction.AddFear(100); }   // the exterminator stays in panic mode
            if (foggerClock >= (waves == 0 ? 3f : stage.foggerInterval)) { foggerClock = 0; SetOffFogger(); }
            SmokeDamage();
            UpdateFumigation(dt);
            if (sprayer) sprayer.Tick(dt, NestFumigation != Fumigation.Approach);   // the ceiling is no hiding place either
            if (defenceClock >= stage.finalDefenceSeconds) BeginRetreat();
        }

        // ---------------- The exterminator smokes the nest ----------------
        /// <summary>
        /// At set times of the final defence the exterminator walks to the sink cabinet (Approach), sets a fogger in
        /// its door (Warning: a ring and a countdown; allies leave by themselves) and the inside fills with smoke
        /// (Smoke): the nest is neither safe nor usable, anyone still inside takes damage, allies there die. Then it
        /// clears. Hiding in the nest no longer wins the final defence: everyone has to run.
        /// </summary>
        private void UpdateFumigation(float dt)
        {
            if (!nestFogger) return;
            fumigationClock += dt;
            switch (NestFumigation)
            {
                case Fumigation.None:
                    if (fumigationsDone < stage.nestFumigations.Length && defenceClock >= stage.nestFumigations[fumigationsDone]) StartFumigation();
                    break;
                case Fumigation.Approach:
                    if (human.State == HumanAIController.HumanState.Attack && !human.Suspended) break;   // finish the swing first
                    human.Suspended = true;
                    if (agent.isOnNavMesh && NavMesh.SamplePosition(nestApproach, out NavMeshHit spot, 1.5f, agent.areaMask))
                    { agent.isStopped = false; agent.speed = session.Settings.chaseSpeed; if ((agent.destination - spot.position).sqrMagnitude > 0.04f) agent.SetDestination(spot.position); }
                    bool there = agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance < 0.6f;
                    if (there || fumigationClock >= 4f) ArmNestFogger();
                    break;
                case Fumigation.Warning:
                    if (fumigationClock >= stage.nestFumigationWarning)
                    {
                        NestFumigation = Fumigation.Smoke; fumigationClock = 0; nestDamageClock = stage.nestSmokeDamageEvery;
                        session.NestSmoked = true;
                        if (nestSmoke) nestSmoke.SetActive(true);
                        Say("巣が煙でいっぱいだ！  中にいるとダメージ ― 外を逃げ回れ");
                    }
                    break;
                case Fumigation.Smoke:
                    nestDamageClock += dt;
                    if (nestDamageClock >= stage.nestSmokeDamageEvery)
                    {
                        nestDamageClock = 0;
                        if (InsideNest(session.Player.position) && session.TryDamage(1)) SmokeHits++;
                        foreach (var ally in commander.Allies)
                            if (ally.Active && InsideNest(ally.Position)) { ally.Crush(commander.Settings3.allyRespawnSeconds); SmokeLosses++; }
                    }
                    if (fumigationClock >= stage.nestFumigationSeconds) { EndFumigation(); Say("巣の煙が晴れた ― また隠れられる"); }
                    break;
            }
        }
        private void StartFumigation()
        {
            fumigationsDone++;
            NestFumigation = Fumigation.Approach; fumigationClock = 0;
            reaction.Say("巣はここか…！");
            Say("駆除業者が巣に向かっている！  燻される前に外へ");
        }
        private void ArmNestFogger()
        {
            NestFumigation = Fumigation.Warning; fumigationClock = 0;
            human.Suspended = false;
            commander.NestSmoked = true;   // allies leave and wait at the refuge, carried food included
            nestFogger.Arm(nestDoor, 1.2f, stage.nestFumigationWarning, stage.nestFumigationSeconds, stage.smokeHeight);
            reaction.Say("巣ごと燻してやる！");
            Say("巣が燻される！  " + stage.nestFumigationWarning.ToString("F0") + "秒以内に外へ逃げろ");
        }
        private void EndFumigation()
        {
            if (NestFumigation == Fumigation.Approach) human.Suspended = false;   // it was walking to the cabinet
            NestFumigation = Fumigation.None; fumigationClock = 0;
            session.NestSmoked = false;
            commander.NestSmoked = false;
            if (nestSmoke) nestSmoke.SetActive(false);
            if (nestFogger) nestFogger.Stop();
        }
        // The fogger in the door counts as a smoke source like the others (its smoke spills out of the cabinet).
        private IEnumerable<SmokeFogger> AllFoggers()
        {
            foreach (var f in foggers) yield return f;
            if (nestFogger) yield return nestFogger;
        }

        /// <summary>Alternates between the player's spot and the busiest zone; never over the nest.</summary>
        public SmokeFogger SetOffFogger()
        {
            SmokeFogger can = null;
            foreach (var f in foggers) if (!f.Active) { can = f; break; }
            int live = 0; foreach (var f in foggers) if (f.Active) live++;
            if (!can || live >= stage.maxFoggers) return null;
            Vector3 target;
            Vector3 player = session.Motor.FootPoint;
            if (waves % 2 == 0 && player.y < 0.5f && !session.InSafeNest) target = Flat(player);
            else
            {
                int best = 0;
                for (int i = 1; i < presence.Length; i++) if (presence[i] > presence[best]) best = i;
                target = stage.zones.Length > 0 ? stage.zones[best].centre : Flat(player);
            }
            Vector3 nest = commander.NestGround;
            float keep = stage.foggerRadius + 1.4f;
            Vector3 fromNest = Flat(target - nest);
            if (fromNest.magnitude < keep) target = nest + (fromNest.sqrMagnitude > 1e-3f ? fromNest.normalized : Vector3.right) * keep;
            if (!commander.Grid.Walkable(target)) commander.Grid.NearestWalkable(target, 2f, out target);
            waves++;
            can.Arm(target, stage.foggerRadius, stage.foggerWarning, stage.foggerDuration, stage.smokeHeight);
            Say("燻煙剤だ！  橙の輪の外か、高い所・巣へ逃げろ");
            return can;
        }

        private void SmokeDamage()
        {
            foreach (var f in AllFoggers())
            {
                if (!f.IsArmed) continue;
                if (f.Contains(session.Motor.BodyCentre) && session.TryDamage(1)) SmokeHits++;
                foreach (var ally in commander.Allies)
                    if (ally.Active && !InsideNest(ally.Position) && f.Contains(ally.Position + Vector3.up * 0.2f)) { ally.Crush(commander.Settings3.allyRespawnSeconds); SmokeLosses++; }
            }
        }

        /// <summary>Allies flee smoke rings before any order; the escape is free floor outside the ring, else the nest.</summary>
        public bool Threatens(Vector3 position, out Vector3 escape)
        {
            escape = position;
            // The nest is about to be (or is being) smoked: out to the refuge.
            if ((NestFumigation == Fumigation.Warning || NestFumigation == Fumigation.Smoke) && InsideNest(position)) { escape = nestRefuge; return true; }
            foreach (var f in AllFoggers())
            {
                if (!f.Threatens(position, out Vector3 away)) continue;
                Vector3 centre = f.transform.position;
                Vector3 dir = Flat(away - centre).normalized;
                for (int k = 0; k < 8; k++)
                {
                    float angle = (k + 1) / 2 * 45f * (k % 2 == 0 ? 1 : -1);
                    Vector3 p = centre + Quaternion.Euler(0, angle, 0) * dir * (f.Radius + 1.0f);
                    p.y = 0;
                    if (commander.NestSmoked && InsideNest(p)) continue;   // never flee into a nest about to fill with smoke
                    if (commander.Grid.Walkable(p)) { escape = p; return true; }
                }
                escape = commander.NestSmoked ? nestRefuge : commander.NestGround;
                return true;
            }
            return false;
        }

        // ---------------- Retreat and clear ----------------
        private void BeginRetreat()
        {
            Current = Phase.Retreat;
            retreatClock = 0;
            foreach (var f in foggers) f.Stop();
            EndFumigation();
            if (sprayer) sprayer.Stop();
            reaction.Spray.Stop();
            reaction.enabled = false;
            human.Suspended = true;
            if (agent.isOnNavMesh && NavMesh.SamplePosition(stage.exitPoint, out NavMeshHit exit, 1.5f, agent.areaMask))
            {
                agent.isStopped = false;
                agent.speed = session.Settings.chaseSpeed;
                agent.SetDestination(exit.position);
            }
            reaction.Say(exterminator && exterminator.Showing ? "撤収！  この部屋はもう手遅れだ！" : "もう無理！  この部屋は出ていく！");
            Say("駆除作戦を耐えきった！  人間が逃げていく…");
        }

        private void Retreat(float dt)
        {
            retreatClock += dt;
            bool arrived = !agent.pathPending && agent.remainingDistance < 0.6f;
            if (!hidden && (arrived || retreatClock > 7f)) { ShowResident(false); retreatClock = Mathf.Max(retreatClock, 7f); }
            if (hidden && retreatClock > 8.2f) FinishStage();
        }

        private void FinishStage()
        {
            Current = Phase.Cleared;
            Rating = stage.Rate(session.RunSeconds, session.Discoveries, colony.Allies, colony.TotalCollected, Successors, out int points);
            RatingPoints = points;
            Save = SaveData.Load();
            Save.RecordClear(stage.id, Rating, session.RunSeconds, colony.TotalCollected, stage.unlocks, stage.reward, out bool newStage, out bool newReward);
            NewStageUnlocked = newStage; NewRewardUnlocked = newReward;
            ClearSaved = Save.Save();
            if (!ClearSaved) Say("結果を保存できませんでした");
            session.Complete();
        }

        private void ShowResident(bool show)
        {
            hidden = !show;
            for (int i = 0; i < residentRenderers.Length; i++) if (residentRenderers[i]) residentRenderers[i].enabled = show && residentShown[i];
        }

        // ---------------- Defeat ----------------
        /// <summary>An ally takes over the player's role at the nest; the colony loses that ally.</summary>
        public bool Succeed()
        {
            if (!CanSucceed || !commander.RemoveAlly()) return false;
            Successors++;
            session.Revive();
            Say("仲間が後を継いだ！  （残り " + colony.Allies + "匹）");
            return true;
        }
        public void RestartStage()
        {
            if (Current == Phase.Briefing) Current = Phase.Invasion;
            session.ResetRun();
        }

        // ---------------- Save ----------------
        private void Autosave(float dt)
        {
            autosaveClock += dt;
            bool changed = colony.TotalCollected != savedCollected || CapturedZones != savedCaptured || colony.Allies != savedAllies || colony.NestLevel != savedNestLevel;
            if (changed || autosaveClock >= AutosaveSeconds) SaveCheckpoint(false);
        }
        /// <summary>Keeps the run when the player leaves mid-invasion (title, window closed).</summary>
        public void SaveOnLeave() { if (Current == Phase.Invasion && session.State != PhaseTwoSession.RunState.Dead) SaveCheckpoint(false); }
        private void OnApplicationQuit() { SaveOnLeave(); }

        public bool SaveCheckpoint(bool announce)
        {
            autosaveClock = 0;
            savedCollected = colony.TotalCollected; savedCaptured = CapturedZones; savedAllies = colony.Allies; savedNestLevel = colony.NestLevel;
            Save = SaveData.Load();
            var c = new SaveData.Checkpoint
            {
                valid = true, stage = stage.id, savedAt = DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
                food = colony.Food, totalCollected = colony.TotalCollected, nestLevel = colony.NestLevel, allies = colony.Allies,
                upgradesBought = colony.UpgradesBought, born = colony.Born, upgrades = colony.UpgradeLevels,
                zones = new float[zoneTime.Length], fear = reaction.Fear, fearGained = reaction.FearGained,
                seconds = session.RunSeconds, discoveries = session.Discoveries, successors = Successors
            };
            for (int i = 0; i < zoneTime.Length; i++) c.zones[i] = captured[i] ? stage.holdSeconds : zoneTime[i];
            Save.checkpoint = c;
            bool ok = Save.Save();
            if (announce) Say(ok ? "巣で記録した（つづきから再開できます）" : "記録できませんでした");
            else if (!ok) Say("自動記録できませんでした");
            return ok;
        }

        public bool RestoreCheckpoint()
        {
            Save = SaveData.Load();
            if (!Save.HasCheckpoint(stage.id)) return false;
            var c = Save.checkpoint;
            session.ResetRun();
            Current = Phase.Invasion;
            pendingStartSave = false;
            colony.Restore(c.food, c.totalCollected, c.nestLevel, c.upgrades, c.allies, c.upgradesBought, c.born);
            session.Discoveries = c.discoveries;
            session.RestoreRunSeconds(c.seconds);
            reaction.RestoreFear(c.fear, c.fearGained);
            Successors = c.successors;
            for (int i = 0; i < zoneTime.Length && i < c.zones.Length; i++)
            {
                zoneTime[i] = c.zones[i];
                captured[i] = c.zones[i] >= stage.holdSeconds;
                if (i < flags.Length && flags[i]) flags[i].gameObject.SetActive(captured[i]);
            }
            return true;
        }

        // ---------------- Cosmetics ----------------
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private void ApplySkin()
        {
            if (Save.skin != "skin_gold") return;
            var block = new MaterialPropertyBlock();
            foreach (var r in session.Player.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.IndexOf("Eye", StringComparison.OrdinalIgnoreCase) >= 0 || !r.sharedMaterial || !r.sharedMaterial.HasProperty(ColorId)) continue;
                r.GetPropertyBlock(block);
                block.SetColor(ColorId, r.sharedMaterial.GetColor(ColorId) * new Color(1.55f, 1.2f, 0.45f));
                r.SetPropertyBlock(block);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Verification only.
        public void DebugCapture(int index) { zoneTime[index] = stage.holdSeconds + 0.5f; }   // taken on the next zone update
        public void DebugFinishDefence() { defenceClock = stage.finalDefenceSeconds; }
        public void DebugAddFear(float amount) { reaction.AddFear(amount); }
        public void DebugFumigateNest() { if (Current == Phase.FinalDefence && NestFumigation == Fumigation.None) StartFumigation(); }
        public void DebugEndFumigation() { EndFumigation(); }
#endif

        private bool InsideNest(Vector3 position) => session.HasNestArea && session.InNest(position);
        private void Say(string text) { Notice = text; commander.Say(text); Announce?.Invoke(text); }
        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
