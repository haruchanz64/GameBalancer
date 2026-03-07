using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameBalancer.Editor.UI
{
    public sealed class ChartSeries
    {
        public string Label;
        public Color Color;
        public double[] Values;
    }

    /// <summary>
    /// GPU-accelerated UI Toolkit vector line chart built with Painter2D.
    /// </summary>
    public sealed class BalanceLineChartElement : VisualElement
    {
        private readonly List<ChartSeries> _series = new List<ChartSeries>();
        private readonly VisualElement _canvas;
        private readonly VisualElement _legendRow;
        private readonly Label _rangeLabel;

        private double? _thresholdY;
        private string _thresholdLabel;
        private string _xStartLabel = "1";
        private string _xEndLabel = "50";

        public BalanceLineChartElement(float height = 220f)
        {
            AddToClassList("gb-chart-container");

            _legendRow = new VisualElement();
            _legendRow.AddToClassList("gb-chart-legend");
            Add(_legendRow);

            _canvas = new VisualElement();
            _canvas.AddToClassList("gb-chart-canvas");
            _canvas.style.height = height;
            _canvas.generateVisualContent += OnGenerateVisualContent;
            Add(_canvas);

            _rangeLabel = new Label();
            _rangeLabel.AddToClassList("gb-chart-caption");
            Add(_rangeLabel);
        }

        public void SetData(
            IReadOnlyList<ChartSeries> series,
            string xStartLabel,
            string xEndLabel,
            double? thresholdY = null,
            string thresholdLabel = null)
        {
            _series.Clear();
            if (series != null)
            {
                for (int i = 0; i < series.Count; i++)
                {
                    if (series[i] != null && series[i].Values != null && series[i].Values.Length > 0)
                    {
                        _series.Add(series[i]);
                    }
                }
            }

            _xStartLabel = xStartLabel ?? string.Empty;
            _xEndLabel = xEndLabel ?? string.Empty;
            _thresholdY = thresholdY;
            _thresholdLabel = thresholdLabel;

            RebuildLegend();
            _canvas.MarkDirtyRepaint();
        }

        private void RebuildLegend()
        {
            _legendRow.Clear();
            double minV = double.MaxValue;
            double maxV = double.MinValue;

            for (int i = 0; i < _series.Count; i++)
            {
                var s = _series[i];
                var item = new VisualElement();
                item.AddToClassList("gb-legend-item");

                var swatch = new VisualElement();
                swatch.AddToClassList("gb-legend-swatch");
                swatch.style.backgroundColor = s.Color;
                item.Add(swatch);

                var lbl = new Label(s.Label);
                lbl.AddToClassList("gb-legend-label");
                item.Add(lbl);
                _legendRow.Add(item);

                for (int k = 0; k < s.Values.Length; k++)
                {
                    if (s.Values[k] < minV) minV = s.Values[k];
                    if (s.Values[k] > maxV) maxV = s.Values[k];
                }
            }

            if (_series.Count == 0)
            {
                _rangeLabel.text = "No data plotted.";
                return;
            }

            string threshText = _thresholdY.HasValue
                ? $"  |  Threshold ({_thresholdLabel}): {_thresholdY.Value:F0}"
                : string.Empty;
            _rangeLabel.text = $"X: [{_xStartLabel} .. {_xEndLabel}]  |  Y: [{minV:F1} .. {maxV:F1}]{threshText}";
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            Rect r = _canvas.contentRect;
            if (r.width < 10f || r.height < 10f) return;

            var painter = ctx.painter2D;
            float padL = 10f;
            float padR = 10f;
            float padT = 10f;
            float padB = 10f;
            float plotW = r.width - padL - padR;
            float plotH = r.height - padT - padB;

            // Horizontal grid lines
            painter.strokeColor = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.10f)
                : new Color(0f, 0f, 0f, 0.12f);
            painter.lineWidth = 1f;
            for (int g = 0; g <= 4; g++)
            {
                float y = padT + (plotH * (g / 4f));
                painter.BeginPath();
                painter.MoveTo(new Vector2(padL, y));
                painter.LineTo(new Vector2(padL + plotW, y));
                painter.Stroke();
            }

            if (_series.Count == 0) return;

            double minY = double.MaxValue;
            double maxY = double.MinValue;
            for (int i = 0; i < _series.Count; i++)
            {
                var vals = _series[i].Values;
                for (int k = 0; k < vals.Length; k++)
                {
                    if (vals[k] < minY) minY = vals[k];
                    if (vals[k] > maxY) maxY = vals[k];
                }
            }

            if (_thresholdY.HasValue)
            {
                minY = System.Math.Min(minY, _thresholdY.Value);
                maxY = System.Math.Max(maxY, _thresholdY.Value);
            }

            minY = System.Math.Min(0d, minY);
            double spanY = System.Math.Max(1e-6d, maxY - minY);

            // Optional threshold line
            if (_thresholdY.HasValue)
            {
                float ty = padT + plotH - (float)(((_thresholdY.Value - minY) / spanY) * plotH);
                painter.strokeColor = EditorGUIUtility.isProSkin
                    ? new Color(1f, 0.35f, 0.32f, 0.85f)
                    : new Color(0.78f, 0.16f, 0.14f, 0.90f);
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(padL, ty));
                painter.LineTo(new Vector2(padL + plotW, ty));
                painter.Stroke();
            }

            // Plot series
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            for (int s = 0; s < _series.Count; s++)
            {
                var series = _series[s];
                var vals = series.Values;
                if (vals.Length == 0) continue;

                painter.strokeColor = series.Color;
                painter.lineWidth = 2.25f;
                painter.BeginPath();

                for (int i = 0; i < vals.Length; i++)
                {
                    float tx = vals.Length > 1 ? i / (float)(vals.Length - 1) : 0.5f;
                    float x = padL + (tx * plotW);
                    float y = padT + plotH - (float)(((vals[i] - minY) / spanY) * plotH);

                    if (i == 0) painter.MoveTo(new Vector2(x, y));
                    else painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }
        }
    }
}
