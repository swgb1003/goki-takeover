using System;
using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// One stage (spec 9.1-9.4): the three takeover gauges, the required territory, the final defence
    /// and the rating thresholds. Stages that are not built yet keep only their card text (playable = false).
    /// All numbers are provisional and meant to be tuned here.
    /// </summary>
    [CreateAssetMenu(menuName = "GOKI TAKEOVER/Stage Definition")]
    public sealed class StageDefinition : ScriptableObject
    {
        [Serializable] public sealed class Zone
        {
            public string name;
            public Vector3 centre;
            [Min(0.5f)] public float radius = 1.6f;
        }

        [Header("ステージ")]
        public string id = "stage01";
        public string title = "ワンルームのキッチン";
        public string subtitle = "一人暮らしの部屋";
        [TextArea] public string description = "ひとり暮らしの部屋。油断しきった人間のスキを突け。";
        [Range(1, 5)] public int difficulty = 1;
        public int residents = 1;
        public bool playable = true;
        public string unlocks = "stage02";       // stage id opened by clearing this one
        public string unlocksTitle = "一軒家";
        public string reward = "skin_gold";      // cosmetic unlocked by the first clear
        public string rewardTitle = "見た目「金色の甲殻」";
        public string sceneName = "Phase5_Stage01_Kitchen";

        [Header("3つのゲージ（各100%で最終防衛）")]
        [Min(1)] public int foodGoal = 30;                 // delivered food value
        [Min(1)] public float fearGoal = 120f;             // fear added in total (sightings, allies seen, breeding, food, territory)
        public Zone[] zones = new Zone[0];                 // required territory
        [Min(1)] public float holdSeconds = 12f;           // occupation time per zone
        [Min(1)] public int holdPresence = 2;              // roaches needed in a zone (the player counts as one)
        [Min(0)] public float residentClearRate = 0.35f;   // per second of the resident standing in an unheld, unfinished zone
        [Min(0)] public float fearPerZone = 10f;

        [Header("仲間・人間（Phase 5 調整）")]
        [Range(0, 9)] public int startAllies = 0;          // allies at the sortie; the player alone at 0
        [Min(0)] public float allyCorpseSeconds = 25f;     // a fallen ally stays this long as a corpse (0 = respawn at the nest instead)
        [Min(0)] public int corpseHeal = 1;                // health gained by eating a corpse
        [Min(0)] public float slipperReach = 3.6f;         // the slipper hits the worktop and walls up to this height (resident 4.1 tall; 0 = floor only)
        public bool tutorial = true;                       // guided first steps until finished once
        public bool residentHuntsAllies = true;            // the resident also spots, chases and swats allies, not only the player
        [Min(0)] public float allyReactionSeconds = 0.75f; // an ally under a raised slipper freezes 0.6-1.4x this before it runs (windup 0.9; 0 = runs at once)
        // Balance (2026-10-24): a first slipper flips an ally over instead of killing it, its friends scatter at once.
        [Min(0)] public float allyStunSeconds = 2f;        // on its back this long after a first hit (0 = one hit kills)
        [Min(1)] public float allyWoundHealSeconds = 30f;  // the wound heals after this, or at once in the nest
        [Min(0)] public float allyAlarmRadius = 2.2f;      // allies this close to a slipper aimed at a friend run without freezing (0 = off)
        [Min(1)] public float allyCollectReactionScale = 1.6f;   // collectors react this much later and ignore the alarm
        // The exterminator smokes the nest itself (2026-10-24): hiding inside is no longer safe in the final defence.
        public float[] nestFumigations = { 8f, 35f };      // seconds into the final defence when the exterminator heads for the nest
        [Min(1)] public float nestFumigationWarning = 5f, nestFumigationSeconds = 15f;
        [Min(0.5f)] public float nestSmokeDamageEvery = 1.2f;

        [Header("家族（ステージ2: 複数の人間）")]
        [Min(0.3f)] public float humanDetectionScale = 1f;   // every member's detection time x this (< 1: they notice roaches sooner)
        [Min(0.5f)] public float humanSpeedScale = 1f;       // every member's walking and chasing speed x this
        public Rect heatArea = new Rect(-7, -6, 14, 12);     // floor plan for the trap heat map (x, z, width, depth)
        public bool familyLeavesForDefence = true;           // the family walks out when the exterminator comes

        [Header("最終防衛（反応段階4: 駆除業者）")]
        [Min(10)] public float finalDefenceSeconds = 60f;
        [Min(4)] public float foggerInterval = 13f;
        [Min(1)] public float foggerWarning = 3.5f, foggerDuration = 9f;
        [Min(1)] public float foggerRadius = 3.0f;
        [Min(0.5f)] public float smokeHeight = 2.4f;       // the worktop and high walls stay clear
        [Min(1)] public int maxFoggers = 2;
        public Vector3 exitPoint = new Vector3(6.2f, 0, -3.6f);

        [Header("評価（A ≥ ratingA、B ≥ ratingB）")]
        [Min(1)] public float parSeconds = 720f;
        public int fewDiscoveries = 3, someDiscoveries = 8;
        public int manyAllies = 8, someAllies = 5;
        public int ratingA = 6, ratingB = 3;

        /// <summary>Points: time, stealth, survivors, food (2 each); a successor costs one. 6+ = A, 3+ = B.</summary>
        public string Rate(float seconds, int discoveries, int allies, int food, int successors, out int points)
        {
            points = 0;
            points += seconds <= parSeconds ? 2 : seconds <= parSeconds * 1.5f ? 1 : 0;
            points += discoveries <= fewDiscoveries ? 2 : discoveries <= someDiscoveries ? 1 : 0;
            points += allies >= manyAllies ? 2 : allies >= someAllies ? 1 : 0;
            points += food >= foodGoal * 1.3f ? 2 : food >= foodGoal ? 1 : 0;
            points -= successors;
            return points >= ratingA ? "A" : points >= ratingB ? "B" : "C";
        }
    }
}
