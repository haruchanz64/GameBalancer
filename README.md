# GameBalancer (Unity 6)

Unity 6 runtime and Editor workbench for progression curves, loot and pity Monte Carlo odds, combat Time-to-Kill (TTK) matchup matrices, and resource economy simulation.

The Editor interface uses UI Toolkit and `Painter2D` vector charts, and automatically discovers custom `IGameBalancerModule` tabs via `TypeCache`.

## Modules

- **Curves and formulas:** Evaluate `ProgressionCurveAsset` assets (`Linear`, `Polynomial`, `Exponential`, `Logarithmic`, `SigmoidSoftCap`, `DiminishingReturns`, `UnityAnimationCurve`, or custom math expressions) across a level range, plot multi-series curves and A/B ratios, and export CSV or JSON.
- **Loot and pity odds:** Run deterministic-seedable weighted rolls, soft and hard pity rules, draw-without-replacement batches, and Pseudo-Random Distribution (PRD) procs. Compare exact theoretical weights against Monte Carlo simulations and inspect P50, P90, P95, P99, and worst-case pulls to a target rarity.
- **Combat matchup matrix:** Simulate round-robin duels across `CombatantProfileAsset` or custom `ICombatStatSource` rosters using armor percentage, flat reduction, custom formula expressions, or `IDamageFormula`. Inspect Win-Rate and TTK heatmaps and flagged balance outliers.
- **Economy flow:** Model currencies, faucets, and sinks over a multi-day or multi-session horizon using flat values or formulas (`day`, `session`, `balance`). Detect inflation spikes and mandatory-sink starvation.
- **Extensible module architecture:** Implement `IGameBalancerModule` in any Editor assembly to add project-specific tabs directly to the GameBalancer sidebar.

## Installation

1. Open **Window > Package Manager** in Unity 6 (`6000.0+`).
2. Click **+** (top-left) > **Install package from git URL...**
3. Enter:
   ```text
   https://github.com/haruchanz64/GameBalancer.git
   ```

## Quick start

1. Open **Tools > GameBalancer > Balance Workbench**.
2. Select any of the four built-in modules in the sidebar. Each module runs with built-in demo data when no asset is assigned.
3. To save project-specific configurations, create assets via **Assets > Create > GameBalancer** (`Progression Curve`, `Loot Table`, `Combatant Profile`, or `Economy Model`) and click **Open in GameBalancer** from the Inspector.

## Extending GameBalancer

### Add a custom Editor module

Implement `IGameBalancerModule` in an Editor assembly. `GameBalancerWindow` discovers the class automatically via `TypeCache` and adds it to the sidebar.

```csharp
using GameBalancer.Editor.Core;
using UnityEngine.UIElements;

public sealed class StaminaBudgetModule : IGameBalancerModule
{
    public string ModuleId => "studio.stamina-budget";
    public string DisplayName => "Stamina Budget";
    public string Description => "Audits sprint and dodge stamina costs.";
    public int SortOrder => 50;

    public VisualElement CreateView(GameBalancerHost host)
    {
        var root = new VisualElement();
        root.Add(new Label("Custom stamina simulator UI"));
        return root;
    }

    public void OnRefresh() { }
    public bool TryAcceptAsset(UnityEngine.Object asset) => false;
}
```

### Connect existing game ScriptableObjects

- Implement `IProgressionCurve` to feed external scaling tables into `CurveComparison`.
- Implement `ICombatStatSource` on your character or enemy ScriptableObjects to run them inside `MatchupMatrixRunner`.
- Implement `IDamageFormula` to plug your game's exact mitigation math into `CombatSimulator`.

## Export compatibility

Curve and economy CSV exports use plain header rows and invariant numeric formatting, making them compatible with [`SpreadsheetToScriptableObjects`](https://github.com/haruchanz64/SpreadsheetToScriptableObjects) for round-trip asset updates.

## License

[MIT](LICENSE.md)
