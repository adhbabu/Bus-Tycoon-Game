using BusTycoon.Data;
using BusTycoon.Routes;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüzde Otobüs Galerisi: üç bölüm (şehir içi, şehirlerarası, servis araçları), sıfır / 2. el, model kartları
    /// (çizim, künye, konfor, fiyat, satın al). Midibüs iki yerde de çalıştığı için iki bölümde de görünür.
    /// </summary>
    public sealed partial class GameUI
    {
        public const int GalleryCity = 0, GalleryIntercity = 1, GalleryService = 2;

        /// <summary>Galerinin açık bölümü; -1: hedef hatta göre seçilir (şehir içi hat → şehir içi).</summary>
        int dealershipSection = -1;

        static bool InGallerySection(BusModelData model, int section) => section switch
        {
            GalleryService => model.IsService,
            GalleryIntercity => !model.IsService && model.ServesIntercity,
            _ => !model.IsService && model.ServesUrban
        };

        int CurrentGallerySection()
        {
            if (dealershipSection >= 0) return dealershipSection;
            return dealershipTargetRoute != null && !dealershipTargetRoute.IsUrban ? GalleryIntercity : GalleryCity;
        }

        int DealershipSignature()
        {
            int signature = session.State.currentDay * 3 + (dealershipUsed ? 1 : 0);
            signature = signature * 7 + CurrentGallerySection();
            signature = signature * 31 + (dealershipTargetRoute?.routeId.GetHashCode() ?? 0);
            float condition = dealershipUsed ? session.ProgressionConfig.StartingBusCondition : 1f;
            foreach (var model in session.Catalog.SortedByTier)
            {
                int owned = 0;
                foreach (var bus in session.Fleet.Buses) if (bus.Model == model) owned++;
                signature = signature * 31 + (session.Progression.IsModelUnlocked(model) ? 1 : 0) +
                            (session.Finance.CanAfford(session.Fleet.GetPurchasePrice(model, condition)) ? 2 : 0) + owned * 4;
            }
            return signature;
        }

        void BuildDealership(VisualElement body)
        {
            SheetHeader("Otobüs Galerisi", "Sıfır ve 2. el otobüsler");

            if (dealershipTargetRoute != null && session.Routes.GetRoute(dealershipTargetRoute.routeId) != null)
            {
                VisualElement note = Ui.El("bt-item bt-note", body);
                VisualElement row = Ui.El("bt-row", note);
                Ui.Text("bt-item__text bt-grow", $"Alınan otobüs {RouteName(dealershipTargetRoute)} hattına atanacak.", UiWeight.Bold, row);
                RouteState route = dealershipTargetRoute;
                ActionButton(row, "Hatta dön", () => OpenRoutePanelNow(route, RouteTab.Buses));
            }

            int section = CurrentGallerySection();
            VisualElement sections = Ui.El("bt-gallery-tabs", body);
            GallerySectionButton(sections, GalleryCity, "location_city", "Şehir içi", section);
            GallerySectionButton(sections, GalleryIntercity, "directions_bus", "Şehirlerarası", section);
            GallerySectionButton(sections, GalleryService, "airport_shuttle", "Servis", section);

            if (section == GalleryService)
            {
                VisualElement note = Ui.El("bt-item bt-note bt-full", body);
                VisualElement row = Ui.El("bt-row", note);
                Ui.Symbol("handshake", "bt-note__icon", row);
                Ui.Text("bt-item__text bt-grow", "Servis araçları yalnız ihale işlerinde çalışır: okul, fabrika ve şirketlerin servis " +
                                                  "sözleşmeleri. Hatlara atanmaz.", UiWeight.Bold, row);
                if (ContractsAvailable) ActionButton(row, "İhaleler", () => OpenPage(Panel.Contracts));
            }

            VisualElement tabs = Ui.El("bt-tabs-inline", body);
            Button fresh = Ui.Button("bt-tab", "Sıfır", () => { dealershipUsed = false; tkSheetDirty = true; }, tabs);
            Button used = Ui.Button("bt-tab", "2. el (durum %60)", () => { dealershipUsed = true; tkSheetDirty = true; }, tabs);
            Ui.Toggle(fresh, "is-active", !dealershipUsed);
            Ui.Toggle(used, "is-active", dealershipUsed);

            BusModelData next = null;
            foreach (var model in session.Catalog.SortedByTier)
                if (InGallerySection(model, section) && !session.Progression.IsModelUnlocked(model)) { next = model; break; }
            if (next != null) Ui.Text("bt-item__reward bt-next", $"Sıradaki: {next.DisplayName} — {session.Progression.GetLockReason(next)}", UiWeight.Bold, body);

            float condition = dealershipUsed ? session.ProgressionConfig.StartingBusCondition : 1f;
            foreach (var model in session.Catalog.SortedByTier)
                if (InGallerySection(model, section)) BuildModelCard(body, model, condition);
        }

        void GallerySectionButton(VisualElement parent, int section, string symbol, string label, int current)
        {
            int count = 0;
            foreach (var model in session.Catalog.SortedByTier) if (InGallerySection(model, section)) count++;
            Button button = Ui.Button("bt-gallery-tab", string.Empty, () =>
            {
                dealershipSection = section;
                tkSheetDirty = true;
                if (tkSheetScroll != null) tkSheetScroll.scrollOffset = Vector2.zero;
            }, parent);
            button.name = "gallery-section-" + section;
            Ui.Symbol(symbol, "bt-gallery-tab__icon", button);
            Ui.Text("bt-gallery-tab__label", $"{label} · {count}", UiWeight.Bold, button);
            Ui.Toggle(button, "is-active", section == current);
        }

        void BuildModelCard(VisualElement body, BusModelData model, float condition)
        {
            bool unlocked = session.Progression.IsModelUnlocked(model);
            float price = session.Fleet.GetPurchasePrice(model, condition);
            int owned = 0;
            foreach (var bus in session.Fleet.Buses) if (bus.Model == model) owned++;

            VisualElement card = Ui.El("bt-item bt-model", body);
            card.name = "model-" + model.Id;
            Ui.Toggle(card, "is-locked", !unlocked);
            // Büyük çizim (önden-yandan, koyu sahnede): galeride otobüs kartın en üstünde
            VisualElement stage = Ui.El("bt-model__stage", card);
            VisualElement art = Ui.El("bt-model__art", stage);
            Sprite sprite = UIBusArt.Sprite(model);
            if (sprite != null) art.style.backgroundImage = new StyleBackground(sprite);
            if (!unlocked) Ui.Text("bt-model__lock", "KİLİTLİ", UiWeight.Black, stage);
            else if (owned > 0) Ui.Text("bt-model__lock is-owned", $"FİLODA {owned}", UiWeight.Black, stage);
            VisualElement info = Ui.El("bt-col", card);
            Ui.Text("bt-model__name", model.DisplayName, UiWeight.Black, info);
            Ui.Text("bt-detail__text", model.IsService ? $"{model.Manufacturer} · {SegmentName(model.Segment)}"
                : $"{model.Manufacturer} · {SegmentName(model.Segment)} · kademe {model.Tier + 1}", UiWeight.Regular, info);
            Detail(info, UIIcon.Seat, $"{model.Seats} {(model.Segment == BusSegment.CityBus ? "yolcu" : "koltuk")} · {model.AverageSpeedKmh:0} km/s");
            if (model.IsService) Detail(info, UIIcon.Road, "Yalnız ihale işleri (servis sözleşmeleri)");
            else if (session.State.vehicleClasses)
            {
                // Araç sınıfı: hangi hatlarda çalışır
                string where = model.Category switch
                {
                    BusCategory.Dolmus => "Şehir içi hatlar (dolmuş)",
                    BusCategory.CityBus => "Şehir içi hatlar",
                    BusCategory.Midibus => "Şehir içi ve şehirlerarası",
                    _ => "Şehirlerarası hatlar"
                };
                Detail(info, UIIcon.Road, where);
            }
            else Detail(info, UIIcon.Road, $"Rahat menzil {Fmt.Km(model.ComfortableRangeKm)}");

            VisualElement comfort = Ui.El("bt-row bt-detail", card);
            Ui.Icon(UIIcon.Star, "bt-detail__icon", comfort);
            Ui.Text("bt-detail__text bt-nowrap", "Konfor", UiWeight.Regular, comfort);
            VisualElement bar = Ui.El("bt-bar bt-bus__condition", comfort);
            Ui.Fill(Ui.El("bt-bar__fill bt-bar__fill--gold", bar), model.Comfort);
            VisualElement specs = Ui.El("bt-row bt-detail", card);
            if (!model.IsService)
            {
                Ui.Icon(UIIcon.Ticket, "bt-detail__icon", specs);
                Ui.Text("bt-detail__text bt-spec", $"Bilet ×{model.FareMultiplier:0.00}", UiWeight.Regular, specs);
            }
            Ui.Icon(UIIcon.Fuel, "bt-detail__icon", specs);
            Ui.Text("bt-detail__text", $"{model.FuelLitersPer100Km:0} L/100 km", UiWeight.Regular, specs);
            Ui.Text("bt-item__text", model.Description, UiWeight.Regular, card);
            if (owned > 0) Ui.Text("bt-owned", $"Filonuzda {owned} adet", UiWeight.Bold, card);

            VisualElement footer = Ui.El("bt-actions", card);
            if (unlocked)
            {
                Ui.Text("bt-model__price bt-grow", Fmt.Money(price), UiWeight.Black, footer);
                bool affordable = session.Finance.CanAfford(price);
                BusModelData target = model;
                RouteState route = dealershipTargetRoute;
                float buyCondition = condition;
                ActionButton(footer, affordable ? "SATIN AL" : "Bakiye yetersiz", () =>
                {
                    BuyBus(target, buyCondition, route);
                    tkSheetDirty = true;
                }, primary: true, enabled: affordable);
            }
            else
            {
                Ui.Text("bt-item__text is-warning-text bt-grow", $"Kilitli: {session.Progression.GetLockReason(model)}", UiWeight.Bold, footer);
            }
        }
    }
}
