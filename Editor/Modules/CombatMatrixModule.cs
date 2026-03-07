using System.Collections.Generic;
using System.IO;
using GameBalancer.Combat;
using GameBalancer.Editor.Core;
using GameBalancer.Export;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameBalancer.Editor.Modules
{
    public sealed class CombatMatrixModule : IGameBalancerModule
    {
        public string ModuleId => "builtin.combat";
        public string DisplayName => "Combat Matrix";
        public string Description => "Run Monte Carlo round-robin duels across combat archetypes to generate Win-Rate and Time-to-Kill heatmaps.";
        public int SortOrder => 30;

        private sealed class StaticCombatant : ICombatStatSource
        {
            public string DisplayName { get; }
            private readonly CombatStatSnapshot _base;
            private readonly double _hpStep;
            private readonly double _atkStep;
            private readonly double _defStep;

            public StaticCombatant(string name, double hp, double atk, double def, double speed, double crit, double dodge, double hpStep, double atkStep, double defStep)
            {
                DisplayName = name;
                _base = new CombatStatSnapshot
                {
                    name = name,
                    maxHp = hp,
                    attack = atk,
                    defense = def,
                    attackSpeed = speed,
                    critChance = crit,
                    critMultiplier = 1.65d,
                    dodgeChance = dodge,
                    damageVariance = 0.1d
                };
                _hpStep = hpStep;
                _atkStep = atkStep;
                _defStep = defStep;
            }

            public CombatStatSnapshot EvaluateStatsAtLevel(int level)
            {
                var snap = _base;
                int d = System.Math.Max(0, level - 1);
                snap.level = System.Math.Max(1, level);
                snap.maxHp += _hpStep * d;
                snap.attack += _atkStep * d;
                snap.defense += _defStep * d;
                return snap;
            }
        }

        private GameBalancerHost _host;
        private readonly List<CombatantProfileAsset> _rosterAssets = new List<CombatantProfileAsset>();
        private VisualElement _rosterListContainer;
        private IntegerField _levelField;
        private SliderInt _trialsSlider;
        private EnumField _formulaField;
        private TextField _customFormulaField;
        private VisualElement _matrixContainer;
        private VisualElement _outliersContainer;
        private MatchupMatrixReport _lastReport;

        public VisualElement CreateView(GameBalancerHost host)
        {
            _host = host;
            var root = new VisualElement();

            var ctrl = new VisualElement();
            ctrl.AddToClassList("gb-card");
            var title = new Label("Combat Roster & Simulation Settings");
            title.AddToClassList("gb-section-title");
            ctrl.Add(title);

            var topRow = new VisualElement();
            topRow.AddToClassList("gb-row");
            _levelField = new IntegerField("Test Level") { value = 10 };
            _levelField.style.minWidth = 140f;
            _levelField.style.flexGrow = 0.4f;
            _formulaField = new EnumField("Damage Formula", DamageFormulaMode.ArmorPercentage);
            _formulaField.style.minWidth = 180f;
            _formulaField.style.flexGrow = 1f;
            topRow.Add(_levelField);
            topRow.Add(_formulaField);
            ctrl.Add(topRow);

            _customFormulaField = new TextField("Custom Formula (atk, def, hp, level)")
            {
                value = "max(1, atk * (100 / (100 + def)))"
            };
            ctrl.Add(_customFormulaField);

            _trialsSlider = new SliderInt("Duels per Matchup", 50, 5000) { value = 500, showInputField = true };
            ctrl.Add(_trialsSlider);

            _rosterListContainer = new VisualElement();
            ctrl.Add(_rosterListContainer);

            var btnRow = new VisualElement();
            btnRow.AddToClassList("gb-row");
            btnRow.Add(new Button(AddEmptyRosterSlot) { text = "+ Add Profile Slot" });
            btnRow.Add(new Button(RunMatrix) { text = "Run Matchup Matrix" });
            btnRow.Add(new Button(CopyMarkdown) { text = "Copy Markdown" });
            btnRow.Add(new Button(ExportJson) { text = "Export JSON..." });
            ctrl.Add(btnRow);
            root.Add(ctrl);

            var heatCard = new VisualElement();
            heatCard.AddToClassList("gb-card");
            var heatTitle = new Label("Win-Rate & TTK Heatmap (Row vs. Column)");
            heatTitle.AddToClassList("gb-section-title");
            heatCard.Add(heatTitle);
            var matrixScroll = new ScrollView(ScrollViewMode.Horizontal);
            matrixScroll.AddToClassList("gb-matrix-scroll");
            _matrixContainer = new VisualElement();
            matrixScroll.Add(_matrixContainer);
            heatCard.Add(matrixScroll);
            root.Add(heatCard);

            var outCard = new VisualElement();
            outCard.AddToClassList("gb-card");
            var outTitle = new Label("Flagged Balance Outliers");
            outTitle.AddToClassList("gb-section-title");
            outCard.Add(outTitle);
            _outliersContainer = new VisualElement();
            outCard.Add(_outliersContainer);
            root.Add(outCard);

            RebuildRosterSlots();
            return root;
        }

        public void OnRefresh() => RunMatrix();

        public bool TryAcceptAsset(UnityEngine.Object asset)
        {
            if (asset is CombatantProfileAsset profile && _rosterListContainer != null)
            {
                if (!_rosterAssets.Contains(profile))
                {
                    _rosterAssets.Add(profile);
                    RebuildRosterSlots();
                }
                RunMatrix();
                return true;
            }
            return false;
        }

        private void AddEmptyRosterSlot()
        {
            _rosterAssets.Add(null);
            RebuildRosterSlots();
        }

        private void RebuildRosterSlots()
        {
            _rosterListContainer.Clear();
            if (_rosterAssets.Count == 0)
            {
                var hint = new Label("No CombatantProfileAsset assigned — using built-in 4-archetype RPG roster (Vanguard, Rogue, Mage, Berserker).");
                hint.AddToClassList("gb-chart-caption");
                _rosterListContainer.Add(hint);
                return;
            }

            for (int i = 0; i < _rosterAssets.Count; i++)
            {
                int idx = i;
                var row = new VisualElement();
                row.AddToClassList("gb-row");
                var field = new ObjectField($"Slot {i + 1}")
                {
                    objectType = typeof(CombatantProfileAsset),
                    value = _rosterAssets[i]
                };
                field.style.flexGrow = 1f;
                field.RegisterValueChangedCallback(evt => _rosterAssets[idx] = evt.newValue as CombatantProfileAsset);
                var removeBtn = new Button(() =>
                {
                    _rosterAssets.RemoveAt(idx);
                    RebuildRosterSlots();
                }) { text = "Remove" };
                row.Add(field);
                row.Add(removeBtn);
                _rosterListContainer.Add(row);
            }
        }

        private void RunMatrix()
        {
            if (_matrixContainer == null) return;

            var roster = new List<ICombatStatSource>();
            for (int i = 0; i < _rosterAssets.Count; i++)
            {
                if (_rosterAssets[i] != null) roster.Add(_rosterAssets[i]);
            }

            if (roster.Count < 2)
            {
                roster = BuildDefaultRoster();
            }

            var opts = CombatSimulationOptions.Default;
            opts.formulaMode = (DamageFormulaMode)_formulaField.value;
            opts.customFormulaExpression = _customFormulaField.value;

            _lastReport = MatchupMatrixRunner.RunMatrix(
                roster,
                level: Mathf.Max(1, _levelField.value),
                trialsPerMatchup: _trialsSlider.value,
                simOptions: opts);

            RenderHeatmap();
            RenderOutliers();
            _host?.SetStatus($"Simulated {_lastReport.rosterNames.Count}x{_lastReport.rosterNames.Count} matrix ({_lastReport.trialsPerMatchup} duels/pair) at Lv {_lastReport.level}.");
        }

        private void RenderHeatmap()
        {
            _matrixContainer.Clear();
            if (_lastReport == null) return;

            int n = _lastReport.rosterNames.Count;
            var headerRow = new VisualElement();
            headerRow.AddToClassList("gb-matrix-row");
            var corner = new Label("Row \\ Col");
            corner.AddToClassList("gb-matrix-header");
            headerRow.Add(corner);

            for (int c = 0; c < n; c++)
            {
                var h = new Label(_lastReport.rosterNames[c]);
                h.AddToClassList("gb-matrix-cell");
                h.style.unityFontStyleAndWeight = FontStyle.Bold;
                headerRow.Add(h);
            }
            _matrixContainer.Add(headerRow);

            for (int r = 0; r < n; r++)
            {
                var row = new VisualElement();
                row.AddToClassList("gb-matrix-row");
                var rowLbl = new Label(_lastReport.rosterNames[r]);
                rowLbl.AddToClassList("gb-matrix-header");
                row.Add(rowLbl);

                for (int c = 0; c < n; c++)
                {
                    var cell = _lastReport.GetCell(r, c);
                    var cellLbl = new Label($"{cell.rowWinRatePercent:F1}%\n{cell.avgTtkSeconds:F1}s");
                    cellLbl.AddToClassList("gb-matrix-cell");
                    cellLbl.style.backgroundColor = GetHeatmapColor(cell, r == c);
                    cellLbl.tooltip = $"{cell.rowName} vs {cell.colName}\nWin Rate: {cell.rowWinRatePercent:F1}%\nAvg TTK: {cell.avgTtkSeconds:F2}s (Min {cell.minTtkSeconds:F1}s / Max {cell.maxTtkSeconds:F1}s)\nStatus: {cell.outlierFlag}";
                    row.Add(cellLbl);
                }
                _matrixContainer.Add(row);
            }
        }

        private void RenderOutliers()
        {
            _outliersContainer.Clear();
            if (_lastReport == null || _lastReport.outlierSummaries.Count == 0)
            {
                var okLbl = new Label("All non-mirror matchups fall within the configured win-rate (40%–60%) and TTK targets.");
                okLbl.style.whiteSpace = WhiteSpace.Normal;
                _outliersContainer.Add(okLbl);
                return;
            }

            for (int i = 0; i < _lastReport.outlierSummaries.Count; i++)
            {
                var alert = new Label(_lastReport.outlierSummaries[i]);
                alert.AddToClassList("gb-alert");
                _outliersContainer.Add(alert);
            }
        }

        private static Color GetHeatmapColor(MatchupCellResult cell, bool isMirror)
        {
            bool dark = EditorGUIUtility.isProSkin;
            if (isMirror)
            {
                return dark
                    ? new Color(0.26f, 0.28f, 0.32f, 0.85f)
                    : new Color(0.82f, 0.84f, 0.87f, 0.90f);
            }
            if (cell.rowWinRatePercent > 60d)
            {
                return dark
                    ? new Color(0.76f, 0.24f, 0.24f, 0.78f)
                    : new Color(0.95f, 0.68f, 0.68f, 0.90f);
            }
            if (cell.rowWinRatePercent < 40d)
            {
                return dark
                    ? new Color(0.22f, 0.42f, 0.75f, 0.78f)
                    : new Color(0.68f, 0.82f, 0.97f, 0.90f);
            }
            return dark
                ? new Color(0.22f, 0.60f, 0.36f, 0.78f)
                : new Color(0.68f, 0.90f, 0.74f, 0.90f);
        }

        private void CopyMarkdown()
        {
            if (_lastReport == null) return;
            EditorGUIUtility.systemCopyBuffer = BalanceReportExporter.CombatMatrixToMarkdown(_lastReport);
            _host?.SetStatus("Copied Combat Matrix Markdown table to clipboard.");
        }

        private void ExportJson()
        {
            if (_lastReport == null) return;
            string path = EditorUtility.SaveFilePanel("Export Combat Matrix JSON", Application.dataPath, "combat_matrix", "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, BalanceReportExporter.ToJson(_lastReport));
            _host?.SetStatus($"Exported JSON to {path}");
        }

        private static List<ICombatStatSource> BuildDefaultRoster() => new List<ICombatStatSource>
        {
            new StaticCombatant("Vanguard",  240, 22, 26, 0.95, 0.10, 0.05, 30, 3.0, 3.0),
            new StaticCombatant("Rogue",     175, 25, 12, 1.35, 0.28, 0.18, 22, 3.4, 1.6),
            new StaticCombatant("Mage",      160, 34, 10, 1.05, 0.20, 0.08, 19, 4.4, 1.4),
            new StaticCombatant("Berserker", 215, 31, 14, 1.10, 0.18, 0.04, 27, 4.0, 1.8)
        };
    }
}
