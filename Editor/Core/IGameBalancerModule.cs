using System;
using UnityEngine.UIElements;

namespace GameBalancer.Editor.Core
{
    /// <summary>
    /// Host callbacks passed to each IGameBalancerModule when its UI Toolkit view is created.
    /// </summary>
    public sealed class GameBalancerHost
    {
        public Action<string> SetStatus { get; }
        public Action RequestRefresh { get; }

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
        string ModuleId { get; }
        string DisplayName { get; }
        string Description { get; }
        int SortOrder { get; }

        VisualElement CreateView(GameBalancerHost host);
        void OnRefresh();
        bool TryAcceptAsset(UnityEngine.Object asset);
    }
}
