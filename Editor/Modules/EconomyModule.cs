using System.Collections.Generic;
using System.IO;
using GameBalancer.Economy;
using GameBalancer.Editor.Core;
using GameBalancer.Editor.UI;
using GameBalancer.Export;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameBalancer.Editor.Modules
{
    public sealed class EconomyModule : IGameBalancerModule
    {
        public string ModuleId => "builtin.economy";
        public string DisplayName => "Economy Flow";
        public string Description => "Simulate multi-day currency faucets and sinks to catch hyperinflation or mandatory-sink starvation.";
        public int SortOrder => 40;

        private GameBalancerHost _host;
        private ObjectField _modelField;
        private SliderInt _horizonSlider;
        private BalanceLineChartElement _chart;
        private VisualElement _kpiRow;
        private VisualElement _alertsContainer;
        private EconomySimulationReport _lastReport;

        public VisualElement CreateView(GameBalancerHost host)
        {
            _host = host;
            var root = new VisualElement();

            var ctrl = new VisualElement();
            ctrl.AddToClassList("gb-card");
            var title = new Label("Economy Model & Simulation Horizon");
            title.AddToClassList("gb-section-title");
            ctrl.Add(title);

            _modelField = new ObjectField("EconomyModelAsset (Optional — Uses Demo Economy If Empty)")
            {
                objectType = typeof(EconomyModelAsset)
            };
            _modelField.RegisterValueChangedCallback(_ => RunSimulation());
            ctrl.Add(_modelField);

            _horizonSlider = new SliderInt("Horizon (Days / Sessions)", 7, 365) { value = 60, showInputField = true };
            _horizonSlider.RegisterValueChangedCallback(_ => RunSimulation());
            ctrl.Add(_horizonSlider);

            var btnRow = new VisualElement();
            btnRow.AddToClassList("gb-row");
            btnRow.Add(new Button(RunSimulation) { text = "Simulate Economy" });
            btnRow.Add(new Button(ExportCsv) { text = "Export CSV..." });
            btnRow.Add(new Button(ExportJson) { text = "Export JSON..." });
            ctrl.Add(btnRow);
            root.Add(ctrl);

            var chartCard = new VisualElement();
            chartCard.AddToClassList("gb-card");
            var chartTitle = new Label("Currency Balance & Daily Flow");
            chartTitle.AddToClassList("gb-section-title");
            chartCard.Add(chartTitle);

            _kpiRow = new VisualElement();
            _kpiRow.AddToClassList("gb-wrap-row");
            chartCard.Add(_kpiRow);

            _chart = new BalanceLineChartElement(230f);
            chartCard.Add(_chart);
            root.Add(chartCard);

            var alertCard = new VisualElement();
            alertCard.AddToClassList("gb-card");
            var alertTitle = new Label("Economy Diagnostics");
            alertTitle.AddToClassList("gb-section-title");
            alertCard.Add(alertTitle);
            _alertsContainer = new VisualElement();
            alertCard.Add(_alertsContainer);
            root.Add(alertCard);

            return root;
        }

        public void OnRefresh() => RunSimulation();

        public bool TryAcceptAsset(UnityEngine.Object asset)
        {
            if (asset is EconomyModelAsset model && _modelField != null)
            {
                _modelField.value = model;
                return true;
            }
            return false;
        }

        private void RunSimulation()
        {
            if (_chart == null) return;

            var asset = _modelField.value as EconomyModelAsset;
            if (asset != null)
            {
                _lastReport = EconomySimulator.Simulate(asset, _horizonSlider.value);
            }
            else
            {
                var temp = ScriptableObject.CreateInstance<EconomyModelAsset>();
                _lastReport = EconomySimulator.Simulate(temp, _horizonSlider.value);
                Object.DestroyImmediate(temp);
            }

            RenderReport(asset);
            _host?.SetStatus($"Simulated '{_lastReport.modelName}' over {_lastReport.horizonDays} days.");
        }

        private void RenderReport(EconomyModelAsset asset)
        {
            _kpiRow.Clear();
            _alertsContainer.Clear();
            if (_lastReport == null || _lastReport.currencies.Count == 0) return;

            var primary = _lastReport.currencies[0];
            AddKpi($"{primary.currencyId} Ending", primary.endingBalance.ToString("N0"));
            AddKpi("Peak Balance", primary.peakBalance.ToString("N0"));
            AddKpi("Total Inflow", primary.totalInflow.ToString("N0"));
            AddKpi("Total Outflow", primary.totalOutflow.ToString("N0"));
            AddKpi("Starvation Days", primary.starvationDays.ToString());

            int count = primary.timeline.Count;
            var balances = new double[count];
            var inflows = new double[count];
            var outflows = new double[count];
            for (int i = 0; i < count; i++)
            {
                balances[i] = primary.timeline[i].endingBalance;
                inflows[i] = primary.timeline[i].inflow;
                outflows[i] = primary.timeline[i].outflow;
            }

            double? threshold = null;
            if (asset != null && asset.currencies.Count > 0 && asset.currencies[0].inflationAlertThreshold > 0d)
            {
                threshold = asset.currencies[0].inflationAlertThreshold;
            }

            _chart.SetData(
                new List<ChartSeries>
                {
                    new ChartSeries { Label = $"{primary.currencyId} Balance", Color = new Color(0.28f, 0.72f, 1f), Values = balances },
                    new ChartSeries { Label = "Daily Inflow", Color = new Color(0.32f, 0.88f, 0.55f), Values = inflows },
                    new ChartSeries { Label = "Daily Outflow", Color = new Color(1f, 0.62f, 0.25f), Values = outflows }
                },
                "Day 1",
                $"Day {_lastReport.horizonDays}",
                threshold,
                "Inflation Alert");

            if (_lastReport.warnings.Count == 0)
            {
                _alertsContainer.Add(new Label("No inflation or mandatory-sink starvation warnings triggered across the simulated horizon."));
            }
            else
            {
                for (int i = 0; i < _lastReport.warnings.Count; i++)
                {
                    var lbl = new Label(_lastReport.warnings[i]);
                    lbl.AddToClassList("gb-alert");
                    if (_lastReport.warnings[i].Contains("Starvation"))
                    {
                        lbl.AddToClassList("gb-alert--error");
                    }
                    _alertsContainer.Add(lbl);
                }
            }
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

        private void ExportCsv()
        {
            if (_lastReport == null) return;
            string path = EditorUtility.SaveFilePanel("Export Economy CSV", Application.dataPath, "economy_timeline", "csv");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, BalanceReportExporter.EconomyToCsv(_lastReport, 0));
            _host?.SetStatus($"Exported CSV to {path}");
        }

        private void ExportJson()
        {
            if (_lastReport == null) return;
            string path = EditorUtility.SaveFilePanel("Export Economy JSON", Application.dataPath, "economy_report", "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, BalanceReportExporter.ToJson(_lastReport));
            _host?.SetStatus($"Exported JSON to {path}");
        }
    }
}
