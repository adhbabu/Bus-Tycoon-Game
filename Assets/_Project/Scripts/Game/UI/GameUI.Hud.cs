using System;
using System.Collections.Generic;
using BusTycoon.Core;
using UnityEngine;

namespace BusTycoon.Game
{
    // Üst şerit, sol araç çubuğu (masaüstü) / alt menü çubuğu (telefon), görev kutusu ve bildirimler.
    public sealed partial class GameUI
    {
        readonly List<Notifications.Entry> activeToasts = new List<Notifications.Entry>();

        void DrawTopBar()
        {
            var bar = new Rect(0f, 0f, W, SafeTop + TopBarHeight);
            uiRects.Add(bar);
            GUI.Box(bar, GUIContent.none, s.TopBar);

            var state = session.State;
            var progression = session.Progression;
            double cash = session.Finance.Cash;
            int idle = 0;
            foreach (var bus in session.Fleet.Buses) if (!bus.IsAssigned) idle++;

            GUILayout.BeginArea(new Rect(SafeLeft + 16f * u, SafeTop + 6f * u, SafeRight - SafeLeft - 32f * u, TopBarHeight - 10f * u));
            GUILayout.BeginHorizontal();

            // Şirket ve seviye
            GUILayout.BeginVertical(Wd(N(250f, 190f)));
            GUILayout.Label(state.companyName, s.Bold);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Seviye {progression.Level}", s.Small, Wd(72f));
            LayoutBar(progression.LevelProgress01, UIStyles.Gold, 8f, N(150f, 100f), 7f);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // Kasa
            GUILayout.BeginVertical(Wd(N(250f, 200f)));
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
            GUILayout.BeginVertical(Wd(N(220f, 200f)));
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
            GUILayout.BeginVertical(Wd(N(170f, 140f)));
            GUILayout.Label($"İtibar {progression.Reputation:0} / 100", s.Small);
            LayoutBar(progression.Reputation / 100f, UIStyles.Blue, 8f, N(150f, 120f), 4f);
            GUILayout.EndVertical();

            GUILayout.BeginVertical(Wd(N(190f, 150f)));
            GUILayout.Label($"Hat {session.Routes.Routes.Count} / {progression.MaxRoutes}", s.Small);
            GUILayout.Label($"Otobüs {session.Fleet.Buses.Count} ({idle} garajda)", s.Small);
            GUILayout.EndVertical();

            if (GUILayout.Button("Menü", s.Button, Wd(80f), Ht(42f))) Defer(OpenPauseMenu);

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Günün ilerleyişi: üst şeridin altında ince çizgi
            if (Event.current.type == EventType.Repaint)
                DrawRect(new Rect(0f, SafeTop + TopBarHeight - 3f * u, W * session.Clock.DayProgress01, 3f * u), new Color(0.45f, 0.74f, 1f, 0.85f));
        }

        void SpeedButton(string label, int speed)
        {
            bool active = session.Clock.Speed == speed;
            if (GUILayout.Button(label, active ? s.SpeedButtonActive : s.SpeedButton, Ht(28f)))
                session.Clock.SetSpeed(speed);
        }

        double LastDayNet()
        {
            var history = session.State.financeHistory;
            return history.Count == 0 ? 0 : history[history.Count - 1].Net;
        }

        string RoutesLabel => warningCount > 0 ? $"Hatlarım ({warningCount}!)" : "Hatlarım";

        /// <summary>Masaüstü: solda dikey araç çubuğu.</summary>
        void DrawToolbar()
        {
            var rect = new Rect(SafeLeft + Margin, SafeTop + TopBarHeight + Margin, ToolbarWidth, 476f * u);
            uiRects.Add(rect);
            GUILayout.BeginArea(rect, s.Panel);
            ToolButton(Key("Yeni Hat", "N"), Panel.Planner, () => OpenPlannerNow(null), 38f);
            ToolButton(Key(RoutesLabel, "R"), Panel.RouteList, () => OpenPanelNow(Panel.RouteList), 38f);
            ToolButton(Key("Filo", "F"), Panel.Fleet, () => OpenPanelNow(Panel.Fleet), 38f);
            ToolButton(Key("Galeri", "G"), Panel.Dealership, () => OpenDealershipNow(null), 38f);
            ToolButton(Key("Finans", "K"), Panel.Finance, () => OpenPanelNow(Panel.Finance), 38f);
            ToolButton(Key("Bölgeler", "B"), Panel.Regions, () => OpenPanelNow(Panel.Regions), 38f);
            ToolButton(Key("Görevler", "J"), Panel.Goals, () => OpenPanelNow(Panel.Goals), 38f);
            ToolButton(Key("Günlük", "L"), Panel.Log, () => OpenPanelNow(Panel.Log), 38f);
            ToolButton(Key("Yardım", "H"), Panel.Help, () => OpenPanelNow(Panel.Help), 38f);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Key("Kaydet", "F5"), s.Ghost, Ht(30f))) Defer(() => SaveTo(SaveSlot));
            GUILayout.EndArea();
        }

        /// <summary>Telefon: ekranın altında yatay menü çubuğu (başparmakla erişilebilir).</summary>
        void DrawBottomBar()
        {
            var bar = new Rect(0f, H - SafeBottom - BottomBarHeight, W, SafeBottom + BottomBarHeight);
            uiRects.Add(bar);
            GUI.Box(bar, GUIContent.none, s.TopBar);

            GUILayout.BeginArea(new Rect(SafeLeft + 6f * u, bar.y + 5f * u, SafeRight - SafeLeft - 12f * u, BottomBarHeight - 10f * u));
            GUILayout.BeginHorizontal();
            ToolButton("Yeni Hat", Panel.Planner, () => OpenPlannerNow(null), 50f);
            ToolButton(RoutesLabel, Panel.RouteList, () => OpenPanelNow(Panel.RouteList), 50f);
            ToolButton("Filo", Panel.Fleet, () => OpenPanelNow(Panel.Fleet), 50f);
            ToolButton("Galeri", Panel.Dealership, () => OpenDealershipNow(null), 50f);
            ToolButton("Finans", Panel.Finance, () => OpenPanelNow(Panel.Finance), 50f);
            ToolButton("Bölgeler", Panel.Regions, () => OpenPanelNow(Panel.Regions), 50f);
            ToolButton("Görevler", Panel.Goals, () => OpenPanelNow(Panel.Goals), 50f);
            ToolButton("Yardım", Panel.Help, () => OpenPanelNow(Panel.Help), 50f);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void ToolButton(string text, Panel target, Action open, float height)
        {
            bool active = panel == target;
            GUIStyle style = active ? s.TabActive : s.Button;
            if (!active && target == Panel.RouteList && warningCount > 0) style = s.Danger;
            if (GUILayout.Button(text, style, Ht(height)))
                Defer(active ? ClosePanel : open);
        }

        void DrawGoalWidget()
        {
            GoalTracker.Goal goal = goals.Current;
            if (goal == null) return;
            if (compact && panel != Panel.None) return; // telefonda harita alanı dar; panel açıkken gizle

            float width = 400f * u, height = 104f * u;
            var rect = new Rect(SafeLeft + Margin, ContentBottom - height - Margin, width, height);
            if (panel != Panel.None && rect.xMax > PanelRect.x - Margin) return; // dar ekranda panelle çakışmasın
            uiRects.Add(rect);

            GUI.Box(rect, GUIContent.none, s.Panel);
            float pad = 14f * u, inner = rect.width - 2f * pad;
            Color previous = GUI.contentColor;
            GUI.contentColor = UIStyles.Gold;
            GUI.Label(new Rect(rect.x + pad, rect.y + 8f * u, inner, 20f * u),
                $"GÖREV {goals.CompletedCount + 1}/{goals.Goals.Count}" + (goal.Reward > 0 ? $"  ·  Ödül {Fmt.Money(goal.Reward)}" : ""), s.Small);
            GUI.contentColor = previous;
            GUI.Label(new Rect(rect.x + pad, rect.y + 28f * u, inner, 24f * u), goal.Title, s.Bold);
            GUI.Label(new Rect(rect.x + pad, rect.y + 52f * u, inner, 44f * u), goal.Hint, s.WrapDim);

            if (GUI.Button(new Rect(rect.xMax - 92f * u, rect.y + 6f * u, 80f * u, 26f * u), "Tümü", s.Ghost)) Defer(() => OpenPanelNow(Panel.Goals));
        }

        void DrawToasts()
        {
            notes.GetActive(6f, 4, activeToasts);
            float width = Mathf.Min(560f * u, SafeRight - SafeLeft - 2f * Margin);
            float x = SafeLeft + (SafeRight - SafeLeft - width) * 0.5f;
            float y = SafeTop + TopBarHeight + 10f * u;

            foreach (var toast in activeToasts)
            {
                float age = Time.unscaledTime - toast.Time;
                float alpha = Mathf.Clamp01(Mathf.Min(age * 6f, (6f - age) * 2f));
                var rect = new Rect(x, y, width, 36f * u);

                Color previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Box(rect, GUIContent.none, s.Toast);
                DrawRect(new Rect(rect.x, rect.y + 4f * u, 4f * u, rect.height - 8f * u), new Color(toast.Color.r, toast.Color.g, toast.Color.b, alpha));
                GUI.Label(new Rect(rect.x + 16f * u, rect.y + 7f * u, rect.width - 24f * u, 24f * u), toast.Text, s.Label);
                GUI.color = previous;
                y += 42f * u;
            }
        }
    }
}
