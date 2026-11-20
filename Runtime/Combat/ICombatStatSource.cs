using System;

namespace GameBalancer.Combat
{
    /// <summary>
    /// Evaluated per-level combat statistics consumed by <see cref="CombatSimulator"/> and <see cref="IDamageFormula"/>.
    /// </summary>
    [Serializable]
    public struct CombatStatSnapshot
    {
        /// <summary>Display name of the combatant.</summary>
        public string name;
        /// <summary>Optional role or class tag (for example, Tank, Rogue, Mage).</summary>
        public string archetype;
        /// <summary>1-based level at which these stats were evaluated.</summary>
        public int level;
        /// <summary>Maximum health pool (clamped to at least 1 during simulation).</summary>
        public double maxHp;
        /// <summary>Base attack power before mitigation, variance, and critical hits.</summary>
        public double attack;
        /// <summary>Defense or armor rating used by the active damage mitigation formula.</summary>
        public double defense;
        /// <summary>Attacks performed per second (clamped to at least 0.05 in simulation).</summary>
        public double attackSpeed;
        /// <summary>Critical hit probability in [0, 1].</summary>
        public double critChance;
        /// <summary>Damage multiplier applied on a critical hit (minimum 1.0).</summary>
        public double critMultiplier;
        /// <summary>Probability in [0, 0.95] to completely avoid an incoming hit.</summary>
        public double dodgeChance;
        /// <summary>Symmetric random damage spread factor (for example, 0.1 applies +/-10% variance).</summary>
        public double damageVariance;
        /// <summary>Continuous health regenerated per second between discrete attack events.</summary>
        public double hpRegenPerSec;
    }

    /// <summary>
    /// Implement on your own character, enemy, or weapon ScriptableObjects to feed live game data
    /// directly into GameBalancer's CombatSimulator and MatchupMatrixRunner.
    /// </summary>
    public interface ICombatStatSource
    {
        /// <summary>
        /// Label shown in matchup matrix headers and exported reports.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Evaluates this combatant's stats at the requested 1-based level.
        /// </summary>
        /// <param name="level">Target combat level (1 or greater).</param>
        /// <returns>Populated <see cref="CombatStatSnapshot"/> for <paramref name="level"/>.</returns>
        CombatStatSnapshot EvaluateStatsAtLevel(int level);
    }

    /// <summary>
    /// Implement to supply a custom damage calculation formula for combat simulations.
    /// </summary>
    public interface IDamageFormula
    {
        /// <summary>
        /// Descriptive name of the custom damage formula.
        /// </summary>
        string FormulaName { get; }

        /// <summary>
        /// Computes mitigated base damage for a single hit before variance and critical hit rolls.
        /// </summary>
        /// <param name="attacker">Attacking combatant stats.</param>
        /// <param name="defender">Defending combatant stats.</param>
        /// <returns>Raw mitigated damage value (clamped against <see cref="CombatSimulationOptions.minDamageFloor"/> by the simulator).</returns>
        double ComputeRawHitDamage(in CombatStatSnapshot attacker, in CombatStatSnapshot defender);
    }
}
