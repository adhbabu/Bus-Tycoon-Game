using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Fleet;
using BusTycoon.Routes;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüzde Filo paneli. Filoda yüzü aşkın otobüs olabilir: kartlar her gün baştan kurulmaz. Her kartın
    /// gösterdiği durumun özeti (hat, durum yüzdesi, arıza, açık/kapalı atama listesi, alınabilirlik) tutulur; gün
    /// sonunda yalnız özeti değişen kart yeniden kurulur. Otobüs eklenip çıkınca yalnız o kart eklenir/silinir.
    /// </summary>
    public sealed partial class GameUI
    {
        readonly Dictionary<string, (VisualElement Card, int Signature)> tkFleetCards = new Dictionary<string, (VisualElement, int)>();
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
                    entry = (Ui.El("bt-item bt-bus", null), int.MinValue);
                }
                if (entry.Signature != signature)
                {
                    entry.Card.Clear();
                    BuildFleetCard(entry.Card, bus);
                    entry.Signature = signature;
                }
                tkFleetCards[bus.busId] = entry;
                if (entry.Card.parent != tkFleetList || tkFleetList.IndexOf(entry.Card) != index)
                    tkFleetList.Insert(index, entry.Card);
                Ui.Show(entry.Card, visible);
                index++;
            }
        }

        /// <summary>Kartın gösterdiği her şeyin özeti: değişmediyse kart yeniden kurulmaz.</summary>
        int FleetCardSignature(BusInstance bus)
        {
            var fleet = session.Fleet;
            RouteState route = bus.IsAssigned ? session.Routes.GetRoute(bus.assignedRouteId) : null;
            int signature = bus.Model.Id.GetHashCode();
            signature = signature * 31 + (bus.assignedRouteId?.GetHashCode() ?? 0);
            signature = signature * 31 + Mathf.RoundToInt(bus.condition * 100f);
            signature = signature * 31 + (int)(bus.odometerKm / 100.0);
            signature = signature * 31 + bus.outOfServiceDays;
            signature = signature * 31 + (expandedBusId == bus.busId ? 1 + session.Routes.Routes.Count : 0);
            signature = signature * 31 + (session.Finance.CanAfford(fleet.GetExpressRepairCost(bus)) ? 1 : 0);
            if (route != null && route.HasHistory)
                signature = signature * 31 + Mathf.RoundToInt(route.LastReport.loadFactor * 100f) + (int)route.LastReport.passengers;
            BusModelData upgrade = BestUnlockedUpgrade(bus.Model);
            if (upgrade != null)
                signature = signature * 31 + upgrade.Id.GetHashCode() + (session.Finance.CanAfford(fleet.GetPurchasePrice(upgrade) - fleet.GetResaleValue(bus)) ? 1 : 0);
            return signature;
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

        void BuildFleetCard(VisualElement card, BusInstance bus)
        {
            var fleet = session.Fleet;
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
            if (route != null && route.HasHistory)
                Detail(card, UIIcon.People, $"Doluluk: {Fmt.Pct(route.LastReport.loadFactor)} · hat dün {Fmt.Num(route.LastReport.passengers)} yolcu");
            VisualElement condition = Ui.El("bt-row bt-detail", card);
            Ui.Icon(UIIcon.Wrench, "bt-detail__icon", condition);
            Ui.Text("bt-detail__text", $"Durum: {Fmt.Pct(bus.condition)}", UiWeight.Regular, condition);
            VisualElement bar = Ui.El("bt-bar bt-bus__condition", condition);
            VisualElement fill = Ui.El("bt-bar__fill", bar);
            fill.AddToClassList(bus.condition > 0.6f ? "bt-bar__fill--green" : bus.condition > 0.35f ? "bt-bar__fill--yellow" : "bt-bar__fill--red");
            Ui.Fill(fill, bus.condition);
            Ui.El("bt-grow", condition);
            Ui.Icon(UIIcon.Road, "bt-detail__icon", condition);
            Ui.Text("bt-detail__text", $"{Fmt.Num(bus.odometerKm)} km", UiWeight.Regular, condition);
            if (!bus.IsAvailableToday)
            {
                VisualElement warning = Detail(card, UIIcon.Warning, $"{bus.outOfServiceDays} gün servis dışı");
                warning.AddToClassList("is-warning-text");
            }

            // Ana düğmeler: hattaysa "Garaja çek" + "Hatta ata"; garajdaysa "Revizyon" + "Hatta ata" (turuncu)
            bool expanded = expandedBusId == busId;
            float overhaul = fleet.GetOverhaulCost(bus);
            bool canOverhaul = bus.condition < session.Economy.OverhaulTargetCondition - 0.01f;
            VisualElement main = Ui.El("bt-actions bt-actions--fill", card);
            if (!bus.IsAvailableToday)
            {
                float repair = fleet.GetExpressRepairCost(bus);
                ActionButton(main, $"Acil tamir ({Fmt.Compact(repair)})", () =>
                {
                    var result = session.Fleet.TryExpressRepair(busId);
                    Toast(result.Message, result ? UIStyles.Green : UIStyles.Red);
                    RefreshFleet();
                }, primary: true, enabled: session.Finance.CanAfford(repair));
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
                ActionButton(main, $"Revizyon ({Fmt.Compact(overhaul)})", () => { Overhaul(busId); RefreshFleet(); }, enabled: canOverhaul);
            }
            ActionButton(main, expanded ? "Kapat" : "Hatta ata", () =>
            {
                expandedBusId = expanded ? null : busId;
                RefreshFleet();
            }, primary: !expanded && route == null);

            // İkincil eylemler
            float resale = fleet.GetResaleValue(bus);
            VisualElement secondary = Ui.El("bt-actions bt-actions--fill", card);
            if (route != null) ActionButton(secondary, $"Revizyon {Fmt.Compact(overhaul)}", () => { Overhaul(busId); RefreshFleet(); }, enabled: canOverhaul);
            Button sell = ActionButton(secondary, $"Sat {Fmt.Compact(resale)}", () =>
            {
                string nickname = bus.nickname;
                AskConfirm("Otobüs satışı", $"{nickname} {Fmt.Money(resale)} karşılığında satılacak.", () =>
                {
                    var result = session.Fleet.TrySell(busId);
                    Toast(result.Message, result ? UIStyles.Green : UIStyles.Red);
                });
            });
            sell.AddToClassList("is-danger");

            BusModelData upgrade = BestUnlockedUpgrade(bus.Model);
            if (upgrade != null)
            {
                float net = fleet.GetPurchasePrice(upgrade) - resale;
                Button trade = ActionButton(card, $"Takasla yükselt: {upgrade.DisplayName} (net {Fmt.Compact(net)})", () =>
                {
                    BusModelData target = upgrade;
                    AskConfirm("Takasla yükselt",
                        $"{bus.nickname} satılıp yerine sıfır {target.DisplayName} alınacak ve aynı hatta atanacak. Net maliyet {Fmt.Money(net)}.",
                        () =>
                        {
                            var result = session.Fleet.TryTradeIn(busId, target, out _);
                            Toast(result.Message, result ? UIStyles.Green : UIStyles.Red);
                            goals.Evaluate();
                        });
                }, enabled: session.Finance.CanAfford(net));
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

        static VisualElement Detail(VisualElement parent, UIIcon icon, string text)
        {
            VisualElement row = Ui.El("bt-row bt-detail", parent);
            Ui.Icon(icon, "bt-detail__icon", row);
            Ui.Text("bt-detail__text", text, UiWeight.Regular, row);
            return row;
        }
    }
}
