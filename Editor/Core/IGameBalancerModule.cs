using System;
using UnityEngine.UIElements;

namespace GameBalancer.Editor.Core
{
    /// <summary>
    /// Host callbacks passed to each IGameBalancerModule when its UI Toolkit view is created.
    /// </summary>
    public sealed class GameBalancerHost
    {
        /// <summary>
        /// Updates the Workbench footer status bar with a short feedback message.
        /// </summary>
        public Action<string> SetStatus { get; }

        /// <summary>
        /// Requests an immediate refresh of the currently active module view.
        /// </summary>
        public Action RequestRefresh { get; }

        /// <summary>
        /// Initializes a host context bound to the parent <see cref="GameBalancerWindow"/>.
        /// </summary>
        /// <param name="setStatus">Callback updating the status bar text; defaults to a no-op if null.</param>
        /// <param name="requestRefresh">Callback triggering a module refresh; defaults to a no-op if null.</param>
        public GameBalancerHost(Action<string> setStatus, Action requestRefresh)
        {
            SetStatus = setStatus ?? (_ => { });
            RequestRefresh = requestRefresh ?? (() => { });
        }
    }

    /// <summary>
    /// Implement this interface in any Editor assembly to add a custom balancing simulator or tool
    /// directly into the GameBalancer UI Toolkit Workbench. Modules are auto-discovered via TypeCache.
    /// </summary>
    public interface IGameBalancerModule
    {
        /// <summary>
        /// Unique stable identifier used to cache the module's UI view and sidebar button state.
        /// </summary>
        string ModuleId { get; }

        /// <summary>
        /// Short title displayed in the Workbench sidebar and header bar.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Brief description shown in the Workbench subtitle and sidebar button tooltip.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Ascending sidebar ordering priority (built-in modules use 10, 20, 30, and 40).
        /// </summary>
        int SortOrder { get; }

        /// <summary>
        /// Builds and returns the module's root UI Toolkit <see cref="VisualElement"/> (called once per window lifecycle).
        /// </summary>
        /// <param name="host">Callbacks for posting status messages and requesting refreshes.</param>
        /// <returns>Root UI Toolkit element mounted in the Workbench content pane.</returns>
        VisualElement CreateView(GameBalancerHost host);

        /// <summary>
        /// Re-evaluates simulation data and redraws the module UI when selected or when Refresh is clicked.
        /// </summary>
        void OnRefresh();

        /// <summary>
        /// Attempts to bind an asset opened via <see cref="GameBalancerWindow.OpenWithAsset"/> into this module.
        /// </summary>
        /// <param name="asset">Unity asset to inspect or simulate.</param>
        /// <returns><c>true</c> if this module accepted the asset and should become active; otherwise <c>false</c>.</returns>
        bool TryAcceptAsset(UnityEngine.Object asset);
    }
}
