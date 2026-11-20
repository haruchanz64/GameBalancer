using System;
using System.Collections.Generic;
using GameBalancer.Math;

namespace GameBalancer.Combat
{
    /// <summary>
    /// Built-in damage mitigation formulas used when no <see cref="IDamageFormula"/> override is supplied.
    /// </summary>
    public enum DamageFormulaMode
    {
        /// <summary>Hyperbolic armor mitigation: <c>attack * (armorConstant / (armorConstant + defense))</c>.</summary>
        ArmorPercentage = 0,
        /// <summary>Flat subtraction: <c>attack - defense</c> clamped to <see cref="CombatSimulationOptions.minDamageFloor"/>.</summary>
        FlatReduction = 1,
        /// <summary>Evaluates <see cref="CombatSimulationOptions.customFormulaExpression"/> with <c>atk</c>, <c>def</c>, <c>hp</c>, and <c>level</c>.</summary>
        CustomExpression = 2
    }

    /// <summary>
    /// Configuration options controlling damage mitigation and timeout bounds for <see cref="CombatSimulator"/>.
    /// </summary>
    [Serializable]
    public struct CombatSimulationOptions
    {
        /// <summary>Active built-in damage formula mode (ignored when a custom <see cref="IDamageFormula"/> is passed).</summary>
        public DamageFormulaMode formulaMode;
        /// <summary>Half-mitigation armor constant <c>K</c> used by <see cref="DamageFormulaMode.ArmorPercentage"/> (defaults to 100 when &lt;= 0).</summary>
        public double armorConstant;
        /// <summary>Minimum damage dealt by any non-dodged hit.</summary>
        public double minDamageFloor;
        /// <summary>Math expression evaluated in <see cref="DamageFormulaMode.CustomExpression"/> mode.</summary>
        public string customFormulaExpression;
        /// <summary>Maximum simulated duel time in seconds before declaring a draw/timeout (defaults to 120 when &lt;= 0).</summary>
        public double maxDuelDurationSeconds;

        /// <summary>
        /// Default options using <see cref="DamageFormulaMode.ArmorPercentage"/> with armor constant 100,
        /// minimum damage 1, and a 120-second duel timeout.
        /// </summary>
        public static CombatSimulationOptions Default => new CombatSimulationOptions
        {
            formulaMode = DamageFormulaMode.ArmorPercentage,
            armorConstant = 100d,
            minDamageFloor = 1d,
            customFormulaExpression = "max(1, atk * (100 / (100 + def)))",
            maxDuelDurationSeconds = 120d
        };
    }

    /// <summary>
    /// Result of a single simulated duel between two combatants.
    /// </summary>
    public readonly struct DuelResult
    {
        /// <summary>1 if A won, -1 if B won, 0 if draw/timeout.</summary>
        public readonly int Winner;
        /// <summary>Simulated combat duration in seconds until defeat or timeout.</summary>
        public readonly double DurationSeconds;
        /// <summary>Remaining health percentage of the winner in [0, 100], or 0 on timeout.</summary>
        public readonly double WinnerRemainingHpPercent;
        /// <summary><c>true</c> if neither combatant fell before <see cref="CombatSimulationOptions.maxDuelDurationSeconds"/>.</summary>
        public readonly bool TimedOut;

        /// <summary>
        /// Creates a duel outcome record.
        /// </summary>
        /// <param name="winner">1 for combatant A, -1 for combatant B, or 0 for draw/timeout.</param>
        /// <param name="durationSeconds">Elapsed simulated seconds.</param>
        /// <param name="winnerRemainingHpPercent">Winner's remaining HP percentage (0 to 100).</param>
        /// <param name="timedOut">Whether the duel reached the duration limit.</param>
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

        /// <summary>
        /// Initializes a new <see cref="CombatSimulator"/> with an optional fixed PRNG seed.
        /// </summary>
        /// <param name="seed">Optional seed for deterministic combat outcomes; omit for time-based seeding.</param>
        public CombatSimulator(int? seed = null)
        {
            _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        /// <summary>
        /// Simulates a 1v1 discrete-event duel between combatants <paramref name="a"/> and <paramref name="b"/>
        /// by stepping directly to each attack timestamp, applying continuous HP regeneration between events.
        /// Simultaneous attack timestamps tie-break uniformly at random so mirror matchups stay symmetric.
        /// </summary>
        /// <param name="a">Stats for combatant A.</param>
        /// <param name="b">Stats for combatant B.</param>
        /// <param name="options">Damage formula and duel timeout configuration.</param>
        /// <param name="customFormula">Optional custom damage formula overriding <see cref="CombatSimulationOptions.formulaMode"/>.</param>
        /// <returns>Outcome containing winner, duration in seconds, remaining HP percentage, and timeout status.</returns>
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

        /// <summary>
        /// Computes mitigated base hit damage before dodge, random variance, and critical hit rolls.
        /// </summary>
        /// <param name="attacker">Attacking combatant snapshot.</param>
        /// <param name="defender">Defending combatant snapshot.</param>
        /// <param name="options">Formula configuration and minimum damage floor.</param>
        /// <param name="customFormula">Optional custom formula overriding built-in mitigation modes.</param>
        /// <returns>Mitigated base damage clamped to at least <see cref="CombatSimulationOptions.minDamageFloor"/>.</returns>
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

        /// <summary>
        /// Resolves a single attack event including dodge check, base mitigation, random variance, and critical multiplier.
        /// </summary>
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
