using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Stations;
using UnityEngine;

namespace BusTycoon.Game
{
    // Durak ekranı: haritada bir durağa tıklayınca içine girilir (StationView). Solda durak kartı (şehir, büyüklük,
    // popülerlik yıldızları, yolcu, konfor), sağda geliştirme mağazası; "Buradan hat aç" ve haritaya dönüş.
    public sealed partial class GameUI
    {
        static readonly Dictionary<string, UIIcon> UpgradeIcons = new Dictionary<string, UIIcon>
        {
            [StationCatalog.Seats] = UIIcon.Seat,
            [StationCatalog.Plants] = UIIcon.Plant,
            [StationCatalog.Lights] = UIIcon.Bulb,
            [StationCatalog.Kiosk] = UIIcon.Bagel,
            [StationCatalog.Board] = UIIcon.Screen,
            [StationCatalog.Wifi] = UIIcon.Wifi,
            [StationCatalog.Ticket] = UIIcon.Ticket,
            [StationCatalog.Cafe] = UIIcon.Coffee,
            [StationCatalog.Lockers] = UIIcon.Luggage,
            [StationCatalog.Taxi] = UIIcon.Taxi,
            [StationCatalog.Canopy] = UIIcon.Umbrella,
            [StationCatalog.Kids] = UIIcon.Teddy,
            [StationCatalog.Shop] = UIIcon.Shopping,
            [StationCatalog.Vip] = UIIcon.Sofa,
            [StationCatalog.Fountain] = UIIcon.Fountain,
            [StationCatalog.Clock] = UIIcon.MantelClock,
        };

        readonly List<StationUpgrade> sortedUpgrades = new List<StationUpgrade>();
        Vector2 stationScroll;

        StationView Stations => StationView.Instance;

        /// <summary>Durak arayüzü gösteriliyor mu (içeride ya da çıkış perdesi kapanırken).</summary>
        bool StationUi => mode == Mode.Playing && Stations != null && Stations.Scene != null &&
                          (Stations.State == StationView.Phase.Inside || Stations.State == StationView.Phase.Leaving);

        /// <summary>Durağa dalış sürerken harita arayüzü sakinleşir (etiket, ipucu yok).</summary>
        bool StationBusy => Stations != null && Stations.IsActive;

        static UIIcon UpgradeIcon(StationUpgrade upgrade) => UpgradeIcons.TryGetValue(upgrade.Id, out UIIcon icon) ? icon : UIIcon.Sparkles;

        void EnterStationNow(CityData city)
        {
            if (Stations == null || city == null || mode != Mode.Playing) return;
            ClosePanel();
            hoveredCity = null;
            stationScroll = Vector2.zero;
            Stations.Enter(city);
        }

        void ExitStationNow()
        {
            if (Stations != null) Stations.Exit();
        }

        /// <summary>Durağın şehrinden yeni hat: durak kapanır, planlayıcı bu şehirle açılır.</summary>
        void PlanFromStationNow(CityData city)
        {
            if (Stations != null) Stations.ExitImmediate();
            OpenPlannerNow(city);
        }

        Rect StationCardRect
        {
            get
            {
                float width = (compact ? 300f : 360f) * u;
                float top = SafeTop + TopBarHeight + Margin + 52f * u;
                float height = Mathf.Min((compact ? 330f : 420f) * u, ContentBottom - Margin - top);
                return new Rect(SafeLeft + Margin, top, width, height);
            }
        }

        void DrawStation()
        {
            StationView view = Stations;
            CityData city = view.City;
            if (city == null) return;

            // Haritaya dönüş
            var back = new Rect(SafeLeft + Margin, SafeTop + TopBarHeight + Margin, (compact ? 170f : 200f) * u, 42f * u);
            uiRects.Add(back);
            GUI.Box(back, GUIContent.none, s.Panel); // açık renkli sahnede de okunsun
            if (GUI.Button(back, "     Haritaya dön", s.Button)) Defer(ExitStationNow);
            UIIcons.Draw(new Rect(back.x + 12f * u, back.center.y - 11f * u, 22f * u, 22f * u), UIIcon.Routes);

            DrawStationCard(city);
            DrawUpgradePanel(city);

            // İpucu
            string hint = mobile ? "Kaydır: döndür · İki parmak: yakınlaştır" : "Sürükle: döndür · Tekerlek: yakınlaştır · Esc: haritaya dön";
            var hintRect = new Rect(StationCardRect.xMax + Margin, ContentBottom - Margin - 26f * u, PanelRect.x - StationCardRect.xMax - 2f * Margin, 24f * u);
            if (hintRect.width > 120f * u) GUI.Label(hintRect, hint, s.MapLabelShadow);

            // Sahne, iki panelin arasındaki alanın ortasında görünsün
            float center = (StationCardRect.xMax + PanelRect.x) / 2f;
            view.SetViewportShift(1f - 2f * center / Mathf.Max(1f, W));
        }

        void DrawStationCard(CityData city)
        {
            StationSystem stations = session.Stations;
            StationTier tier = StationCatalog.TierOf(city);
            StationPopularity popularity = stations.Popularity(city);
            bool unlocked = session.Progression.IsRegionUnlocked(city.Region);
            bool operating = stations.IsOperating(city);

            Rect rect = StationCardRect;
            uiRects.Add(rect);
            GUILayout.BeginArea(rect, s.Panel);
            GUILayout.BeginHorizontal();
            LayoutCityIcon(city, 46f, 2f);
            Sp(8f);
            GUILayout.BeginVertical();
            GUILayout.Label(city.DisplayName, s.H1);
            GUILayout.BeginHorizontal();
            LayoutIcon(UIIcons.Flag(city.Region.Id), 16f, 3f);
            Sp(4f);
            GUILayout.Label($"{StationCatalog.TierName(tier)} · {city.Region.DisplayName}", s.Dim);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            string famous = UICityIcons.Description(city);
            if (!compact && !string.IsNullOrEmpty(famous)) ColorLabel(famous, UIStyles.Gold, s.Small);
            Divider();

            // Popülerlik: yıldızlar ve bileşenleri
            GUILayout.BeginHorizontal();
            GUILayout.Label("POPÜLERLİK", s.StatLabel);
            GUILayout.FlexibleSpace();
            Stars(popularity.Stars, 18f);
            Sp(6f);
            ColorLabel($"{popularity.Score:0}", UIStyles.Gold, s.Bold);
            GUILayout.EndHorizontal();
            if (!compact) // dar ekranda kart kısa kalsın: bileşenler yalnız geniş ekranda
            {
                Sp(2f);
                PopularityBar("Şehrin ünü", popularity.Fame, UIStyles.Blue);
                PopularityBar("Yolcu trafiği", popularity.Traffic, UIStyles.Green);
                PopularityBar("Konfor", popularity.Comfort, UIStyles.Orange);
            }
            Divider();

            int built = stations.UpgradeCount(city);
            float bonus = stations.DemandBonus(city);
            KeyValue("Nüfus", Fmt.Population(city.Population), null, UIIcon.Building);
            KeyValue("Hatlarınız", $"{stations.RouteCount(city)}", null, UIIcon.Routes);
            KeyValue("Günlük yolcu", $"{stations.DailyPassengers(city):N0}", null, UIIcon.People);
            KeyValue("Geliştirmeler", $"{built} / {StationCatalog.AvailableCount(tier)}", null, UIIcon.Sparkles);
            KeyValue("Talep bonusu", bonus > 0f ? $"+%{bonus * 100f:0.#}" : "—", bonus > 0f ? UIStyles.Green : UIStyles.DimText, UIIcon.ChartUp);
            Sp(6f);

            if (!unlocked)
                GUILayout.Label("Kilitli bölge: Bölgeler (B) panelinden lisans alın.", s.WrapDim);
            else if (!operating)
                ColorLabel("Bu şehre hat açınca durağı geliştirebilirsiniz.", UIStyles.Orange, s.Wrap);
            GUILayout.FlexibleSpace();
            if (EnabledButton(unlocked, "Buradan hat aç", s.Primary, Ht(40f))) Defer(() => PlanFromStationNow(city));
            GUILayout.EndArea();
        }

        void PopularityBar(string label, float value01, Color color)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, s.Small, Wd(compact ? 92f : 104f));
            LayoutBar(value01, color, 7f, -1f, 6f);
            GUILayout.EndHorizontal();
        }

        /// <summary>0..5 yıldız (yarım yıldız kırpılarak çizilir; boş yıldızlar soluk).</summary>
        void Stars(float stars, float size)
        {
            Rect rect = GUILayoutUtility.GetRect(5f * (size + 2f) * u, (size + 4f) * u, Wd(5f * (size + 2f)), Ht(size + 4f));
            if (Event.current.type != EventType.Repaint) return;
            for (int i = 0; i < 5; i++)
            {
                var star = new Rect(rect.x + i * (size + 2f) * u, rect.y + 3f * u, size * u, size * u);
                Color previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.22f);
                UIIcons.Draw(star, UIIcon.Star);
                GUI.color = previous;
                float fill = Mathf.Clamp01(stars - i);
                if (fill > 0.01f) UIIcons.Draw(star, UIIcon.Star, fill);
            }
        }

        void DrawUpgradePanel(CityData city)
        {
            StationSystem stations = session.Stations;
            StationTier tier = StationCatalog.TierOf(city);
            Rect rect = PanelRect;
            uiRects.Add(rect);
            GUILayout.BeginArea(rect, s.Panel);
            GUILayout.Label("Durak Geliştirmeleri", s.H1);
            GUILayout.Label($"{stations.UpgradeCount(city)} / {StationCatalog.AvailableCount(tier)} yapıldı · her konfor puanı bu duraktaki hatların talebini %{StationSystem.DemandBonusPerComfort * 100f:0.#} artırır", s.WrapDim);
            Divider();

            // Sıra: yapılabilecekler, yapılanlar, daha büyük durak isteyenler
            sortedUpgrades.Clear();
            foreach (var upgrade in StationCatalog.Upgrades)
                if (StationCatalog.IsAvailable(upgrade, tier) && !stations.Has(city, upgrade)) sortedUpgrades.Add(upgrade);
            foreach (var upgrade in StationCatalog.Upgrades)
                if (stations.Has(city, upgrade)) sortedUpgrades.Add(upgrade);
            foreach (var upgrade in StationCatalog.Upgrades)
                if (!StationCatalog.IsAvailable(upgrade, tier)) sortedUpgrades.Add(upgrade);

            stationScroll = BeginScroll(stationScroll);
            foreach (var upgrade in sortedUpgrades) UpgradeCard(city, upgrade, tier);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void UpgradeCard(CityData city, StationUpgrade upgrade, StationTier tier)
        {
            StationSystem stations = session.Stations;
            bool built = stations.Has(city, upgrade);
            bool available = StationCatalog.IsAvailable(upgrade, tier);
            float cost = stations.Cost(city, upgrade);

            GUILayout.BeginVertical(s.Card);
            GUILayout.BeginHorizontal();
            Color previous = GUI.color;
            if (!available) GUI.color = new Color(1f, 1f, 1f, 0.45f);
            LayoutIcon(UpgradeIcon(upgrade), 34f, 2f);
            GUI.color = previous;
            Sp(8f);
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            ColorLabel(upgrade.Name, available ? UIStyles.Text : UIStyles.DimText, s.CardTitle);
            GUILayout.FlexibleSpace();
            if (built) Badge("Yapıldı", s.BadgeSuccess);
            else if (!available) Badge(StationCatalog.TierName(upgrade.MinTier), s.BadgeInfo);
            GUILayout.EndHorizontal();
            GUILayout.Label(upgrade.Description, s.WrapDim);
            ColorLabel($"Konfor +{upgrade.Comfort} · talep +%{upgrade.Comfort * StationSystem.DemandBonusPerComfort * 100f:0.#}", built ? UIStyles.DimText : UIStyles.Green, s.Small);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            if (!built && available)
            {
                var check = stations.CanBuild(city, upgrade);
                Sp(4f);
                if (EnabledButton(check.Success, $"Yap · {Fmt.Money(cost)}", s.Primary, Ht(36f)))
                    Defer(() => BuildUpgradeNow(city, upgrade));
                if (!check.Success && !string.IsNullOrEmpty(check.Message)) ColorLabel(check.Message, UIStyles.Orange, s.Small);
            }
            GUILayout.EndVertical();
        }

        void BuildUpgradeNow(CityData city, StationUpgrade upgrade)
        {
            var result = session.Stations.TryBuild(city, upgrade);
            Toast(result.Success ? $"{upgrade.Name} yapıldı! {city.DisplayName} durağı güzelleşti." : result.Message, result.Success ? UIStyles.Green : UIStyles.Red,
                result.Success ? Sfx.Coin : Sfx.Error);
            if (!result.Success) return;
            MarkDataDirty();
            goals.Evaluate();
        }

        /// <summary>Durağa girme/çıkma perdesi: koyu, yumuşak geçiş.</summary>
        void DrawStationFade()
        {
            if (Stations == null || Stations.Fade <= 0.001f || Event.current.type != EventType.Repaint) return;
            DrawRect(new Rect(0f, 0f, W, H), new Color(0.07f, 0.07f, 0.09f, Stations.Fade));
        }

        /// <summary>Durak içinde girdi: sürükleme döndürür, tekerlek yakınlaştırır.</summary>
        bool HandleStationInput(Event e)
        {
            if (!StationUi) return false;
            Vector2 mouse = e.mousePosition;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (IsOverUI(mouse)) return true;
                    pointerOnMap = true;
                    lastPointerPos = mouse;
                    if (textFieldFocused) GUIUtility.keyboardControl = 0;
                    e.Use();
                    return true;
                case EventType.MouseDrag:
                    if (!pointerOnMap) return true;
                    if (!multiTouch) Stations.Orbit(mouse - lastPointerPos);
                    lastPointerPos = mouse;
                    e.Use();
                    return true;
                case EventType.MouseUp:
                    if (pointerOnMap) e.Use();
                    pointerOnMap = false;
                    return true;
                case EventType.ScrollWheel:
                    if (IsOverUI(mouse)) return true;
                    Stations.Zoom(Mathf.Clamp(e.delta.y, -6f, 6f) * 0.5f);
                    e.Use();
                    return true;
            }
            return true;
        }
    }
}
