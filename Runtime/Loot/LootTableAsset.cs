using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameBalancer.Loot
{
    [Serializable]
    public sealed class WeightedLootEntry
    {
        public string itemId = "item_common";
        public string displayName = "Iron Scrap";
        public string rarity = "Common";
        [Min(0f)] public float weight = 50f;
        [Min(1)] public int minQuantity = 1;
        [Min(1)] public int maxQuantity = 1;
        public float valueScore = 10f;
        public UnityEngine.Object assetReference;
    }

    [Serializable]
    public sealed class PityRule
    {
        public bool enabled = false;
        public string targetRarity = "Legendary";
        [Min(1)] public int softPityStartPull = 60;
        [Range(0f, 1f)] public float softPityBonusPerPull = 0.05f;
        [Min(1)] public int hardPityPull = 80;
    }

    /// <summary>
    /// Reusable ScriptableObject defining a weighted loot table with optional soft/hard pity rules.
    /// </summary>
    [CreateAssetMenu(fileName = "LootTable", menuName = "GameBalancer/Loot Table", order = 101)]
    public class LootTableAsset : ScriptableObject
    {
        public string tableName = "Standard Chest";
        public List<WeightedLootEntry> entries = new List<WeightedLootEntry>
        {
            new WeightedLootEntry { itemId = "common_scrap", displayName = "Iron Scrap", rarity = "Common", weight = 65f, valueScore = 10f },
            new WeightedLootEntry { itemId = "uncommon_gem", displayName = "Mana Shard", rarity = "Uncommon", weight = 25f, valueScore = 30f },
            new WeightedLootEntry { itemId = "rare_blade", displayName = "Knight Blade", rarity = "Rare", weight = 8.5f, valueScore = 120f },
            new WeightedLootEntry { itemId = "legendary_relic", displayName = "Sunfire Relic", rarity = "Legendary", weight = 1.5f, valueScore = 600f }
        };

        public PityRule pityRule = new PityRule
        {
            enabled = true,
            targetRarity = "Legendary",
            softPityStartPull = 50,
            softPityBonusPerPull = 0.04f,
            hardPityPull = 75
        };

        public double GetTotalWeight()
        {
            double total = 0d;
            if (entries == null) return 0d;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].weight > 0f)
                {
                    total += entries[i].weight;
                }
            }
            return total;
        }
    }
}
