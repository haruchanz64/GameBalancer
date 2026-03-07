using System;
using System.Collections.Generic;

namespace GameBalancer.Loot
{
    [Serializable]
    public sealed class LootEntryAnalysis
    {
        public string itemId;
        public string displayName;
        public string rarity;
        public float weight;
        public double theoreticalPercent;
        public double simulatedPercent;
        public int dropCount;
        public double deviationPercent;
    }

    [Serializable]
    public sealed class LootAnalysisReport
    {
        public string tableName;
        public int totalRolls;
        public bool pityEnabled;
        public string targetRarity;
        public double expectedValuePerRoll;
        public double simulatedValuePerRoll;
        public int pityTriggeredCount;
        public int targetHitCount;
        public double pullsToTargetMean;
        public int pullsToTargetP50;
        public int pullsToTargetP90;
        public int pullsToTargetP95;
        public int pullsToTargetP99;
        public int pullsToTargetMax;
        public List<LootEntryAnalysis> entries = new List<LootEntryAnalysis>();
    }

    /// <summary>
    /// Runs exact theoretical weight analysis and Monte Carlo roll trials with soft/hard pity
    /// and pulls-to-target percentile calculation.
    /// </summary>
    public static class LootOddsAnalyzer
    {
        public static LootAnalysisReport Analyze(
            LootTableAsset table,
            int trialCount = 50000,
            string targetRarityOverride = null,
            int? seed = null)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            return Analyze(
                table.tableName,
                table.entries,
                table.pityRule,
                trialCount,
                targetRarityOverride,
                seed);
        }

        public static LootAnalysisReport Analyze(
            string tableName,
            IReadOnlyList<WeightedLootEntry> entries,
            PityRule pityRule,
            int trialCount = 50000,
            string targetRarityOverride = null,
            int? seed = null)
        {
            int rolls = System.Math.Max(100, trialCount);
            string targetRarity = !string.IsNullOrWhiteSpace(targetRarityOverride)
                ? targetRarityOverride
                : (pityRule != null ? pityRule.targetRarity : string.Empty);

            var report = new LootAnalysisReport
            {
                tableName = string.IsNullOrWhiteSpace(tableName) ? "Loot Table" : tableName,
                totalRolls = rolls,
                pityEnabled = pityRule != null && pityRule.enabled,
                targetRarity = targetRarity
            };

            if (entries == null || entries.Count == 0)
            {
                return report;
            }

            double totalWeight = 0d;
            double theoreticalEv = 0d;
            var indexByEntry = new Dictionary<WeightedLootEntry, int>(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null) continue;
                double w = System.Math.Max(0f, e.weight);
                totalWeight += w;
                indexByEntry[e] = report.entries.Count;
                report.entries.Add(new LootEntryAnalysis
                {
                    itemId = e.itemId,
                    displayName = e.displayName,
                    rarity = e.rarity,
                    weight = e.weight
                });
            }

            if (totalWeight <= 0d)
            {
                return report;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || !indexByEntry.TryGetValue(e, out int idx)) continue;
                double prob = System.Math.Max(0f, e.weight) / totalWeight;
                report.entries[idx].theoreticalPercent = prob * 100d;
                double avgQty = (System.Math.Max(1, e.minQuantity) + System.Math.Max(e.minQuantity, e.maxQuantity)) * 0.5d;
                theoreticalEv += prob * avgQty * e.valueScore;
            }

            report.expectedValuePerRoll = theoreticalEv;

            var roller = new LootRoller(seed);
            var pityState = new PityState();
            var pullsBetweenTargetHits = new List<int>(System.Math.Max(16, rolls / 50));
            int streak = 0;
            double simValueTotal = 0d;

            for (int r = 0; r < rolls; r++)
            {
                var drop = roller.Roll(entries, pityRule, pityState);
                if (drop.Entry == null) continue;

                streak++;
                simValueTotal += drop.Quantity * drop.Entry.valueScore;
                if (drop.TriggeredPity) report.pityTriggeredCount++;

                if (indexByEntry.TryGetValue(drop.Entry, out int idx))
                {
                    report.entries[idx].dropCount++;
                }

                if (!string.IsNullOrWhiteSpace(targetRarity) &&
                    string.Equals(drop.Entry.rarity, targetRarity, StringComparison.OrdinalIgnoreCase))
                {
                    pullsBetweenTargetHits.Add(streak);
                    streak = 0;
                    report.targetHitCount++;
                }
            }

            report.simulatedValuePerRoll = simValueTotal / rolls;

            for (int i = 0; i < report.entries.Count; i++)
            {
                var ea = report.entries[i];
                ea.simulatedPercent = (ea.dropCount / (double)rolls) * 100d;
                ea.deviationPercent = ea.simulatedPercent - ea.theoreticalPercent;
            }

            if (pullsBetweenTargetHits.Count > 0)
            {
                pullsBetweenTargetHits.Sort();
                long sum = 0;
                for (int i = 0; i < pullsBetweenTargetHits.Count; i++) sum += pullsBetweenTargetHits[i];

                report.pullsToTargetMean = sum / (double)pullsBetweenTargetHits.Count;
                report.pullsToTargetP50 = Percentile(pullsBetweenTargetHits, 0.50d);
                report.pullsToTargetP90 = Percentile(pullsBetweenTargetHits, 0.90d);
                report.pullsToTargetP95 = Percentile(pullsBetweenTargetHits, 0.95d);
                report.pullsToTargetP99 = Percentile(pullsBetweenTargetHits, 0.99d);
                report.pullsToTargetMax = pullsBetweenTargetHits[pullsBetweenTargetHits.Count - 1];
            }

            return report;
        }

        private static int Percentile(List<int> sorted, double p)
        {
            if (sorted.Count == 0) return 0;
            int idx = (int)System.Math.Ceiling(p * sorted.Count) - 1;
            idx = System.Math.Clamp(idx, 0, sorted.Count - 1);
            return sorted[idx];
        }
    }
}
