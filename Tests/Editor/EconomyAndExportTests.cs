using System.Collections.Generic;
using GameBalancer.Curves;
using GameBalancer.Economy;
using GameBalancer.Export;
using NUnit.Framework;
using UnityEngine;

namespace GameBalancer.Tests
{
    public sealed class EconomyAndExportTests
    {
        [Test]
        public void EconomySimulator_DetectsInflationAndMandatorySinkStarvation()
        {
            var currencies = new List<CurrencyDefinition>
            {
                new CurrencyDefinition { currencyId = "Gold", startingBalance = 100d, inflationAlertThreshold = 5000d },
                new CurrencyDefinition { currencyId = "Gems", startingBalance = 10d, inflationAlertThreshold = 5000d }
            };

            var flows = new List<EconomyFlowNode>
            {
                // Gold inflates rapidly
                new EconomyFlowNode { nodeName = "Gold Faucet", currencyId = "Gold", kind = EconomyFlowKind.Faucet, cadence = EconomyCadence.EveryDay, flatAmount = 500d },
                new EconomyFlowNode { nodeName = "Gold Sink", currencyId = "Gold", kind = EconomyFlowKind.Sink, cadence = EconomyCadence.EveryDay, flatAmount = 50d },
                // Gems starve on mandatory sink
                new EconomyFlowNode { nodeName = "Gem Upkeep", currencyId = "Gems", kind = EconomyFlowKind.Sink, cadence = EconomyCadence.EveryDay, flatAmount = 25d, mandatorySink = true }
            };

            var report = EconomySimulator.Simulate("TestEcon", currencies, flows, horizonDays: 30);

            Assert.AreEqual(2, report.currencies.Count);
            Assert.IsTrue(report.currencies[0].inflationFlagged);
            Assert.Greater(report.currencies[1].starvationDays, 0);
            Assert.AreEqual(2, report.warnings.Count);

            string csv = BalanceReportExporter.EconomyToCsv(report, 0);
            Assert.That(csv, Does.Contain("Day,Inflow,Outflow,NetDelta,EndingBalance"));
        }

        [Test]
        public void BalanceReportExporter_ProducesValidCurveCsv()
        {
            var curve = ScriptableObject.CreateInstance<ProgressionCurveAsset>();
            try
            {
                curve.curveName = "XP";
                var cmp = CurveComparison.Evaluate(new IProgressionCurve[] { curve }, 1, 5);
                string csv = BalanceReportExporter.CurvesToCsv(cmp);
                Assert.That(csv, Does.Contain("Level,XP"));
                Assert.That(csv, Does.Contain("\n5,"));
            }
            finally
            {
                Object.DestroyImmediate(curve);
            }
        }
    }
}
