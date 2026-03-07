using System.Collections.Generic;
using GameBalancer.Combat;
using GameBalancer.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GameBalancer.Tests
{
    public sealed class CombatMatrixTests
    {
        [Test]
        public void MatchupMatrix_MirrorMatchIsBalancedAndOutlierIsFlagged()
        {
            var balanced = ScriptableObject.CreateInstance<CombatantProfileAsset>();
            var boss = ScriptableObject.CreateInstance<CombatantProfileAsset>();

            try
            {
                balanced.combatantName = "Knight";
                balanced.maxHp = 200f;
                balanced.attack = 25f;
                balanced.defense = 15f;
                balanced.attackSpeed = 1f;
                balanced.critChance = 0.1f;

                boss.combatantName = "OverpoweredBoss";
                boss.maxHp = 800f;
                boss.attack = 70f;
                boss.defense = 50f;
                boss.attackSpeed = 1.2f;

                var report = MatchupMatrixRunner.RunMatrix(
                    new ICombatStatSource[] { balanced, boss },
                    level: 1,
                    trialsPerMatchup: 400,
                    seed: 101);

                var mirror = report.GetCell(0, 0);
                Assert.That(mirror.rowWinRatePercent, Is.InRange(42d, 58d));

                var knightVsBoss = report.GetCell(0, 1);
                Assert.AreEqual(BalanceOutlierFlag.Underpowered, knightVsBoss.outlierFlag);
                Assert.Less(knightVsBoss.rowWinRatePercent, 10d);

                var bossVsKnight = report.GetCell(1, 0);
                Assert.AreEqual(BalanceOutlierFlag.Overpowered, bossVsKnight.outlierFlag);
                Assert.Greater(bossVsKnight.rowWinRatePercent, 90d);
                Assert.GreaterOrEqual(report.outlierSummaries.Count, 1);
            }
            finally
            {
                Object.DestroyImmediate(balanced);
                Object.DestroyImmediate(boss);
            }
        }

        [Test]
        public void GameBalancerWindow_DiscoversAllFourBuiltInModules()
        {
            var modules = GameBalancerWindow.DiscoverAllModules();
            Assert.GreaterOrEqual(modules.Count, 4);
            Assert.IsTrue(modules.Exists(m => m.ModuleId == "builtin.curves"));
            Assert.IsTrue(modules.Exists(m => m.ModuleId == "builtin.loot"));
            Assert.IsTrue(modules.Exists(m => m.ModuleId == "builtin.combat"));
            Assert.IsTrue(modules.Exists(m => m.ModuleId == "builtin.economy"));
        }
    }
}
