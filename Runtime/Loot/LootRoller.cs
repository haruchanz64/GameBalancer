using System;
using System.Collections.Generic;

namespace GameBalancer.Loot
{
    public readonly struct LootDropResult
    {
        public readonly WeightedLootEntry Entry;
        public readonly int Quantity;
        public readonly bool TriggeredPity;

        public LootDropResult(WeightedLootEntry entry, int quantity, bool triggeredPity)
        {
            Entry = entry;
            Quantity = quantity;
            TriggeredPity = triggeredPity;
        }
    }

    /// <summary>
    /// Tracks consecutive non-target pulls for soft and hard pity evaluation.
    /// </summary>
    [Serializable]
    public sealed class PityState
    {
        public int pullsSinceTarget;

        public void Reset() => pullsSinceTarget = 0;
    }

    /// <summary>
    /// Deterministic-seedable loot roller supporting weighted drops, soft/hard pity,
    /// draw-without-replacement batches, and Pseudo-Random Distribution (PRD) procs.
    /// </summary>
    public sealed class LootRoller
    {
        private readonly System.Random _rng;

        public LootRoller(int? seed = null)
        {
            _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        public LootDropResult Roll(LootTableAsset table, PityState pityState = null)
        {
            if (table == null || table.entries == null || table.entries.Count == 0)
            {
                return default;
            }

            return Roll(table.entries, table.pityRule, pityState);
        }

        public LootDropResult Roll(
            IReadOnlyList<WeightedLootEntry> entries,
            PityRule pityRule = null,
            PityState pityState = null)
        {
            if (entries == null || entries.Count == 0)
            {
                return default;
            }

            bool pityActive = pityRule != null &&
                              pityRule.enabled &&
                              pityState != null &&
                              !string.IsNullOrWhiteSpace(pityRule.targetRarity);

            if (pityActive)
            {
                int nextPullNumber = pityState.pullsSinceTarget + 1;
                double baseTargetProb = ComputeRarityProbability(entries, pityRule.targetRarity);

                if (baseTargetProb > 0d)
                {
                    bool forceTarget = nextPullNumber >= System.Math.Max(1, pityRule.hardPityPull);
                    double effectiveProb = baseTargetProb;

                    if (!forceTarget && nextPullNumber >= pityRule.softPityStartPull)
                    {
                        int stepsIntoSoft = (nextPullNumber - pityRule.softPityStartPull) + 1;
                        effectiveProb = System.Math.Min(1d, baseTargetProb + (stepsIntoSoft * pityRule.softPityBonusPerPull));
                    }

                    if (forceTarget || _rng.NextDouble() < effectiveProb)
                    {
                        var targetEntry = RollWithinRarity(entries, pityRule.targetRarity);
                        if (targetEntry != null)
                        {
                            bool triggeredByPity = forceTarget || nextPullNumber >= pityRule.softPityStartPull;
                            pityState.pullsSinceTarget = 0;
                            return new LootDropResult(targetEntry, RollQuantity(targetEntry), triggeredByPity);
                        }
                    }

                    // Roll from non-target entries so pity doesn't double-count the target rarity
                    var nonTarget = RollExcludingRarity(entries, pityRule.targetRarity);
                    if (nonTarget != null)
                    {
                        pityState.pullsSinceTarget = nextPullNumber;
                        return new LootDropResult(nonTarget, RollQuantity(nonTarget), false);
                    }
                }
            }

            var picked = RollWeighted(entries);
            if (picked == null) return default;

            if (pityState != null && pityRule != null && !string.IsNullOrWhiteSpace(pityRule.targetRarity))
            {
                if (string.Equals(picked.rarity, pityRule.targetRarity, StringComparison.OrdinalIgnoreCase))
                {
                    pityState.pullsSinceTarget = 0;
                }
                else
                {
                    pityState.pullsSinceTarget++;
                }
            }

            return new LootDropResult(picked, RollQuantity(picked), false);
        }

        /// <summary>
        /// Draws up to <paramref name="count"/> unique entries without replacement (for chests or card packs).
        /// </summary>
        public List<LootDropResult> RollWithoutReplacement(IReadOnlyList<WeightedLootEntry> entries, int count)
        {
            var results = new List<LootDropResult>();
            if (entries == null || entries.Count == 0 || count <= 0) return results;

            var pool = new List<WeightedLootEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].weight > 0f)
                {
                    pool.Add(entries[i]);
                }
            }

            int draws = System.Math.Min(count, pool.Count);
            for (int d = 0; d < draws; d++)
            {
                var picked = RollWeighted(pool);
                if (picked == null) break;
                results.Add(new LootDropResult(picked, RollQuantity(picked), false));
                pool.Remove(picked);
            }

            return results;
        }

        /// <summary>
        /// Pseudo-Random Distribution (PRD) proc roll: chance grows linearly with consecutive misses
        /// using constant C calibrated to the target nominal probability p.
        /// </summary>
        public bool RollPseudoRandomProc(double nominalProbability, ref int consecutiveMisses)
        {
            double p = System.Math.Clamp(nominalProbability, 0d, 1d);
            if (p <= 0d) return false;
            if (p >= 1d)
            {
                consecutiveMisses = 0;
                return true;
            }

            double c = ComputePrdConstant(p);
            double chance = System.Math.Min(1d, c * (consecutiveMisses + 1));
            if (_rng.NextDouble() < chance)
            {
                consecutiveMisses = 0;
                return true;
            }

            consecutiveMisses++;
            return false;
        }

        public static double ComputePrdConstant(double targetProbability)
        {
            double p = System.Math.Clamp(targetProbability, 0.0001d, 1d);
            double lo = 0d;
            double hi = p;

            for (int iter = 0; iter < 40; iter++)
            {
                double mid = (lo + hi) * 0.5d;
                double achieved = ComputeExpectedProbabilityFromC(mid);
                if (achieved < p) lo = mid;
                else hi = mid;
            }

            return (lo + hi) * 0.5d;
        }

        private static double ComputeExpectedProbabilityFromC(double c)
        {
            if (c <= 0d) return 0d;
            double pSum = 0d;
            double pFailSoFar = 1d;
            int maxN = (int)System.Math.Ceiling(1d / c);

            for (int n = 1; n <= maxN; n++)
            {
                double curP = System.Math.Min(1d, n * c);
                double pSuccessOnN = pFailSoFar * curP;
                pSum += n * pSuccessOnN;
                pFailSoFar *= (1d - curP);
            }

            return pSum > 0d ? 1d / pSum : 0d;
        }

        private WeightedLootEntry RollWeighted(IReadOnlyList<WeightedLootEntry> entries)
        {
            double total = 0d;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].weight > 0f)
                {
                    total += entries[i].weight;
                }
            }

            if (total <= 0d) return null;

            double roll = _rng.NextDouble() * total;
            double acc = 0d;
            WeightedLootEntry lastValid = null;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.weight <= 0f) continue;
                lastValid = entry;
                acc += entry.weight;
                if (roll < acc)
                {
                    return entry;
                }
            }

            return lastValid;
        }

        private WeightedLootEntry RollWithinRarity(IReadOnlyList<WeightedLootEntry> entries, string rarity)
        {
            var filtered = new List<WeightedLootEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && e.weight > 0f && string.Equals(e.rarity, rarity, StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(e);
                }
            }
            return RollWeighted(filtered);
        }

        private WeightedLootEntry RollExcludingRarity(IReadOnlyList<WeightedLootEntry> entries, string rarity)
        {
            var filtered = new List<WeightedLootEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && e.weight > 0f && !string.Equals(e.rarity, rarity, StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(e);
                }
            }
            return filtered.Count > 0 ? RollWeighted(filtered) : null;
        }

        private static double ComputeRarityProbability(IReadOnlyList<WeightedLootEntry> entries, string rarity)
        {
            double total = 0d;
            double match = 0d;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.weight <= 0f) continue;
                total += e.weight;
                if (string.Equals(e.rarity, rarity, StringComparison.OrdinalIgnoreCase))
                {
                    match += e.weight;
                }
            }
            return total > 0d ? match / total : 0d;
        }

        private int RollQuantity(WeightedLootEntry entry)
        {
            int min = System.Math.Max(1, entry.minQuantity);
            int max = System.Math.Max(min, entry.maxQuantity);
            return min == max ? min : _rng.Next(min, max + 1);
        }
    }
}
