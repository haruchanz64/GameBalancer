# Changelog

## [1.0.1] - 2026-11-20
### Documentation
- Upgraded `README.md` into a full developer guide covering architecture, math expression variables, loot pity and PRD behavior, combat and economy simulation rules, verified C# code examples, extension contracts, samples, tests, and troubleshooting.
- Added XML doc comments across all public types, methods, constructors, properties, key struct fields, and non-trivial internal/private helpers in `Runtime/`, `Editor/`, and `Samples~/`.

## [1.0.0] - 2026-03-07
### Added
- Recursive-descent `MathExpressionEvaluator` supporting arithmetic operators, exponentiation, named variables, and common math functions.
- Progression curve engine (`ProgressionCurveAsset`, `IProgressionCurve`, `CurveComparison`) with eight curve modes and lookup table baking.
- Loot and pity simulation engine (`LootTableAsset`, `LootRoller`, `LootOddsAnalyzer`) supporting weighted rolls, soft and hard pity, draw without replacement, PRD proc calculation, and pulls-to-target percentiles.
- Discrete-event combat simulator and matchup matrix runner (`CombatantProfileAsset`, `ICombatStatSource`, `IDamageFormula`, `CombatSimulator`, `MatchupMatrixRunner`) with win-rate and TTK outlier detection.
- Multi-day resource economy simulator (`EconomyModelAsset`, `EconomySimulator`) with inflation and mandatory-sink starvation diagnostics.
- UI Toolkit Workbench (`GameBalancerWindow`) with `Painter2D` vector line charts (`BalanceLineChartElement`), UI Toolkit custom inspectors, and automatic `TypeCache` discovery for custom `IGameBalancerModule` tabs.
- CSV, JSON, and Markdown report exporter (`BalanceReportExporter`), sample custom module, and NUnit EditMode test suite.
