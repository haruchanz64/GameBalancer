using System.Collections.Generic;
using GameBalancer.Curves;
using GameBalancer.Math;
using NUnit.Framework;
using UnityEngine;

namespace GameBalancer.Tests
{
    public sealed class MathAndCurveTests
    {
        [Test]
        public void ExpressionEvaluator_RespectsPrecedenceVariablesAndFunctions()
        {
            var vars = new Dictionary<string, double>
            {
                ["level"] = 10d,
                ["base"] = 50d,
                ["growth"] = 20d
            };

            bool ok = MathExpressionEvaluator.TryEvaluate("base + growth * pow(level - 1, 2) / 3", vars, out double val, out string err);
            Assert.IsTrue(ok, err);
            Assert.AreEqual(50d + (20d * 81d / 3d), val, 1e-6d);

            // Right-associative power: 2 ^ 3 ^ 2 = 2 ^ 9 = 512
            Assert.IsTrue(MathExpressionEvaluator.TryEvaluate("2 ^ 3 ^ 2", null, out double powVal, out _));
            Assert.AreEqual(512d, powVal, 1e-6d);

            // Division by zero returns error safely
            Assert.IsFalse(MathExpressionEvaluator.TryEvaluate("10 / (5 - 5)", null, out _, out string divErr));
            Assert.IsNotEmpty(divErr);
        }

        [Test]
        public void ProgressionCurve_EvaluatesModesAndRatioComparison()
        {
            var hpAsset = ScriptableObject.CreateInstance<ProgressionCurveAsset>();
            var dpsAsset = ScriptableObject.CreateInstance<ProgressionCurveAsset>();
            try
            {
                hpAsset.curveName = "PlayerHP";
                hpAsset.settings = ProgressionCurveSettings.CreateDefaultHpCurve();
                hpAsset.settings.baseValue = 100f;
                hpAsset.settings.growthRate = 20f;
                hpAsset.settings.rounding = CurveRoundingMode.None;

                dpsAsset.curveName = "EnemyDPS";
                dpsAsset.settings = ProgressionCurveSettings.CreateDefaultHpCurve();
                dpsAsset.settings.baseValue = 20f;
                dpsAsset.settings.growthRate = 4f;
                dpsAsset.settings.rounding = CurveRoundingMode.None;

                Assert.AreEqual(100d, hpAsset.Evaluate(1), 1e-4d);
                Assert.AreEqual(280d, hpAsset.Evaluate(10), 1e-4d);

                var report = CurveComparison.Evaluate(new IProgressionCurve[] { hpAsset, dpsAsset }, 1, 10, 0, 1);
                Assert.AreEqual(2, report.series.Count);
                Assert.IsNotNull(report.ratioValues);
                // 100/20 = 5.0 and 280/56 = 5.0
                Assert.AreEqual(5d, report.ratioValues[0], 1e-4d);
                Assert.AreEqual(5d, report.ratioValues[9], 1e-4d);
            }
            finally
            {
                Object.DestroyImmediate(hpAsset);
                Object.DestroyImmediate(dpsAsset);
            }
        }
    }
}
