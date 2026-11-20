using System;
using System.Collections.Generic;

namespace GameBalancer.Loot
{
    /// <summary>
    /// Outcome of a single loot roll, including the selected entry, rolled stack quantity, and pity flag.
    /// </summary>
    public readonly struct LootDropResult
    {
        /// <summary>Selected loot entry, or <c>null</c> when the table has no positive-weight entries.</summary>
        public readonly WeightedLootEntry Entry;
        /// <summary>Rolled item quantity between the entry's <c>minQuantity</c> and <c>maxQuantity</c> (inclusive).</summary>
        public readonly int Quantity;
        /// <summary><c>true</c> if this drop occurred on or after the configured soft/hard pity threshold.</summary>
        public readonly bool TriggeredPity;

        /// <summary>
        /// Creates a new loot drop result snapshot.
        /// </summary>
        /// <param name="entry">Selected loot entry.</param>
        /// <param name="quantity">Rolled item count.</param>
        /// <param name="triggeredPity">Whether soft or hard pity was active on the winning pull.</param>
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

        /// <summary>
        /// Resets <see cref="pullsSinceTarget"/> to zero.
        /// </summary>
        public void Reset() => pullsSinceTarget = 0;
    }

    /// <summary>
    /// Deterministic-seedable loot roller supporting weighted drops, soft/hard pity,
    /// draw-without-replacement batches, and Pseudo-Random Distribution (PRD) procs.
    /// </summary>
    public sealed class LootRoller
    {
        private readonly System.Random _rng;

        /// <summary>
        /// Initializes a new <see cref="LootRoller"/> with an optional fixed PRNG seed for deterministic replays.
        /// </summary>
        /// <param name="seed">Optional seed passed to <see cref="System.Random"/>; omit for time-based seeding.</param>
        public LootRoller(int? seed = null)
        {
            _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        /// <summary>
        /// Performs a single weighted roll against a <see cref="LootTableAsset"/> using its configured <see cref="PityRule"/>.
        /// </summary>
        /// <param name="table">Loot table asset to roll from.</param>
        /// <param name="pityState">Optional mutable pity counter updated in place; required for soft/hard pity progression.</param>
        /// <returns>The rolled drop, or <c>default</c> if <paramref name="table"/> is null or empty.</returns>
        public LootDropResult Roll(LootTableAsset table, PityState pityState = null)
        {
            if (table == null || table.entries == null || table.entries.Count == 0)
            {
                return default;
            }

            return Roll(table.entries, table.pityRule, pityState);
        }

        /// <summary>
        /// Performs a single weighted roll against raw entries with optional soft and hard pity tracking.
        /// </summary>
        /// <param name="entries">Weighted entries to roll from; entries with weight &lt;= 0 are ignored.</param>
        /// <param name="pityRule">Optional pity configuration boosting or guaranteeing the target rarity.</param>
        /// <param name="pityState">Optional mutable pull streak counter reset on target-rarity hits and incremented on misses.</param>
        /// <returns>The rolled drop, or <c>default</c> if no positive-weight entries exist.</returns>
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
        /// <param name="entries">Candidate weighted entries; only entries with positive weight enter the draw pool.</param>
        /// <param name="count">Maximum number of unique entries to draw.</param>
        /// <returns>List of drawn drops, capped at the number of positive-weight entries.</returns>
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
        /// <param name="nominalProbability">Long-run target trigger probability in [0, 1].</param>
        /// <param name="consecutiveMisses">Mutable miss streak reset to 0 on a proc and incremented on a miss.</param>
        /// <returns><c>true</c> if the proc triggered on this roll; otherwise <c>false</c>.</returns>
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

        /// <summary>
        /// Solves for the Pseudo-Random Distribution base increment constant <c>C</c> whose expected long-run
        /// trigger rate matches <paramref name="targetProbability"/> using 40 iterations of binary search.
        /// </summary>
        /// <param name="targetProbability">Desired average proc probability, clamped to [0.0001, 1].</param>
        /// <returns>Calibrated PRD step constant <c>C</c> such that attempt <c>n</c> triggers with probability <c>min(1, n * C)</c>.</returns>
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

        /// <summary>
        /// Computes the exact long-run proc probability <c>1 / E[N]</c> for a given PRD constant <paramref name="c"/>.
        /// </summary>
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

        /// <summary>
        /// Selects one entry from <paramref name="entries"/> proportional to its positive weight.
        /// </summary>
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

        /// <summary>
        /// Performs a weighted roll restricted to entries matching <paramref name="rarity"/> (case-insensitive).
        /// </summary>
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

        /// <summary>
        /// Performs a weighted roll restricted to entries whose rarity does not match <paramref name="rarity"/>.
        /// </summary>
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

        /// <summary>
        /// Computes the combined weight share of all positive-weight entries matching <paramref name="rarity"/>.
        /// </summary>
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

        /// <summary>
        /// Rolls an inclusive stack quantity between <c>minQuantity</c> and <c>maxQuantity</c> (minimum 1).
        /// </summary>
        private int RollQuantity(WeightedLootEntry entry)
        {
            int min = System.Math.Max(1, entry.minQuantity);
            int max = System.Math.Max(min, entry.maxQuantity);
            return min == max ? min : _rng.Next(min, max + 1);
        }
    }
}
