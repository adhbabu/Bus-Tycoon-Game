using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüzde sağ paneller (UI Toolkit). Panel açılınca ya da verisi değişince (gün sonu, hat/filo değişikliği)
    /// içeriği bir kez kurulur; her kare yalnız yeri güncellenir. Taşınan paneller <see cref="ToolkitPanels"/>'te;
    /// diğerleri geçiş sürerken IMGUI'de çizilir.
    /// </summary>
    public sealed partial class GameUI
    {
        static readonly HashSet<Panel> ToolkitPanels = new HashSet<Panel> { Panel.Goals, Panel.Log, Panel.Help };

        VisualElement tkSheet;
        ScrollView tkSheetScroll;
        Label tkSheetTitle, tkSheetSubtitle;
        Panel tkBuiltPanel = Panel.None;
        bool tkSheetDirty;
        int tkSheetLogCount = -1, tkSheetOpenFrame = -1;

        /// <summary>Panel yeni arayüzde mi çiziliyor (IMGUI çizmez).</summary>
        bool ToolkitDrawsPanel(Panel target) => NewUi && ToolkitPanels.Contains(target);

        void BuildToolkitSheet()
        {
            tkSheet = Ui.El("bt-sheet", tkHud);
            VisualElement header = Ui.El("bt-sheet__header", tkSheet);
            tkSheetTitle = Ui.Text("bt-sheet__title", "", UiWeight.Black, header);
            Ui.El("bt-grow", header).pickingMode = PickingMode.Ignore;
            Button close = Ui.Button("bt-sheet__close", "×", ClosePanel, header, UiWeight.Bold);
            close.name = "close";
            tkSheetSubtitle = Ui.Text("bt-sheet__subtitle", "", UiWeight.Regular, tkSheet);
            tkSheetScroll = new ScrollView(ScrollViewMode.Vertical);
            tkSheetScroll.AddToClassList("bt-sheet__scroll");
            tkSheet.Add(tkSheetScroll);
            tkSheet.style.display = DisplayStyle.None;
        }

        void UpdateToolkitSheet(float k)
        {
            bool show = ToolkitDrawsPanel(panel) && !StationUi;
            Ui.Show(tkSheet, show);
            if (!show)
            {
                tkBuiltPanel = Panel.None;
                return;
            }
            Rect rect = PanelRect;
            Ui.Place(tkSheet, rect.x * k, rect.y * k, rect.width * k, rect.height * k);

            if (panel == Panel.Log && notes.Log.Count != tkSheetLogCount) tkSheetDirty = true;
            if (panel != tkBuiltPanel)
            {
                tkSheet.RemoveFromClassList("is-open"); // açılış: sağdan kayarak gelir (stilde geçiş)
                tkSheetOpenFrame = Time.frameCount + 1;
                tkSheetScroll.scrollOffset = Vector2.zero;
                tkSheetDirty = true;
            }
            else if (tkSheetOpenFrame >= 0 && Time.frameCount >= tkSheetOpenFrame)
            {
                tkSheetOpenFrame = -1;
                tkSheet.AddToClassList("is-open");
            }
            if (!tkSheetDirty) return;
            tkSheetDirty = false;
            tkBuiltPanel = panel;
            RebuildToolkitSheet();
        }

        void RebuildToolkitSheet()
        {
            VisualElement body = tkSheetScroll.contentContainer;
            body.Clear();
            switch (panel)
            {
                case Panel.Goals:
                    SheetHeader("Görevler", $"{goals.CompletedCount} / {goals.Goals.Count} tamamlandı");
                    BuildGoals(body);
                    break;
                case Panel.Log:
                    SheetHeader("Günlük", "Son olaylar");
                    BuildLog(body);
                    break;
                case Panel.Help:
                    SheetHeader("Nasıl Oynanır?", null);
                    BuildHelp(body);
                    break;
            }
        }

        void SheetHeader(string title, string subtitle)
        {
            tkSheetTitle.text = title;
            tkSheetSubtitle.text = subtitle ?? string.Empty;
            Ui.Show(tkSheetSubtitle, !string.IsNullOrEmpty(subtitle));
        }

        void BuildGoals(VisualElement body)
        {
            GoalTracker.Goal current = goals.Current;
            foreach (var goal in goals.Goals)
            {
                bool done = goals.IsCompleted(goal);
                VisualElement card = Ui.El("bt-item", body);
                Ui.Toggle(card, "is-done", done);
                Ui.Toggle(card, "is-current", goal == current);
                VisualElement row = Ui.El("bt-row", card);
                Ui.Icon(done ? UIIcon.Check : goal == current ? UIIcon.Goals : UIIcon.Lock, "bt-item__icon", row);
                Ui.Text("bt-item__title bt-grow", goal.Title, UiWeight.Bold, row);
                if (done) Ui.Text("bt-pill is-success", "Tamam", UiWeight.Bold, row);
                else if (goal == current) Ui.Text("bt-pill is-warning", "Sıradaki", UiWeight.Bold, row);
                Ui.Text("bt-item__text", goal.Hint, UiWeight.Regular, card);
                if (goal.Reward > 0f) Ui.Text("bt-item__reward", $"Ödül: {Fmt.Money(goal.Reward)}", UiWeight.Bold, card);
            }
        }

        void BuildLog(VisualElement body)
        {
            var log = notes.Log;
            tkSheetLogCount = log.Count;
            if (log.Count == 0) Ui.Text("bt-empty", "Henüz olay yok.", UiWeight.Regular, body);
            for (int i = log.Count - 1; i >= 0; i--)
            {
                VisualElement row = Ui.El("bt-log", body);
                Ui.Icon(ToastIcon(log[i].Color), "bt-log__icon", row);
                Ui.Text("bt-log__day", $"Gün {log[i].Day + 1}", UiWeight.Bold, row);
                Ui.Text("bt-log__text", log[i].Display, UiWeight.Regular, row);
            }
        }

        void BuildHelp(VisualElement body)
        {
            foreach (var (title, text) in HelpSections())
            {
                Ui.Text("bt-help__title", title, UiWeight.Bold, body);
                Ui.Text("bt-help__text", text, UiWeight.Regular, body);
            }
        }
    }
}
