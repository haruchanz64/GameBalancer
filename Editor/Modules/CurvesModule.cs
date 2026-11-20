using System.Collections.Generic;
using System.IO;
using GameBalancer.Curves;
using GameBalancer.Editor.Core;
using GameBalancer.Editor.UI;
using GameBalancer.Export;
using GameBalancer.Math;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameBalancer.Editor.Modules
{
    /// <summary>
    /// Built-in GameBalancer Workbench module for plotting <see cref="ProgressionCurveAsset"/> curves,
    /// validating scratchpad math formulas, inspecting A/B ratios, and exporting CSV or JSON.
    /// </summary>
    public sealed class CurvesModule : IGameBalancerModule
    {
        /// <inheritdoc/>
        public string ModuleId => "builtin.curves";
        /// <inheritdoc/>
        public string DisplayName => "Curves & Formulas";
        /// <inheritdoc/>
        public string Description => "Compare progression curves, validate custom math expressions, inspect ratios, and export CSV/JSON.";
        /// <inheritdoc/>
        public int SortOrder => 10;

        /// <summary>
        /// Adapts a freeform scratchpad expression string into an <see cref="IProgressionCurve"/> for comparison plotting.
        /// </summary>
        private sealed class ExpressionCurveAdapter : IProgressionCurve
        {
            public string DisplayName { get; }
            private readonly string _expr;
            private readonly double _base;
            private readonly double _growth;

            public ExpressionCurveAdapter(string name, string expr, double baseVal, double growth)
            {
                DisplayName = name;
                _expr = expr;
                _base = baseVal;
                _growth = growth;
            }

            /// <inheritdoc/>
            public double Evaluate(int level)
            {
                var vars = new Dictionary<string, double>(4)
                {
                    ["level"] = level,
                    ["x"] = level - 1,
                    ["base"] = _base,
                    ["growth"] = _growth,
                    ["cap"] = 10000d
                };
                return MathExpressionEvaluator.EvaluateOrDefault(_expr, vars, _base);
            }
        }

        private GameBalancerHost _host;
        private ObjectField _curveAField;
        private ObjectField _curveBField;
        private TextField _customFormulaField;
        private Label _formulaValidationLabel;
        private IntegerField _minLevelField;
        private IntegerField _maxLevelField;
        private Toggle _showRatioToggle;
        private BalanceLineChartElement _chart;
        private VisualElement _kpiRow;
        private VisualElement _tableContainer;
        private CurveComparisonReport _lastReport;

        /// <inheritdoc/>
        public VisualElement CreateView(GameBalancerHost host)
        {
            _host = host;
            var root = new VisualElement();

            // Controls card
            var ctrlCard = new VisualElement();
            ctrlCard.AddToClassList("gb-card");
            ctrlCard.Add(CreateSectionTitle("Curve Sources & Formula Scratchpad"));

            _curveAField = new ObjectField("Primary Curve Asset (Optional)") { objectType = typeof(ProgressionCurveAsset) };
            _curveAField.RegisterValueChangedCallback(_ => EvaluateAndRender());
            ctrlCard.Add(_curveAField);

            _curveBField = new ObjectField("Comparison Curve Asset (Optional)") { objectType = typeof(ProgressionCurveAsset) };
            _curveBField.RegisterValueChangedCallback(_ => EvaluateAndRender());
            ctrlCard.Add(_curveBField);

            _customFormulaField = new TextField("Scratchpad Formula (level, x, base, growth)")
            {
                value = "100 + 35 * pow(level - 1, 1.5)"
            };
            _customFormulaField.RegisterValueChangedCallback(_ => EvaluateAndRender());
            ctrlCard.Add(_customFormulaField);

            _formulaValidationLabel = new Label();
            _formulaValidationLabel.AddToClassList("gb-chart-caption");
            ctrlCard.Add(_formulaValidationLabel);

            var rangeRow = new VisualElement();
            rangeRow.AddToClassList("gb-row");
            _minLevelField = new IntegerField("Min Level") { value = 1 };
            _minLevelField.style.flexGrow = 1f;
            _minLevelField.RegisterValueChangedCallback(_ => EvaluateAndRender());

            _maxLevelField = new IntegerField("Max Level") { value = 50 };
            _maxLevelField.style.flexGrow = 1f;
            _maxLevelField.RegisterValueChangedCallback(_ => EvaluateAndRender());

            _showRatioToggle = new Toggle("Show A/B Ratio") { value = false };
            _showRatioToggle.RegisterValueChangedCallback(_ => EvaluateAndRender());

            rangeRow.Add(_minLevelField);
            rangeRow.Add(_maxLevelField);
            rangeRow.Add(_showRatioToggle);
            ctrlCard.Add(rangeRow);

            var actionRow = new VisualElement();
            actionRow.AddToClassList("gb-row");
            actionRow.Add(new Button(EvaluateAndRender) { text = "Re-Evaluate" });
            actionRow.Add(new Button(ExportCsv) { text = "Export CSV..." });
            actionRow.Add(new Button(ExportJson) { text = "Export JSON..." });
            ctrlCard.Add(actionRow);
            root.Add(ctrlCard);

            // KPI + Chart card
            var chartCard = new VisualElement();
            chartCard.AddToClassList("gb-card");
            chartCard.Add(CreateSectionTitle("Progression Graph"));

            _kpiRow = new VisualElement();
            _kpiRow.AddToClassList("gb-wrap-row");
            chartCard.Add(_kpiRow);

            _chart = new BalanceLineChartElement(230f);
            chartCard.Add(_chart);
            root.Add(chartCard);

            // Sample table card
            var tableCard = new VisualElement();
            tableCard.AddToClassList("gb-card");
            tableCard.Add(CreateSectionTitle("Sample Level Milestones"));
            _tableContainer = new VisualElement();
            tableCard.Add(_tableContainer);
            root.Add(tableCard);

            return root;
        }

        /// <inheritdoc/>
        public void OnRefresh() => EvaluateAndRender();

        /// <inheritdoc/>
        public bool TryAcceptAsset(UnityEngine.Object asset)
        {
            if (asset is ProgressionCurveAsset curve && _curveAField != null)
            {
                _curveAField.value = curve;
                EvaluateAndRender();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Validates the scratchpad formula, evaluates assigned curves across the selected level range
        /// (capped at a 500-level span), and refreshes the chart, KPI cards, and milestone list.
        /// </summary>
        private void EvaluateAndRender()
        {
            if (_chart == null) return;

            int minLvl = Mathf.Max(1, _minLevelField.value);
            int maxLvl = Mathf.Max(minLvl + 1, Mathf.Min(minLvl + 500, _maxLevelField.value));

            var testVars = new Dictionary<string, double>
            {
                ["level"] = minLvl,
                ["x"] = minLvl - 1,
                ["base"] = 100d,
                ["growth"] = 35d,
                ["cap"] = 10000d
            };
            bool formulaOk = MathExpressionEvaluator.TryEvaluate(_customFormulaField.value, testVars, out double sampleOut, out string formulaErr);
            _formulaValidationLabel.text = formulaOk
                ? $"Formula valid (at Lv {minLvl}: {sampleOut:F2})"
                : $"Formula error: {formulaErr}";
            bool dark = UnityEditor.EditorGUIUtility.isProSkin;
            _formulaValidationLabel.style.color = formulaOk
                ? (dark ? new Color(0.45f, 0.9f, 0.55f) : new Color(0.10f, 0.52f, 0.22f))
                : (dark ? new Color(1f, 0.42f, 0.42f) : new Color(0.76f, 0.14f, 0.14f));

            var curves = new List<IProgressionCurve>();
            var colors = new List<Color>();

            var assetA = _curveAField.value as ProgressionCurveAsset;
            var assetB = _curveBField.value as ProgressionCurveAsset;

            if (assetA != null)
            {
                curves.Add(assetA);
                colors.Add(assetA.chartColor);
            }

            if (assetB != null)
            {
                curves.Add(assetB);
                colors.Add(assetB.chartColor);
            }

            if (formulaOk && (curves.Count == 0 || !string.IsNullOrWhiteSpace(_customFormulaField.value)))
            {
                curves.Add(new ExpressionCurveAdapter("Scratchpad Formula", _customFormulaField.value, 100d, 35d));
                colors.Add(new Color(1f, 0.72f, 0.25f));
            }

            int numIdx = _showRatioToggle.value && curves.Count >= 2 ? 0 : -1;
            int denIdx = _showRatioToggle.value && curves.Count >= 2 ? 1 : -1;
            _lastReport = CurveComparison.Evaluate(curves, minLvl, maxLvl, numIdx, denIdx);

            var chartSeries = new List<ChartSeries>();
            for (int i = 0; i < _lastReport.series.Count; i++)
            {
                chartSeries.Add(new ChartSeries
                {
                    Label = _lastReport.series[i].name,
                    Color = i < colors.Count ? colors[i] : Color.cyan,
                    Values = _lastReport.series[i].values
                });
            }

            if (_lastReport.ratioValues != null)
            {
                chartSeries.Add(new ChartSeries
                {
                    Label = $"Ratio ({_lastReport.ratioLabel})",
                    Color = new Color(0.95f, 0.35f, 0.7f),
                    Values = _lastReport.ratioValues
                });
            }

            _chart.SetData(chartSeries, $"Lv {minLvl}", $"Lv {maxLvl}");
            RenderKpis();
            RenderMilestoneTable();
            _host?.SetStatus($"Evaluated {_lastReport.series.Count} curve(s) across levels {minLvl}..{maxLvl}.");
        }

        /// <summary>
        /// Rebuilds per-series min/max and cumulative sum KPI cards.
        /// </summary>
        private void RenderKpis()
        {
            _kpiRow.Clear();
            if (_lastReport == null) return;
            for (int i = 0; i < _lastReport.series.Count; i++)
            {
                var s = _lastReport.series[i];
                AddKpi(_kpiRow, $"{s.name} (Min -> Max)", $"{s.minValue:F1} -> {s.maxValue:F1}");
                AddKpi(_kpiRow, $"{s.name} Total Sum", $"{s.totalSum:F0}");
            }
        }

        /// <summary>
        /// Renders roughly ten evenly spaced milestone rows summarizing curve and ratio values across the range.
        /// </summary>
        private void RenderMilestoneTable()
        {
            _tableContainer.Clear();
            if (_lastReport == null || _lastReport.series.Count == 0) return;

            int count = _lastReport.levels.Length;
            int step = Mathf.Max(1, count / 10);
            for (int i = 0; i < count; i += step)
            {
                string line = $"Lv {_lastReport.levels[i],3}: ";
                for (int s = 0; s < _lastReport.series.Count; s++)
                {
                    if (s > 0) line += "   |   ";
                    line += $"{_lastReport.series[s].name} = {_lastReport.series[s].values[i]:F1}";
                }
                if (_lastReport.ratioValues != null)
                {
                    line += $"   |   Ratio = {_lastReport.ratioValues[i]:F2}";
                }
                _tableContainer.Add(new Label(line));
            }
        }

        /// <summary>
        /// Opens a save dialog and writes the latest <see cref="CurveComparisonReport"/> as CSV.
        /// </summary>
        private void ExportCsv()
        {
            if (_lastReport == null) return;
            string path = EditorUtility.SaveFilePanel("Export Curves CSV", Application.dataPath, "progression_curves", "csv");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, BalanceReportExporter.CurvesToCsv(_lastReport));
            _host?.SetStatus($"Exported CSV to {path}");
        }

        /// <summary>
        /// Opens a save dialog and writes the latest <see cref="CurveComparisonReport"/> as pretty-printed JSON.
        /// </summary>
        private void ExportJson()
        {
            if (_lastReport == null) return;
            string path = EditorUtility.SaveFilePanel("Export Curves JSON", Application.dataPath, "progression_curves", "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, BalanceReportExporter.ToJson(_lastReport));
            _host?.SetStatus($"Exported JSON to {path}");
        }

        private static Label CreateSectionTitle(string text)
        {
            var lbl = new Label(text);
            lbl.AddToClassList("gb-section-title");
            return lbl;
        }

        private static void AddKpi(VisualElement parent, string label, string value)
        {
            var card = new VisualElement();
            card.AddToClassList("gb-kpi-card");
            var l = new Label(label);
            l.AddToClassList("gb-kpi-label");
            var v = new Label(value);
            v.AddToClassList("gb-kpi-value");
            card.Add(l);
            card.Add(v);
            parent.Add(card);
        }
    }
}
