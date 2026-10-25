using System;
using UnityEngine;

namespace GokiTakeover
{
    /// <summary>Nest, breeding, upgrades and the resident's reactions (spec 5.2-5.4, 6.2). All values are provisional.</summary>
    [CreateAssetMenu(menuName = "GOKI TAKEOVER/Colony Settings")]
    public sealed class ColonySettings : ScriptableObject
    {
        public enum UpgradeKind { Speed, Carry, Stealth, Breeding, Durability }
        [Serializable] public sealed class Upgrade
        {
            public UpgradeKind kind;
            public string name;
            public string description;
            [Min(1)] public int baseCost = 5;
            [Min(0)] public int costStep = 3;   // each level costs this much more
            [Range(1, 6)] public int maxLevel = 3;
            public int CostAt(int level) => baseCost + costStep * level;
        }

        [Header("巣・繁殖")]
        [Range(1, 9)] public int startAllies = 4;
        // Ally cap per nest level (index 0 = Lv1). The pool in the scene holds nine.
        public int[] allyCapByNestLevel = { 6, 9 };
        [Min(1)] public int breedCost = 3;
        [Min(1)] public int expandCost = 8;
        [Range(4, 9)] public int clearAllies = 9;
        [Min(1)] public float foodRespawnSeconds = 20f;

        [Header("強化")]
        public Upgrade[] upgrades =
        {
            new Upgrade { kind = UpgradeKind.Speed, name = "移動速度", description = "主人公と仲間が速く動ける（1段階 +8%）", baseCost = 5, costStep = 3, maxLevel = 3 },
            new Upgrade { kind = UpgradeKind.Carry, name = "運搬力", description = "食料を運ぶ時の減速が小さくなる", baseCost = 5, costStep = 3, maxLevel = 2 },
            new Upgrade { kind = UpgradeKind.Stealth, name = "隠密性", description = "人間に見つかるまでの時間が延びる（1段階 +20%）", baseCost = 6, costStep = 3, maxLevel = 3 },
            new Upgrade { kind = UpgradeKind.Breeding, name = "繁殖力", description = "仲間を増やすのに必要な食料が1減る", baseCost = 8, costStep = 4, maxLevel = 1 },
            new Upgrade { kind = UpgradeKind.Durability, name = "耐久力", description = "主人公の生命力の上限が1増える", baseCost = 6, costStep = 4, maxLevel = 2 },
        };

        [Header("人間の反応（恐怖度 0-100）")]
        [Min(0)] public float fearPerDiscovery = 12f;
        [Min(0)] public float fearPerVisibleAllySecond = 1.6f;
        [Min(0)] public float fearPerFood = 1.5f;
        [Min(0)] public float fearPerBreed = 4f;
        [Min(0)] public float fearDecayPerSecond = 0.35f;
        public float alertFear = 35f, panicFear = 70f, calmFear = 50f;
        [Min(1)] public int alertDiscoveries = 3;
        [Min(5)] public float trapIntervalAlert = 35f, trapIntervalPanic = 24f;
        [Min(1)] public int maxPlacedHazards = 4;

        [Header("殺虫スプレー")]
        public float sprayMinRange = 1.4f, sprayMaxRange = 3.8f;
        [Range(10, 60)] public float sprayHalfAngle = 24f;
        [Min(0.2f)] public float sprayWindup = 0.8f, sprayDuration = 1.6f, sprayCooldown = 7f;
    }
}
