using System;
using System.Collections.Generic;
using GameBalancer.Math;

namespace GameBalancer.Combat
{
    public enum DamageFormulaMode
    {
        ArmorPercentage = 0,
        FlatReduction = 1,
        CustomExpression = 2
    }

    [Serializable]
    public struct CombatSimulationOptions
    {
        public DamageFormulaMode formulaMode;
        public double armorConstant;
        public double minDamageFloor;
        public string customFormulaExpression;
        public double maxDuelDurationSeconds;

        public static CombatSimulationOptions Default => new CombatSimulationOptions
        {
            formulaMode = DamageFormulaMode.ArmorPercentage,
            armorConstant = 100d,
            minDamageFloor = 1d,
            customFormulaExpression = "max(1, atk * (100 / (100 + def)))",
            maxDuelDurationSeconds = 120d
        };
    }

    public readonly struct DuelResult
    {
        /// <summary>1 if A won, -1 if B won, 0 if draw/timeout.</summary>
        public readonly int Winner;
        public readonly double DurationSeconds;
        public readonly double WinnerRemainingHpPercent;
        public readonly bool TimedOut;

        public DuelResult(int winner, double durationSeconds, double winnerRemainingHpPercent, bool timedOut)
        {
            Winner = winner;
            DurationSeconds = durationSeconds;
            WinnerRemainingHpPercent = winnerRemainingHpPercent;
            TimedOut = timedOut;
        }
    }

    /// <summary>
    /// Deterministic-seedable discrete-event combat simulator for TTK and win-rate analysis.
    /// </summary>
    public sealed class CombatSimulator
    {
        private readonly System.Random _rng;
        private readonly Dictionary<string, double> _exprVars = new Dictionary<string, double>(6, StringComparer.OrdinalIgnoreCase);

        public CombatSimulator(int? seed = null)
        {
            _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        public DuelResult SimulateDuel(
            in CombatStatSnapshot a,
            in CombatStatSnapshot b,
            in CombatSimulationOptions options,
            IDamageFormula customFormula = null)
        {
            double hpA = System.Math.Max(1d, a.maxHp);
            double hpB = System.Math.Max(1d, b.maxHp);
            double maxDuration = options.maxDuelDurationSeconds > 0d ? options.maxDuelDurationSeconds : 120d;

            double intervalA = 1d / System.Math.Max(0.05d, a.attackSpeed);
            double intervalB = 1d / System.Math.Max(0.05d, b.attackSpeed);

            // Randomize tie-break slightly on initial swing when speeds match so mirror matches are fair
            double nextA = intervalA;
            double nextB = intervalB;
            double now = 0d;

            while (now < maxDuration && hpA > 0d && hpB > 0d)
            {
                bool aFirst;
                if (System.Math.Abs(nextA - nextB) < 1e-9d)
                {
                    aFirst = _rng.NextDouble() < 0.5d;
                }
                else
                {
                    aFirst = nextA < nextB;
                }

                double eventTime = aFirst ? nextA : nextB;
                if (eventTime > maxDuration)
                {
                    break;
                }

                double dt = eventTime - now;
                now = eventTime;

                if (dt > 0d)
                {
                    if (a.hpRegenPerSec > 0d) hpA = System.Math.Min(a.maxHp, hpA + (a.hpRegenPerSec * dt));
                    if (b.hpRegenPerSec > 0d) hpB = System.Math.Min(b.maxHp, hpB + (b.hpRegenPerSec * dt));
                }

                if (aFirst)
                {
                    hpB -= RollHitDamage(in a, in b, in options, customFormula);
                    nextA += intervalA;
                }
                else
                {
                    hpA -= RollHitDamage(in b, in a, in options, customFormula);
                    nextB += intervalB;
                }
            }

            if (hpA > 0d && hpB <= 0d)
            {
                return new DuelResult(1, now, (hpA / a.maxHp) * 100d, false);
            }

            if (hpB > 0d && hpA <= 0d)
            {
                return new DuelResult(-1, now, (hpB / b.maxHp) * 100d, false);
            }

            return new DuelResult(0, now, 0d, true);
        }

        public double ComputeMitigatedBaseDamage(
            in CombatStatSnapshot attacker,
            in CombatStatSnapshot defender,
            in CombatSimulationOptions options,
            IDamageFormula customFormula = null)
        {
            if (customFormula != null)
            {
                return System.Math.Max(options.minDamageFloor, customFormula.ComputeRawHitDamage(in attacker, in defender));
            }

            double floor = System.Math.Max(0d, options.minDamageFloor);
            switch (options.formulaMode)
            {
                case DamageFormulaMode.FlatReduction:
                    return System.Math.Max(floor, attacker.attack - defender.defense);

                case DamageFormulaMode.CustomExpression:
                    _exprVars["atk"] = attacker.attack;
                    _exprVars["def"] = defender.defense;
                    _exprVars["hp"] = defender.maxHp;
                    _exprVars["level"] = attacker.level;
                    double eval = MathExpressionEvaluator.EvaluateOrDefault(options.customFormulaExpression, _exprVars, attacker.attack);
                    return System.Math.Max(floor, eval);

                default:
                {
                    double k = System.Math.Max(1d, options.armorConstant > 0d ? options.armorConstant : 100d);
                    double mitigation = k / (k + System.Math.Max(0d, defender.defense));
                    return System.Math.Max(floor, attacker.attack * mitigation);
                }
            }
        }

        private double RollHitDamage(
            in CombatStatSnapshot attacker,
            in CombatStatSnapshot defender,
            in CombatSimulationOptions options,
            IDamageFormula customFormula)
        {
            if (defender.dodgeChance > 0d && _rng.NextDouble() < defender.dodgeChance)
            {
                return 0d;
            }

            double dmg = ComputeMitigatedBaseDamage(in attacker, in defender, in options, customFormula);

            if (attacker.damageVariance > 0d)
            {
                double factor = 1d + (((_rng.NextDouble() * 2d) - 1d) * attacker.damageVariance);
                dmg *= System.Math.Max(0.1d, factor);
            }

            if (attacker.critChance > 0d && _rng.NextDouble() < attacker.critChance)
            {
                dmg *= System.Math.Max(1d, attacker.critMultiplier);
            }

            return System.Math.Max(0d, dmg);
        }
    }
}
