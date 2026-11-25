using BusTycoon.Fleet;
using BusTycoon.Routes;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Bekleme süreleri ve reklamlar (arayüz tarafı): hat kurulumu ve araç teslimi bitince bildirim; "Reklam izle, hemen
    /// bitir" düğmeleri (reklam varsa); seviye atlayınca geçiş reklamı (en sık 3 dakikada bir); test reklamının örtüsü.
    /// </summary>
    public sealed partial class GameUI
    {
        TestAdProvider testAds;
        VisualElement tkAdOverlay;
        Label tkAdOverlayText;

        void InitAds()
        {
            // Android mağaza derlemesinde AdMob (G4); geliştirmede ?reklam=test ile sahte reklam; web'de reklam yok
            if (LaunchOptions.Get("reklam") == "test")
            {
                testAds = new TestAdProvider();
                AdService.SetProvider(testAds);
            }
        }

        void BuildToolkitAdOverlay()
        {
            tkAdOverlay = Ui.El("bt-adoverlay", toolkit.Root);
            tkAdOverlay.name = "ad-overlay";
            Ui.Text("bt-caps", "REKLAM (TEST)", UiWeight.Black, tkAdOverlay);
            tkAdOverlayText = Ui.Text("bt-adoverlay__text", "", UiWeight.Black, tkAdOverlay);
            tkAdOverlay.style.display = DisplayStyle.None;
        }

        void UpdateAds(float dt)
        {
            testAds?.Tick(dt);
            if (tkAdOverlay == null) return;
            bool show = testAds != null && testAds.Remaining > 0f;
            Ui.Show(tkAdOverlay, show);
            if (show) Ui.SetText(tkAdOverlayText, $"{Mathf.CeilToInt(testAds.Remaining)}");
        }

        /// <summary>Hat kurulumunu ödüllü reklamla bitirir.</summary>
        void WatchAdToFinishSetup(RouteState route)
        {
            string id = route.routeId;
            if (!AdService.ShowRewarded(() =>
                {
                    if (session.Routes.FinishSetupNow(id)) tkSheetDirty = true;
                }))
                Toast("Reklam şu an hazır değil, birazdan tekrar dene.", UIStyles.Orange);
        }

        /// <summary>Araç teslimini ödüllü reklamla bitirir.</summary>
        void WatchAdToDeliver(BusInstance bus)
        {
            string id = bus.busId;
            if (!AdService.ShowRewarded(() =>
                {
                    if (session.Fleet.FinishDeliveryNow(id)) tkSheetDirty = true;
                }))
                Toast("Reklam şu an hazır değil, birazdan tekrar dene.", UIStyles.Orange);
        }

        void OnSetupFinished(RouteState route)
        {
            int buses = session.Fleet.CountBusesOnRoute(route.routeId);
            Toast(buses > 0 ? $"{RouteName(route)} hattı kuruldu: araçlar yola çıktı!" : $"{RouteName(route)} hattı kuruldu: araç ata!",
                buses > 0 ? UIStyles.Green : UIStyles.Orange, Sfx.Horn);
            MarkDataDirty();
            cityRoutesSignature = int.MinValue;
        }

        void OnBusDelivered(BusInstance bus)
        {
            Toast($"{bus.nickname} teslim edildi.", UIStyles.Green);
            MarkDataDirty();
            cityRoutesSignature = int.MinValue;
        }

        /// <summary>Kurulum kartı (hat panelinin başı): kalan gün, reklamla hemen bitirme.</summary>
        void BuildSetupCard(VisualElement body, RouteState route)
        {
            VisualElement card = Ui.El("bt-item bt-wait", body);
            VisualElement row = Ui.El("bt-row", card);
            Ui.Symbol("schedule", "bt-wait__icon", row);
            VisualElement text = Ui.El("bt-col bt-grow", row);
            Ui.Text("bt-wait__title", $"Hat kuruluyor · {route.setupDaysLeft} gün kaldı", UiWeight.Black, text);
            Ui.Text("bt-item__text", "Duraklar ve ruhsat hazırlanıyor; kurulum bitince araçlar sefere başlar.", UiWeight.Regular, text);
            if (AdService.Enabled)
            {
                Button watch = ActionButton(card, "▶  Reklam izle, hemen aç", () => WatchAdToFinishSetup(route), primary: true);
                watch.name = "ad-route";
                watch.AddToClassList("bt-btn--block");
            }
        }
    }
}
