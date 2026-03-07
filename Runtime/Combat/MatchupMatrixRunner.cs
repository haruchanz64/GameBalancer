using System;
using System.Collections.Generic;

namespace GameBalancer.Combat
{
    public enum BalanceOutlierFlag
    {
        Balanced = 0,
        Overpowered = 1,
        Underpowered = 2,
        TtkTooFast = 3,
        TtkTooSlow = 4
    }

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
