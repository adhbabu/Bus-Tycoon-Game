using System;
using System.Collections.Generic;
using BusTycoon.Routes;
using UnityEngine;

namespace BusTycoon.Presentation.UI
{
    /// <summary>
    /// Ekran akışı (state machine). Kurallar:
    ///  • Aynı anda en fazla BİR yan panel açıktır → oyuncu asla pencere yığınında kaybolmaz.
    ///  • Harita her zaman arka planda canlıdır; yan paneller haritayı kapatmaz.
    ///  • ESC / Geri her zaman bir önceki ekrana, en sonunda temiz haritaya döner.
    /// Akış diyagramı: docs/04-UI-Akis-Plani.md
    /// </summary>
    public sealed class UIFlowController : MonoBehaviour
    {
        [SerializeField] List<UIScreen> screens = new List<UIScreen>();
        [SerializeField] UIScreenId initialScreen = UIScreenId.MainMenu;
        [Tooltip("Tam ekran menülerde gizlenen HUD (kasa, tarih, hız kontrolleri, hat tepsisi).")]
        [SerializeField] GameObject hudRoot;

        readonly Dictionary<UIScreenId, UIScreen> lookup = new Dictionary<UIScreenId, UIScreen>();
        readonly Stack<UIScreenId> history = new Stack<UIScreenId>();
        UIScreen current;

        public event Action<UIScreenId> ScreenChanged;

        public UIScreenId Current => current != null ? current.Id : UIScreenId.None;

        void Awake()
        {
            foreach (var screen in screens)
            {
                if (screen == null) continue;
                lookup[screen.Id] = screen;
                screen.Initialize(this);
            }
        }

        void Start() => Show(initialScreen, addToHistory: false);

        void Update()
        {
            if (BackPressed()) Back();
        }

        public T Get<T>() where T : UIScreen
        {
            foreach (var screen in lookup.Values)
                if (screen is T typed) return typed;
            return null;
        }

        public void Show(UIScreenId id, bool addToHistory = true)
        {
            if (current != null && current.Id == id) return;
            if (addToHistory && current != null) history.Push(current.Id);

            if (current != null) current.SetVisible(false);
            current = id != UIScreenId.None && lookup.TryGetValue(id, out var next) ? next : null;
            if (current != null) current.SetVisible(true);

            if (hudRoot != null) hudRoot.SetActive(current == null || current.IsOverlay);
            ScreenChanged?.Invoke(Current);
        }

        /// <summary>Panelleri kapatıp temiz haritaya döner.</summary>
        public void CloseAll()
        {
            history.Clear();
            Show(UIScreenId.None, addToHistory: false);
        }

        public bool Back()
        {
            if (current == null) return false;
            if (!current.IsOverlay && history.Count == 0) return false; // ana menüden geri yok

            Show(history.Count > 0 ? history.Pop() : UIScreenId.None, addToHistory: false);
            return true;
        }

        // ------------------------------------------------------------------ kısayollar (diğer panellerden çağrılır)

        public void OpenPricing(RouteState route)
        {
            var panel = Get<TicketPricePanel>();
            if (panel == null) return;
            panel.Bind(route);
            Show(UIScreenId.Pricing);
        }

        public void OpenDealership() => Show(UIScreenId.Dealership);

        static bool BackPressed()
        {
#if BUSTYCOON_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }
    }
}
