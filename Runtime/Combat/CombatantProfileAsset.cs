using System;
using GameBalancer.Curves;
using UnityEngine;

namespace GameBalancer.Combat
{
    /// <summary>
    /// ScriptableObject describing a combatant archetype with optional progression curve scaling.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatantProfile", menuName = "GameBalancer/Combatant Profile", order = 102)]
    public class CombatantProfileAsset : ScriptableObject, ICombatStatSource
    {
        public string combatantName = "Vanguard";
        public string archetype = "Tank";

        [Header("Base Stats (Level 1)")]
        [Min(1f)] public float maxHp = 220f;
        [Min(0.1f)] public float attack = 24f;
        [Min(0f)] public float defense = 18f;
        [Min(0.05f)] public float attackSpeed = 1.0f;
        [Range(0f, 1f)] public float critChance = 0.12f;
        [Min(1f)] public float critMultiplier = 1.5f;
        [Range(0f, 0.95f)] public float dodgeChance = 0.05f;
        [Range(0f, 0.5f)] public float damageVariance = 0.1f;
        [Min(0f)] public float hpRegenPerSec = 0f;

        [Header("Linear Per-Level Growth (Used When No Curve Override Is Set)")]
        public float hpPerLevel = 28f;
        public float attackPerLevel = 3.5f;
        public float defensePerLevel = 2.2f;

        [Header("Optional Progression Curve Overrides")]
        public ProgressionCurveAsset hpCurveOverride;
        public ProgressionCurveAsset attackCurveOverride;
        public ProgressionCurveAsset defenseCurveOverride;

        /// <inheritdoc/>
        public string DisplayName => string.IsNullOrWhiteSpace(combatantName) ? name : combatantName;

        /// <summary>
        /// Evaluates level-scaled HP, attack, and defense using curve overrides when assigned,
        /// falling back to linear per-level growth, and clamps combat rates to valid ranges.
        /// </summary>
        /// <param name="level">1-based level to evaluate (values below 1 are clamped to 1).</param>
        /// <returns>Clamped <see cref="CombatStatSnapshot"/> ready for simulation.</returns>
        public CombatStatSnapshot EvaluateStatsAtLevel(int level)
        {
            int lvl = System.Math.Max(1, level);
            int delta = lvl - 1;

            double hp = hpCurveOverride != null ? hpCurveOverride.Evaluate(lvl) : maxHp + (hpPerLevel * delta);
            double atk = attackCurveOverride != null ? attackCurveOverride.Evaluate(lvl) : attack + (attackPerLevel * delta);
            double def = defenseCurveOverride != null ? defenseCurveOverride.Evaluate(lvl) : defense + (defensePerLevel * delta);

            return new CombatStatSnapshot
            {
                name = DisplayName,
                archetype = archetype,
                level = lvl,
                maxHp = System.Math.Max(1d, hp),
                attack = System.Math.Max(0.1d, atk),
                defense = System.Math.Max(0d, def),
                attackSpeed = System.Math.Max(0.05d, attackSpeed),
                critChance = System.Math.Clamp(critChance, 0d, 1d),
                critMultiplier = System.Math.Max(1d, critMultiplier),
                dodgeChance = System.Math.Clamp(dodgeChance, 0d, 0.95d),
                damageVariance = System.Math.Clamp(damageVariance, 0d, 0.5d),
                hpRegenPerSec = System.Math.Max(0d, hpRegenPerSec)
            };
        }
    }
}
