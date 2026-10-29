using System;
using System.Collections.Generic;
using BusTycoon.Core;
using UnityEngine;

namespace BusTycoon.Game
{
    // Gösterge paneli (üst şerit), menü (masaüstü: solda tam boy; telefon: altta), görev kartı ve bildirimler.
    public sealed partial class GameUI
    {
        readonly List<Notifications.Entry> activeToasts = new List<Notifications.Entry>();
        static readonly GUIContent ToastContent = new GUIContent("");

        /// <summary>Masaüstünde sol menü vardır; telefonda menü alttadır.</summary>
        bool HasSideBar => mode == Mode.Playing && !compact && !StationUi;
        float SideBarWidth => 96f * u;
        /// <summary>Harita/arayüz için kullanılabilir alanın sol sınırı (sol menünün sağı).</summary>
        float ContentLeft => SafeLeft + (HasSideBar ? SideBarWidth : 0f);

        void DrawTopBar()
        {
            float left = HasSideBar ? SafeLeft + SideBarWidth : 0f;
            var bar = new Rect(left, 0f, W - left, SafeTop + TopBarHeight);
            uiRects.Add(bar);
            GUI.Box(bar, GUIContent.none, s.TopBar);

            var state = session.State;
            var progression = session.Progression;
            double cash = session.Finance.Cash;
            int idle = 0;
            foreach (var bus in session.Fleet.Buses) if (!bus.IsAssigned) idle++;

            float x = Mathf.Max(left, SafeLeft) + 20f * u;
            GUILayout.BeginArea(new Rect(x, SafeTop + 9f * u, SafeRight - x - 16f * u, TopBarHeight - 14f * u));
            GUILayout.BeginHorizontal();

            // Şirket ve seviye
            GUILayout.BeginVertical(Wd(N(220f, 170f)));
            GUILayout.Label($"ŞİRKET · SEVİYE {progression.Level}", s.StatLabel);
            GUILayout.Label(state.companyName, s.Value);
            LayoutBar(progression.LevelProgress01, UIStyles.Orange, 5f, N(180f, 140f), 2f);
            GUILayout.EndVertical();
            Sp(N(16f, 8f));

            // Kasa: parlak yeşil (eksideyse kırmızı), yanında dünkü net
            GUILayout.BeginVertical(Wd(N(270f, 215f)));
            GUILayout.Label("ŞİRKET KASASI", s.StatLabel);
            GUILayout.BeginHorizontal();
            MoneyLabel(cash);
            GUILayout.BeginVertical();
            Sp(10f);
            if (session.Finance.DaysInDebt > 0)
            {
                int daysLeft = session.ProgressionConfig.BankruptcyGraceDays - session.Finance.DaysInDebt;
                ColorLabel($"İflasa {daysLeft} gün!", UIStyles.Red, s.Small);
            }
            else
            {
                double net = LastDayNet();
                ColorLabel($"Dün {Fmt.Signed(net)}", net >= 0 ? UIStyles.Green : UIStyles.Red, s.Small);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Tarih ve hız
            GUILayout.BeginVertical();
            GUILayout.Label("TARİH", s.StatLabel);
            GUILayout.BeginHorizontal();
            DayNightCycle cycle = DayNightCycle.Instance;
            if (cycle != null && cycle.Enabled)
            {
                LayoutIcon(cycle.CurrentPhase switch
                {
                    DayNightCycle.Phase.Sunrise => UIIcon.Sunrise,
                    DayNightCycle.Phase.Sunset => UIIcon.Sunset,
                    DayNightCycle.Phase.Night => UIIcon.Moon,
                    _ => UIIcon.Sun
                }, 18f, 3f);
                Sp(2f);
            }
            GUILayout.Label(SimulationClock.FormatDate(state.currentDay), s.Bold);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            Sp(9f);
            GUILayout.BeginHorizontal();
            SpeedButton("II", 0);
            SpeedButton("1x", 1);
            SpeedButton("2x", 2);
            SpeedButton("4x", 4);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // İtibar, hatlar, filo
            GUILayout.BeginVertical(Wd(N(96f, 82f)));
            GUILayout.Label("İTİBAR", s.StatLabel);
            GUILayout.BeginHorizontal();
            LayoutIcon(UIIcon.Star, 18f, 4f);
            GUILayout.Label($"{progression.Reputation:0}", s.Value);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(Wd(N(96f, 82f)));
            GUILayout.Label("HATLAR", s.StatLabel);
            GUILayout.Label($"{session.Routes.Routes.Count} / {progression.MaxRoutes}", s.Value);
            GUILayout.EndVertical();

            GUILayout.BeginVertical(Wd(N(150f, 96f)));
            GUILayout.Label("FİLO", s.StatLabel);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{session.Fleet.Buses.Count}", s.Value);
            if (!Narrow || idle > 0) ColorLabel(Narrow ? $"{idle} boşta" : $"otobüs · {idle} garajda", idle > 0 ? UIStyles.Orange : UIStyles.DimText, s.Small);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // Telefonda menü düğmesi burada (masaüstünde sol menünün altında)
            if (compact)
            {
                GUILayout.BeginVertical();
                Sp(4f);
                if (IconButton(UIIcon.Settings, "Menü", 46f)) Defer(OpenPauseMenu);
                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Günün ilerleyişi: gösterge panelinin altında ince turuncu çizgi
            if (Event.current.type == EventType.Repaint)
                DrawRect(new Rect(bar.x, SafeTop + TopBarHeight - 2f * u, bar.width * session.Clock.DayProgress01, 2f * u), new Color(1f, 0.57f, 0f, 0.8f));
        }

        /// <summary>Kasa: büyük, parlak yeşil; çevresine aynı rengin saydam kopyaları çizilerek hafif parıltı verilir.</summary>
        void MoneyLabel(double cash)
        {
            ToastContent.text = Fmt.Money(cash);
            bool negative = cash < 0;
            GUIStyle style = negative ? s.MoneyNegative : s.Money;
            Rect rect = GUILayoutUtility.GetRect(ToastContent, style);
            if (Event.current.type != EventType.Repaint) return;
            GUIStyle glow = negative ? s.MoneyNegativeGlow : s.MoneyGlow;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f, d = 2.5f * u;
                GUI.Label(new Rect(rect.x + Mathf.Cos(angle) * d, rect.y + Mathf.Sin(angle) * d, rect.width, rect.height), ToastContent, glow);
            }
            GUI.Label(rect, ToastContent, style);
        }

        void SpeedButton(string label, int speed)
        {
            bool active = session.Clock.Speed == speed;
            if (GUILayout.Button(label, active ? s.SpeedButtonActive : s.SpeedButton, Ht(32f)))
                session.Clock.SetSpeed(speed);
        }

        /// <summary>Düzen içinde ikon (yatayda boşluk bırakır, dikeyde ortalar).</summary>
        void LayoutIcon(UIIcon icon, float size, float topPadding = 0f)
        {
            Rect rect = GUILayoutUtility.GetRect(size * u, (size + topPadding) * u, Wd(size), Ht(size + topPadding));
            UIIcons.Draw(new Rect(rect.x, rect.y + topPadding * u, size * u, size * u), icon);
        }

        /// <summary>Kare ikon düğmesi; yazısı görünmez (stil saydam yazar), yalnız düğmeyi adlandırır.</summary>
        bool IconButton(UIIcon icon, string label, float size)
        {
            Rect rect = GUILayoutUtility.GetRect(size * u, size * u, Wd(size), Ht(size));
            bool clicked = GUI.Button(rect, label, s.IconOnly);
            float iconSize = size * 0.5f * u;
            UIIcons.Draw(new Rect(rect.center.x - iconSize / 2f, rect.center.y - iconSize / 2f, iconSize, iconSize), icon);
            return clicked;
        }

        double LastDayNet()
        {
            var history = session.State.financeHistory;
            return history.Count == 0 ? 0 : history[history.Count - 1].Net;
        }

        // ------------------------------------------------------------------ menü

        readonly struct NavEntry
        {
            public readonly UIIcon Icon;
            public readonly string Label;
            public readonly Panel Target;
            public readonly Action Open;

            public NavEntry(UIIcon icon, string label, Panel target, Action open)
            {
                Icon = icon;
                Label = label;
                Target = target;
                Open = open;
            }
        }

        NavEntry[] navEntries;

        NavEntry[] NavEntries => navEntries ??= new[]
        {
            new NavEntry(UIIcon.NewRoute, "Yeni Hat", Panel.Planner, () => OpenPlannerNow(null)),
            new NavEntry(UIIcon.Routes, "Hatlarım", Panel.RouteList, () => OpenPanelNow(Panel.RouteList)),
            new NavEntry(UIIcon.Fleet, "Filo", Panel.Fleet, () => OpenPanelNow(Panel.Fleet)),
            new NavEntry(UIIcon.Dealership, "Galeri", Panel.Dealership, () => OpenDealershipNow(null)),
            new NavEntry(UIIcon.Finance, "Finans", Panel.Finance, () => OpenPanelNow(Panel.Finance)),
            new NavEntry(UIIcon.Regions, "Bölgeler", Panel.Regions, () => OpenPanelNow(Panel.Regions)),
            new NavEntry(UIIcon.Goals, "Görevler", Panel.Goals, () => OpenPanelNow(Panel.Goals)),
            new NavEntry(UIIcon.Log, "Günlük", Panel.Log, () => OpenPanelNow(Panel.Log)),
            new NavEntry(UIIcon.Help, "Yardım", Panel.Help, () => OpenPanelNow(Panel.Help)),
        };

        /// <summary>Masaüstü: solda tam boy menü; ikon üstte, yazı altta, seçili öğe turuncu. En altta Kaydet ve Menü.</summary>
        void DrawSideBar()
        {
            var bar = new Rect(0f, 0f, SafeLeft + SideBarWidth, H);
            uiRects.Add(bar);
            GUI.Box(bar, GUIContent.none, s.SideBar);

            NavEntry[] entries = NavEntries;
            float top = SafeTop + 14f * u, bottom = H - SafeBottom - 10f * u;
            float itemHeight = Mathf.Min(64f * u, (bottom - top) / (entries.Length + 2.4f));
            float x = SafeLeft + 6f * u, width = SideBarWidth - 12f * u;
            float y = top;
            foreach (var entry in entries)
            {
                NavItem(new Rect(x, y, width, itemHeight - 4f * u), entry, 26f);
                y += itemHeight;
            }

            var save = new Rect(x, bottom - 2f * itemHeight, width, itemHeight - 4f * u);
            if (NavButton(save, UIIcon.Save, "Kaydet", false, 0, 22f)) Defer(() => SaveTo(SaveSlot));
            var menu = new Rect(x, bottom - itemHeight, width, itemHeight - 4f * u);
            if (NavButton(menu, UIIcon.Settings, "Menü", false, 0, 22f)) Defer(OpenPauseMenu);
        }

        /// <summary>Telefon: ekranın altında ikonlu menü çubuğu (başparmakla erişilebilir).</summary>
        void DrawBottomBar()
        {
            var bar = new Rect(0f, H - SafeBottom - BottomBarHeight, W, SafeBottom + BottomBarHeight);
            uiRects.Add(bar);
            GUI.Box(bar, GUIContent.none, s.BottomBar);

            NavEntry[] entries = NavEntries;
            int count = 0;
            foreach (var entry in entries) if (entry.Target != Panel.Log) count++;
            float x = SafeLeft + 6f * u, width = (SafeRight - SafeLeft - 12f * u) / count;
            foreach (var entry in entries)
            {
                if (entry.Target == Panel.Log) continue; // telefonda Günlük, Görevler panelinden açılır
                NavItem(new Rect(x + 2f * u, bar.y + 4f * u, width - 4f * u, BottomBarHeight - 8f * u), entry, 24f);
                x += width;
            }
        }

        void NavItem(Rect rect, NavEntry entry, float iconSize)
        {
            bool active = panel == entry.Target;
            int badge = entry.Target == Panel.RouteList ? warningCount : 0;
            if (NavButton(rect, entry.Icon, entry.Label, active, badge, iconSize))
                Defer(active ? ClosePanel : entry.Open);
        }

        /// <summary>Menü düğmesi: ikon üstte, yazı altta; uyarı sayısı kırmızı rozetle gösterilir.</summary>
        bool NavButton(Rect rect, UIIcon icon, string label, bool active, int badge, float iconSize)
        {
            bool clicked = GUI.Button(rect, label, active ? s.NavItemActive : s.NavItem);
            float size = iconSize * u;
            float labelSpace = s.NavItem.fontSize * 1.25f + 7f * u;
            float iconTop = rect.y + Mathf.Max(3f * u, (rect.height - labelSpace - size) * 0.5f);
            var iconRect = new Rect(rect.center.x - size / 2f, iconTop, size, size);
            UIIcons.Draw(iconRect, icon);
            if (badge > 0 && Event.current.type == EventType.Repaint)
            {
                float r = 8f * u;
                var center = new Vector2(iconRect.xMax + 2f * u, iconRect.y + 2f * u);
                DrawDot(center, r, UIStyles.Red);
                GUI.Label(new Rect(center.x - r, center.y - r, 2f * r, 2f * r), badge > 9 ? "9+" : badge.ToString(), s.CountBadge);
            }
            return clicked;
        }

        // ------------------------------------------------------------------ görev kartı ve bildirimler

        void DrawGoalWidget()
        {
            GoalTracker.Goal goal = goals.Current;
            if (goal == null) return;
            if (compact && panel != Panel.None) return; // telefonda harita alanı dar; panel açıkken gizle

            float width = 400f * u, height = 108f * u;
            var rect = new Rect(MinimapRect.xMax + Margin, ContentBottom - height - Margin, width, height); // mini haritanın sağında
            if (panel != Panel.None && rect.xMax > PanelRect.x - Margin) return; // dar ekranda panelle çakışmasın
            uiRects.Add(rect);

            GUI.Box(rect, GUIContent.none, s.Panel);
            float pad = 16f * u, icon = 30f * u;
            UIIcons.Draw(new Rect(rect.x + pad, rect.y + 16f * u, icon, icon), UIIcon.Goals);
            float textX = rect.x + pad + icon + 12f * u, inner = rect.xMax - pad - textX;
            GUI.Label(new Rect(textX, rect.y + 10f * u, inner - 70f * u, 18f * u),
                $"GÖREV {goals.CompletedCount + 1}/{goals.Goals.Count}" + (goal.Reward > 0 ? $"  ·  ÖDÜL {Fmt.Money(goal.Reward)}" : ""), s.StatLabel);
            GUI.Label(new Rect(textX, rect.y + 28f * u, inner, 24f * u), goal.Title, s.Bold);
            GUI.Label(new Rect(textX, rect.y + 52f * u, inner, 48f * u), goal.Hint, s.WrapDim);

            if (GUI.Button(new Rect(rect.xMax - 78f * u, rect.y + 8f * u, 68f * u, 28f * u), "Tümü", s.TabActive)) Defer(() => OpenPanelNow(Panel.Goals));
        }

        /// <summary>Hap biçimli bildirimler: yeşil = başarı (siyah yazı), kırmızı = hata, diğerleri koyu; başta ikon.</summary>
        void DrawToasts()
        {
            notes.GetActive(6f, 4, activeToasts);
            float maxWidth = Mathf.Min(620f * u, SafeRight - ContentLeft - 2f * Margin);
            float y = SafeTop + TopBarHeight + 14f * u;

            foreach (var toast in activeToasts)
            {
                float age = Time.unscaledTime - toast.Time;
                float alpha = Mathf.Clamp01(Mathf.Min(age * 6f, (6f - age) * 2f));
                float slide = (1f - Mathf.Clamp01(age * 4f)) * -16f * u; // yukarıdan kayarak gelir

                GUIStyle box = s.ToastBox(toast.Color);
                GUIStyle text = box == s.ToastSuccessBox ? s.ToastDarkText : s.ToastText;
                ToastContent.text = toast.Text;
                float icon = 20f * u, height = 44f * u;
                float width = Mathf.Min(maxWidth, text.CalcSize(ToastContent).x + icon + 52f * u);
                var rect = new Rect(ContentLeft + (SafeRight - ContentLeft - width) * 0.5f, y + slide, width, height);

                Color previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Box(rect, GUIContent.none, box);
                UIIcons.Draw(new Rect(rect.x + 20f * u, rect.center.y - icon / 2f, icon, icon), ToastIcon(toast.Color));
                GUI.Label(new Rect(rect.x + 20f * u + icon + 10f * u, rect.y + 11f * u, rect.width - icon - 50f * u, 24f * u), toast.Text, text);
                GUI.color = previous;
                y += height + 10f * u;
            }
        }

        static UIIcon ToastIcon(Color color)
        {
            if (color.Equals(UIStyles.Green)) return UIIcon.Check;
            if (color.Equals(UIStyles.Red)) return UIIcon.Cross;
            if (color.Equals(UIStyles.Gold)) return UIIcon.Trophy;
            if (color.Equals(UIStyles.Orange)) return UIIcon.Warning;
            return UIIcon.Info;
        }
    }
}
