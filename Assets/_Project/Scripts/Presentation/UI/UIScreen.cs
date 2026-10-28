using BusTycoon.Core;
using UnityEngine;

namespace BusTycoon.Presentation.UI
{
    public enum UIScreenId
    {
        None,          // Yalnızca harita + HUD (ana oyun ekranı)
        MainMenu,
        StartCity,
        RoutePlanner,
        RouteList,
        Pricing,
        Dealership,
        Fleet,
        Finance
    }

    /// <summary>
    /// Tüm paneller için temel sınıf. Görünürlük CanvasGroup ile yönetilir (SetActive yerine):
    /// panel kapalıyken de olay aboneliklerini koruyabilir ve layout yeniden hesaplanmaz.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        [SerializeField] UIScreenId screenId;
        [Tooltip("true: haritanın üstünde yan panel, harita tıklanabilir kalır. false: tam ekran, HUD gizlenir.")]
        [SerializeField] bool isOverlay = true;

        CanvasGroup canvasGroup;

        public UIScreenId Id => screenId;
        public bool IsOverlay => isOverlay;
        public bool IsVisible { get; private set; }

        protected GameSession Session => GameSession.Instance;
        protected UIFlowController Flow { get; private set; }

        internal void Initialize(UIFlowController flow)
        {
            Flow = flow;
            if (!TryGetComponent(out canvasGroup)) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            ApplyVisibility(false);
            OnInitialized();
        }

        internal void SetVisible(bool visible)
        {
            if (IsVisible == visible) return;
            ApplyVisibility(visible);
            if (visible) OnShown();
            else OnHidden();
        }

        void ApplyVisibility(bool visible)
        {
            IsVisible = visible;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        protected virtual void OnInitialized() { }
        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
    }
}
