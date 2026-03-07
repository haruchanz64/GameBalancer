# Changelog

## [1.0.0] - 2026-03-07
### Added
- Recursive-descent `MathExpressionEvaluator` supporting arithmetic operators, exponentiation, named variables, and common math functions.
- Progression curve engine (`ProgressionCurveAsset`, `IProgressionCurve`, `CurveComparison`) with eight curve modes and lookup table baking.
- Loot and pity simulation engine (`LootTableAsset`, `LootRoller`, `LootOddsAnalyzer`) supporting weighted rolls, soft and hard pity, draw without replacement, PRD proc calculation, and pulls-to-target percentiles.
- Discrete-event combat simulator and matchup matrix runner (`CombatantProfileAsset`, `ICombatStatSource`, `IDamageFormula`, `CombatSimulator`, `MatchupMatrixRunner`) with win-rate and TTK outlier detection.
- Multi-day resource economy simulator (`EconomyModelAsset`, `EconomySimulator`) with inflation and mandatory-sink starvation diagnostics.
- UI Toolkit Workbench (`GameBalancerWindow`) with `Painter2D` vector line charts (`BalanceLineChartElement`), UI Toolkit custom inspectors, and automatic `TypeCache` discovery for custom `IGameBalancerModule` tabs.
- CSV, JSON, and Markdown report exporter (`BalanceReportExporter`), sample custom module, and NUnit EditMode test suite.
