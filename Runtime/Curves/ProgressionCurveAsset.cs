using System;
using System.Collections.Generic;
using GameBalancer.Math;
using UnityEngine;

namespace GameBalancer.Curves
{
    /// <summary>
    /// Mathematical model used by <see cref="ProgressionCurveSettings"/> to evaluate values across levels.
    /// </summary>
    public enum ProgressionCurveMode
    {
        /// <summary>Linear scaling: <c>base + growth * (level - 1)</c>.</summary>
        Linear = 0,
        /// <summary>Polynomial scaling: <c>base + growth * (level - 1)^exponent</c>.</summary>
        Polynomial = 1,
        /// <summary>Exponential scaling: <c>base * (1 + growth)^(level - 1)</c>.</summary>
        Exponential = 2,
        /// <summary>Logarithmic scaling: <c>base + growth * ln(level)</c>.</summary>
        Logarithmic = 3,
        /// <summary>Logistic S-curve transitioning from <c>base</c> toward <c>softCap</c> around <c>midpointLevel</c>.</summary>
        SigmoidSoftCap = 4,
        /// <summary>Hyperbolic diminishing returns: <c>base + softCap * (level / (level + growth))</c>.</summary>
        DiminishingReturns = 5,
        /// <summary>Evaluates a Unity <see cref="AnimationCurve"/> normalized against <c>referenceMaxLevel</c>.</summary>
        UnityAnimationCurve = 6,
        /// <summary>Evaluates <see cref="ProgressionCurveSettings.customExpression"/> via <see cref="MathExpressionEvaluator"/>.</summary>
        CustomExpression = 7
    }

    /// <summary>
    /// Post-clamp rounding strategy applied to evaluated curve values.
    /// </summary>
    public enum CurveRoundingMode
    {
        /// <summary>Retains the raw floating-point result.</summary>
        None = 0,
        /// <summary>Rounds to the nearest integer using midpoint-away-from-zero rounding.</summary>
        Round = 1,
        /// <summary>Rounds down to the largest integer less than or equal to the value.</summary>
        Floor = 2,
        /// <summary>Rounds up to the smallest integer greater than or equal to the value.</summary>
        Ceil = 3
    }

    /// <summary>
    /// Implement to plug a custom curve formula or external lookup table into GameBalancer.
    /// </summary>
    public interface IProgressionCurve
    {
        /// <summary>
        /// Label shown in charts, milestone tables, and exported reports.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Evaluates the curve at the specified 1-based level.
        /// </summary>
        /// <param name="level">Target progression level (typically 1 or greater).</param>
        /// <returns>Computed numeric value at <paramref name="level"/>.</returns>
        double Evaluate(int level);
    }

    /// <summary>
    /// Serializable parameters for a progression or scaling curve.
    /// </summary>
    [Serializable]
    public struct ProgressionCurveSettings
    {
        /// <summary>Curve evaluation formula mode.</summary>
        public ProgressionCurveMode mode;
        /// <summary>Starting value at level 1 (or lower asymptote for sigmoid curves).</summary>
        public float baseValue;
        /// <summary>Slope, polynomial multiplier, exponential rate, logarithmic scale, diminishing-returns half-point, or animation curve scale.</summary>
        public float growthRate;
        /// <summary>Power exponent used when <see cref="mode"/> is <see cref="ProgressionCurveMode.Polynomial"/>.</summary>
        public float exponent;
        /// <summary>Upper asymptote used by <see cref="ProgressionCurveMode.SigmoidSoftCap"/> and <see cref="ProgressionCurveMode.DiminishingReturns"/>.</summary>
        public float softCap;
        /// <summary>Inflection level where <see cref="ProgressionCurveMode.SigmoidSoftCap"/> reaches 50% between <see cref="baseValue"/> and <see cref="softCap"/>.</summary>
        public float midpointLevel;
        /// <summary>Slope steepness factor for <see cref="ProgressionCurveMode.SigmoidSoftCap"/> (clamped to at least 0.001).</summary>
        public float steepness;
        /// <summary>Level mapped to normalized time 1.0 when evaluating <see cref="ProgressionCurveMode.UnityAnimationCurve"/>.</summary>
        public int referenceMaxLevel;
        /// <summary>Custom formula string evaluated when <see cref="mode"/> is <see cref="ProgressionCurveMode.CustomExpression"/> using variables <c>level</c>, <c>x</c>, <c>base</c>, <c>growth</c>, and <c>cap</c>.</summary>
        public string customExpression;
        /// <summary>Rounding rule applied after clamping.</summary>
        public CurveRoundingMode rounding;
        /// <summary>When <c>true</c>, clamps the raw value between <see cref="minClamp"/> and <see cref="maxClamp"/> before rounding.</summary>
        public bool useClamp;
        /// <summary>Minimum allowed value when <see cref="useClamp"/> is enabled.</summary>
        public float minClamp;
        /// <summary>Maximum allowed value when <see cref="useClamp"/> is enabled.</summary>
        public float maxClamp;
        /// <summary>Unity curve sampled over normalized level range [0, 1] in <see cref="ProgressionCurveMode.UnityAnimationCurve"/> mode.</summary>
        public AnimationCurve animationCurve;

        /// <summary>
        /// Creates a default polynomial XP requirement preset starting at 100 and scaling with exponent 1.65.
        /// </summary>
        /// <returns>Preconfigured XP curve settings.</returns>
        public static ProgressionCurveSettings CreateDefaultXpCurve() => new ProgressionCurveSettings
        {
            mode = ProgressionCurveMode.Polynomial,
            baseValue = 100f,
            growthRate = 35f,
            exponent = 1.65f,
            softCap = 10000f,
            midpointLevel = 25f,
            steepness = 0.15f,
            referenceMaxLevel = 50,
            customExpression = "base + growth * pow(level - 1, 1.5)",
            rounding = CurveRoundingMode.Round,
            useClamp = true,
            minClamp = 0f,
            maxClamp = 10000000f,
            animationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f)
        };

        /// <summary>
        /// Creates a default linear HP scaling preset starting at 120 with +24 per level.
        /// </summary>
        /// <returns>Preconfigured HP curve settings.</returns>
        public static ProgressionCurveSettings CreateDefaultHpCurve() => new ProgressionCurveSettings
        {
            mode = ProgressionCurveMode.Linear,
            baseValue = 120f,
            growthRate = 24f,
            exponent = 1f,
            softCap = 5000f,
            midpointLevel = 25f,
            steepness = 0.12f,
            referenceMaxLevel = 50,
            customExpression = "base + (level - 1) * growth",
            rounding = CurveRoundingMode.Round,
            useClamp = true,
            minClamp = 1f,
            maxClamp = 1000000f,
            animationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f)
        };

        /// <summary>
        /// Evaluates the configured curve formula at <paramref name="level"/>, then applies optional clamping and rounding.
        /// Levels below 1 are clamped to 1.
        /// </summary>
        /// <param name="level">1-based level to evaluate.</param>
        /// <returns>Final evaluated value after optional clamping and rounding.</returns>
        public double Evaluate(int level)
        {
            int lvl = System.Math.Max(1, level);
            double x = lvl - 1;
            double raw;

            switch (mode)
            {
                case ProgressionCurveMode.Linear:
                    raw = baseValue + (growthRate * x);
                    break;
                case ProgressionCurveMode.Polynomial:
                    raw = baseValue + (growthRate * System.Math.Pow(x, exponent));
                    break;
                case ProgressionCurveMode.Exponential:
                    raw = baseValue * System.Math.Pow(System.Math.Max(0.0001d, 1d + growthRate), x);
                    break;
                case ProgressionCurveMode.Logarithmic:
                    raw = baseValue + (growthRate * System.Math.Log(1d + x));
                    break;
                case ProgressionCurveMode.SigmoidSoftCap:
                {
                    double k = System.Math.Max(0.001d, steepness);
                    double sig = 1d / (1d + System.Math.Exp(-k * (lvl - midpointLevel)));
                    raw = baseValue + ((softCap - baseValue) * sig);
                    break;
                }
                case ProgressionCurveMode.DiminishingReturns:
                {
                    double k = System.Math.Max(0.0001d, growthRate);
                    double stat = lvl;
                    raw = baseValue + (softCap * (stat / (stat + k)));
                    break;
                }
                case ProgressionCurveMode.UnityAnimationCurve:
                {
                    float maxLvl = Mathf.Max(2, referenceMaxLevel);
                    float t = Mathf.Clamp01((lvl - 1f) / (maxLvl - 1f));
                    float eval = animationCurve != null ? animationCurve.Evaluate(t) : t;
                    raw = baseValue + (eval * growthRate);
                    break;
                }
                case ProgressionCurveMode.CustomExpression:
                {
                    var vars = new Dictionary<string, double>(5, StringComparer.OrdinalIgnoreCase)
                    {
                        ["level"] = lvl,
                        ["x"] = x,
                        ["base"] = baseValue,
                        ["growth"] = growthRate,
                        ["cap"] = softCap
                    };
                    raw = MathExpressionEvaluator.EvaluateOrDefault(customExpression, vars, baseValue);
                    break;
                }
                default:
                    raw = baseValue;
                    break;
            }

            if (useClamp)
            {
                raw = System.Math.Clamp(raw, minClamp, maxClamp);
            }

            switch (rounding)
            {
                case CurveRoundingMode.Round:
                    raw = System.Math.Round(raw, MidpointRounding.AwayFromZero);
                    break;
                case CurveRoundingMode.Floor:
                    raw = System.Math.Floor(raw);
                    break;
                case CurveRoundingMode.Ceil:
                    raw = System.Math.Ceiling(raw);
                    break;
            }

            return raw;
        }
    }

    /// <summary>
    /// Reusable ScriptableObject defining a game progression, stat, XP, or cost curve.
    /// </summary>
    [CreateAssetMenu(fileName = "ProgressionCurve", menuName = "GameBalancer/Progression Curve", order = 100)]
    public class ProgressionCurveAsset : ScriptableObject, IProgressionCurve
    {
        public string curveName = "XP Required";
        public Color chartColor = new Color(0.28f, 0.72f, 1f);
        public ProgressionCurveSettings settings = ProgressionCurveSettings.CreateDefaultXpCurve();

        /// <inheritdoc/>
        public string DisplayName => string.IsNullOrWhiteSpace(curveName) ? name : curveName;

        /// <inheritdoc/>
        public double Evaluate(int level) => settings.Evaluate(level);

        /// <summary>
        /// Pre-evaluates the curve across an inclusive level range into a dense array for O(1) runtime lookup.
        /// </summary>
        /// <param name="minLevel">Inclusive starting level (clamped to at least 1).</param>
        /// <param name="maxLevel">Inclusive ending level (clamped to at least the start level).</param>
        /// <returns>Array of evaluated values where index 0 corresponds to the clamped start level.</returns>
        public double[] BakeTable(int minLevel, int maxLevel)
        {
            int start = System.Math.Max(1, minLevel);
            int end = System.Math.Max(start, maxLevel);
            int count = (end - start) + 1;
            var table = new double[count];
            for (int i = 0; i < count; i++)
            {
                table[i] = Evaluate(start + i);
            }
            return table;
        }
    }
}
