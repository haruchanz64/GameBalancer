using System;

namespace GameBalancer.Combat
{
    [Serializable]
    public struct CombatStatSnapshot
    {
        public string name;
        public string archetype;
        public int level;
        public double maxHp;
        public double attack;
        public double defense;
        public double attackSpeed;
        public double critChance;
        public double critMultiplier;
        public double dodgeChance;
        public double damageVariance;
        public double hpRegenPerSec;
    }

    /// <summary>
    /// Implement on your own character, enemy, or weapon ScriptableObjects to feed live game data
    /// directly into GameBalancer's CombatSimulator and MatchupMatrixRunner.
    /// </summary>
    public interface ICombatStatSource
    {
        string DisplayName { get; }
        CombatStatSnapshot EvaluateStatsAtLevel(int level);
    }

    /// <summary>
    /// Implement to supply a custom damage calculation formula for combat simulations.
    /// </summary>
    public interface IDamageFormula
    {
        string FormulaName { get; }
        double ComputeRawHitDamage(in CombatStatSnapshot attacker, in CombatStatSnapshot defender);
    }
}
