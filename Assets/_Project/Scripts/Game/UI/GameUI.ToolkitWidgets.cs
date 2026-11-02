using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>Yeni arayüzde haritanın üstündeki küçük parçalar: görev kartı (telefonda tek satır) ve yakınlaştırma.</summary>
    public sealed partial class GameUI
    {
        VisualElement tkGoal, tkGoalIcon, tkZoom;
        Label tkGoalHeader, tkGoalTitle, tkGoalHint;
        Button tkGoalAll;
        GoalTracker.Goal tkGoalShown;
        int tkGoalCount = -1;

        void BuildToolkitWidgets()
        {
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
    }
}
