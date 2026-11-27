using System.Collections.Generic;
using BusTycoon.Contracts;
using BusTycoon.Data;
using BusTycoon.Fleet;
using BusTycoon.Urban;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// İhaleler sayfası (yeni arayüz): müşteri memnuniyeti, süren işler (araç atama, memnuniyet, kalan gün), gelen teklifler
    /// (kabul et / daha fazla iste / reddet) ve biten işler. Şehir haritasında müşteri ile mahalle arasında servis araçları
    /// gidip gelir (<see cref="SyncCityRoutes"/>), müşterinin adı haritada yazılır.
    /// </summary>
    public sealed partial class GameUI
    {
        static readonly Color ContractLineColor = new Color(0.96f, 0.77f, 0.32f);
        static readonly string[] TierNames = { "Küçük müşteri", "Orta müşteri", "Büyük müşteri", "Dev müşteri" };

        ContractSystem boundContracts;
        string contractSitesResolvedFor;
        readonly List<BusInstance> contractVanBuffer = new List<BusInstance>();

        /// <summary>İhaleler açık mı: yeni arayüz, şehir içi başlangıçlı tek oyunculu oyun.</summary>
        bool ContractsAvailable => NewUi && session != null && session.Contracts != null && session.Contracts.Enabled && Room == null;

        void BindContracts()
        {
            boundContracts = session.Contracts;
            if (boundContracts == null) return;
            boundContracts.SitePicker = PickContractSite;
            boundContracts.OfferArrived += OnContractOffer;
            boundContracts.OfferWithdrawn += OnContractWithdrawn;
            boundContracts.ContractCompleted += OnContractCompleted;
            boundContracts.ContractCancelled += OnContractCancelled;
            boundContracts.Changed += OnContractsChanged;
            contractSitesResolvedFor = null;
        }

        void UnbindContracts()
        {
            if (boundContracts == null) return;
            boundContracts.SitePicker = null;
            boundContracts.OfferArrived -= OnContractOffer;
            boundContracts.OfferWithdrawn -= OnContractWithdrawn;
            boundContracts.ContractCompleted -= OnContractCompleted;
            boundContracts.ContractCancelled -= OnContractCancelled;
            boundContracts.Changed -= OnContractsChanged;
            boundContracts = null;
        }

        void OnContractsChanged()
        {
            if (panel == Panel.Contracts || panel == Panel.Fleet) tkSheetDirty = true;
            cityRoutesSignature = int.MinValue;
        }

        /// <summary>Mağaza ekran görüntüsü turunda (vitrin) ihale bildirimleri görüntüye girmesin.</summary>
        static bool ShowcaseRun => LaunchOptions.Get("vitrin") != null || LaunchOptions.Get("showcase") != null;

        void OnContractOffer(TransportContract offer)
        {
            if (!ContractsAvailable || ShowcaseRun) return;
            Toast($"Yeni ihale teklifi: {offer.clientName} — {offer.vehicles} servis aracı, günlük {Fmt.Compact(offer.dailyPay)}",
                UIStyles.Gold, quiet: panel == Panel.Contracts);
        }

        void OnContractWithdrawn(TransportContract offer)
        {
            if (ContractsAvailable && panel == Panel.Contracts) tkSheetDirty = true;
        }

        void OnContractCompleted(TransportContract c, double bonus)
        {
            if (!ContractsAvailable) return;
            Toast(bonus > 0
                ? $"{c.clientName} işi bitti: müşteri çok memnun, ikramiye {Fmt.Money(bonus)}!"
                : $"{c.clientName} işi bitti (memnuniyet {Fmt.Pct(c.satisfaction)}).", bonus > 0 ? UIStyles.Gold : UIStyles.Green, bonus > 0 ? Sfx.Horn : (Sfx?)null);
        }

        void OnContractCancelled(TransportContract c)
        {
            if (!ContractsAvailable) return;
            Toast($"{c.clientName} sözleşmeyi feshetti: servis eksik ya da araçlar bakımsız. Müşteri memnuniyeti düştü.", UIStyles.Red);
        }

        // ------------------------------------------------------------------ haritada yer seçimi

        /// <summary>
        /// İşin şehir haritasındaki yeri: servisin toplandığı mahalle (durak) ve müşterinin binası (fabrika şehrin dışına
        /// yakın, otel ünlü bir mekânın yanında, okul/şirket/hastane mahalleye birkaç km). Harita yüklü değilse null.
        /// </summary>
        ContractSite PickContractSite(TransportContract c, System.Random rng)
        {
            if (cityView == null || !cityView.Ready || cityView.City == null || cityView.City.Id != c.cityId) return null;
            UrbanMap map = cityView.Map;
            IReadOnlyList<UrbanStop> stops = map.Stops;
            if (stops.Count < 4) return null;

            // Mahalle: ağırlıkla (kalabalık mahalleler sık), şehrin en büyük merkezleri seyrek
            float total = 0f;
            foreach (UrbanStop s in stops) total += s.Weight < 0.85f ? 0.3f + s.Weight : 0.15f;
            float pick = (float)rng.NextDouble() * total;
            UrbanStop home = stops[0];
            foreach (UrbanStop s in stops)
            {
                pick -= s.Weight < 0.85f ? 0.3f + s.Weight : 0.15f;
                if (pick <= 0f) { home = s; break; }
            }

            Vector2 center = map.Bounds.center;
            float targetKm = c.kind switch
            {
                ClientKind.Factory => 9f + (float)rng.NextDouble() * 8f,
                ClientKind.Hotel => 5f + (float)rng.NextDouble() * 8f,
                _ => 3f + (float)rng.NextDouble() * 6f
            };
            Vector2 target = home.Position;
            if (c.kind == ClientKind.Hotel && map.Landmarks.Count > 0)
                target = map.Landmarks[rng.Next(Mathf.Min(8, map.Landmarks.Count))].Position;

            UrbanStop client = null;
            float best = float.MaxValue;
            foreach (UrbanStop s in stops)
            {
                if (s == home) continue;
                float km = Vector2.Distance(home.Position, s.Position) / 1000f;
                float score = Mathf.Abs(km - targetKm);
                if (c.kind == ClientKind.Factory) score -= Vector2.Distance(center, s.Position) / 1000f * 0.25f; // sanayi şehrin dışında
                if (c.kind == ClientKind.Hotel) score += Vector2.Distance(target, s.Position) / 1000f * 1.5f;
                score += (float)rng.NextDouble() * 1.2f;
                if (score < best) { best = score; client = s; }
            }
            if (client == null) return null;

            var path = new List<int>();
            float meters = map.FindPath(home.Node, client.Node, path, out float m) ? m : Vector2.Distance(home.Position, client.Position) * 1.3f;
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            Vector2 building = client.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 140f; // bina durağın yanında
            return new ContractSite
            {
                ClientX = building.x, ClientY = building.y,
                HomeX = home.Position.x, HomeY = home.Position.y,
                HomeName = home.Name,
                Km = meters / 1000f
            };
        }

        /// <summary>Şehir haritası yüklenince yeri belli olmayan işler yerleşir (SyncCityRoutes içinden).</summary>
        void ResolveContractSites()
        {
            if (session.Contracts == null || cityView == null || !cityView.Ready || cityView.City == null) return;
            if (contractSitesResolvedFor == cityView.City.Id) return;
            contractSitesResolvedFor = cityView.City.Id;
            session.Contracts.ResolveSites(cityView.City.Id, session.Random);
        }

        /// <summary>Şehrin haritasındaki ihale servisleri: mahalle → müşteri, çalışan araç sayısı kadar araç.</summary>
        void AddContractLines(string cityId, ref int signature, bool collect)
        {
            if (!ContractsAvailable) return;
            foreach (var c in session.Contracts.Active)
            {
                if (c.cityId != cityId || !c.hasSite) continue;
                int vans = session.Contracts.CountWorkingVans(c.contractId);
                if (!collect)
                {
                    signature = signature * 31 + c.contractId.GetHashCode() + vans * 7;
                    continue;
                }
                cityRouteBuffer.Add((c.contractId, new Vector2(c.homeX, c.homeY), new Vector2(c.clientX, c.clientY), ContractLineColor, vans));
            }
        }

        /// <summary>Haritada müşteri adları (süren işler): mekân adlarından önce, altın renkte.</summary>
        void DrawContractLabels(Camera cam, Rect card)
        {
            if (!ContractsAvailable || cityView.City == null) return;
            foreach (var c in session.Contracts.Active)
            {
                if (c.cityId != cityView.City.Id || !c.hasSite) continue;
                DrawUrbanLabel(cam, card, cityView.ToWorld(new Vector2(c.clientX, c.clientY)), c.clientName, s.MapLabelBold, 14f * u);
            }
        }

        void ShowContractOnMap(TransportContract c)
        {
            CityData city = session.World.GetCity(c.cityId);
            if (city == null) return;
            if (city != CurrentCity) SelectMapCity(city);
            ClosePanel();
            tkHome = false;
            cityMapWanted = true;
            if (c.hasSite)
            {
                Vector2 mid = (new Vector2(c.homeX, c.homeY) + new Vector2(c.clientX, c.clientY)) * 0.5f;
                cityCameraFocus = cityView.ToWorld(mid);
                cityCameraDistance = Mathf.Max(CityView.MinCameraDistance * 4f, Vector2.Distance(new Vector2(c.homeX, c.homeY), new Vector2(c.clientX, c.clientY)) * CityView.UnitsPerMeter * 1.6f);
                if (cameraInCity && cityView.City == city) mapCamera.FocusOn(cityCameraFocus, cityCameraDistance);
            }
        }

        // ------------------------------------------------------------------ sayfa

        int ContractsSignature()
        {
            ContractSystem contracts = session.Contracts;
            int signature = session.State.currentDay * 13 + Mathf.RoundToInt(contracts.Score);
            foreach (var c in contracts.Offers) signature = signature * 31 + c.contractId.GetHashCode() + c.asks * 3 + Mathf.RoundToInt(c.dailyPay);
            foreach (var c in contracts.Active)
                signature = signature * 31 + c.contractId.GetHashCode() + contracts.CountVans(c.contractId) * 7 + contracts.CountWorkingVans(c.contractId);
            signature = signature * 31 + contracts.History.Count;
            foreach (var bus in session.Fleet.Buses)
                if (bus.Model != null && bus.Model.IsService) signature = signature * 31 + (bus.assignedContractId?.GetHashCode() ?? 1) + (bus.IsAvailableToday ? 1 : 0);
            return signature;
        }

        void BuildContracts(VisualElement body)
        {
            ContractSystem contracts = session.Contracts;
            SheetHeader("İhaleler", $"{contracts.Active.Count} iş · {contracts.Offers.Count} teklif · memnuniyet %{Mathf.RoundToInt(contracts.Score)}");

            // Müşteri memnuniyeti: teklifleri büyüten puan
            VisualElement summary = Ui.El("bt-item bt-full bt-contract-summary", body);
            VisualElement top = Ui.El("bt-row", summary);
            Ui.Symbol("handshake", "bt-contract__icon is-gold", top);
            VisualElement text = Ui.El("bt-col bt-grow", top);
            Ui.Text("bt-item__title", "Müşteri memnuniyeti", UiWeight.Bold, text);
            Ui.Text("bt-item__text", $"{contracts.Completed} iş tamamlandı · şu an {TierNames[contracts.MaxTier].ToLowerInvariant()} teklifleri geliyor · aynı anda en çok {contracts.MaxActive} iş",
                UiWeight.Regular, text);
            Ui.Text("bt-contract__score", $"%{Mathf.RoundToInt(contracts.Score)}", UiWeight.Black, top);
            VisualElement bar = Ui.El("bt-bar bt-contract__bar", summary);
            Ui.Fill(Ui.El("bt-bar__fill bt-bar__fill--gold", bar), contracts.Score / 100f);
            Ui.Text("bt-item__text bt-dim", "İşleri eksiksiz ve bakımlı araçlarla bitir: memnuniyet arttıkça okullar, fabrikalar ve oteller daha büyük ve kazançlı işler teklif eder.",
                UiWeight.Regular, summary);

            if (contracts.Active.Count > 0)
            {
                Section(body, "Süren işler").AddToClassList("bt-section--full");
                foreach (var c in contracts.Active) BuildActiveContract(body, c);
            }

            Section(body, contracts.Offers.Count > 0 ? "Gelen teklifler" : "Gelen teklifler (yeni teklifler her gün gelebilir)").AddToClassList("bt-section--full");
            foreach (var c in contracts.Offers) BuildOffer(body, c);
            if (contracts.Offers.Count == 0)
                Ui.Text("bt-item__text bt-dim", "Şu an teklif yok. Okullar, fabrikalar ve şirketler birkaç gün içinde yeni teklifler gönderir.", UiWeight.Regular, body);

            if (contracts.History.Count > 0)
            {
                Section(body, "Biten işler").AddToClassList("bt-section--full");
                VisualElement history = Ui.El("bt-item bt-full bt-contract-history", body);
                foreach (var c in contracts.History)
                {
                    VisualElement row = Ui.El("bt-row bt-detail", history);
                    Ui.Symbol(ContractNames.KindSymbol(c.kind), "bt-contract__mini-icon", row);
                    Ui.Text("bt-detail__text bt-grow", c.clientName, UiWeight.Bold, row);
                    string outcome = c.outcome switch { 1 => "tamamlandı", 2 => "feshedildi", 3 => "bırakıldı", _ => "" };
                    Ui.Text("bt-detail__text" + (c.outcome == 1 ? " is-up" : " is-down"), $"{outcome} · {Fmt.Pct(c.satisfaction)} · {Fmt.Compact(c.earned)}", UiWeight.Bold, row);
                }
            }
        }

        VisualElement ContractCardHeader(VisualElement card, TransportContract c, out VisualElement pills)
        {
            VisualElement header = Ui.El("bt-row", card);
            VisualElement box = Ui.El("bt-contract__kind is-" + c.kind.ToString().ToLowerInvariant(), header);
            Ui.Symbol(ContractNames.KindSymbol(c.kind), "bt-contract__kind-icon", box);
            VisualElement title = Ui.El("bt-col bt-grow", header);
            Ui.Text("bt-item__title", c.clientName, UiWeight.Bold, title);
            CityData city = session.World.GetCity(c.cityId);
            string where = city != null ? city.DisplayName : c.cityId;
            if (!string.IsNullOrEmpty(c.homeName)) where += $" · {c.homeName} → {ContractNames.KindName(c.kind).ToLowerInvariant()}";
            Ui.Text("bt-item__text bt-dim", $"{where} · {Fmt.Num(c.km)} km", UiWeight.Regular, title);
            pills = Ui.El("bt-row bt-contract__pills", card);
            Ui.Text("bt-pill", TierNames[Mathf.Clamp(c.tier, 0, 3)], UiWeight.Bold, pills);
            if (c.IsVip) Ui.Text("bt-pill is-gold", "VIP", UiWeight.Black, pills);
            return header;
        }

        void ContractNeeds(VisualElement card, TransportContract c)
        {
            Detail(card, UIIcon.Seat, $"{c.vehicles} servis aracı · en az {c.minSeats} koltuk" + (c.IsVip ? " · lüks (VIP) araç" : string.Empty));
            Detail(card, UIIcon.Clock, ContractNames.Schedule(c.kind));
        }

        void BuildOffer(VisualElement body, TransportContract c)
        {
            ContractSystem contracts = session.Contracts;
            VisualElement card = Ui.El("bt-item bt-contract", body);
            card.name = "offer-" + c.contractId;
            ContractCardHeader(card, c, out VisualElement pills);
            int expires = Mathf.Max(0, c.expiresDay - session.State.currentDay);
            Ui.Text("bt-pill is-warning", expires <= 1 ? "Son gün" : $"{expires} gün içinde cevap", UiWeight.Bold, pills);
            if (c.asks > 0) Ui.Text("bt-pill is-success", "Pazarlık edildi", UiWeight.Bold, pills);
            ContractNeeds(card, c);
            Detail(card, UIIcon.Calendar, $"{c.durationDays} gün · toplam {Fmt.Compact(c.dailyPay * c.durationDays)}");

            BusModelData fit = contracts.CheapestFit(c);
            float cost = fit != null ? contracts.DailyCost(fit, c, out _, out _, out _, out _) * c.vehicles : 0f;
            VisualElement money = Ui.El("bt-row bt-contract__money", card);
            VisualElement payCol = Ui.El("bt-col bt-grow", money);
            Ui.Text("bt-contract__label", "GÜNLÜK ÖDEME", UiWeight.Bold, payCol);
            Ui.Text("bt-contract__pay", Fmt.Money(c.dailyPay), UiWeight.Black, payCol);
            VisualElement profitCol = Ui.El("bt-col", money);
            Ui.Text("bt-contract__label", "TAHMİNİ KÂR / GÜN", UiWeight.Bold, profitCol);
            Ui.Text("bt-contract__profit" + (c.dailyPay - cost >= 0 ? " is-up" : " is-down"), Fmt.Signed(c.dailyPay - cost), UiWeight.Black, profitCol);

            contracts.GetFreeFittingVans(c, contractVanBuffer);
            int free = contractVanBuffer.Count;
            if (free < c.vehicles)
            {
                string need = fit != null ? $"Uygun araç: {fit.DisplayName} ({Fmt.Compact(session.Fleet.GetPurchasePrice(fit))})" : "Uygun servis aracı henüz açılmadı.";
                VisualElement note = Detail(card, UIIcon.Info, $"Garajda {free} uygun araç var, {c.vehicles} gerekli. {need}");
                note.AddToClassList("is-warning-text");
            }

            VisualElement actions = Ui.El("bt-actions bt-actions--fill", card);
            string id = c.contractId;
            Button accept = ActionButton(actions, "Kabul et", () =>
            {
                var result = contracts.Accept(id);
                if (result)
                {
                    int assigned = contracts.AutoAssign(id);
                    TransportContract started = contracts.GetContract(id);
                    int missing = started != null ? started.vehicles - contracts.CountVans(id) : 0;
                    Toast(missing <= 0 ? $"{started?.clientName} işi başladı: {assigned} servis aracı yola çıktı."
                                       : $"{started?.clientName} işi kabul edildi: {missing} servis aracı daha gerekli (Galeri ▸ Servis).",
                        missing <= 0 ? UIStyles.Green : UIStyles.Gold, Sfx.Horn);
                }
                else Toast(result.Message, UIStyles.Red);
                tkSheetDirty = true;
            }, primary: true);
            accept.name = "accept-" + id;
            if (c.asks < ContractSystem.MaxAsks)
            {
                float chance = contracts.AskChance(c);
                Button ask = ActionButton(actions, $"Daha fazla iste (%{Mathf.RoundToInt(chance * 100f)})", () =>
                {
                    string name = c.clientName;
                    NegotiationResult result = contracts.AskMore(id, session.Random, out float pay);
                    if (result == NegotiationResult.Accepted)
                        Toast($"{name} kabul etti: günlük ödeme {Fmt.Money(pay)}.", UIStyles.Green, Sfx.Coin);
                    else if (result == NegotiationResult.Withdrawn)
                        Toast($"{name} teklifini geri çekti.", UIStyles.Orange);
                    tkSheetDirty = true;
                });
                ask.name = "ask-" + id;
            }
            Button reject = ActionButton(actions, "Reddet", () =>
            {
                contracts.Reject(id);
                tkSheetDirty = true;
            });
            reject.name = "reject-" + id;
            reject.AddToClassList("is-danger");
        }

        void BuildActiveContract(VisualElement body, TransportContract c)
        {
            ContractSystem contracts = session.Contracts;
            VisualElement card = Ui.El("bt-item bt-contract is-active", body);
            card.name = "contract-" + c.contractId;
            ContractCardHeader(card, c, out VisualElement pills);
            Ui.Text("bt-pill is-success", $"{c.daysLeft} gün kaldı", UiWeight.Bold, pills);
            ContractNeeds(card, c);

            float coverage = contracts.Coverage(c);
            VisualElement money = Ui.El("bt-row bt-contract__money", card);
            VisualElement payCol = Ui.El("bt-col bt-grow", money);
            Ui.Text("bt-contract__label", "GÜNLÜK ÖDEME", UiWeight.Bold, payCol);
            Ui.Text("bt-contract__pay", Fmt.Money(c.dailyPay * coverage), UiWeight.Black, payCol);
            VisualElement earnedCol = Ui.El("bt-col", money);
            Ui.Text("bt-contract__label", "ŞİMDİYE KADAR", UiWeight.Bold, earnedCol);
            Ui.Text("bt-contract__profit is-up", Fmt.Compact(c.earned), UiWeight.Black, earnedCol);

            VisualElement satisfaction = Ui.El("bt-row bt-detail", card);
            Ui.Icon(UIIcon.Smile, "bt-detail__icon", satisfaction);
            Ui.Text("bt-detail__text bt-nowrap", $"Memnuniyet {Fmt.Pct(c.satisfaction)}", UiWeight.Regular, satisfaction);
            VisualElement bar = Ui.El("bt-bar bt-bus__condition", satisfaction);
            VisualElement fill = Ui.El("bt-bar__fill", bar);
            Ui.Fill(fill, c.satisfaction);
            fill.AddToClassList(c.satisfaction > 0.7f ? "bt-bar__fill--green" : c.satisfaction > 0.45f ? "bt-bar__fill--yellow" : "bt-bar__fill--red");

            // Araçlar
            contracts.GetVans(c.contractId, contractVanBuffer);
            int working = contracts.CountWorkingVans(c.contractId);
            foreach (var bus in contractVanBuffer)
            {
                VisualElement row = Ui.El("bt-row bt-contract__van", card);
                VisualElement art = Ui.El("bt-contract__van-art", row);
                Sprite sprite = UIBusArt.Sprite(bus.Model);
                if (sprite != null) art.style.backgroundImage = new StyleBackground(sprite);
                VisualElement info = Ui.El("bt-col bt-grow", row);
                Ui.Text("bt-detail__text", bus.nickname, UiWeight.Bold, info);
                string state = bus.InDelivery ? $"teslimatta · {bus.deliveryDaysLeft} gün" : !bus.IsAvailableToday ? "arızalı" : $"durum {Fmt.Pct(bus.condition)}";
                Ui.Text("bt-detail__text bt-dim", $"{bus.Model.DisplayName} · {state}", UiWeight.Regular, info);
                string busId = bus.busId;
                Button remove = ActionButton(row, "Çıkar", () =>
                {
                    var result = contracts.UnassignVan(busId);
                    Toast(result.Message, result ? UIStyles.Blue : UIStyles.Red);
                    tkSheetDirty = true;
                });
                remove.AddToClassList("bt-btn--small");
            }

            int missing = c.vehicles - contractVanBuffer.Count;
            string id = c.contractId;
            if (missing > 0 || working < contractVanBuffer.Count)
            {
                string warning = missing > 0
                    ? $"{missing} araç eksik: bugün ödemenin {Fmt.Pct(coverage)}'i alınıyor, müşteri memnuniyeti düşüyor."
                    : "Bazı araçlar arızalı ya da teslimatta: ödeme eksik alınıyor.";
                VisualElement note = Detail(card, UIIcon.Warning, warning);
                note.AddToClassList("is-warning-text");
            }

            VisualElement actions = Ui.El("bt-actions bt-actions--fill", card);
            if (missing > 0)
            {
                contracts.GetFreeFittingVans(c, contractVanBuffer);
                if (contractVanBuffer.Count > 0)
                {
                    Button assign = ActionButton(actions, $"Garajdan ata ({Mathf.Min(missing, contractVanBuffer.Count)})", () =>
                    {
                        int assigned = contracts.AutoAssign(id);
                        Toast($"{assigned} servis aracı işe gönderildi.", UIStyles.Green);
                        tkSheetDirty = true;
                    }, primary: true);
                    assign.name = "assign-" + id;
                }
                else
                {
                    Button buy = ActionButton(actions, "Servis aracı al", () =>
                    {
                        dealershipSection = GalleryService;
                        OpenPage(Panel.Dealership);
                    }, primary: true);
                    buy.name = "buyvan-" + id;
                }
            }
            if (c.hasSite && CityView.Supports(session.World.GetCity(c.cityId)))
                ActionButton(actions, "Haritada göster", () => ShowContractOnMap(c));
            Button quit = ActionButton(actions, "İşi bırak", () =>
            {
                AskConfirm("İşi bırak", $"{c.clientName} işi bırakılacak. Araçlar garaja döner, müşteri memnuniyeti puanı düşer.", () =>
                {
                    var result = contracts.Abandon(id);
                    Toast(result.Message, result ? UIStyles.Orange : UIStyles.Red);
                    tkSheetDirty = true;
                });
            });
            quit.AddToClassList("is-danger");
        }

        /// <summary>Filo kartında servis aracı için iş seçimi: araca uyan ve araç bekleyen işler.</summary>
        void BuildVanContractChoices(VisualElement card, BusInstance bus)
        {
            ContractSystem contracts = session.Contracts;
            if (contracts == null || !ContractsAvailable)
            {
                Ui.Text("bt-item__text", "İhale işleri bu oyunda kapalı.", UiWeight.Regular, card);
                return;
            }
            Ui.Text("bt-item__text", "Hangi işte çalışsın?", UiWeight.Bold, card);
            int shown = 0;
            string busId = bus.busId;
            foreach (var c in contracts.Active)
            {
                bool current = bus.assignedContractId == c.contractId;
                int vans = contracts.CountVans(c.contractId);
                bool fits = ContractSystem.Fits(bus.Model, c);
                if (!current && (!fits || vans >= c.vehicles)) continue;
                TransportContract target = c;
                Button choice = ActionButton(card, $"{c.clientName} · {vans}/{c.vehicles} araç" + (current ? " (şu an)" : string.Empty), () =>
                {
                    if (!string.IsNullOrEmpty(bus.assignedContractId) && bus.assignedContractId != target.contractId) contracts.UnassignVan(busId);
                    var result = contracts.AssignVan(busId, target.contractId);
                    expandedBusId = null;
                    Toast(result ? $"{bus.nickname} {target.clientName} işine gönderildi." : result.Message, result ? UIStyles.Green : UIStyles.Red);
                    RefreshFleet();
                }, enabled: !current);
                choice.AddToClassList("bt-btn--choice");
                choice.style.borderLeftColor = ContractLineColor;
                shown++;
            }
            if (shown == 0)
            {
                Ui.Text("bt-item__text", "Bu araca uyan, araç bekleyen iş yok. İhaleler sayfasından teklif kabul et.", UiWeight.Regular, card);
                ActionButton(card, "İhaleler", () => OpenPage(Panel.Contracts), primary: true).AddToClassList("bt-btn--block");
            }
        }
    }
}
