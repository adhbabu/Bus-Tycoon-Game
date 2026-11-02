using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Fleet;
using BusTycoon.Routes;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüzde Filo paneli. Filoda yüzü aşkın otobüs olabilir: kartlar her gün baştan kurulmaz.
    /// Kartın yapısı (model, hat, arıza, açık atama listesi, takas modeli) bir imzada tutulur; yalnız bu değişince kart
    /// yeniden kurulur. Her gün değişen sayılar (durum, km, doluluk, satış/revizyon/takas tutarları ve alınabilirlik)
    /// kartın yerinde yazılır. Otobüs eklenip çıkınca yalnız o kart eklenir/silinir.
    /// </summary>
    public sealed partial class GameUI
    {
        /// <summary>Bir filo kartı: yapı imzası ve her gün yerinde güncellenen öğeler.</summary>
        sealed class FleetCardView
        {
            public VisualElement Card;
            public int Signature = int.MinValue;
            public Label Condition, Km, Load, OutOfService;
            public VisualElement ConditionFill;
            public Button Repair, Overhaul, SecondaryOverhaul, Sell, Trade;
            public BusModelData Upgrade;
        }

        readonly Dictionary<string, FleetCardView> tkFleetCards = new Dictionary<string, FleetCardView>();
        readonly List<string> tkFleetStale = new List<string>();
        VisualElement tkFleetList, tkFleetMaintenance;
        Label tkFleetEmpty;
        Button tkFleetAll, tkFleetIdle;

        /// <summary>Filo panelinin iskeleti (sekmeler, bakım asistanı, liste); kartlar <see cref="RefreshFleet"/>'te.</summary>
        void BuildFleet(VisualElement body)
        {
            tkFleetCards.Clear();
            VisualElement tabs = Ui.El("bt-tabs-inline", body);
            tkFleetAll = Ui.Button("bt-tab", "Tümü", () => { fleetIdleOnly = false; RefreshFleet(); }, tabs);
            tkFleetIdle = Ui.Button("bt-tab", "Garajdakiler", () => { fleetIdleOnly = true; RefreshFleet(); }, tabs);
            Ui.El("bt-grow", tabs).pickingMode = PickingMode.Ignore;
            ActionButton(tabs, "Galeri", () => OpenDealershipNow(null), primary: true);
            tkFleetMaintenance = Ui.El("bt-item", body);
            tkFleetEmpty = Ui.Text("bt-empty", "Filonuz boş. Galeriden otobüs alın.", UiWeight.Regular, body);
            tkFleetList = Ui.El("bt-list", body);
            RefreshFleet();
        }

        /// <summary>Başlık, bakım asistanı ve değişen kartlar (gün sonu, filo/hat değişikliği, tıklama).</summary>
        void RefreshFleet()
        {
            var fleet = session.Fleet;
            int idle = 0;
            double value = 0;
            foreach (var bus in fleet.Buses)
            {
                if (!bus.IsAssigned) idle++;
                value += fleet.GetResaleValue(bus);
            }
            SheetHeader("Filo / Garaj", $"{fleet.Buses.Count} otobüs · {idle} garajda · filo değeri {Fmt.Money(value)}");
            Ui.SetText(tkFleetIdle, $"Garajdakiler ({idle})");
            Ui.Toggle(tkFleetAll, "is-active", !fleetIdleOnly);
            Ui.Toggle(tkFleetIdle, "is-active", fleetIdleOnly);
            BuildMaintenanceCard(tkFleetMaintenance);
            Ui.Show(tkFleetEmpty, fleet.Buses.Count == 0);

            // Kartlar: filoda olmayanlar silinir, değişenler yeniden kurulur, sıra filo sırasıdır
            tkFleetStale.Clear();
            foreach (var pair in tkFleetCards)
                if (fleet.GetBus(pair.Key) == null) tkFleetStale.Add(pair.Key);
            foreach (string id in tkFleetStale)
            {
                tkFleetCards[id].Card.RemoveFromHierarchy();
                tkFleetCards.Remove(id);
            }
            int index = 0;
            foreach (var bus in fleet.Buses)
            {
                if (bus.Model == null) continue;
                bool visible = !fleetIdleOnly || !bus.IsAssigned;
                int signature = FleetCardSignature(bus);
                if (!tkFleetCards.TryGetValue(bus.busId, out var entry))
                {
                    entry = new FleetCardView { Card = Ui.El("bt-item bt-bus", null) };
                    tkFleetCards[bus.busId] = entry;
                }
                if (entry.Signature != signature)
                {
                    entry.Card.Clear();
                    BuildFleetCard(entry, bus);
                    entry.Signature = signature;
                }
                UpdateFleetCardValues(entry, bus);
                if (entry.Card.parent != tkFleetList || tkFleetList.IndexOf(entry.Card) != index)
                    tkFleetList.Insert(index, entry.Card);
                Ui.Show(entry.Card, visible);
                index++;
            }
        }

        /// <summary>Kartın yapısının özeti (her gün değişen sayılar hariç): değişmediyse kart yeniden kurulmaz.</summary>
        int FleetCardSignature(BusInstance bus)
        {
            RouteState route = bus.IsAssigned ? session.Routes.GetRoute(bus.assignedRouteId) : null;
            int signature = bus.Model.Id.GetHashCode();
            signature = signature * 31 + (bus.assignedRouteId?.GetHashCode() ?? 0);
            signature = signature * 31 + (bus.IsAvailableToday ? 1 : 0);
            signature = signature * 31 + (route != null && route.HasHistory ? 1 : 0);
            signature = signature * 31 + (expandedBusId == bus.busId ? 1 + session.Routes.Routes.Count : 0);
            if (expandedBusId == bus.busId) // açık atama listesinde hatların otobüs sayıları yazılı
                foreach (var candidate in session.Routes.Routes) signature = signature * 31 + session.Fleet.CountBusesOnRoute(candidate.routeId);
            BusModelData upgrade = BestUnlockedUpgrade(bus.Model);
            signature = signature * 31 + (upgrade != null ? upgrade.Id.GetHashCode() : 0);
            return signature;
        }

        /// <summary>Her gün değişen sayılar: yalnız metni ya da durumu değişen öğeye yazılır.</summary>
        void UpdateFleetCardValues(FleetCardView view, BusInstance bus)
        {
            var fleet = session.Fleet;
            RouteState route = bus.IsAssigned ? session.Routes.GetRoute(bus.assignedRouteId) : null;
            Ui.SetText(view.Condition, $"Durum: {Fmt.Pct(bus.condition)}");
            Ui.Fill(view.ConditionFill, bus.condition);
            Ui.Toggle(view.ConditionFill, "bt-bar__fill--green", bus.condition > 0.6f);
            Ui.Toggle(view.ConditionFill, "bt-bar__fill--yellow", bus.condition <= 0.6f && bus.condition > 0.35f);
            Ui.Toggle(view.ConditionFill, "bt-bar__fill--red", bus.condition <= 0.35f);
            Ui.SetText(view.Km, $"{Fmt.Num(bus.odometerKm)} km");
            if (view.Load != null && route != null && route.HasHistory)
                Ui.SetText(view.Load, $"Doluluk: {Fmt.Pct(route.LastReport.loadFactor)} · hat dün {Fmt.Num(route.LastReport.passengers)} yolcu");
            if (view.OutOfService != null) Ui.SetText(view.OutOfService, $"{bus.outOfServiceDays} gün servis dışı");

            if (view.Repair != null)
            {
                float repair = fleet.GetExpressRepairCost(bus);
                Ui.SetText(view.Repair, $"Acil tamir ({Fmt.Compact(repair)})");
                SetEnabled(view.Repair, session.Finance.CanAfford(repair));
            }
            float overhaul = fleet.GetOverhaulCost(bus);
            bool canOverhaul = bus.condition < session.Economy.OverhaulTargetCondition - 0.01f;
            if (view.Overhaul != null)
            {
                Ui.SetText(view.Overhaul, $"Revizyon ({Fmt.Compact(overhaul)})");
                SetEnabled(view.Overhaul, canOverhaul);
            }
            if (view.SecondaryOverhaul != null)
            {
                Ui.SetText(view.SecondaryOverhaul, $"Revizyon {Fmt.Compact(overhaul)}");
                SetEnabled(view.SecondaryOverhaul, canOverhaul);
            }
            float resale = fleet.GetResaleValue(bus);
            Ui.SetText(view.Sell, $"Sat {Fmt.Compact(resale)}");
            if (view.Trade != null)
            {
                float net = fleet.GetPurchasePrice(view.Upgrade) - resale;
                Ui.SetText(view.Trade, $"Takasla yükselt: {view.Upgrade.DisplayName} (net {Fmt.Compact(net)})");
                SetEnabled(view.Trade, session.Finance.CanAfford(net));
            }
        }

        static void SetEnabled(VisualElement element, bool enabled)
        {
            if (element.enabledSelf != enabled) element.SetEnabled(enabled);
        }

        void BuildMaintenanceCard(VisualElement card)
        {
            card.Clear();
            MaintenanceAssistant assistant = session.Maintenance;
            bool on = assistant.Enabled;
            VisualElement row = Ui.El("bt-row", card);
            Ui.Icon(UIIcon.Wrench, "bt-item__icon", row);
            VisualElement text = Ui.El("bt-col bt-grow", row);
            Ui.Text("bt-item__title", "Bakım asistanı", UiWeight.Bold, text);
            Ui.Text("bt-item__text", $"Durum {Fmt.Pct(session.Economy.AutoOverhaulBelowCondition)} altına inen otobüsü revizyona sokar, arızalananı hemen tamir edip hattına geri gönderir.", UiWeight.Regular, text);
            Button toggle = ActionButton(row, on ? "AÇIK" : "KAPALI", () =>
            {
                SetAutoMaintenance(!on);
                RefreshFleet();
            }, primary: on);
            toggle.AddToClassList("bt-btn--toggle");
            int pending = assistant.PendingWork(out double cost);
            Button run = ActionButton(card, pending > 0 ? $"Hepsini onar ve hatlara gönder ({Fmt.Compact((float)cost)})" : "Bütün otobüsler hatta ve bakımlı", () =>
            {
                MaintenanceReport report = session.Maintenance.RunNow();
                if (report.IsEmpty && report.Postponed == 0) Toast("Yapılacak bakım yok.", UIStyles.Blue);
                RefreshFleet();
            }, enabled: pending > 0);
            run.AddToClassList("bt-btn--block");
        }

        void BuildFleetCard(FleetCardView view, BusInstance bus)
        {
            VisualElement card = view.Card;
            view.Load = view.OutOfService = null;
            view.Repair = view.Overhaul = view.SecondaryOverhaul = view.Trade = null;
            RouteState route = bus.IsAssigned ? session.Routes.GetRoute(bus.assignedRouteId) : null;
            string busId = bus.busId;
            if (route != null) card.style.borderLeftColor = world.RouteIdentityColor(route.routeId);
            Ui.Toggle(card, "bt-route", route != null); // hat rengi şeridi

            VisualElement header = Ui.El("bt-row", card);
            VisualElement art = Ui.El("bt-bus__art", header);
            Sprite sprite = UIBusArt.Sprite(bus.Model);
            if (sprite != null) art.style.backgroundImage = new StyleBackground(sprite);
            Ui.Text("bt-item__title bt-grow", bus.nickname, UiWeight.Bold, header);
            if (!bus.IsAvailableToday) Ui.Text("bt-pill is-danger", "Arızalı", UiWeight.Bold, header);
            else if (route != null) Ui.Text("bt-pill is-success", "Yolda", UiWeight.Bold, header);
            else Ui.Text("bt-pill is-warning", "Garajda", UiWeight.Bold, header);

            Detail(card, UIIcon.Pin, route != null ? $"Hat: {RouteName(route)}" : "Hat: atanmadı (garajda sigorta ödüyor)");
            Detail(card, UIIcon.Seat, $"{bus.Model.DisplayName} · {SegmentName(bus.Model.Segment)} · {bus.Model.Seats} koltuk");
            // Sayılar boş kurulur; UpdateFleetCardValues yazar (her gün yalnız onlar değişir)
            if (route != null && route.HasHistory)
                view.Load = DetailLabel(Detail(card, UIIcon.People, string.Empty));
            VisualElement condition = Ui.El("bt-row bt-detail", card);
            Ui.Icon(UIIcon.Wrench, "bt-detail__icon", condition);
            view.Condition = Ui.Text("bt-detail__text", string.Empty, UiWeight.Regular, condition);
            VisualElement bar = Ui.El("bt-bar bt-bus__condition", condition);
            view.ConditionFill = Ui.El("bt-bar__fill", bar);
            Ui.El("bt-grow", condition);
            Ui.Icon(UIIcon.Road, "bt-detail__icon", condition);
            view.Km = Ui.Text("bt-detail__text", string.Empty, UiWeight.Regular, condition);
            if (!bus.IsAvailableToday)
            {
                VisualElement warning = Detail(card, UIIcon.Warning, string.Empty);
                warning.AddToClassList("is-warning-text");
                view.OutOfService = DetailLabel(warning);
            }

            // Ana düğmeler: hattaysa "Garaja çek" + "Hatta ata"; garajdaysa "Revizyon" + "Hatta ata" (turuncu).
            // Tutarlar tıklama anında hesaplanır (yazılar her gün yerinde güncellenir).
            bool expanded = expandedBusId == busId;
            VisualElement main = Ui.El("bt-actions bt-actions--fill", card);
            if (!bus.IsAvailableToday)
            {
                view.Repair = ActionButton(main, string.Empty, () =>
                {
                    var result = session.Fleet.TryExpressRepair(busId);
                    Toast(result.Message, result ? UIStyles.Green : UIStyles.Red);
                    RefreshFleet();
                }, primary: true);
            }
            else if (route != null)
            {
                ActionButton(main, "Garaja çek", () =>
                {
                    session.Routes.TryAssignBus(busId, null);
                    expandedBusId = null;
                    Toast("Otobüs garaja çekildi.", UIStyles.Blue);
                    RefreshFleet();
                });
            }
            else
            {
                view.Overhaul = ActionButton(main, string.Empty, () => { Overhaul(busId); RefreshFleet(); });
            }
            ActionButton(main, expanded ? "Kapat" : "Hatta ata", () =>
            {
                expandedBusId = expanded ? null : busId;
                RefreshFleet();
            }, primary: !expanded && route == null);

            // İkincil eylemler
            VisualElement secondary = Ui.El("bt-actions bt-actions--fill", card);
            if (route != null) view.SecondaryOverhaul = ActionButton(secondary, string.Empty, () => { Overhaul(busId); RefreshFleet(); });
            Button sell = view.Sell = ActionButton(secondary, string.Empty, () =>
            {
                string nickname = bus.nickname;
                float resale = session.Fleet.GetResaleValue(bus);
                AskConfirm("Otobüs satışı", $"{nickname} {Fmt.Money(resale)} karşılığında satılacak.", () =>
                {
                    var result = session.Fleet.TrySell(busId);
                    Toast(result.Message, result ? UIStyles.Green : UIStyles.Red);
                });
            });
            sell.AddToClassList("is-danger");

            BusModelData upgrade = view.Upgrade = BestUnlockedUpgrade(bus.Model);
            if (upgrade != null)
            {
                Button trade = view.Trade = ActionButton(card, string.Empty, () =>
                {
                    BusModelData target = upgrade;
                    float net = session.Fleet.GetPurchasePrice(target) - session.Fleet.GetResaleValue(bus);
                    AskConfirm("Takasla yükselt",
                        $"{bus.nickname} satılıp yerine sıfır {target.DisplayName} alınacak ve aynı hatta atanacak. Net maliyet {Fmt.Money(net)}.",
                        () =>
                        {
                            var result = session.Fleet.TryTradeIn(busId, target, out _);
                            Toast(result.Message, result ? UIStyles.Green : UIStyles.Red);
                            goals.Evaluate();
                        });
                });
                trade.AddToClassList("bt-btn--block");
            }

            if (!expanded) return;
            Ui.Text("bt-item__text", "Hangi hatta çalışsın?", UiWeight.Bold, card);
            foreach (var candidate in session.Routes.Routes)
            {
                bool current = candidate.routeId == bus.assignedRouteId;
                RouteState target = candidate;
                string label = $"{RouteName(candidate)} · {session.Fleet.CountBusesOnRoute(candidate.routeId)} otobüs" + (current ? " (şu an)" : "");
                Button choice = ActionButton(card, label, () =>
                {
                    session.Routes.TryAssignBus(busId, target.routeId);
                    expandedBusId = null;
                    Toast($"Otobüs {RouteName(target)} hattına atandı.", UIStyles.Green);
                    RefreshFleet();
                }, enabled: !current);
                choice.AddToClassList("bt-btn--choice");
                choice.style.borderLeftColor = world.RouteIdentityColor(candidate.routeId);
            }
            if (session.Routes.Routes.Count == 0) Ui.Text("bt-item__text", "Önce bir hat açın (Yeni Hat).", UiWeight.Regular, card);
        }

        static Label DetailLabel(VisualElement detail)
        {
            foreach (VisualElement child in detail.Children())
                if (child is Label label) return label;
            return null;
        }

        static VisualElement Detail(VisualElement parent, UIIcon icon, string text)
        {
            VisualElement row = Ui.El("bt-row bt-detail", parent);
            Ui.Icon(icon, "bt-detail__icon", row);
            Ui.Text("bt-detail__text", text, UiWeight.Regular, row);
            return row;
        }
    }
}
