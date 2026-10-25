using System;
using System.Collections.Generic;
using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Phase 4 colony loop: deliveries become a food stock; at the nest (E) the stock buys allies, a bigger
    /// nest and upgrades (spec 5.2-5.4). Delivered food reappears elsewhere after a while so the loop never
    /// runs dry. Also owns the saved settings / encyclopedia and applies them.
    /// </summary>
    [DefaultExecutionOrder(-130)]   // before SwarmCommander, so the ally count is set before its reset
    public sealed class ColonyManager : MonoBehaviour
    {
        [SerializeField] private PhaseTwoSession session;
        [SerializeField] private SwarmCommander commander;
        [SerializeField] private HumanReaction reaction;
        [SerializeField] private ColonySettings settings;
        [SerializeField] private Vector3[] respawnSpots;
        [SerializeField] private PolishedPresentation presentation;
        private FoodItem[] foods;
        private FoodItem.FoodState[] previous;
        private float[] respawnAt;
        private int[] levels;
        private System.Random random = new System.Random(4);
        private float messageUntil;
        public ColonySettings Settings => settings;
        public GameSettings Options { get; private set; }
        public int Food { get; private set; }
        public int TotalCollected { get; private set; }
        public int NestLevel { get; private set; } = 1;
        public int MaxNestLevel => settings.allyCapByNestLevel.Length;
        public int AllyCap => settings.allyCapByNestLevel[Mathf.Clamp(NestLevel - 1, 0, MaxNestLevel - 1)];
        public int Allies => commander.ActiveAllyCount;
        public int BreedCost => Mathf.Max(1, settings.breedCost - Level(ColonySettings.UpgradeKind.Breeding));
        public int UpgradesBought { get; private set; }
        public int Born { get; private set; }
        public bool NestMenuOpen { get; private set; }
        public string Message { get; private set; }
        public bool MessageVisible => Time.unscaledTime < messageUntil;
        public HumanReaction Reaction => reaction;
        // Phase 5 clears on the stage gauges instead of colony size. Defaults keep Phase 4 unchanged.
        public bool ClearsOnColonySize { get; set; } = true;
        public string MaxNestNote { get; set; } = "（施設はPhase 5）";
        // Allies at the start of a run; negative keeps the colony settings (Phase 5 starts with the player alone).
        public int StartAlliesOverride { get; set; } = -1;
        public int StartAllies => StartAlliesOverride >= 0 ? StartAlliesOverride : settings.startAllies;
        public event Action NestClosed;

        public void Configure(PhaseTwoSession game, SwarmCommander swarm, HumanReaction resident, ColonySettings config, Vector3[] spots, PolishedPresentation look)
        { session = game; commander = swarm; reaction = resident; settings = config; respawnSpots = spots; presentation = look; }

        private void Awake()
        {
            foods = new FoodItem[commander.Foods.Count];
            for (int i = 0; i < foods.Length; i++) foods[i] = commander.Foods[i];
            previous = new FoodItem.FoodState[foods.Length];
            respawnAt = new float[foods.Length];
            levels = new int[settings.upgrades.Length];
            commander.AllyLimitOverride = StartAllies;
            session.RunReset += ResetColony;
            reaction.Learned += Learn;
            reaction.Announce += Say;
            reaction.LevelChanged += level =>
            {
                if (level == HumanReaction.Level.Alert) Say("人間が「警戒」している！  罠や毒餌を置き始める");
                else if (level == HumanReaction.Level.Panic) Say("人間が「パニック」！  殺虫スプレーに注意");
            };
            Options = GameSettings.Load();
            if (GameSettings.LastLoadMessage != null) Say(GameSettings.LastLoadMessage);
        }
        private void Start()
        {
            // After every Awake (SwarmCommander sets its own goal there): this phase clears on colony size,
            // never on a delivery count.
            session.DeliveryGoalOverride = int.MaxValue;
            ApplyOptions();
            Learn("resident");
        }
        private void OnDestroy() { if (session) session.RunReset -= ResetColony; }

        public void ResetColony()
        {
            Food = TotalCollected = UpgradesBought = Born = 0;
            NestLevel = 1;
            System.Array.Clear(levels, 0, levels.Length);
            random = new System.Random(4);
            for (int i = 0; i < foods.Length; i++) { previous[i] = foods[i].State; respawnAt[i] = -1; }
            commander.AllyLimitOverride = StartAllies;
            NestMenuOpen = false;
            ApplyUpgrades();
        }

        private void Update()
        {
            if (NestMenuOpen && session.State != PhaseTwoSession.RunState.Paused) NestMenuOpen = false;
            if (!session.IsRunning) return;
            var interactor = session.Interactor;
            // Runs before PlayerInteractor: E at the nest with empty jaws opens the nest; near poison bait it is eaten.
            if (session.CanControl && Input.GetKeyDown(KeyCode.E) && !interactor.Carried && !interactor.NearbyFood)
            {
                if (interactor.AtNest) { OpenNestMenu(); return; }
                var bait = reaction.NearbyBait(session.Player.position, session.Settings.interactionRange);
                if (bait) reaction.EatBait(bait);
            }
            for (int i = 0; i < foods.Length; i++)
            {
                var state = foods[i].State;
                if (state == FoodItem.FoodState.Delivered && previous[i] != FoodItem.FoodState.Delivered) Collected(i);
                previous[i] = state;
                if (respawnAt[i] >= 0 && Time.time >= respawnAt[i]) Respawn(i);
            }
            if (session.IsSticky) Learn("sticky");
            if (ClearsOnColonySize && Allies >= settings.clearAllies) session.Complete();
        }

        private void Collected(int index)
        {
            int amount = FoodValue.AmountOf(foods[index]);
            Food += amount;
            TotalCollected += amount;
            reaction.AddFear(settings.fearPerFood);
            if (foods[index].TryGetComponent(out FoodValue value)) Learn("food_" + value.FoodKind);
            respawnAt[index] = Time.time + settings.foodRespawnSeconds;
        }

        // A new crumb appears at a free spot away from the player; it is visible only if that area is scouted.
        private void Respawn(int index)
        {
            var candidates = new List<Vector3>();
            foreach (var spot in respawnSpots)
            {
                if (Vector3.Distance(spot, session.Player.position) < 3f) continue;
                bool taken = false;
                foreach (var f in foods) if (f.gameObject.activeSelf && f.State != FoodItem.FoodState.Delivered && Vector3.Distance(f.transform.position, spot) < 1.2f) { taken = true; break; }
                foreach (var b in reaction.Baits) if (b.IsArmed && Vector3.Distance(b.transform.position, spot) < 1.2f) { taken = true; break; }
                if (!taken) candidates.Add(spot);
            }
            if (candidates.Count == 0) { respawnAt[index] = Time.time + 5; return; }
            Vector3 at = candidates[random.Next(candidates.Count)];
            var food = foods[index];
            food.ResetItem();
            food.transform.position = at;
            respawnAt[index] = -1;
            previous[index] = FoodItem.FoodState.Available;
            int scout = commander.NearestScoutPoint(at);
            commander.Reveal(food, at.y < 0.5f ? commander.IsScouted(scout) : true);
        }

        /// <summary>
        /// Stage 2: a crumb or leftover the family drops appears where it was placed and is known at once. Only one that
        /// is hidden or waiting to reappear elsewhere can drop; false while it still lies somewhere (or is carried).
        /// </summary>
        public bool DropFood(FoodItem food)
        {
            int index = System.Array.IndexOf(foods, food);
            if (index < 0 || food.State == FoodItem.FoodState.Available || food.State == FoodItem.FoodState.Carried) return false;
            food.ResetItem();
            respawnAt[index] = -1;
            previous[index] = FoodItem.FoodState.Available;
            commander.Reveal(food, true);
            return true;
        }

        // ---- Nest ----
        public void OpenNestMenu()
        {
            if (!session.IsRunning) return;
            session.Pause();
            NestMenuOpen = true;
        }
        public void CloseNestMenu() { if (NestMenuOpen) { NestMenuOpen = false; session.Resume(); NestClosed?.Invoke(); } }

        public bool TryBreed()
        {
            if (Allies >= AllyCap) return Fail(NestLevel < MaxNestLevel ? "巣がいっぱいです — 巣を拡張しよう" : "これ以上は増やせません");
            if (Food < BreedCost) return Fail("食料が足りません（あと" + (BreedCost - Food) + "）");
            if (!commander.AddAlly()) return Fail("これ以上は増やせません");
            Food -= BreedCost;
            Born++;
            reaction.AddFear(settings.fearPerBreed);
            Say("仲間が1匹増えた！  （" + Allies + "匹）");
            if (ClearsOnColonySize && Allies >= settings.clearAllies) { NestMenuOpen = false; session.Complete(); }
            return true;
        }

        public bool TryExpand()
        {
            if (NestLevel >= MaxNestLevel) return Fail("巣はこれ以上広げられません" + MaxNestNote);
            if (Food < settings.expandCost) return Fail("食料が足りません（あと" + (settings.expandCost - Food) + "）");
            Food -= settings.expandCost;
            NestLevel++;
            Say("巣を拡張した！  仲間の上限 " + AllyCap + "匹");
            return true;
        }

        public int Level(ColonySettings.UpgradeKind kind)
        {
            for (int i = 0; i < settings.upgrades.Length; i++) if (settings.upgrades[i].kind == kind) return levels[i];
            return 0;
        }
        public int LevelAt(int index) => levels[index];
        public int[] UpgradeLevels => (int[])levels.Clone();

        /// <summary>Checkpoint restore (Phase 5), applied right after a reset: stock, nest, upgrades and colony size.</summary>
        public void Restore(int food, int totalCollected, int nestLevel, int[] upgradeLevels, int allies, int upgradesBought, int born)
        {
            Food = Mathf.Max(0, food);
            TotalCollected = Mathf.Max(0, totalCollected);
            NestLevel = Mathf.Clamp(nestLevel, 1, MaxNestLevel);
            UpgradesBought = Mathf.Max(0, upgradesBought);
            Born = Mathf.Max(0, born);
            for (int i = 0; i < levels.Length; i++)
                levels[i] = upgradeLevels != null && i < upgradeLevels.Length ? Mathf.Clamp(upgradeLevels[i], 0, settings.upgrades[i].maxLevel) : 0;
            int durability = Level(ColonySettings.UpgradeKind.Durability);
            if (durability > 0) session.AddMaxHp(durability);
            int target = Mathf.Clamp(allies, 0, AllyCap);
            commander.AllyLimitOverride = Mathf.Max(StartAllies, target);
            while (Allies < target && commander.AddAlly()) { }
            ApplyUpgrades();
        }
        public int CostAt(int index) => settings.upgrades[index].CostAt(levels[index]);
        public bool IsMaxed(int index) => levels[index] >= settings.upgrades[index].maxLevel;

        /// <summary>Spends food only when the upgrade can actually be bought; otherwise explains why.</summary>
        public bool TryUpgrade(int index)
        {
            var up = settings.upgrades[index];
            if (IsMaxed(index)) return Fail(up.name + "は最大です");
            int cost = CostAt(index);
            if (Food < cost) return Fail("食料が足りません（あと" + (cost - Food) + "）");
            Food -= cost;
            levels[index]++;
            UpgradesBought++;
            if (up.kind == ColonySettings.UpgradeKind.Durability) session.AddMaxHp(1);
            ApplyUpgrades();
            Say(up.name + "が Lv" + levels[index] + " になった！");
            return true;
        }

        private void ApplyUpgrades()
        {
            float speed = 1 + 0.08f * Level(ColonySettings.UpgradeKind.Speed);
            session.Motor.UpgradeSpeed = speed;
            commander.AllySpeedScale = speed;
            int carry = Level(ColonySettings.UpgradeKind.Carry);
            float baseCarry = session.Settings.carryingSpeed;
            session.CarrySpeedOverride = carry > 0 ? Mathf.Lerp(baseCarry, 1f, carry / 2f) : 0;
            reaction.StealthScale = 1 + 0.2f * Level(ColonySettings.UpgradeKind.Stealth);
            reaction.ApplyLevel();
        }

        // ---- Settings & encyclopedia ----
        public void ApplyOptions()
        {
            var o = Options;
            session.FollowCamera.Sensitivity = o.mouseSensitivity;
            session.FollowCamera.ShakeScale = o.cameraShake ? 1 : 0;
            AudioListener.volume = o.masterVolume;
            if (presentation)
            {
                // 中 = the kitchen's own lighting setup; 低 = no shadow map, no MSAA, one pixel light.
                presentation.enabled = false; presentation.enabled = true;
                if (o.quality == 0)
                {
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.pixelLightCount = 1;
                }
            }
        }
        public void LoadOptions() { Options = GameSettings.Load(); ApplyOptions(); }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Verification only: skip the waiting and the grinding.
        public void DebugGrant(int amount) { Food += amount; }
        public void DebugRespawnDue() { for (int i = 0; i < respawnAt.Length; i++) if (respawnAt[i] >= 0) respawnAt[i] = Time.time; }
#endif
        public void SaveOptions() { ApplyOptions(); if (!Options.Save()) Say("設定を保存できませんでした"); }

        public void Learn(string entry)
        {
            if (!Options.Learn(entry)) return;
            Options.Save();
            string name = CodexName(entry);
            if (name != null) commander.Say("図鑑に「" + name + "」を記録した");
        }
        public static string CodexName(string entry)
        {
            switch (entry)
            {
                case "slipper": return "スリッパ";
                case "mother": return "一軒家の母";
                case "son": return "一軒家の息子";
                case "cat": return "飼い猫ミケ";
                case "sticky": return "粘着シート";
                case "spray": return "殺虫スプレー";
                case "bait": return "毒餌";
                case "food_Rice": return "米粒";
                case "food_Sweets": return "お菓子";
                case "food_Leftovers": return "食べ残し";
                case "food_Garbage": return "生ごみ";
                default: return null;
            }
        }

        private bool Fail(string text) { Say(text); return false; }
        public void Say(string text) { Message = text; messageUntil = Time.unscaledTime + 2.8f; if (commander) commander.Say(text); }
    }
}
