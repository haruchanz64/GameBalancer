using System;
using System.Collections.Generic;

namespace GameBalancer.Combat
{
    /// <summary>
    /// Balance classification assigned to a non-mirror cell in a <see cref="MatchupMatrixReport"/>.
    /// </summary>
    public enum BalanceOutlierFlag
    {
        /// <summary>Win rate and average TTK are within configured target bounds.</summary>
        Balanced = 0,
        /// <summary>Row combatant win rate exceeded <see cref="MatchupMatrixReport.targetMaxWinRatePercent"/>.</summary>
        Overpowered = 1,
        /// <summary>Row combatant win rate fell below <see cref="MatchupMatrixReport.targetMinWinRatePercent"/>.</summary>
        Underpowered = 2,
        /// <summary>Win rate is balanced, but average TTK is shorter than <see cref="MatchupMatrixReport.targetMinTtkSeconds"/>.</summary>
        TtkTooFast = 3,
        /// <summary>Win rate is balanced, but average TTK is longer than <see cref="MatchupMatrixReport.targetMaxTtkSeconds"/>.</summary>
        TtkTooSlow = 4
    }

    /// <summary>
    /// Aggregated Monte Carlo statistics for a single (row vs. column) pair in a matchup matrix.
    /// </summary>
    [Serializable]
    public sealed class MatchupCellResult
    {
        public int rowIndex;
        public int colIndex;
        public string rowName;
        public string colName;
        public int trials;
        public double rowWinRatePercent;
        public double timeoutPercent;
        public double avgTtkSeconds;
        public double minTtkSeconds;
        public double maxTtkSeconds;
        public double avgWinnerHpPercent;
        public BalanceOutlierFlag outlierFlag;
    }

    /// <summary>
    /// Complete N x N round-robin matchup report including flattened cell results and human-readable outlier summaries.
    /// </summary>
    [Serializable]
    public sealed class MatchupMatrixReport
    {
        public int level;
        public int trialsPerMatchup;
        public double targetMinWinRatePercent;
        public double targetMaxWinRatePercent;
        public double targetMinTtkSeconds;
        public double targetMaxTtkSeconds;
        public List<string> rosterNames = new List<string>();
        public List<MatchupCellResult> cells = new List<MatchupCellResult>();
        public List<string> outlierSummaries = new List<string>();

        /// <summary>
        /// Retrieves the cell result for the given zero-based <paramref name="row"/> and <paramref name="col"/> indices.
        /// </summary>
        /// <param name="row">Row combatant index.</param>
        /// <param name="col">Column combatant index.</param>
        /// <returns>The matching <see cref="MatchupCellResult"/>, or <c>null</c> if out of range.</returns>
        public MatchupCellResult GetCell(int row, int col)
        {
            int count = rosterNames.Count;
            int idx = (row * count) + col;
            return idx >= 0 && idx < cells.Count ? cells[idx] : null;
        }
    }

    /// <summary>
    /// Runs Monte Carlo round-robin duel matrices across a roster of combatants and flags balance outliers.
    /// </summary>
    public static class MatchupMatrixRunner
    {
        /// <summary>
        /// Evaluates every combatant pair in <paramref name="roster"/> at <paramref name="level"/> across
        /// <paramref name="trialsPerMatchup"/> duels and flags non-mirror win-rate or TTK outliers.
        /// </summary>
        /// <param name="roster">Combatants to match against each other; null entries are skipped.</param>
        /// <param name="level">1-based level passed to <see cref="ICombatStatSource.EvaluateStatsAtLevel"/> (clamped to at least 1).</param>
        /// <param name="trialsPerMatchup">Simulated duels per ordered pair (clamped to at least 10).</param>
        /// <param name="simOptions">Optional simulation options; uses <see cref="CombatSimulationOptions.Default"/> when omitted.</param>
        /// <param name="customFormula">Optional custom damage formula overriding <see cref="CombatSimulationOptions.formulaMode"/>.</param>
        /// <param name="targetMinWinRate">Minimum acceptable non-mirror win-rate percentage before flagging <see cref="BalanceOutlierFlag.Underpowered"/>.</param>
        /// <param name="targetMaxWinRate">Maximum acceptable non-mirror win-rate percentage before flagging <see cref="BalanceOutlierFlag.Overpowered"/>.</param>
        /// <param name="targetMinTtk">Minimum acceptable average TTK in seconds before flagging <see cref="BalanceOutlierFlag.TtkTooFast"/>.</param>
        /// <param name="targetMaxTtk">Maximum acceptable average TTK in seconds before flagging <see cref="BalanceOutlierFlag.TtkTooSlow"/>.</param>
        /// <param name="seed">Optional PRNG seed for reproducible matrix runs.</param>
        /// <returns>Completed <see cref="MatchupMatrixReport"/> with N x N cells and upper-triangle outlier summaries.</returns>
        public static MatchupMatrixReport RunMatrix(
            IReadOnlyList<ICombatStatSource> roster,
            int level = 1,
            int trialsPerMatchup = 1000,
            CombatSimulationOptions? simOptions = null,
            IDamageFormula customFormula = null,
            double targetMinWinRate = 40d,
            double targetMaxWinRate = 60d,
            double targetMinTtk = 3d,
            double targetMaxTtk = 30d,
            int? seed = null)
        {
            var opts = simOptions ?? CombatSimulationOptions.Default;
            int trials = System.Math.Max(10, trialsPerMatchup);
            var report = new MatchupMatrixReport
            {
                level = System.Math.Max(1, level),
                trialsPerMatchup = trials,
                targetMinWinRatePercent = targetMinWinRate,
                targetMaxWinRatePercent = targetMaxWinRate,
                targetMinTtkSeconds = targetMinTtk,
                targetMaxTtkSeconds = targetMaxTtk
            };

            if (roster == null || roster.Count == 0)
            {
                return report;
            }

            var snapshots = new List<CombatStatSnapshot>(roster.Count);
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i] == null) continue;
                var snap = roster[i].EvaluateStatsAtLevel(report.level);
                snapshots.Add(snap);
                report.rosterNames.Add(snap.name);
            }

            int n = snapshots.Count;
            var simulator = new CombatSimulator(seed);

            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++)
                {
                    var a = snapshots[r];
                    var b = snapshots[c];

                    int winsA = 0;
                    int timeouts = 0;
                    double ttkSum = 0d;
                    double minTtk = double.MaxValue;
                    double maxTtk = 0d;
                    double winHpSum = 0d;
                    int nonTimeoutCount = 0;

                    for (int t = 0; t < trials; t++)
                    {
                        var duel = simulator.SimulateDuel(in a, in b, in opts, customFormula);
                        if (duel.TimedOut)
                        {
                            timeouts++;
                        }
                        else
                        {
                            if (duel.Winner == 1) winsA++;
                            ttkSum += duel.DurationSeconds;
                            if (duel.DurationSeconds < minTtk) minTtk = duel.DurationSeconds;
                            if (duel.DurationSeconds > maxTtk) maxTtk = duel.DurationSeconds;
                            winHpSum += duel.WinnerRemainingHpPercent;
                            nonTimeoutCount++;
                        }
                    }

                    double winRate = (winsA / (double)trials) * 100d;
                    double avgTtk = nonTimeoutCount > 0 ? ttkSum / nonTimeoutCount : opts.maxDuelDurationSeconds;
                    double avgHp = nonTimeoutCount > 0 ? winHpSum / nonTimeoutCount : 0d;

                    var flag = BalanceOutlierFlag.Balanced;
                    if (r != c)
                    {
                        if (winRate > targetMaxWinRate) flag = BalanceOutlierFlag.Overpowered;
                        else if (winRate < targetMinWinRate) flag = BalanceOutlierFlag.Underpowered;
                        else if (avgTtk < targetMinTtk) flag = BalanceOutlierFlag.TtkTooFast;
                        else if (avgTtk > targetMaxTtk) flag = BalanceOutlierFlag.TtkTooSlow;

                        if (flag != BalanceOutlierFlag.Balanced && r < c)
                        {
                            report.outlierSummaries.Add(
                                $"{a.name} vs {b.name}: {winRate:F1}% win rate, {avgTtk:F1}s avg TTK ({flag})");
                        }
                    }

                    report.cells.Add(new MatchupCellResult
                    {
                        rowIndex = r,
                        colIndex = c,
                        rowName = a.name,
                        colName = b.name,
                        trials = trials,
                        rowWinRatePercent = winRate,
                        timeoutPercent = (timeouts / (double)trials) * 100d,
                        avgTtkSeconds = avgTtk,
                        minTtkSeconds = nonTimeoutCount > 0 ? minTtk : 0d,
                        maxTtkSeconds = maxTtk,
                        avgWinnerHpPercent = avgHp,
                        outlierFlag = flag
                    });
                }
            }

            return report;
        }
    }
}
