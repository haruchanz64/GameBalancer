using GameBalancer.Combat;
using GameBalancer.Curves;
using GameBalancer.Economy;
using GameBalancer.Loot;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameBalancer.Editor.Inspectors
{
    /// <summary>
    /// Shared UI Toolkit inspector builder that appends a live summary card and an Open in GameBalancer button.
    /// </summary>
    internal static class GameBalancerInspectorHelper
    {
        /// <summary>
        /// Renders the default serialized inspector for <paramref name="editor"/> followed by a styled summary card
        /// with a button that opens <see cref="GameBalancerWindow"/> targeted at the inspected asset.
        /// </summary>
        /// <param name="editor">Active custom editor instance.</param>
        /// <param name="summaryText">Multi-line preview text shown above the launch button.</param>
        /// <param name="buttonLabel">Text displayed on the Workbench launch button.</param>
        /// <returns>Complete inspector root <see cref="VisualElement"/>.</returns>
        public static VisualElement BuildInspector(UnityEditor.Editor editor, string summaryText, string buttonLabel)
        {
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, editor.serializedObject, editor);

            var card = new VisualElement();
            card.style.marginTop = 10f;
            card.style.paddingTop = 8f;
            card.style.paddingBottom = 8f;
            card.style.paddingLeft = 10f;
            card.style.paddingRight = 10f;
            bool dark = EditorGUIUtility.isProSkin;
            card.style.backgroundColor = dark
                ? new Color(0f, 0f, 0f, 0.18f)
                : new Color(1f, 1f, 1f, 0.45f);
            Color borderColor = dark
                ? new Color(1f, 1f, 1f, 0.08f)
                : new Color(0f, 0f, 0f, 0.14f);
            card.style.borderTopWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftWidth = 1f;
            card.style.borderRightWidth = 1f;
            card.style.borderTopColor = borderColor;
            card.style.borderBottomColor = borderColor;
            card.style.borderLeftColor = borderColor;
            card.style.borderRightColor = borderColor;
            card.style.borderTopLeftRadius = 5f;
            card.style.borderTopRightRadius = 5f;
            card.style.borderBottomLeftRadius = 5f;
            card.style.borderBottomRightRadius = 5f;

            var summary = new Label(summaryText);
            summary.style.whiteSpace = WhiteSpace.Normal;
            summary.style.marginBottom = 6f;
            card.Add(summary);

            var openBtn = new Button(() => GameBalancerWindow.OpenWithAsset(editor.target))
            {
                text = buttonLabel
            };
            openBtn.style.height = 26f;
            card.Add(openBtn);

            root.Add(card);
            return root;
        }
    }

    /// <summary>
    /// Custom UI Toolkit inspector for <see cref="ProgressionCurveAsset"/> showing level 1, 10, and 50 value previews.
    /// </summary>
    [CustomEditor(typeof(ProgressionCurveAsset))]
    public sealed class ProgressionCurveAssetEditor : UnityEditor.Editor
    {
        /// <inheritdoc/>
        public override VisualElement CreateInspectorGUI()
        {
            var asset = (ProgressionCurveAsset)target;
            string summary =
                $"Preview: Lv1 = {asset.Evaluate(1):F1}  |  Lv10 = {asset.Evaluate(10):F1}  |  Lv50 = {asset.Evaluate(50):F1}";
            return GameBalancerInspectorHelper.BuildInspector(this, summary, "Open in GameBalancer (Curves)");
        }
    }

    /// <summary>
    /// Custom UI Toolkit inspector for <see cref="LootTableAsset"/> showing entry count, total weight, and pity thresholds.
    /// </summary>
    [CustomEditor(typeof(LootTableAsset))]
    public sealed class LootTableAssetEditor : UnityEditor.Editor
    {
        /// <inheritdoc/>
        public override VisualElement CreateInspectorGUI()
        {
            var asset = (LootTableAsset)target;
            string summary =
                $"Entries: {asset.entries?.Count ?? 0}  |  Total Weight: {asset.GetTotalWeight():F1}  |  Pity: {(asset.pityRule != null && asset.pityRule.enabled ? $"Soft {asset.pityRule.softPityStartPull} / Hard {asset.pityRule.hardPityPull}" : "Off")}";
            return GameBalancerInspectorHelper.BuildInspector(this, summary, "Open in GameBalancer (Loot & Pity)");
        }
    }

    /// <summary>
    /// Custom UI Toolkit inspector for <see cref="CombatantProfileAsset"/> showing level 1 and level 20 stat snapshots.
    /// </summary>
    [CustomEditor(typeof(CombatantProfileAsset))]
    public sealed class CombatantProfileAssetEditor : UnityEditor.Editor
    {
        /// <inheritdoc/>
        public override VisualElement CreateInspectorGUI()
        {
            var asset = (CombatantProfileAsset)target;
            var lv1 = asset.EvaluateStatsAtLevel(1);
            var lv20 = asset.EvaluateStatsAtLevel(20);
            string summary =
                $"Lv1: {lv1.maxHp:F0} HP / {lv1.attack:F1} ATK / {lv1.defense:F1} DEF\n" +
                $"Lv20: {lv20.maxHp:F0} HP / {lv20.attack:F1} ATK / {lv20.defense:F1} DEF";
            return GameBalancerInspectorHelper.BuildInspector(this, summary, "Open in GameBalancer (Combat Matrix)");
        }
    }

    /// <summary>
    /// Custom UI Toolkit inspector for <see cref="EconomyModelAsset"/> showing currency and flow node counts.
    /// </summary>
    [CustomEditor(typeof(EconomyModelAsset))]
    public sealed class EconomyModelAssetEditor : UnityEditor.Editor
    {
        /// <inheritdoc/>
        public override VisualElement CreateInspectorGUI()
        {
            var asset = (EconomyModelAsset)target;
            string summary = $"Currencies: {asset.currencies?.Count ?? 0}  |  Flow Nodes: {asset.flows?.Count ?? 0}";
            return GameBalancerInspectorHelper.BuildInspector(this, summary, "Open in GameBalancer (Economy)");
        }
    }
}
