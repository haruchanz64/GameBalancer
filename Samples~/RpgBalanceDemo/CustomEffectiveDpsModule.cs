#if UNITY_EDITOR
using GameBalancer.Editor.Core;
using UnityEngine.UIElements;

namespace GameBalancer.Samples
{
    /// <summary>
    /// Example custom GameBalancer module demonstrating how to add a project-specific
    /// UI Toolkit tab to the Balance Workbench. Auto-discovered via UnityEditor.TypeCache.
    /// </summary>
    public sealed class CustomEffectiveDpsModule : IGameBalancerModule
    {
        public string ModuleId => "sample.effective-dps";
        public string DisplayName => "Effective DPS (Sample)";
        public string Description => "Example custom IGameBalancerModule computing expected DPS from attack, speed, and crit stats.";
        public int SortOrder => 100;

        private FloatField _atkField;
        private FloatField _speedField;
        private Slider _critChanceSlider;
        private FloatField _critMultField;
        private Label _outputLabel;

        public VisualElement CreateView(GameBalancerHost host)
        {
            var root = new VisualElement();
            var card = new VisualElement();
            card.AddToClassList("gb-card");

            var title = new Label("Custom Module Example: Effective DPS Calculator");
            title.AddToClassList("gb-section-title");
            card.Add(title);

            _atkField = new FloatField("Base Attack") { value = 45f };
            _speedField = new FloatField("Attacks / Second") { value = 1.25f };
            _critChanceSlider = new Slider("Crit Chance", 0f, 1f) { value = 0.25f, showInputField = true };
            _critMultField = new FloatField("Crit Multiplier") { value = 1.75f };
            _outputLabel = new Label();
            _outputLabel.AddToClassList("gb-kpi-value");

            _atkField.RegisterValueChangedCallback(_ => Recalculate());
            _speedField.RegisterValueChangedCallback(_ => Recalculate());
            _critChanceSlider.RegisterValueChangedCallback(_ => Recalculate());
            _critMultField.RegisterValueChangedCallback(_ => Recalculate());

            card.Add(_atkField);
            card.Add(_speedField);
            card.Add(_critChanceSlider);
            card.Add(_critMultField);
            card.Add(_outputLabel);
            root.Add(card);

            Recalculate();
            return root;
        }

        public void OnRefresh() => Recalculate();

        public bool TryAcceptAsset(UnityEngine.Object asset) => false;

        private void Recalculate()
        {
            if (_outputLabel == null) return;
            float expectedMult = 1f + (_critChanceSlider.value * (_critMultField.value - 1f));
            float dps = _atkField.value * _speedField.value * expectedMult;
            _outputLabel.text = $"Expected DPS: {dps:F2}";
        }
    }
}
#endif
