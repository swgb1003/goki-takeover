using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    /// <summary>
    /// The resident's reaction levels (spec 6.2, levels 0-3): fear and repeated sightings raise the level;
    /// each level makes the resident faster and more watchful. From "警戒" it places traps and poison bait
    /// where roaches were seen most (rule-based history, not learning); in "パニック" it also sprays.
    /// It drives the resident only while HumanAIController is suspended, and always gives control back.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class HumanReaction : MonoBehaviour
    {
        public enum Level { Peace, Noticed, Alert, Panic }
        public enum Activity { None, Walking, Placing, SprayWindup, Spraying, SprayRecover }
        private const int ZonesX = 4, ZonesZ = 3;
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private SwarmCommander commander;
        [SerializeField] private ColonySettings settings;
        [SerializeField] private InsecticideSpray spray;
        [SerializeField] private StickyTrap[] trapPool;
        [SerializeField] private PoisonBait[] baitPool;
        private HumanAIController human;
        private NavMeshAgent agent;
        private readonly float[] heat = new float[ZonesX * ZonesZ];
        private readonly Vector3[] lastSeenIn = new Vector3[ZonesX * ZonesZ];
        private readonly List<Component> placed = new List<Component>();
        private readonly Collider[] probe = new Collider[8];
        private float sensorClock, trapClock, actionTime, sprayReadyAt, lastSightingAt, identifyClock, bubbleUntil;
        private int lastDiscoveries, lastZone = -1, placements;
        private Vector3 placeAt;
        private HumanAIController.HumanState lastState;
        private bool alliesSeenInPanic;
        public float Fear { get; private set; }
        // Phase 5: every bit of fear ever added (the terror gauge counts it even while Fear is capped or decaying).
        public float FearGained { get; private set; }
        public Level CurrentLevel { get; private set; }
        public Level HighestLevel { get; private set; }
        public Activity CurrentAction { get; private set; }
        public int VisibleAllies { get; private set; }
        public int TrapsPlaced { get; private set; }
        public int SprayHits { get; private set; }
        public float StealthScale { get; set; } = 1f;
        // Stage 2: a harder house scales every member's detection time and speed, and the sighting heat map covers the
        // whole floor plan (default: the Stage 1 kitchen, 14 x 12 around the origin).
        public float StageDetectionScale { get; set; } = 1f;
        public float StageSpeedScale { get; set; } = 1f;
        public Rect HeatArea { get; set; } = new Rect(-7, -6, 14, 12);
        // Stage 2: after this long without a sighting (fear low, nobody hunting) 「警戒」 calms back to 「発見」 and the
        // family's daily life returns; sightings count again from there. 0: alertness stays for good (Stage 1 and before).
        public float CalmAfterSeconds { get; set; }
        public int Calmed { get; private set; }
        // Stage 2 tuning (defaults: the colony settings, unchanged): how fast fear fades, how many visible allies add to
        // the fear felt at once, and how low fear must be before 「警戒」 calms down.
        public float FearDecayOverride { get; set; }
        public int AllyFearCap { get; set; } = int.MaxValue;
        public float CalmBelowFear { get; set; }
        private int alertBase;
        public string Bubble { get; private set; }
        public bool BubbleVisible => Time.time < bubbleUntil && !string.IsNullOrEmpty(Bubble);
        public InsecticideSpray Spray => spray;
        public IReadOnlyList<PoisonBait> Baits => baitPool;
        public IReadOnlyList<StickyTrap> PlacedTraps => trapPool;
        public event Action<Level> LevelChanged;
        public event Action<string> Learned;
        public event Action<string> Announce;

        public void Configure(PhaseTwoSession game, SwarmCommander swarm, ColonySettings config, InsecticideSpray sprayer, StickyTrap[] traps, PoisonBait[] baits)
        { session = game; commander = swarm; settings = config; spray = sprayer; trapPool = traps; baitPool = baits; }

        private void Awake()
        {
            human = session.Human;
            agent = human.GetComponent<NavMeshAgent>();
            session.RunReset += ResetReaction;
            human.SlipperLanding += (point, crushed) => { if (crushed) Say("やったか！？"); };
        }
        private void OnDestroy() { if (session) session.RunReset -= ResetReaction; }

        public void ResetReaction()
        {
            Fear = FearGained = 0; CurrentLevel = HighestLevel = Level.Peace;
            alertBase = 0; Calmed = 0;
            Array.Clear(heat, 0, heat.Length);
            lastDiscoveries = session.Discoveries;
            sensorClock = trapClock = identifyClock = 0;
            sprayReadyAt = 0; lastZone = -1; placements = 0;
            TrapsPlaced = SprayHits = VisibleAllies = 0;
            alliesSeenInPanic = false;
            bubbleUntil = 0;
            foreach (var t in trapPool) t.gameObject.SetActive(false);
            foreach (var b in baitPool) b.Disarm();
            placed.Clear();
            spray.Stop();
            CurrentAction = Activity.None;
            human.Suspended = false;
            ApplyLevel();
        }

        public void AddFear(float amount) { Fear = Mathf.Clamp(Fear + amount, 0, 100); if (amount > 0) FearGained += amount; }
        /// <summary>Checkpoint restore (Phase 5): the level follows from fear and sightings on the next frame.</summary>
        public void RestoreFear(float fear, float gained) { Fear = Mathf.Clamp(fear, 0, 100); FearGained = Mathf.Max(0, gained); lastDiscoveries = session.Discoveries; }

        private void Update()
        {
            if (!session.IsRunning) return;
            float dt = Time.deltaTime;
            if (session.Discoveries > lastDiscoveries)
            {
                lastDiscoveries = session.Discoveries;
                AddFear(settings.fearPerDiscovery);
                Say(lastDiscoveries == 1 ? "ひっ！ ゴキブリ！" : "また出た！");
            }
            if (lastState == HumanAIController.HumanState.Chase && human.State == HumanAIController.HumanState.Search) Say("どこ行った…？");
            for (int i = 0; i < session.HumanCount; i++)
                if (session.HumanPresent(i) && session.HumanAt(i).State == HumanAIController.HumanState.Attack) { Learned?.Invoke("slipper"); break; }
            lastState = human.State;
            sensorClock += dt;
            if (sensorClock >= 0.5f) { Sense(sensorClock); sensorClock = 0; }
            if (Time.time - lastSightingAt > 3f) Fear = Mathf.Max(0, Fear - (FearDecayOverride > 0 ? FearDecayOverride : settings.fearDecayPerSecond) * dt);
            EvaluateLevel();
            identifyClock += dt;
            if (identifyClock >= 0.25f) { identifyClock = 0; IdentifyBaits(); }
            Act(dt);
        }

        // ---- Perception history ----
        private void Sense(float step)
        {
            bool seen = false;
            for (int i = 0; i < session.HumanCount; i++) seen |= session.HumanPresent(i) && session.HumanAt(i).CanSeePlayer;
            if (seen) RecordSighting(session.Motor.FootPoint, 1);
            VisibleAllies = 0;
            var s = session.Settings;
            foreach (var ally in commander.Allies)
            {
                if (!ally.Active) continue;
                // Seen by anyone in the house (Stage 2); each ally counts once.
                for (int i = 0; i < session.HumanCount; i++)
                {
                    if (!session.HumanPresent(i)) continue;
                    var h = session.HumanAt(i);
                    if (h.State == HumanAIController.HumanState.Attack || h == human && human.Suspended && CurrentAction >= Activity.SprayWindup) continue;
                    Vector3 delta = ally.Position - h.transform.position; delta.y = 0;
                    if (delta.sqrMagnitude > h.SightRange * h.SightRange || Vector3.Angle(h.transform.forward, delta) > h.SightAngle * 0.5f) continue;
                    if (Physics.Linecast(h.Eye, ally.Position + Vector3.up * 0.2f, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                    VisibleAllies++;
                    RecordSighting(ally.Position, 0.5f);
                    break;
                }
            }
            if (VisibleAllies > 0)
            {
                // The fear felt grows with at most AllyFearCap of them; the terror gauge still counts every one.
                float all = VisibleAllies * settings.fearPerVisibleAllySecond * step, felt = Mathf.Min(VisibleAllies, AllyFearCap) * settings.fearPerVisibleAllySecond * step;
                AddFear(felt);
                FearGained += all - felt;
                if (CurrentLevel == Level.Panic && !alliesSeenInPanic && VisibleAllies >= 2) { alliesSeenInPanic = true; Say("うわああ！ 増えてる！"); }
            }
        }

        public void RecordSighting(Vector3 position, float weight)
        {
            int zone = Zone(position);
            heat[zone] += weight;
            lastSeenIn[zone] = position;
            lastSightingAt = Time.time;
        }
        private int Zone(Vector3 p)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((p.x - HeatArea.xMin) / HeatArea.width * ZonesX), 0, ZonesX - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((p.z - HeatArea.yMin) / HeatArea.height * ZonesZ), 0, ZonesZ - 1);
            return z * ZonesX + x;
        }

        private void EvaluateLevel()
        {
            Level next = CurrentLevel;
            int seen = session.Discoveries;
            if (next == Level.Peace && (seen >= 1 || Fear >= 15)) next = Level.Noticed;
            if (next == Level.Noticed && (seen - alertBase >= settings.alertDiscoveries || Fear >= settings.alertFear)) next = Level.Alert;
            if (next == Level.Alert && Fear >= settings.panicFear) next = Level.Panic;
            if (next == Level.Panic && Fear < settings.calmFear) next = Level.Alert;   // panic subsides; alertness stays
            bool calming = CalmAfterSeconds > 0 && next == Level.Alert && CurrentLevel == Level.Alert && CurrentAction == Activity.None
                && Fear < (CalmBelowFear > 0 ? CalmBelowFear : settings.alertFear * 0.5f) && Time.time - lastSightingAt >= CalmAfterSeconds && !AnyoneHunting();
            if (calming) { next = Level.Noticed; alertBase = seen; Calmed++; Say("気のせいだったかな…"); }
            if (next == CurrentLevel) return;
            bool rising = next > CurrentLevel;
            CurrentLevel = next;
            if (next > HighestLevel) HighestLevel = next;
            ApplyLevel();
            if (rising) Say(next == Level.Noticed ? "今、何か動いた…？" : next == Level.Alert ? "またか…罠を置いておこう" : "もう無理！ スプレー持ってくる！");
            LevelChanged?.Invoke(next);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Verification: as if nothing had been seen for this long.</summary>
        public void DebugQuiet(float seconds) { lastSightingAt = Time.time - seconds; }
#endif
        private bool AnyoneHunting()
        {
            for (int i = 0; i < session.HumanCount; i++)
                if (session.HumanPresent(i) && session.HumanAt(i).State != HumanAIController.HumanState.Routine) return true;
            return false;
        }

        private static readonly float[] SpeedByLevel = { 1f, 1.1f, 1.15f, 1.25f };
        private static readonly float[] DetectByLevel = { 1f, 0.9f, 0.75f, 0.7f };
        public void ApplyLevel()
        {
            int l = (int)CurrentLevel;
            for (int i = 0; i < session.HumanCount; i++)
            {
                var h = session.HumanAt(i);
                if (!h) continue;
                h.SpeedScale = SpeedByLevel[l] * StageSpeedScale;
                h.DetectionTimeScale = DetectByLevel[l] * StealthScale * StageDetectionScale;
                h.SearchTimeScale = l >= 2 ? 1.5f : 1f;
            }
        }

        public static string LevelName(Level level)
        {
            switch (level)
            {
                case Level.Noticed: return "発見";
                case Level.Alert: return "警戒";
                case Level.Panic: return "パニック";
                default: return "平和";
            }
        }

        // ---- Countermeasures ----
        private void Act(float dt)
        {
            actionTime += dt;
            switch (CurrentAction)
            {
                case Activity.None:
                    if (CanSpray()) { BeginSpray(); return; }
                    if (CurrentLevel >= Level.Alert && human.State == HumanAIController.HumanState.Routine)
                    {
                        trapClock += dt;
                        if (trapClock >= (CurrentLevel == Level.Panic ? settings.trapIntervalPanic : settings.trapIntervalAlert)) { trapClock = 0; BeginPlacement(); }
                    }
                    break;
                case Activity.Walking:
                    if (human.SensePlayer() || actionTime > 14f) { Resume(); return; }
                    if (!agent.pathPending && agent.remainingDistance < 0.6f) { Begin(Activity.Placing); agent.isStopped = true; Say("ここに置いとくか…"); }
                    break;
                case Activity.Placing:
                    if (actionTime >= 1.2f) { Place(placeAt); Resume(); }
                    break;
                case Activity.SprayWindup:
                    human.transform.rotation = Quaternion.RotateTowards(human.transform.rotation, Quaternion.LookRotation(spray.Direction), 540 * dt);
                    if (actionTime >= settings.sprayWindup) { spray.Fire(); Begin(Activity.Spraying); }
                    break;
                case Activity.Spraying:
                    SprayDamage();
                    if (actionTime >= settings.sprayDuration) { spray.Stop(); Begin(Activity.SprayRecover); }
                    break;
                case Activity.SprayRecover:
                    if (actionTime >= 0.45f) { sprayReadyAt = Time.time + settings.sprayCooldown; Resume(); }
                    break;
            }
        }

        private void Begin(Activity next) { CurrentAction = next; actionTime = 0; }
        private void Resume()
        {
            spray.Stop();
            CurrentAction = Activity.None;
            human.Suspended = false;
            if (agent.isOnNavMesh) agent.isStopped = false;
        }

        // Spray only at mid range, on a target the resident can see, and never on the worktop or high walls.
        private bool CanSpray()
        {
            if (CurrentLevel < Level.Panic || Time.time < sprayReadyAt || human.State != HumanAIController.HumanState.Chase || !human.CanSeePlayer) return false;
            Vector3 body = session.Motor.BodyCentre;
            Vector3 d = body - human.transform.position; d.y = 0;
            return body.y < 2.3f && d.magnitude >= settings.sprayMinRange && d.magnitude <= settings.sprayMaxRange - 0.4f && !session.InSafeNest;
        }

        public void BeginSpray()
        {
            human.Suspended = true;
            agent.isStopped = true;
            Vector3 d = session.Motor.BodyCentre - human.transform.position; d.y = 0;
            Vector3 dir = d.sqrMagnitude > 1e-4f ? d.normalized : human.transform.forward;
            // The aim is fixed when the warning appears: stepping out of the fan in time is always possible.
            spray.Telegraph(human.transform.position + dir * 0.55f + Vector3.up * 2.2f, dir, settings.sprayMaxRange, settings.sprayHalfAngle);
            Begin(Activity.SprayWindup);
            Say("くらえっ！");
            Learned?.Invoke("spray");
        }

        private void SprayDamage()
        {
            if (spray.Hits(session.Motor.BodyCentre, human.transform) && session.TryDamage(1)) SprayHits++;
            foreach (var ally in commander.Allies)
                if (ally.Active && spray.Hits(ally.Position + Vector3.up * 0.3f, human.transform))
                    ally.Crush(commander.Settings3.allyRespawnSeconds);
        }

        // The zone with the most sightings (not the same one twice in a row), at the last place a roach was
        // seen there, moved to free floor that is reachable, away from the nest, food and other traps.
        public bool BeginPlacement()
        {
            int zone = -1; float best = 0.01f;
            for (int i = 0; i < heat.Length; i++)
                if (heat[i] > best && (i != lastZone || CountHeat() == 1)) { best = heat[i]; zone = i; }
            if (zone < 0) return false;
            if (!FindSpot(lastSeenIn[zone], out placeAt)) { heat[zone] *= 0.5f; return false; }
            if (!NavMesh.SamplePosition(placeAt, out NavMeshHit stand, 1.6f, agent.areaMask)) return false;
            lastZone = zone;
            heat[zone] *= 0.4f;
            human.Suspended = true;
            agent.isStopped = false;
            agent.speed = session.Settings.patrolSpeed * human.SpeedScale;
            agent.SetDestination(stand.position);
            Begin(Activity.Walking);
            return true;
        }
        private int CountHeat() { int n = 0; foreach (float h in heat) if (h > 0.01f) n++; return n; }

        public bool FindSpot(Vector3 around, out Vector3 spot)
        {
            for (int ring = 0; ring < 5; ring++)
                for (int k = 0; k < 8; k++)
                {
                    float a = k * 45f + ring * 20f;
                    spot = around + Quaternion.Euler(0, a, 0) * Vector3.forward * (ring * 0.6f);
                    spot.y = 0;
                    if (SpotAllowed(spot)) return true;
                    if (ring == 0) break;
                }
            spot = around;
            return false;
        }

        public bool SpotAllowed(Vector3 p)
        {
            var grid = commander.Grid;
            if (!grid.Walkable(p) || grid.IsSticky(p)) return false;
            // A whole trap's footprint must be free floor so it never closes a passage.
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if (!grid.Walkable(p + new Vector3(dx * 0.5f, 0, dz * 0.4f))) return false;
            if (Vector3.Distance(p, commander.NestGround) < 2.6f) return false;
            foreach (var food in commander.Foods) if (food.gameObject.activeSelf && Flat(food.transform.position - p).magnitude < 1.0f) return false;
            foreach (var c in placed) if (c.gameObject.activeSelf && Flat(c.transform.position - p).magnitude < 1.6f) return false;
            return Physics.OverlapSphereNonAlloc(p + Vector3.up * 0.3f, 0.25f, probe, ~(1 << 2), QueryTriggerInteraction.Collide) == 0;
        }

        // Alternates poison bait and sticky sheets; the oldest placed hazard is recycled beyond the limit.
        public Component Place(Vector3 at)
        {
            bool bait = placements % 2 == 0;
            placements++;
            Component item = bait ? (Component)Free(baitPool) : Free(trapPool);
            if (!item && placed.Count > 0) { item = Oldest(bait); }
            if (!item) return null;
            placed.Remove(item);
            if (placed.Count >= settings.maxPlacedHazards) Retire(placed[0]);
            if (item is PoisonBait b)
            {
                int scout = commander.NearestScoutPoint(at);
                b.Arm(at, scout);
                commander.Unscout(scout);
            }
            else
            {
                item.transform.position = at;
                item.gameObject.SetActive(true);
                RebuildOverlays();
            }
            placed.Add(item);
            TrapsPlaced++;
            Announce?.Invoke(bait ? "人間が何かを置いた…  食料に見えても注意（2 探索で確かめよう）" : "人間が粘着シートを仕掛けた！");
            return item;
        }
        private Component Free<T>(T[] pool) where T : Component
        {
            foreach (var item in pool) if (!item.gameObject.activeSelf) return item;
            return null;
        }
        private Component Oldest(bool bait)
        {
            foreach (var c in placed) if (bait == c is PoisonBait) return c;
            return null;
        }
        private void Retire(Component item)
        {
            placed.Remove(item);
            if (item is PoisonBait b) b.Disarm(); else item.gameObject.SetActive(false);
            RebuildOverlays();
        }
        private void RebuildOverlays()
        {
            commander.Grid.ClearOverlays();
            foreach (var t in trapPool)
            {
                if (!t.gameObject.activeSelf) continue;
                var bounds = t.GetComponent<BoxCollider>().bounds;
                bounds.Expand(new Vector3(0.5f, 0, 0.5f));
                commander.Grid.AddStickyArea(bounds);
            }
        }

        // ---- Poison bait ----
        private void IdentifyBaits()
        {
            foreach (var bait in baitPool)
            {
                if (!bait.IsArmed || bait.Identified) continue;
                bool found = bait.ScoutIndex >= 0 && commander.IsScouted(bait.ScoutIndex);
                if (!found)
                    foreach (var ally in commander.Allies)
                        if (ally.Active && Flat(ally.Position - bait.transform.position).sqrMagnitude < 1.6f * 1.6f) { found = true; break; }
                if (!found) continue;
                bait.Identify();
                Learned?.Invoke("bait");
                Announce?.Invoke("仲間が毒餌を見破った！  黄緑ではなく橙の目印に注意");
            }
        }

        /// <summary>The player takes a bait as if it were food: one HP, and the bait is gone.</summary>
        public PoisonBait NearbyBait(Vector3 position, float range)
        {
            foreach (var bait in baitPool)
                if (bait.IsArmed && Flat(bait.transform.position - position).magnitude <= range) return bait;
            return null;
        }
        public void EatBait(PoisonBait bait)
        {
            session.TryDamage(1);
            placed.Remove(bait);
            bait.Disarm();
            Learned?.Invoke("bait");
            Announce?.Invoke("毒餌だった！  生命力 -1");
        }

        public void Say(string text) { Bubble = text; bubbleUntil = Time.time + 2.4f; Speaker = null; }
        /// <summary>A line from another member of the household (Stage 2): the bubble appears over them.</summary>
        public void SayAs(HumanAIController who, string text) { Say(text); Speaker = who == human ? null : who; }
        /// <summary>Who the bubble belongs to: null = the resident.</summary>
        public HumanAIController Speaker { get; private set; }
        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
