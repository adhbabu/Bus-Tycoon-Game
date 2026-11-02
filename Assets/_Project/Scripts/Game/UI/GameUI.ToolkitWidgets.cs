using BusTycoon.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüzde haritanın üstündeki küçük parçalar: görev kartı (telefonda tek satır), yakınlaştırma, mini haritanın
    /// çerçevesi ve başlığı (harita içeriğini IMGUI çizer) ve şehir ipucu (fare şehrin üstündeyken).
    /// </summary>
    public sealed partial class GameUI
    {
        VisualElement tkMinimap, tkMinimapHeader, tkTooltip;
        Button tkMinimapToggle;
        VisualElement tkMinimapPlus;
        bool? tkMinimapCollapsedShown;
        int tkTooltipSignature = int.MinValue;
        VisualElement tkGoal, tkGoalIcon, tkZoom;
        Label tkGoalHeader, tkGoalTitle, tkGoalHint;
        Button tkGoalAll;
        GoalTracker.Goal tkGoalShown;
        int tkGoalCount = -1;

        void BuildToolkitWidgets()
        {
            // Mini harita çerçevesi: başlık ve küçült/büyüt (telefonda başlığın tamamı düğme)
            tkMinimap = Ui.El("bt-minimap", tkHud);
            tkMinimapHeader = Ui.El("bt-row bt-minimap__header", tkMinimap);
            Ui.Icon(UIIcon.Routes, "bt-icon bt-minimap__icon", tkMinimapHeader);
            Ui.Text("bt-caps bt-grow", "HARİTA", UiWeight.Bold, tkMinimapHeader);
            // Eksi / artı işareti çizgilerle (yakınlaştırmanın +/− düğmeleriyle karışmasın, her boyutta net)
            tkMinimapToggle = Ui.Button("bt-minimap__toggle", string.Empty, () => { if (!phone) minimapCollapsed = !minimapCollapsed; }, tkMinimapHeader);
            Ui.El("bt-minimap__mark", tkMinimapToggle).pickingMode = PickingMode.Ignore;
            tkMinimapPlus = Ui.El("bt-minimap__mark bt-minimap__mark--vertical", tkMinimapToggle);
            tkMinimapPlus.pickingMode = PickingMode.Ignore;
            tkMinimapHeader.RegisterCallback<ClickEvent>(_ => { if (phone) minimapCollapsed = !minimapCollapsed; });
            tkMinimap.style.display = DisplayStyle.None;

            tkGoal = Ui.El("bt-goal", tkHud);
            tkGoalIcon = Ui.Icon(UIIcon.Goals, "bt-goal__icon", tkGoal);
            VisualElement text = Ui.El("bt-col bt-grow bt-goal__text", tkGoal);
            text.pickingMode = PickingMode.Ignore;
            tkGoalHeader = Ui.Text("bt-caps", "", UiWeight.Bold, text);
            tkGoalTitle = Ui.Text("bt-goal__title", "", UiWeight.Bold, text);
            tkGoalHint = Ui.Text("bt-goal__hint", "", UiWeight.Regular, text);
            tkGoalAll = Ui.Button("bt-tab is-active bt-goal__all", "Tümü", () => OpenPanelNow(Panel.Goals), tkGoal);
            tkGoal.RegisterCallback<ClickEvent>(_ => { if (phone) OpenPanelNow(Panel.Goals); }); // telefonda şeridin tamamı
            tkGoal.style.display = DisplayStyle.None;

            tkZoom = Ui.El("bt-zoom", tkHud);
            tkZoom.pickingMode = PickingMode.Ignore;
            Ui.Button("bt-zoom__button", "+", () => mapCamera.Zoom(-4f, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), tkZoom);
            Ui.Button("bt-zoom__button", "−", () => mapCamera.Zoom(4f, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), tkZoom);
        }

        void UpdateToolkitWidgets(float k)
        {
            bool onMap = !StationUi;

            Ui.Show(tkMinimap, onMap);
            if (onMap)
            {
                Rect mini = MinimapRect;
                Ui.Place(tkMinimap, mini.x * k, mini.y * k, mini.width * k, mini.height * k);
                tkMinimapHeader.style.height = MinimapHeader * k;
                tkCovered.Add(mini);
                if (tkMinimapCollapsedShown != minimapCollapsed)
                {
                    tkMinimapCollapsedShown = minimapCollapsed;
                    Ui.Show(tkMinimapPlus, minimapCollapsed);
                }
            }
            // Görev kartı: masaüstünde mini haritanın sağında; telefonda tek satır; dar ekranda panelle çakışırsa gizli
            GoalTracker.Goal goal = goals.Current;
            bool showGoal = onMap && goal != null && !(compact && panel != Panel.None);
            float height = (phone ? 44f : 108f) * u;
            float left = MinimapRect.xMax + Margin;
            float width = phone ? Mathf.Min(460f * u, SafeRight - Margin - left) : 400f * u;
            if (phone && width < 200f * u) showGoal = false;
            if (!phone && panel != Panel.None && left + width > PanelRect.x - Margin) showGoal = false;
            Ui.Show(tkGoal, showGoal);
            if (showGoal)
            {
                Ui.Place(tkGoal, left * k, (ContentBottom - height - Margin) * k, width * k, height * k);
                tkCovered.Add(new Rect(left, ContentBottom - height - Margin, width, height));
                Ui.Toggle(tkGoal, "is-chip", phone);
                if (goal != tkGoalShown || goals.CompletedCount != tkGoalCount)
                {
                    tkGoalShown = goal;
                    tkGoalCount = goals.CompletedCount;
                    tkGoalHeader.text = GoalHeader(goal);
                    tkGoalTitle.text = goal.Title;
                    tkGoalHint.text = goal.Hint;
                }
                Ui.Show(tkGoalHint, !phone);
                Ui.Show(tkGoalAll, !phone);
            }

            // Yakınlaştırma (telefonda iki parmakla)
            bool zoom = onMap && !phone;
            Ui.Show(tkZoom, zoom);
            if (zoom)
            {
                float size = 52f * u;
                float right = panel != Panel.None ? PanelRect.x : SafeRight;
                float top = ContentBottom - 2f * size - 2f * Margin;
                Ui.Place(tkZoom, (right - size - Margin) * k, top * k, size * k, (2f * size + 6f * u) * k);
                tkCovered.Add(new Rect(right - size - Margin, top, size, 2f * size + 6f * u));
            }
        }

        // ------------------------------------------------------------------ şehir ipucu

        void BuildToolkitTooltip()
        {
            tkTooltip = Ui.El("bt-tooltip", toolkit.Root);
            tkTooltip.pickingMode = PickingMode.Ignore;
            tkTooltip.style.display = DisplayStyle.None;
        }

        /// <summary>Fare bir şehrin üstündeyken: ad, ünü, bölge ve nüfus, hat sayısı, tıklayınca ne olacağı. İçerik yalnız değişince kurulur.</summary>
        void UpdateToolkitTooltip()
        {
            CityData city = hoveredCity;
            bool show = city != null && !InMenu && modal == Modal.None;
            Ui.Show(tkTooltip, show);
            if (!show)
            {
                tkTooltipSignature = int.MinValue;
                return;
            }

            int routes = 0;
            if (mode == Mode.Playing)
                foreach (var route in session.Routes.Routes)
                    if (route.cityAId == city.Id || route.cityBId == city.Id) routes++;
            bool planning = mode == Mode.Playing && panel == Panel.Planner && plannerOrigin != null && plannerOrigin != city;
            int signature = city.Id.GetHashCode() * 31 + (int)mode;
            signature = signature * 31 + routes;
            signature = signature * 31 + (planning ? plannerOrigin.Id.GetHashCode() : 0);
            signature = signature * 31 + (mode == Mode.Playing && session.Progression.IsRegionUnlocked(city.Region) ? 1 : 0);
            if (signature != tkTooltipSignature)
            {
                tkTooltipSignature = signature;
                BuildTooltip(city, routes, planning);
            }

            // Fareyi izler; ekrandan taşacaksa öbür yana geçer
            float k = 1f / u;
            float width = (UICityIcons.Has(city) ? 372f : 330f) * u;
            float height = float.IsNaN(tkTooltip.resolvedStyle.height) ? 80f * u : tkTooltip.resolvedStyle.height * u;
            Vector2 mouse = hoverMouseGui;
            float x = mouse.x + 18f * u, y = mouse.y + 14f * u;
            if (x + width > W) x = mouse.x - width - 12f * u;
            if (y + height > H) y = mouse.y - height - 12f * u;
            Ui.Move(tkTooltip, x * k, y * k);
        }

        void BuildTooltip(CityData city, int routes, bool planning)
        {
            tkTooltip.Clear();
            tkTooltip.style.width = (UICityIcons.Has(city) ? 372f : 330f);
            VisualElement row = Ui.El("bt-row bt-tooltip__row", tkTooltip);
            if (UICityIcons.Has(city))
            {
                VisualElement icon = Ui.El("bt-tooltip__city-icon", row);
                Sprite sprite = UICityIcons.Sprite(city);
                if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
            }
            VisualElement text = Ui.El("bt-col bt-grow", row);
            Ui.Text("bt-tooltip__title", city.DisplayName, UiWeight.Black, text);
            string famousFor = UICityIcons.Description(city);
            if (!string.IsNullOrEmpty(famousFor)) Ui.Text("bt-tooltip__line is-gold", famousFor, UiWeight.Bold, text);
            Ui.Text("bt-tooltip__line bt-dim", $"{city.Region.DisplayName} · nüfus {Fmt.Population(city.Population)}", UiWeight.Regular, text);
            if (mode == Mode.Playing)
            {
                if (routes > 0) Ui.Text("bt-tooltip__line is-up", $"{routes} hattınız var", UiWeight.Bold, text);
                if (!session.Progression.IsRegionUnlocked(city.Region))
                    Ui.Text("bt-tooltip__line is-warning-text", "Kilitli bölge — Bölgeler (B) panelinden lisans alın", UiWeight.Bold, text);
                else if (planning)
                    Ui.Text("bt-tooltip__line is-blue", $"Tıkla: varış olarak seç · {Fmt.Km(session.World.GetRoadDistanceKm(plannerOrigin, city))}", UiWeight.Bold, text);
                else
                    Ui.Text("bt-tooltip__line is-blue", "Tıkla: durağa gir", UiWeight.Bold, text);
            }
            else if (mode == Mode.NewGame)
            {
                Ui.Text("bt-tooltip__line is-blue", "Tıkla: merkez ofis olarak seç", UiWeight.Bold, text);
            }
            Ui.Icon(UIIcons.Flag(city.Region.Id), "bt-tooltip__flag", row);
        }
    }
}
