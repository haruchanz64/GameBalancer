using System;
using System.Globalization;
using System.Text;
using GameBalancer.Combat;
using GameBalancer.Curves;
using GameBalancer.Economy;
using GameBalancer.Loot;
using UnityEngine;

namespace GameBalancer.Export
{
    /// <summary>
    /// Exports GameBalancer reports to CSV, JSON, and Markdown tables.
    /// CSV output is directly compatible with SpreadsheetToScriptableObjects.
    /// </summary>
    public static class BalanceReportExporter
    {
        public static string CurvesToCsv(CurveComparisonReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var ic = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(1024);

            sb.Append("Level");
            for (int s = 0; s < report.series.Count; s++)
            {
                sb.Append(',').Append(EscapeCsv(report.series[s].name));
            }
            if (report.ratioValues != null)
            {
                sb.Append(',').Append(EscapeCsv(report.ratioLabel));
            }
            sb.AppendLine();

            for (int i = 0; i < report.levels.Length; i++)
            {
                sb.Append(report.levels[i].ToString(ic));
                for (int s = 0; s < report.series.Count; s++)
                {
                    sb.Append(',').Append(report.series[s].values[i].ToString("F4", ic));
                }
                if (report.ratioValues != null)
                {
                    sb.Append(',').Append(report.ratioValues[i].ToString("F4", ic));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public static string LootToMarkdown(LootAnalysisReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var ic = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(1024);

            sb.AppendLine($"# Loot Odds Report: {report.tableName}");
            sb.AppendLine();
            sb.AppendLine($"- **Rolls simulated:** {report.totalRolls.ToString(ic)}");
            sb.AppendLine($"- **Pity enabled:** {report.pityEnabled} (Triggered: {report.pityTriggeredCount.ToString(ic)})");
            sb.AppendLine($"- **Expected / Simulated EV per roll:** {report.expectedValuePerRoll.ToString("F2", ic)} / {report.simulatedValuePerRoll.ToString("F2", ic)}");
            if (report.targetHitCount > 0)
            {
                sb.AppendLine($"- **Pulls to `{report.targetRarity}`:** Mean {report.pullsToTargetMean.ToString("F1", ic)} | P50: {report.pullsToTargetP50} | P90: {report.pullsToTargetP90} | P99: {report.pullsToTargetP99} | Max: {report.pullsToTargetMax}");
            }
            sb.AppendLine();
            sb.AppendLine("| Item | Rarity | Weight | Theoretical % | Simulated % | Drops |");
            sb.AppendLine("| :--- | :--- | ---: | ---: | ---: | ---: |");
            for (int i = 0; i < report.entries.Count; i++)
            {
                var e = report.entries[i];
                sb.AppendLine($"| {e.displayName} | {e.rarity} | {e.weight.ToString("F2", ic)} | {e.theoreticalPercent.ToString("F2", ic)}% | {e.simulatedPercent.ToString("F2", ic)}% | {e.dropCount} |");
            }

            return sb.ToString();
        }

        public static string CombatMatrixToMarkdown(MatchupMatrixReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var ic = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(1024);

            sb.AppendLine($"# Combat Matchup Matrix (Level {report.level}, {report.trialsPerMatchup} trials/pair)");
            sb.AppendLine();
            sb.Append("| Combatant |");
            for (int c = 0; c < report.rosterNames.Count; c++)
            {
                sb.Append(' ').Append(report.rosterNames[c]).Append(" |");
            }
            sb.AppendLine();

            sb.Append("| :--- |");
            for (int c = 0; c < report.rosterNames.Count; c++)
            {
                sb.Append(" ---: |");
            }
            sb.AppendLine();

            for (int r = 0; r < report.rosterNames.Count; r++)
            {
                sb.Append("| **").Append(report.rosterNames[r]).Append("** |");
                for (int c = 0; c < report.rosterNames.Count; c++)
                {
                    var cell = report.GetCell(r, c);
                    sb.Append(' ')
                      .Append(cell.rowWinRatePercent.ToString("F1", ic))
                      .Append("% (")
                      .Append(cell.avgTtkSeconds.ToString("F1", ic))
                      .Append("s) |");
                }
                sb.AppendLine();
            }

            if (report.outlierSummaries.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Flagged Outliers");
                for (int i = 0; i < report.outlierSummaries.Count; i++)
                {
                    sb.AppendLine($"- {report.outlierSummaries[i]}");
                }
            }

            return sb.ToString();
        }

        public static string EconomyToCsv(EconomySimulationReport report, int currencyIndex = 0)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (currencyIndex < 0 || currencyIndex >= report.currencies.Count) return string.Empty;

            var series = report.currencies[currencyIndex];
            var ic = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(1024);
            sb.AppendLine("Day,Inflow,Outflow,NetDelta,EndingBalance,StarvedMandatorySink");

            for (int i = 0; i < series.timeline.Count; i++)
            {
                var d = series.timeline[i];
                sb.Append(d.day.ToString(ic)).Append(',')
                  .Append(d.inflow.ToString("F2", ic)).Append(',')
                  .Append(d.outflow.ToString("F2", ic)).Append(',')
                  .Append(d.netDelta.ToString("F2", ic)).Append(',')
                  .Append(d.endingBalance.ToString("F2", ic)).Append(',')
                  .AppendLine(d.starvedMandatorySink ? "1" : "0");
            }

            return sb.ToString();
        }

        public static string ToJson<T>(T report, bool prettyPrint = true)
        {
            return JsonUtility.ToJson(report, prettyPrint);
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }
    }
}
