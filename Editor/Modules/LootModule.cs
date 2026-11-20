using System.Collections.Generic;
using System.IO;
using GameBalancer.Editor.Core;
using GameBalancer.Export;
using GameBalancer.Loot;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameBalancer.Editor.Modules
{
    /// <summary>
    /// Built-in GameBalancer Workbench module for running Monte Carlo loot roll simulations,
    /// tuning soft/hard pity rules, and inspecting pulls-to-target percentiles.
    /// </summary>
    public sealed class LootModule : IGameBalancerModule
    {
        /// <inheritdoc/>
        public string ModuleId => "builtin.loot";
        /// <inheritdoc/>
        public string DisplayName => "Loot & Pity Odds";
        /// <inheritdoc/>
        public string Description => "Compare exact weighted drop rates against Monte Carlo trials, soft/hard pity rules, and P50/P90/P99 pulls-to-target.";
        /// <inheritdoc/>
        public int SortOrder => 20;

        private GameBalancerHost _host;
        private ObjectField _tableField;
        private SliderInt _trialsSlider;
        private Toggle _pityToggle;
        private IntegerField _softPityField;
        private IntegerField _hardPityField;
        private TextField _targetRarityField;
        private VisualElement _kpiRow;
        private VisualElement _barsContainer;
        private LootAnalysisReport _lastReport;

        /// <inheritdoc/>
        public VisualElement CreateView(GameBalancerHost host)
        {
            _host = host;
            var root = new VisualElement();

            var ctrl = new VisualElement();
            ctrl.AddToClassList("gb-card");
            var title = new Label("Loot Table & Pity Configuration");
            title.AddToClassList("gb-section-title");
            ctrl.Add(title);

            _tableField = new ObjectField("LootTableAsset (Optional — Uses Demo Table If Empty)")
            {
                objectType = typeof(LootTableAsset)
            };
            _tableField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is LootTableAsset asset && asset.pityRule != null)
                {
                    _pityToggle.SetValueWithoutNotify(asset.pityRule.enabled);
                    _softPityField.SetValueWithoutNotify(asset.pityRule.softPityStartPull);
                    _hardPityField.SetValueWithoutNotify(asset.pityRule.hardPityPull);
                    _targetRarityField.SetValueWithoutNotify(asset.pityRule.targetRarity);
                }
                RunSimulation();
            });
            ctrl.Add(_tableField);

            _trialsSlider = new SliderInt("Monte Carlo Rolls", 1000, 100000) { value = 25000, showInputField = true };
            ctrl.Add(_trialsSlider);

            var pityRow = new VisualElement();
            pityRow.AddToClassList("gb-row");
            _pityToggle = new Toggle("Enable Pity") { value = true };
            _targetRarityField = new TextField("Target Rarity") { value = "Legendary" };
            _targetRarityField.style.flexGrow = 1f;
            _targetRarityField.style.minWidth = 160f;
            _softPityField = new IntegerField("Soft Start") { value = 50 };
            _softPityField.style.flexGrow = 1f;
            _softPityField.style.minWidth = 125f;
            _hardPityField = new IntegerField("Hard Guarantee") { value = 75 };
            _hardPityField.style.flexGrow = 1f;
            _hardPityField.style.minWidth = 140f;
            pityRow.Add(_pityToggle);
            pityRow.Add(_targetRarityField);
            pityRow.Add(_softPityField);
            pityRow.Add(_hardPityField);
            ctrl.Add(pityRow);

            var btnRow = new VisualElement();
            btnRow.AddToClassList("gb-row");
            btnRow.Add(new Button(RunSimulation) { text = "Run Monte Carlo" });
            btnRow.Add(new Button(CopyMarkdown) { text = "Copy Markdown" });
            btnRow.Add(new Button(ExportJson) { text = "Export JSON..." });
            ctrl.Add(btnRow);
            root.Add(ctrl);

            var resultsCard = new VisualElement();
            resultsCard.AddToClassList("gb-card");
            var resTitle = new Label("Expected Value & Pulls-To-Target Percentiles");
            resTitle.AddToClassList("gb-section-title");
            resultsCard.Add(resTitle);

            _kpiRow = new VisualElement();
            _kpiRow.AddToClassList("gb-wrap-row");
            resultsCard.Add(_kpiRow);

            _barsContainer = new VisualElement();
            resultsCard.Add(_barsContainer);
            root.Add(resultsCard);

            return root;
        }

        /// <inheritdoc/>
        public void OnRefresh() => RunSimulation();

        /// <inheritdoc/>
        public bool TryAcceptAsset(UnityEngine.Object asset)
        {
            if (asset is LootTableAsset table && _tableField != null)
            {
                _tableField.value = table;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Runs <see cref="LootOddsAnalyzer.Analyze"/> using the assigned table (or a 4-tier demo table) and current UI pity controls.
        /// </summary>
        private void RunSimulation()
        {
            if (_barsContainer == null) return;

            var table = _tableField.value as LootTableAsset;
            var entries = table != null && table.entries.Count > 0 ? table.entries : BuildDefaultEntries();
            string name = table != null ? table.tableName : "Demo Standard Chest";

            var pity = new PityRule
            {
                enabled = _pityToggle.value,
                targetRarity = _targetRarityField.value,
                softPityStartPull = Mathf.Max(1, _softPityField.value),
                softPityBonusPerPull = table != null && table.pityRule != null ? table.pityRule.softPityBonusPerPull : 0.04f,
                hardPityPull = Mathf.Max(1, _hardPityField.value)
            };

            _lastReport = LootOddsAnalyzer.Analyze(name, entries, pity, _trialsSlider.value, _targetRarityField.value);
            RenderReport();
            _host?.SetStatus($"Simulated {_lastReport.totalRolls:N0} rolls on '{_lastReport.tableName}'.");
        }

        /// <summary>
        /// Populates expected-value and pulls-to-target KPI cards plus theoretical vs. simulated comparison bars for each entry.
        /// </summary>
        private void RenderReport()
        {
            _kpiRow.Clear();
            _barsContainer.Clear();
            if (_lastReport == null) return;

            AddKpi("Theoretical EV / Roll", _lastReport.expectedValuePerRoll.ToString("F2"));
            AddKpi("Simulated EV / Roll", _lastReport.simulatedValuePerRoll.ToString("F2"));
            AddKpi("Pity Triggers", _lastReport.pityTriggeredCount.ToString("N0"));
            if (_lastReport.targetHitCount > 0)
            {
                AddKpi($"Mean Pulls ({_lastReport.targetRarity})", _lastReport.pullsToTargetMean.ToString("F1"));
                AddKpi("P50 / P90 / P99", $"{_lastReport.pullsToTargetP50} / {_lastReport.pullsToTargetP90} / {_lastReport.pullsToTargetP99}");
                AddKpi("Worst-Case Pulls", _lastReport.pullsToTargetMax.ToString());
            }

            for (int i = 0; i < _lastReport.entries.Count; i++)
            {
                var e = _lastReport.entries[i];
                var row = new VisualElement();
                row.AddToClassList("gb-bar-row");

                var header = new Label(
                    $"{e.displayName} [{e.rarity}]  —  Theoretical: {e.theoreticalPercent:F2}%  |  Simulated: {e.simulatedPercent:F2}% ({e.dropCount:N0} drops, delta {e.deviationPercent:+0.00;-0.00}%)");
                row.Add(header);

                bool dark = UnityEditor.EditorGUIUtility.isProSkin;
                row.Add(CreateBar(e.theoreticalPercent, dark ? new Color(0.32f, 0.68f, 1f) : new Color(0.14f, 0.48f, 0.86f)));
                row.Add(CreateBar(e.simulatedPercent, dark ? new Color(0.35f, 0.88f, 0.56f) : new Color(0.15f, 0.62f, 0.34f)));
                _barsContainer.Add(row);
            }
        }

        private static VisualElement CreateBar(double percent, Color color)
        {
            var track = new VisualElement();
            track.AddToClassList("gb-bar-track");
            var fill = new VisualElement();
            fill.AddToClassList("gb-bar-fill");
            fill.style.width = Length.Percent(Mathf.Clamp((float)percent, 0.5f, 100f));
            fill.style.backgroundColor = color;
            track.Add(fill);
            return track;
        }

        private void AddKpi(string label, string value)
        {
            var card = new VisualElement();
            card.AddToClassList("gb-kpi-card");
            var l = new Label(label);
            l.AddToClassList("gb-kpi-label");
            var v = new Label(value);
            v.AddToClassList("gb-kpi-value");
            card.Add(l);
            card.Add(v);
            _kpiRow.Add(card);
        }

        /// <summary>
        /// Copies the Markdown-formatted loot report to the system clipboard.
        /// </summary>
        private void CopyMarkdown()
        {
            if (_lastReport == null) return;
            EditorGUIUtility.systemCopyBuffer = BalanceReportExporter.LootToMarkdown(_lastReport);
            _host?.SetStatus("Copied Loot Odds Markdown report to clipboard.");
        }

        /// <summary>
        /// Opens a save dialog and writes the latest <see cref="LootAnalysisReport"/> as JSON.
        /// </summary>
        private void ExportJson()
        {
            if (_lastReport == null) return;
            string path = EditorUtility.SaveFilePanel("Export Loot Analysis JSON", Application.dataPath, "loot_odds_report", "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, BalanceReportExporter.ToJson(_lastReport));
            _host?.SetStatus($"Exported JSON to {path}");
        }

        /// <summary>
        /// Builds the fallback 4-tier chest entries used when no <see cref="LootTableAsset"/> is assigned.
        /// </summary>
        private static List<WeightedLootEntry> BuildDefaultEntries() => new List<WeightedLootEntry>
        {
            new WeightedLootEntry { itemId = "common_scrap", displayName = "Iron Scrap", rarity = "Common", weight = 65f, valueScore = 10f },
            new WeightedLootEntry { itemId = "uncommon_gem", displayName = "Mana Shard", rarity = "Uncommon", weight = 25f, valueScore = 30f },
            new WeightedLootEntry { itemId = "rare_blade", displayName = "Knight Blade", rarity = "Rare", weight = 8.5f, valueScore = 120f },
            new WeightedLootEntry { itemId = "legendary_relic", displayName = "Sunfire Relic", rarity = "Legendary", weight = 1.5f, valueScore = 600f }
        };
    }
}
