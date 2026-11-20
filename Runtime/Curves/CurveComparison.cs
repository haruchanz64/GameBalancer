using System;
using System.Collections.Generic;

namespace GameBalancer.Curves
{
    /// <summary>
    /// Evaluated sample values and summary statistics for a single progression curve across a level range.
    /// </summary>
    [Serializable]
    public sealed class CurveSeriesData
    {
        public string name;
        public double[] values;
        public double minValue;
        public double maxValue;
        public double totalSum;
    }

    /// <summary>
    /// Multi-curve comparison report containing evaluated level samples, per-series aggregates,
    /// and an optional derived ratio series.
    /// </summary>
    [Serializable]
    public sealed class CurveComparisonReport
    {
        public int minLevel;
        public int maxLevel;
        public int[] levels;
        public List<CurveSeriesData> series = new List<CurveSeriesData>();
        public string ratioLabel;
        public double[] ratioValues;
    }

    /// <summary>
    /// Evaluates one or more progression curves across a level range and optionally computes
    /// a derived ratio series (such as Player HP / Enemy DPS).
    /// </summary>
    public static class CurveComparison
    {
        /// <summary>
        /// Evaluates every non-null curve in <paramref name="curves"/> across [<paramref name="minLevel"/>, <paramref name="maxLevel"/>]
        /// and optionally computes an element-wise ratio between two series.
        /// </summary>
        /// <param name="curves">Ordered collection of progression curves to evaluate; null entries are skipped.</param>
        /// <param name="minLevel">Inclusive starting level (clamped to at least 1).</param>
        /// <param name="maxLevel">Inclusive ending level (clamped to at least the start level).</param>
        /// <param name="ratioNumeratorIndex">Zero-based index in the evaluated series list for the ratio numerator, or -1 to omit ratio calculation.</param>
        /// <param name="ratioDenominatorIndex">Zero-based index in the evaluated series list for the ratio denominator, or -1 to omit ratio calculation. Denominators within 1e-9 of zero yield 0.</param>
        /// <returns>Populated <see cref="CurveComparisonReport"/> covering the requested level range.</returns>
        public static CurveComparisonReport Evaluate(
            IReadOnlyList<IProgressionCurve> curves,
            int minLevel,
            int maxLevel,
            int ratioNumeratorIndex = -1,
            int ratioDenominatorIndex = -1)
        {
            int start = System.Math.Max(1, minLevel);
            int end = System.Math.Max(start, maxLevel);
            int count = (end - start) + 1;

            var report = new CurveComparisonReport
            {
                minLevel = start,
                maxLevel = end,
                levels = new int[count]
            };

            for (int i = 0; i < count; i++)
            {
                report.levels[i] = start + i;
            }

            if (curves != null)
            {
                for (int c = 0; c < curves.Count; c++)
                {
                    var curve = curves[c];
                    if (curve == null) continue;

                    var values = new double[count];
                    double minVal = double.MaxValue;
                    double maxVal = double.MinValue;
                    double sum = 0d;

                    for (int i = 0; i < count; i++)
                    {
                        double v = curve.Evaluate(report.levels[i]);
                        values[i] = v;
                        if (v < minVal) minVal = v;
                        if (v > maxVal) maxVal = v;
                        sum += v;
                    }

                    report.series.Add(new CurveSeriesData
                    {
                        name = curve.DisplayName,
                        values = values,
                        minValue = minVal,
                        maxValue = maxVal,
                        totalSum = sum
                    });
                }
            }

            if (ratioNumeratorIndex >= 0 &&
                ratioDenominatorIndex >= 0 &&
                ratioNumeratorIndex < report.series.Count &&
                ratioDenominatorIndex < report.series.Count)
            {
                var num = report.series[ratioNumeratorIndex];
                var den = report.series[ratioDenominatorIndex];
                report.ratioLabel = $"{num.name} / {den.name}";
                report.ratioValues = new double[count];

                for (int i = 0; i < count; i++)
                {
                    double d = den.values[i];
                    report.ratioValues[i] = System.Math.Abs(d) > 1e-9d ? num.values[i] / d : 0d;
                }
            }

            return report;
        }
    }
}
