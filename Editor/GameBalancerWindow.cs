using System;
using System.Collections.Generic;
using GameBalancer.Editor.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameBalancer.Editor
{
    /// <summary>
    /// Modern Unity 6 UI Toolkit Workbench hosting built-in and developer-defined IGameBalancerModule tabs.
    /// </summary>
    public sealed class GameBalancerWindow : EditorWindow
    {
        private readonly List<IGameBalancerModule> _modules = new List<IGameBalancerModule>();
        private readonly Dictionary<string, Button> _navButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> _cachedViews = new Dictionary<string, VisualElement>(StringComparer.Ordinal);

        private VisualElement _sidebar;
        private ScrollView _contentHost;
        private Label _moduleTitleLabel;
        private Label _moduleDescLabel;
        private Label _statusLabel;
        private GameBalancerHost _host;
        private IGameBalancerModule _activeModule;

        [MenuItem("Tools/GameBalancer/Balance Workbench", false, 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<GameBalancerWindow>("GameBalancer");
            window.minSize = new Vector2(400f, 360f);
            window.Show();
        }

        public static void OpenWithAsset(UnityEngine.Object asset)
        {
            var window = GetWindow<GameBalancerWindow>("GameBalancer");
            window.minSize = new Vector2(400f, 360f);
            window.Show();
            window.RouteAssetToModule(asset);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("gb-root");
            AttachStyleSheet(rootVisualElement);
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                rootVisualElement.EnableInClassList("gb-root--narrow", evt.newRect.width > 0f && evt.newRect.width < 680f);
            });

            _host = new GameBalancerHost(
                status =>
                {
                    if (_statusLabel != null) _statusLabel.text = status;
                },
                () => _activeModule?.OnRefresh());

            // Header bar
            var header = new VisualElement();
            header.AddToClassList("gb-header");

            var titleCol = new VisualElement();
            titleCol.style.flexGrow = 1f;
            titleCol.style.flexShrink = 1f;
            titleCol.style.minWidth = 0f;
            titleCol.style.marginRight = 8f;
            _moduleTitleLabel = new Label("GameBalancer");
            _moduleTitleLabel.AddToClassList("gb-header-title");
            _moduleDescLabel = new Label("Zero-dependency systems balancing and Monte Carlo workbench.");
            _moduleDescLabel.AddToClassList("gb-header-subtitle");
            titleCol.Add(_moduleTitleLabel);
            titleCol.Add(_moduleDescLabel);
            header.Add(titleCol);

            var refreshBtn = new Button(() => _activeModule?.OnRefresh()) { text = "Refresh" };
            header.Add(refreshBtn);
            rootVisualElement.Add(header);

            // Split body: Sidebar + Content
            var body = new VisualElement();
            body.AddToClassList("gb-body");

            _sidebar = new VisualElement();
            _sidebar.AddToClassList("gb-sidebar");
            body.Add(_sidebar);

            _contentHost = new ScrollView(ScrollViewMode.Vertical);
            _contentHost.AddToClassList("gb-content");
            body.Add(_contentHost);
            rootVisualElement.Add(body);

            // Status bar
            _statusLabel = new Label("Ready.");
            _statusLabel.AddToClassList("gb-status-bar");
            rootVisualElement.Add(_statusLabel);

            DiscoverModules();
            BuildSidebar();

            if (_modules.Count > 0)
            {
                SelectModule(_modules[0]);
            }
        }

        public static List<IGameBalancerModule> DiscoverAllModules()
        {
            var found = new List<IGameBalancerModule>();
            var types = TypeCache.GetTypesDerivedFrom<IGameBalancerModule>();
            for (int i = 0; i < types.Count; i++)
            {
                Type t = types[i];
                if (t.IsAbstract || t.IsInterface || t.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                try
                {
                    if (Activator.CreateInstance(t) is IGameBalancerModule module)
                    {
                        found.Add(module);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GameBalancer] Failed to instantiate module '{t.FullName}': {ex.Message}");
                }
            }

            found.Sort((a, b) =>
            {
                int cmp = a.SortOrder.CompareTo(b.SortOrder);
                return cmp != 0 ? cmp : string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
            });
            return found;
        }

        private void DiscoverModules()
        {
            _modules.Clear();
            _cachedViews.Clear();
            _navButtons.Clear();
            _modules.AddRange(DiscoverAllModules());
        }

        private void BuildSidebar()
        {
            _sidebar.Clear();

            var navHeader = new Label($"MODULES ({_modules.Count})");
            navHeader.AddToClassList("gb-kpi-label");
            navHeader.style.marginBottom = 6f;
            _sidebar.Add(navHeader);

            for (int i = 0; i < _modules.Count; i++)
            {
                var module = _modules[i];
                var btn = new Button(() => SelectModule(module)) { text = module.DisplayName };
                btn.AddToClassList("gb-nav-button");
                btn.tooltip = module.Description;
                _sidebar.Add(btn);
                _navButtons[module.ModuleId] = btn;
            }
        }

        private void SelectModule(IGameBalancerModule module)
        {
            if (module == null) return;
            _activeModule = module;

            foreach (var kvp in _navButtons)
            {
                kvp.Value.EnableInClassList("gb-nav-button--active", kvp.Key == module.ModuleId);
            }

            _moduleTitleLabel.text = module.DisplayName;
            _moduleDescLabel.text = module.Description;

            if (!_cachedViews.TryGetValue(module.ModuleId, out VisualElement view))
            {
                view = module.CreateView(_host);
                _cachedViews[module.ModuleId] = view;
            }

            _contentHost.Clear();
            if (view != null)
            {
                _contentHost.Add(view);
            }

            module.OnRefresh();
        }

        private void RouteAssetToModule(UnityEngine.Object asset)
        {
            if (asset == null) return;
            for (int i = 0; i < _modules.Count; i++)
            {
                var mod = _modules[i];
                if (!_cachedViews.ContainsKey(mod.ModuleId))
                {
                    _cachedViews[mod.ModuleId] = mod.CreateView(_host);
                }

                if (mod.TryAcceptAsset(asset))
                {
                    SelectModule(mod);
                    _statusLabel.text = $"Loaded '{asset.name}' into {mod.DisplayName}.";
                    return;
                }
            }
        }

        private static void AttachStyleSheet(VisualElement target)
        {
            const string packagePath = "Packages/com.haruchanz64.game-balancer/Editor/UI/GameBalancerStyles.uss";
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(packagePath);
            if (sheet == null)
            {
                string[] guids = AssetDatabase.FindAssets("GameBalancerStyles t:StyleSheet");
                if (guids.Length > 0)
                {
                    sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            if (sheet != null)
            {
                target.styleSheets.Add(sheet);
            }
        }
    }
}
