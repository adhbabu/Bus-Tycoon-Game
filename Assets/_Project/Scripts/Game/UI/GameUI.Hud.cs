using System;
using System.Collections.Generic;
using BusTycoon.Core;
using UnityEngine;

namespace BusTycoon.Game
{
    // Üst şerit, sol araç çubuğu, görev kutusu ve bildirimler.
    public sealed partial class GameUI
    {
        readonly List<Notifications.Entry> activeToasts = new List<Notifications.Entry>();

        void DrawTopBar()
        {
            var bar = new Rect(0f, 0f, W, TopBarHeight);
            uiRects.Add(bar);
            GUI.Box(bar, GUIContent.none, s.TopBar);

            var state = session.State;
            var progression = session.Progression;
            double cash = session.Finance.Cash;
            int idle = 0;
            foreach (var bus in session.Fleet.Buses) if (!bus.IsAssigned) idle++;

            GUILayout.BeginArea(new Rect(16f, 6f, W - 32f, TopBarHeight - 10f));
            GUILayout.BeginHorizontal();

            // Şirket ve seviye
            GUILayout.BeginVertical(GUILayout.Width(250f));
            GUILayout.Label(state.companyName, s.Bold);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Seviye {progression.Level}", s.Small, GUILayout.Width(72f));
            LayoutBar(progression.LevelProgress01, UIStyles.Gold, 8f, 150f, 7f);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // Kasa
            GUILayout.BeginVertical(GUILayout.Width(250f));
            ColorLabel(Fmt.Money(cash), cash < 0 ? UIStyles.Red : UIStyles.Text, s.H1);
            if (session.Finance.DaysInDebt > 0)
            {
                int left = session.ProgressionConfig.BankruptcyGraceDays - session.Finance.DaysInDebt;
                ColorLabel($"İflasa {left} gün!", UIStyles.Red, s.Small);
            }
            else
            {
                double net = LastDayNet();
                ColorLabel($"Dün {Fmt.Signed(net)}", net >= 0 ? UIStyles.Green : UIStyles.Red, s.Small);
            }
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Tarih ve hız
            GUILayout.BeginVertical(GUILayout.Width(220f));
            GUILayout.Label(SimulationClock.FormatDate(state.currentDay), s.Bold);
            GUILayout.BeginHorizontal();
            SpeedButton("II", 0);
            SpeedButton("1x", 1);
            SpeedButton("2x", 2);
            SpeedButton("4x", 4);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // İtibar, hat, filo
            GUILayout.BeginVertical(GUILayout.Width(170f));
            GUILayout.Label($"İtibar {progression.Reputation:0} / 100", s.Small);
            LayoutBar(progression.Reputation / 100f, UIStyles.Blue, 8f, 150f, 4f);
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(190f));
            GUILayout.Label($"Hat {session.Routes.Routes.Count} / {progression.MaxRoutes}", s.Small);
            GUILayout.Label($"Otobüs {session.Fleet.Buses.Count} ({idle} garajda)", s.Small);
            GUILayout.EndVertical();

            if (GUILayout.Button("Menü", s.Button, GUILayout.Width(80f), GUILayout.Height(42f))) Defer(OpenPauseMenu);

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Günün ilerleyişi: üst şeridin altında ince çizgi
            if (Event.current.type == EventType.Repaint)
                DrawRect(new Rect(0f, TopBarHeight - 3f, W * session.Clock.DayProgress01, 3f), new Color(0.45f, 0.74f, 1f, 0.85f));
        }

        void SpeedButton(string label, int speed)
        {
            bool active = session.Clock.Speed == speed;
            if (GUILayout.Button(label, active ? s.SpeedButtonActive : s.SpeedButton, GUILayout.Height(28f)))
                session.Clock.SetSpeed(speed);
        }

        double LastDayNet()
        {
            var history = session.State.financeHistory;
            return history.Count == 0 ? 0 : history[history.Count - 1].Net;
        }

        void DrawToolbar()
        {
            var rect = new Rect(Margin, TopBarHeight + Margin, ToolbarWidth, 476f);
            uiRects.Add(rect);
            GUILayout.BeginArea(rect, s.Panel);
            ToolButton("Yeni Hat   [N]", Panel.Planner, () => OpenPlannerNow(null));
            ToolButton(warningCount > 0 ? $"Hatlarım ({warningCount}!) [R]" : "Hatlarım   [R]", Panel.RouteList, () => OpenPanelNow(Panel.RouteList));
            ToolButton("Filo        [F]", Panel.Fleet, () => OpenPanelNow(Panel.Fleet));
            ToolButton("Galeri      [G]", Panel.Dealership, () => OpenDealershipNow(null));
            ToolButton("Finans      [K]", Panel.Finance, () => OpenPanelNow(Panel.Finance));
            ToolButton("Bölgeler   [B]", Panel.Regions, () => OpenPanelNow(Panel.Regions));
            ToolButton("Görevler   [J]", Panel.Goals, () => OpenPanelNow(Panel.Goals));
            ToolButton("Günlük      [L]", Panel.Log, () => OpenPanelNow(Panel.Log));
            ToolButton("Yardım      [H]", Panel.Help, () => OpenPanelNow(Panel.Help));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Kaydet [F5]", s.Ghost, GUILayout.Height(30f))) Defer(() => SaveTo(SaveSlot));
            GUILayout.EndArea();
        }

        void ToolButton(string text, Panel target, Action open)
        {
            bool active = panel == target;
            GUIStyle style = active ? s.TabActive : s.Button;
            if (!active && target == Panel.RouteList && warningCount > 0) style = s.Danger;
            if (GUILayout.Button(text, style, GUILayout.Height(38f)))
                Defer(active ? ClosePanel : open);
        }

        void DrawGoalWidget()
        {
            GoalTracker.Goal goal = goals.Current;
            if (goal == null) return;

            var rect = new Rect(Margin, H - 104f - Margin, 400f, 104f);
            if (panel != Panel.None && rect.xMax > W - PanelWidth - Margin) return; // dar ekranda panelle çakışmasın
            uiRects.Add(rect);

            GUI.Box(rect, GUIContent.none, s.Panel);
            Color previous = GUI.contentColor;
            GUI.contentColor = UIStyles.Gold;
            GUI.Label(new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, 20f),
                $"GÖREV {goals.CompletedCount + 1}/{goals.Goals.Count}" + (goal.Reward > 0 ? $"  ·  Ödül {Fmt.Money(goal.Reward)}" : ""), s.Small);
            GUI.contentColor = previous;
            GUI.Label(new Rect(rect.x + 14f, rect.y + 28f, rect.width - 28f, 24f), goal.Title, s.Bold);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 52f, rect.width - 28f, 44f), goal.Hint, s.WrapDim);

            if (GUI.Button(new Rect(rect.xMax - 92f, rect.y + 6f, 80f, 24f), "Tümü", s.Ghost)) Defer(() => OpenPanelNow(Panel.Goals));
        }

        void DrawToasts()
        {
            notes.GetActive(6f, 4, activeToasts);
            const float width = 560f;
            float x = (W - width) * 0.5f;
            float y = TopBarHeight + 10f;

            foreach (var toast in activeToasts)
            {
                float age = Time.unscaledTime - toast.Time;
                float alpha = Mathf.Clamp01(Mathf.Min(age * 6f, (6f - age) * 2f));
                var rect = new Rect(x, y, width, 36f);

                Color previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Box(rect, GUIContent.none, s.Toast);
                DrawRect(new Rect(rect.x, rect.y + 4f, 4f, rect.height - 8f), new Color(toast.Color.r, toast.Color.g, toast.Color.b, alpha));
                GUI.Label(new Rect(rect.x + 16f, rect.y + 7f, rect.width - 24f, 24f), toast.Text, s.Label);
                GUI.color = previous;
                y += 42f;
            }
        }
    }
}
