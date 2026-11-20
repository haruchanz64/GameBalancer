# GameBalancer

Unity 6 runtime simulation library and UI Toolkit Editor workbench for progression curves, loot and pity Monte Carlo odds, combat Time-to-Kill (TTK) matchup matrices, and multi-day resource economy modeling.

All simulation engines live in `GameBalancer.Runtime` with zero external dependencies so they can run in gameplay code, headless batch tests, or EditMode validation suites. The Editor workbench (`GameBalancerWindow`) renders vector charts with UI Toolkit `Painter2D` and automatically discovers custom `IGameBalancerModule` tabs via `TypeCache`.

## Features

- **Recursive-descent math evaluator (`MathExpressionEvaluator`):** Parses `+`, `-`, `*`, `/`, right-associative `^`, unary signs, parentheses, constants (`pi`, `e`), case-insensitive named variables, and 14 built-in math functions (`pow`, `sqrt`, `abs`, `floor`, `ceil`, `round`, `exp`, `ln`, `log`, `min`, `max`, `clamp`, `lerp`, `smoothstep`).
- **Progression curves (`ProgressionCurveAsset`, `CurveComparison`):** Supports eight curve modes (`Linear`, `Polynomial`, `Exponential`, `Logarithmic`, `SigmoidSoftCap`, `DiminishingReturns`, `UnityAnimationCurve`, and `CustomExpression`), optional clamping and rounding, dense `BakeTable` arrays for O(1) runtime lookup, multi-series comparison, and A/B ratio calculation.
- **Loot and pity odds (`LootTableAsset`, `LootRoller`, `LootOddsAnalyzer`):** Provides seedable weighted rolls, soft and hard pity rules tracked via `PityState`, draw-without-replacement batches (`RollWithoutReplacement`), and Pseudo-Random Distribution procs (`RollPseudoRandomProc`, `ComputePrdConstant`). Compares theoretical drop rates and expected value against Monte Carlo trials with P50, P90, P95, P99, and worst-case pulls to a target rarity.
- **Combat matchup matrix (`CombatantProfileAsset`, `CombatSimulator`, `MatchupMatrixRunner`):** Runs seedable discrete-event 1v1 duels with attack speed scheduling, HP regeneration, dodge, damage variance, critical hits, and three built-in mitigation modes (`ArmorPercentage`, `FlatReduction`, `CustomExpression`) plus custom `IDamageFormula` hooks. Produces N x N Win-Rate and TTK heatmaps and flags `Overpowered`, `Underpowered`, `TtkTooFast`, and `TtkTooSlow` outliers.
- **Economy flow modeling (`EconomyModelAsset`, `EconomySimulator`):** Models multi-currency faucets and sinks over daily or per-session horizons using flat values or expressions (`day`, `session`, `level`, `balance`), supporting `EveryDay`, `EveryNDays`, and `OneTimeOnDay` cadences, currency caps, inflation alerts, and mandatory-sink starvation detection.
- **Extensible UI Toolkit workbench and reporting (`GameBalancerWindow`, `BalanceReportExporter`):** Ships with four built-in tabs (`Curves & Formulas`, `Loot & Pity Odds`, `Combat Matrix`, `Economy Flow`), custom UI Toolkit inspectors with live previews and one-click **Open in GameBalancer** routing, GPU-accelerated `BalanceLineChartElement` charts, and CSV/JSON/Markdown exports.

## Requirements

- Unity `6000.0` or newer
- Zero required external dependencies (`GameBalancer.Runtime` and `GameBalancer.Editor` have no third-party package references)

## Installation

### Unity Package Manager (Git URL)

1. Open **Window > Package Manager** in Unity 6 (`6000.0+`).
2. Click **+** (top-left) and choose **Install package from git URL...**.
3. Enter:

```text
https://github.com/haruchanz64/GameBalancer.git
```

### Local package

Add the package path to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.haruchanz64.game-balancer": "file:../Plugins/GameBalancer"
  }
}
```

## Quick start

1. Open **Tools > HaruChanZ64 > Gameplay > GameBalancer > Balance Workbench**.
2. Click any of the four built-in modules in the sidebar (**Curves & Formulas**, **Loot & Pity Odds**, **Combat Matrix**, or **Economy Flow**). Each module runs with built-in demo data when no assets are assigned.
3. To persist project data, create assets from **Assets > Create > GameBalancer**:
   - `Progression Curve` (`ProgressionCurveAsset`)
   - `Loot Table` (`LootTableAsset`)
   - `Combatant Profile` (`CombatantProfileAsset`)
   - `Economy Model` (`EconomyModelAsset`)
4. Select any created asset in the Project window to inspect its live summary card in the Inspector, then click **Open in GameBalancer** to route the asset directly into its matching Workbench tab.
5. Export CSV, JSON, or Markdown reports from the action bar in each module. Curve and economy CSV exports use plain header rows and invariant numeric formatting, making them compatible with [SpreadsheetToScriptableObjects](https://github.com/haruchanz64/SpreadsheetToScriptableObjects) for round-trip asset updates.

## Core concepts

### Formula evaluation (`MathExpressionEvaluator`)

Custom expressions across curves, combat formulas, and economy nodes share `MathExpressionEvaluator.TryEvaluate`. Division by values with magnitude below `1e-12`, trailing characters, `NaN`, or infinity return `false` with an error message; `EvaluateOrDefault` returns a caller-supplied fallback instead of throwing. Exponentiation (`^`) is right-associative (`2 ^ 3 ^ 2 = 512`), and variable lookups fall back to case-insensitive matching.

Available variables depend on the context:
- **Progression curves (`CustomExpression`):** `level`, `x` (`level - 1`), `base`, `growth`, `cap`
- **Combat formulas (`CustomExpression`):** `atk`, `def`, `hp` (defender max HP), `level` (attacker level)
- **Economy flow nodes (`useFormula = true`):** `day`, `session` (alias for `day`), `level` (alias for `day`), `balance` (current currency balance before the node applies)

### Loot pity and Pseudo-Random Distribution

When `PityRule.enabled` is true and a `PityState` is supplied to `LootRoller.Roll`:
1. On pulls below `softPityStartPull`, the target rarity uses its base weight share.
2. Starting at `softPityStartPull`, each consecutive non-target pull adds `softPityBonusPerPull` to the target rarity probability (clamped to `1.0`).
3. At `hardPityPull`, a target-rarity entry is guaranteed.
4. If the target rarity misses, the roller draws exclusively from non-target entries so target weights are not double-counted, and increments `PityState.pullsSinceTarget`. On a hit, `pullsSinceTarget` resets to `0`.

For proc abilities or critical streaks, `LootRoller.RollPseudoRandomProc(nominalProbability, ref consecutiveMisses)` solves for the PRD constant `C` via `ComputePrdConstant` (40-step binary search) so attempt `n` procs with probability `min(1, n * C)` while converging to the target long-run average.

### Discrete-event combat simulation and outlier flags

`CombatSimulator.SimulateDuel` advances time directly to the next attack timestamp (`1 / max(0.05, attackSpeed)`), applying continuous `hpRegenPerSec` across each interval. Simultaneous swings tie-break uniformly at random (`50/50`) so mirror matchups stay symmetric.

`MatchupMatrixRunner.RunMatrix` simulates every ordered pair across the roster and assigns a `BalanceOutlierFlag` to non-mirror cells in priority order: `Overpowered` (`winRate > targetMaxWinRate`), `Underpowered` (`winRate < targetMinWinRate`), `TtkTooFast` (`avgTtk < targetMinTtk`), `TtkTooSlow` (`avgTtk > targetMaxTtk`), or `Balanced`. Outlier summaries are recorded once per unordered pair (`row < col`).

### Economy pass order and diagnostics

`EconomySimulator.Simulate` evaluates each currency independently per day, always executing all matching `Faucet` nodes before `Sink` nodes on the same day. Faucets clamp the balance to `maxCap` when `hasCap` is enabled. When a `Sink` exceeds the current balance:
- If `mandatorySink` is `true`, the balance is left unchanged and that day increments `starvationDays`.
- If `mandatorySink` is `false`, the remaining balance is drained to `0`.

At the end of the horizon, `inflationFlagged` is set when `inflationAlertThreshold > 0`, `endingBalance > inflationAlertThreshold`, and `totalInflow > totalOutflow * 1.25`.

## Code examples

### 1. Bake a progression curve and compare curve ratios

```csharp
using GameBalancer.Curves;
using GameBalancer.Export;
using UnityEngine;

public sealed class ProgressionLookupExample : MonoBehaviour
{
    [SerializeField] private ProgressionCurveAsset playerHpCurve;
    [SerializeField] private ProgressionCurveAsset enemyDpsCurve;

    private double[] _xpTable;

    private void Awake()
    {
        // Pre-bake levels 1..50 for O(1) runtime lookup.
        _xpTable = playerHpCurve.BakeTable(1, 50);

        // Compare Player HP against Enemy DPS and compute the ratio series (index 0 / index 1).
        var report = CurveComparison.Evaluate(
            new IProgressionCurve[] { playerHpCurve, enemyDpsCurve },
            minLevel: 1,
            maxLevel: 50,
            ratioNumeratorIndex: 0,
            ratioDenominatorIndex: 1);

        string csv = BalanceReportExporter.CurvesToCsv(report);
        Debug.Log($"Lv10 HP: {_xpTable[9]:F0}, Lv10 HP/DPS ratio: {report.ratioValues[9]:F2}\n{csv}");
    }
}
```

### 2. Roll loot with pity, draw without replacement, and analyze Monte Carlo odds

```csharp
using GameBalancer.Loot;
using UnityEngine;

public sealed class LootRuntimeExample : MonoBehaviour
{
    [SerializeField] private LootTableAsset chestTable;

    private readonly PityState _pityState = new PityState();
    private LootRoller _roller;

    private void Awake()
    {
        _roller = new LootRoller(seed: 12345);
    }

    public void OpenSinglePull()
    {
        LootDropResult drop = _roller.Roll(chestTable, _pityState);
        if (drop.Entry != null)
        {
            Debug.Log($"Dropped {drop.Quantity}x {drop.Entry.displayName} (Pity: {drop.TriggeredPity})");
        }
    }

    public void AuditTableOdds()
    {
        var report = LootOddsAnalyzer.Analyze(chestTable, trialCount: 50000, seed: 42);
        Debug.Log($"EV/roll: {report.simulatedValuePerRoll:F2}, P90 pulls to {report.targetRarity}: {report.pullsToTargetP90}");
    }
}
```

### 3. Connect custom game assets and a custom damage formula to the combat matrix

Implement `ICombatStatSource` on your own character or enemy ScriptableObjects and `IDamageFormula` for game-specific mitigation math:

```csharp
using GameBalancer.Combat;
using UnityEngine;

public sealed class UnitConfigAsset : ScriptableObject, ICombatStatSource
{
    public string unitName = "Paladin";
    public float baseHp = 260f;
    public float baseAttack = 28f;
    public float baseArmor = 22f;

    public string DisplayName => unitName;

    public CombatStatSnapshot EvaluateStatsAtLevel(int level)
    {
        int d = System.Math.Max(0, level - 1);
        return new CombatStatSnapshot
        {
            name = unitName,
            archetype = "Bruiser",
            level = System.Math.Max(1, level),
            maxHp = baseHp + (30d * d),
            attack = baseAttack + (3.5d * d),
            defense = baseArmor + (2.5d * d),
            attackSpeed = 1.0d,
            critChance = 0.15d,
            critMultiplier = 1.6d,
            dodgeChance = 0.05d,
            damageVariance = 0.1d
        };
    }
}

public sealed class ElementalMitigationFormula : IDamageFormula
{
    public string FormulaName => "Soft Armor Curve";

    public double ComputeRawHitDamage(in CombatStatSnapshot attacker, in CombatStatSnapshot defender)
    {
        double reduction = defender.defense / (defender.defense + 120d);
        return attacker.attack * (1d - reduction);
    }
}
```

Run the matrix headlessly from code or an EditMode test:

```csharp
var report = MatchupMatrixRunner.RunMatrix(
    roster,
    level: 10,
    trialsPerMatchup: 1000,
    customFormula: new ElementalMitigationFormula(),
    targetMinWinRate: 45d,
    targetMaxWinRate: 55d,
    seed: 101);
```

### 4. Add a custom Editor Workbench tab (`IGameBalancerModule`)

Implement `IGameBalancerModule` in any Editor assembly with a public parameterless constructor. `GameBalancerWindow` discovers it automatically via `TypeCache` and adds it to the sidebar sorted by `SortOrder`:

```csharp
#if UNITY_EDITOR
using GameBalancer.Editor.Core;
using UnityEngine.UIElements;

public sealed class StaminaBudgetModule : IGameBalancerModule
{
    public string ModuleId => "studio.stamina-budget";
    public string DisplayName => "Stamina Budget";
    public string Description => "Audits sprint and dodge stamina costs.";
    public int SortOrder => 50;

    private Label _summary;

    public VisualElement CreateView(GameBalancerHost host)
    {
        var root = new VisualElement();
        var card = new VisualElement();
        card.AddToClassList("gb-card");

        _summary = new Label("Sprint + 3 dodges = 95 / 100 stamina");
        card.Add(_summary);
        root.Add(card);

        host.SetStatus("Stamina Budget ready.");
        return root;
    }

    public void OnRefresh() { }
    public bool TryAcceptAsset(UnityEngine.Object asset) => false;
}
#endif
```

## Extension contracts and core API

| Type | Namespace | Purpose | Built-in implementation |
| :--- | :--- | :--- | :--- |
| `IProgressionCurve` | `GameBalancer.Curves` | Evaluates a named curve by 1-based level for `CurveComparison` | `ProgressionCurveAsset` |
| `ICombatStatSource` | `GameBalancer.Combat` | Supplies level-evaluated `CombatStatSnapshot` data to combat sims | `CombatantProfileAsset` |
| `IDamageFormula` | `GameBalancer.Combat` | Overrides raw mitigated hit damage inside `CombatSimulator` | `DamageFormulaMode` (`ArmorPercentage`, `FlatReduction`, `CustomExpression`) |
| `IGameBalancerModule` | `GameBalancer.Editor.Core` | Auto-discovered UI Toolkit tab hosted inside `GameBalancerWindow` | `CurvesModule`, `LootModule`, `CombatMatrixModule`, `EconomyModule` |
| `MathExpressionEvaluator` | `GameBalancer.Math` | Parses and evaluates arithmetic expressions, variables, and math functions | `MathExpressionEvaluator` |
| `LootRoller` / `LootOddsAnalyzer` | `GameBalancer.Loot` | Seedable loot rolling (pity, without replacement, PRD) and Monte Carlo analysis | `LootRoller`, `LootOddsAnalyzer`, `LootTableAsset` |
| `CombatSimulator` / `MatchupMatrixRunner` | `GameBalancer.Combat` | Discrete-event 1v1 duel engine and N x N round-robin outlier analyzer | `CombatSimulator`, `MatchupMatrixRunner` |
| `EconomySimulator` | `GameBalancer.Economy` | Multi-day faucet/sink simulator with inflation and starvation alerts | `EconomySimulator`, `EconomyModelAsset` |
| `BalanceReportExporter` | `GameBalancer.Export` | Exports curve, loot, combat, and economy reports to CSV, JSON, and Markdown | `BalanceReportExporter` |
| `BalanceLineChartElement` | `GameBalancer.Editor.UI` | Reusable UI Toolkit `Painter2D` multi-series line chart with threshold overlay | `BalanceLineChartElement` |

## Included samples

Import **RPG Balance Demo & Custom Module** (`Samples~/RpgBalanceDemo`) from the Package Manager **Samples** tab:

- `CustomEffectiveDpsModule`: an `#if UNITY_EDITOR` custom `IGameBalancerModule` (`SortOrder = 100`) that adds an **Effective DPS (Sample)** tab to the Balance Workbench, computing expected DPS live from Base Attack, Attacks / Second, Crit Chance, and Crit Multiplier fields.

## Testing

The package includes EditMode NUnit tests under `Tests/Editor/` runnable from **Window > General > Test Runner** (EditMode tab):

- `Tests/Editor/MathAndCurveTests.cs`: verifies operator precedence, right-associative exponentiation, division-by-zero rejection in `MathExpressionEvaluator`, and linear curve evaluation plus ratio comparison in `ProgressionCurveAsset` and `CurveComparison`.
- `Tests/Editor/LootOddsTests.cs`: verifies Monte Carlo convergence against theoretical drop weights without pity, hard-pity upper-bound enforcement in `LootOddsAnalyzer`, unique draws in `LootRoller.RollWithoutReplacement`, and PRD convergence at `p = 0.25`.
- `Tests/Editor/CombatMatrixTests.cs`: verifies mirror-match symmetry and `Overpowered` / `Underpowered` outlier flagging in `MatchupMatrixRunner`, plus `TypeCache` discovery of all four built-in modules in `GameBalancerWindow.DiscoverAllModules()`.
- `Tests/Editor/EconomyAndExportTests.cs`: verifies inflation flagging, mandatory-sink starvation detection, and CSV formatting in `EconomySimulator` and `BalanceReportExporter`.

## Troubleshooting and limitations

- **Custom `IGameBalancerModule` tab does not appear in the sidebar:** Ensure the class is concrete (not `abstract`), has a public parameterless constructor, and compiles inside an Editor assembly (or `#if UNITY_EDITOR`). `GameBalancerWindow.DiscoverAllModules()` skips types without a parameterless constructor and logs a warning if instantiation throws.
- **Curves tab caps the plotted span to 500 levels:** `CurvesModule` clamps `maxLevel` to `minLevel + 500` in the Workbench UI to keep chart rendering responsive. Call `CurveComparison.Evaluate` or `ProgressionCurveAsset.BakeTable` directly in C# if you need larger ranges.
- **Loot tab soft-pity step size is read from the asset:** `LootModule` exposes `Enable Pity`, `Target Rarity`, `Soft Start`, and `Hard Guarantee` inline, while `softPityBonusPerPull` is read from the assigned `LootTableAsset.pityRule` (defaulting to `0.04` when no asset is assigned).
- **Combat Matrix uses the demo roster with fewer than two assets:** `CombatMatrixModule` falls back to its built-in 4-archetype roster (`Vanguard`, `Rogue`, `Mage`, `Berserker`) until at least two non-null `CombatantProfileAsset` assets are assigned in the roster slots. Custom win-rate/TTK outlier thresholds and `IDamageFormula` overrides are configured via `MatchupMatrixRunner.RunMatrix` in code.
- **Economy tab charts and exports the first currency only:** `EconomyModule` plots KPIs, chart lines, and CSV output for `report.currencies[0]`. Multi-currency models are still simulated completely by `EconomySimulator.Simulate`, and all currencies are included in JSON exports and `BalanceReportExporter.EconomyToCsv(report, currencyIndex)`.
- **Invalid custom expressions fall back silently at simulation time:** When a formula fails to parse or divides by near-zero (`< 1e-12`), `ProgressionCurveSettings`, `CombatSimulator`, and `EconomySimulator` fall back to `baseValue`, `attacker.attack`, or `flatAmount` via `EvaluateOrDefault`. Test expressions with `MathExpressionEvaluator.TryEvaluate` or the live validation status in **Curves & Formulas** before relying on them.

## License

[MIT](LICENSE.md)
