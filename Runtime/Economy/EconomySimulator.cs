using System;
using System.Collections.Generic;
using GameBalancer.Math;

namespace GameBalancer.Economy
{
    /// <summary>
    /// Daily currency snapshot recording inflows, outflows, net delta, ending balance, and mandatory-sink starvation.
    /// </summary>
    [Serializable]
    public sealed class EconomyDaySample
    {
        public int day;
        public double inflow;
        public double outflow;
        public double netDelta;
        public double endingBalance;
        public bool starvedMandatorySink;
    }

    /// <summary>
    /// Aggregated simulation timeline and diagnostic flags for a single currency.
    /// </summary>
    [Serializable]
    public sealed class CurrencySimulationSeries
    {
        public string currencyId;
        public double startingBalance;
        public double endingBalance;
        public double peakBalance;
        public double minBalance;
        public double totalInflow;
        public double totalOutflow;
        public int starvationDays;
        public bool inflationFlagged;
        public List<EconomyDaySample> timeline = new List<EconomyDaySample>();
    }

    /// <summary>
    /// Multi-currency economy simulation report including per-currency timelines and diagnostic warnings.
    /// </summary>
    [Serializable]
    public sealed class EconomySimulationReport
    {
        public string modelName;
        public int horizonDays;
        public List<CurrencySimulationSeries> currencies = new List<CurrencySimulationSeries>();
        public List<string> warnings = new List<string>();
    }

    /// <summary>
    /// Simulates faucet and sink flows across a multi-day horizon and flags inflation or starvation.
    /// </summary>
    public static class EconomySimulator
    {
        /// <summary>
        /// Simulates an <see cref="EconomyModelAsset"/> over <paramref name="horizonDays"/> days.
        /// </summary>
        /// <param name="model">Economy model asset containing currencies and flow nodes.</param>
        /// <param name="horizonDays">Number of days/sessions to simulate (clamped to at least 1).</param>
        /// <returns>Completed <see cref="EconomySimulationReport"/>.</returns>
        public static EconomySimulationReport Simulate(EconomyModelAsset model, int horizonDays = 60)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            return Simulate(model.modelName, model.currencies, model.flows, horizonDays);
        }

        /// <summary>
        /// Simulates faucet and sink nodes for every currency in <paramref name="currencies"/> across <paramref name="horizonDays"/> days.
        /// Faucets are always evaluated before sinks on the same day. Inflation is flagged when ending balance exceeds
        /// <see cref="CurrencyDefinition.inflationAlertThreshold"/> and total inflow exceeds 1.25x total outflow.
        /// </summary>
        /// <param name="modelName">Display label recorded on the report.</param>
        /// <param name="currencies">Currencies to track independently.</param>
        /// <param name="flows">Faucet and sink flow nodes matched to currencies case-insensitively by <c>currencyId</c>.</param>
        /// <param name="horizonDays">Number of days/sessions to simulate (clamped to at least 1).</param>
        /// <returns>Completed <see cref="EconomySimulationReport"/> with daily timelines and inflation/starvation warnings.</returns>
        public static EconomySimulationReport Simulate(
            string modelName,
            IReadOnlyList<CurrencyDefinition> currencies,
            IReadOnlyList<EconomyFlowNode> flows,
            int horizonDays = 60)
        {
            int days = System.Math.Max(1, horizonDays);
            var report = new EconomySimulationReport
            {
                modelName = string.IsNullOrWhiteSpace(modelName) ? "Economy Model" : modelName,
                horizonDays = days
            };

            if (currencies == null || currencies.Count == 0)
            {
                return report;
            }

            var exprVars = new Dictionary<string, double>(4, StringComparer.OrdinalIgnoreCase);

            for (int c = 0; c < currencies.Count; c++)
            {
                var cur = currencies[c];
                if (cur == null || string.IsNullOrWhiteSpace(cur.currencyId)) continue;

                double balance = System.Math.Max(0d, cur.startingBalance);
                var series = new CurrencySimulationSeries
                {
                    currencyId = cur.currencyId,
                    startingBalance = balance,
                    peakBalance = balance,
                    minBalance = balance
                };

                for (int day = 1; day <= days; day++)
                {
                    double dayInflow = 0d;
                    double dayOutflow = 0d;
                    bool starved = false;

                    if (flows != null)
                    {
                        // Process faucets before sinks on the same day
                        for (int pass = 0; pass < 2; pass++)
                        {
                            EconomyFlowKind targetKind = pass == 0 ? EconomyFlowKind.Faucet : EconomyFlowKind.Sink;
                            for (int f = 0; f < flows.Count; f++)
                            {
                                var node = flows[f];
                                if (node == null || node.kind != targetKind) continue;
                                if (!string.Equals(node.currencyId, cur.currencyId, StringComparison.OrdinalIgnoreCase)) continue;
                                if (!TriggersOnDay(node, day)) continue;

                                double amount = EvaluateFlowAmount(node, day, balance, exprVars);
                                if (amount <= 0d) continue;

                                if (targetKind == EconomyFlowKind.Faucet)
                                {
                                    dayInflow += amount;
                                    balance += amount;
                                    if (cur.hasCap && cur.maxCap > 0d)
                                    {
                                        balance = System.Math.Min(balance, cur.maxCap);
                                    }
                                }
                                else
                                {
                                    if (balance >= amount)
                                    {
                                        balance -= amount;
                                        dayOutflow += amount;
                                    }
                                    else
                                    {
                                        if (node.mandatorySink)
                                        {
                                            starved = true;
                                        }
                                        else
                                        {
                                            dayOutflow += balance;
                                            balance = 0d;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    series.totalInflow += dayInflow;
                    series.totalOutflow += dayOutflow;
                    if (balance > series.peakBalance) series.peakBalance = balance;
                    if (balance < series.minBalance) series.minBalance = balance;
                    if (starved) series.starvationDays++;

                    series.timeline.Add(new EconomyDaySample
                    {
                        day = day,
                        inflow = dayInflow,
                        outflow = dayOutflow,
                        netDelta = dayInflow - dayOutflow,
                        endingBalance = balance,
                        starvedMandatorySink = starved
                    });
                }

                series.endingBalance = balance;
                if (cur.inflationAlertThreshold > 0d &&
                    series.endingBalance > cur.inflationAlertThreshold &&
                    series.totalInflow > series.totalOutflow * 1.25d)
                {
                    series.inflationFlagged = true;
                    report.warnings.Add(
                        $"[{cur.currencyId}] Inflation alert: ending balance ({series.endingBalance:F0}) exceeded {cur.inflationAlertThreshold:F0} with inflow/outflow ratio {(series.totalOutflow > 0d ? series.totalInflow / series.totalOutflow : 99d):F2}x.");
                }

                if (series.starvationDays > 0)
                {
                    report.warnings.Add(
                        $"[{cur.currencyId}] Starvation alert: mandatory sink could not be paid on {series.starvationDays} day(s).");
                }

                report.currencies.Add(series);
            }

            return report;
        }

        /// <summary>
        /// Determines whether <paramref name="node"/> fires on the specified 1-based <paramref name="day"/>.
        /// </summary>
        private static bool TriggersOnDay(EconomyFlowNode node, int day)
        {
            int interval = System.Math.Max(1, node.intervalOrDay);
            switch (node.cadence)
            {
                case EconomyCadence.EveryDay:
                    return true;
                case EconomyCadence.EveryNDays:
                    return (day % interval) == 0;
                case EconomyCadence.OneTimeOnDay:
                    return day == interval;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Evaluates a flow node's non-negative currency amount using either its flat value or math formula
        /// (exposing <c>day</c>, <c>session</c>, <c>level</c>, and current <c>balance</c>).
        /// </summary>
        private static double EvaluateFlowAmount(
            EconomyFlowNode node,
            int day,
            double balance,
            Dictionary<string, double> vars)
        {
            if (!node.useFormula)
            {
                return System.Math.Max(0d, node.flatAmount);
            }

            vars["day"] = day;
            vars["session"] = day;
            vars["level"] = day;
            vars["balance"] = balance;
            double value = MathExpressionEvaluator.EvaluateOrDefault(node.amountFormula, vars, node.flatAmount);
            return System.Math.Max(0d, value);
        }
    }
}
