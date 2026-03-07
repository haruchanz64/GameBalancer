using System;
using System.Collections.Generic;
using GameBalancer.Math;
using UnityEngine;

namespace GameBalancer.Curves
{
    public enum ProgressionCurveMode
    {
        Linear = 0,
        Polynomial = 1,
        Exponential = 2,
        Logarithmic = 3,
        SigmoidSoftCap = 4,
        DiminishingReturns = 5,
        UnityAnimationCurve = 6,
        CustomExpression = 7
    }

    public enum CurveRoundingMode
    {
        None = 0,
        Round = 1,
        Floor = 2,
        Ceil = 3
    }

    /// <summary>
    /// Implement to plug a custom curve formula or external lookup table into GameBalancer.
    /// </summary>
    public interface IProgressionCurve
    {
        string DisplayName { get; }
        double Evaluate(int level);
    }

    /// <summary>
    /// Serializable parameters for a progression or scaling curve.
    /// </summary>
    [Serializable]
    public struct ProgressionCurveSettings
    {
        public ProgressionCurveMode mode;
        public float baseValue;
        public float growthRate;
        public float exponent;
        public float softCap;
        public float midpointLevel;
        public float steepness;
        public int referenceMaxLevel;
        public string customExpression;
        public CurveRoundingMode rounding;
        public bool useClamp;
        public float minClamp;
        public float maxClamp;
        public AnimationCurve animationCurve;

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

        public string DisplayName => string.IsNullOrWhiteSpace(curveName) ? name : curveName;

        public double Evaluate(int level) => settings.Evaluate(level);

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
