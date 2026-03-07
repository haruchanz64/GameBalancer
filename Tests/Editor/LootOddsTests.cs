using System.Collections.Generic;
using GameBalancer.Loot;
using NUnit.Framework;

namespace GameBalancer.Tests
{
    public sealed class LootOddsTests
    {
        [Test]
        public void LootOddsAnalyzer_MonteCarloConvergesToTheoreticalWithoutPity()
        {
            var entries = new List<WeightedLootEntry>
            {
                new WeightedLootEntry { itemId = "a", displayName = "Common", rarity = "Common", weight = 70f, valueScore = 10f },
                new WeightedLootEntry { itemId = "b", displayName = "Rare", rarity = "Rare", weight = 25f, valueScore = 50f },
                new WeightedLootEntry { itemId = "c", displayName = "Legendary", rarity = "Legendary", weight = 5f, valueScore = 200f }
            };

            var report = LootOddsAnalyzer.Analyze("NoPity", entries, pityRule: null, trialCount: 40000, targetRarityOverride: "Legendary", seed: 42);
            Assert.AreEqual(70d, report.entries[0].theoreticalPercent, 0.01d);
            Assert.That(report.entries[0].simulatedPercent, Is.InRange(68.5d, 71.5d));
            Assert.That(report.entries[2].simulatedPercent, Is.InRange(4.2d, 5.8d));
            Assert.Greater(report.pullsToTargetP90, report.pullsToTargetP50);
        }

        [Test]
        public void LootRoller_HardPityGuaranteesTargetWithinHardLimit()
        {
            var entries = new List<WeightedLootEntry>
            {
                new WeightedLootEntry { itemId = "c", displayName = "Common", rarity = "Common", weight = 99f, valueScore = 1f },
                new WeightedLootEntry { itemId = "l", displayName = "Legend", rarity = "Legendary", weight = 1f, valueScore = 100f }
            };

            var pity = new PityRule
            {
                enabled = true,
                targetRarity = "Legendary",
                softPityStartPull = 15,
                softPityBonusPerPull = 0.08f,
                hardPityPull = 20
            };

            var report = LootOddsAnalyzer.Analyze("WithPity", entries, pity, trialCount: 5000, seed: 77);
            Assert.LessOrEqual(report.pullsToTargetMax, 20);
            Assert.Greater(report.pityTriggeredCount, 0);
        }

        [Test]
        public void LootRoller_RollWithoutReplacementReturnsUniqueItemsAndPrdConverges()
        {
            var entries = new List<WeightedLootEntry>
            {
                new WeightedLootEntry { itemId = "1", displayName = "One", weight = 10f },
                new WeightedLootEntry { itemId = "2", displayName = "Two", weight = 20f },
                new WeightedLootEntry { itemId = "3", displayName = "Three", weight = 30f }
            };

            var roller = new LootRoller(seed: 12);
            var batch = roller.RollWithoutReplacement(entries, 3);
            Assert.AreEqual(3, batch.Count);
            var ids = new HashSet<string>();
            for (int i = 0; i < batch.Count; i++) ids.Add(batch[i].Entry.itemId);
            Assert.AreEqual(3, ids.Count);

            // PRD convergence for p = 0.25
            int misses = 0;
            int procs = 0;
            int trials = 20000;
            for (int i = 0; i < trials; i++)
            {
                if (roller.RollPseudoRandomProc(0.25d, ref misses)) procs++;
            }

            double rate = procs / (double)trials;
            Assert.That(rate, Is.InRange(0.23d, 0.27d));
        }
    }
}
