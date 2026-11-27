using System;
using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Economy;
using BusTycoon.Fleet;
using BusTycoon.Progression;
using BusTycoon.Save;
using UnityEngine;

namespace BusTycoon.Contracts
{
    /// <summary>
    /// İhaleler (taşımacılık sözleşmeleri). Okullar, fabrikalar, şirketler, oteller ve hastaneler servis işi teklif eder;
    /// oyuncu kabul eder, reddeder ya da daha fazla ister (firma kabul edebilir ya da teklifini çeker). Kabul edilen işe
    /// servis araçları atanır; ödeme gün içinde saniye saniye kasaya akar (çalışan araç sayısıyla orantılı), giderler
    /// yakıt, şoför, bakım ve sigorta olarak işlenir. İşin sonunda müşteri memnuniyeti şirketin
    /// <see cref="Score"/> puanına yansır; puan ve şirket seviyesi arttıkça daha büyük ve kazançlı teklifler gelir.
    /// Yalnız şehir içi başlangıçlı tek oyunculu oyunda açıktır (<c>vehicleClasses</c>).
    /// </summary>
    public sealed class ContractSystem
    {
        /// <summary>Kabulden sonra araçların yerleşmesi için memnuniyet cezası verilmeyen gün sayısı.</summary>
        public const int GraceDays = 2;
        public const int MaxAsks = 2;
        public const int HistoryLimit = 12;
        /// <summary>Memnuniyet bunun altına düşerse müşteri sözleşmeyi feshder.</summary>
        public const float CancelBelow = 0.33f;
        /// <summary>İş bitiminde memnuniyet bunun üstündeyse ikramiye (3 günlük ödeme).</summary>
        public const float BonusAbove = 0.85f;

        readonly CompanyState state;
        readonly BusCatalog catalog;
        readonly EconomyConfig economy;
        readonly FleetSystem fleet;
        readonly FinanceSystem finance;
        readonly ProgressionSystem progression;
        readonly Func<string, CityData> getCity;
        readonly List<BusInstance> vanBuffer = new List<BusInstance>();
        readonly double[] pending = new double[5];

        /// <summary>Oyun katmanı: şehrin haritasından müşteri ve mahalle seçer (harita yüklü değilse null döner).</summary>
        public Func<TransportContract, System.Random, ContractSite> SitePicker;

        public event Action<TransportContract> OfferArrived;
        public event Action<TransportContract> OfferWithdrawn;
        public event Action<TransportContract> ContractStarted;
        /// <summary>İş tamamlandı: (iş, ikramiye).</summary>
        public event Action<TransportContract, double> ContractCompleted;
        public event Action<TransportContract> ContractCancelled;
        public event Action Changed;

        public ContractSystem(CompanyState state, BusCatalog catalog, EconomyConfig economy, FleetSystem fleet, FinanceSystem finance,
            ProgressionSystem progression, Func<string, CityData> getCity)
        {
            this.state = state;
            this.catalog = catalog;
            this.economy = economy;
            this.fleet = fleet;
            this.finance = finance;
            this.progression = progression;
            this.getCity = getCity;
            state.contractOffers ??= new List<TransportContract>();
            state.contracts ??= new List<TransportContract>();
            state.contractHistory ??= new List<TransportContract>();
            if (state.contractScore <= 0f) state.contractScore = 50f;
        }

        /// <summary>İhaleler açık mı: şehir içi başlangıçlı (araç sınıflı) oyun.</summary>
        public bool Enabled => state.vehicleClasses;
        public IReadOnlyList<TransportContract> Offers => state.contractOffers;
        public IReadOnlyList<TransportContract> Active => state.contracts;
        public IReadOnlyList<TransportContract> History => state.contractHistory;
        /// <summary>Müşteri memnuniyeti puanı (0..100): biten işlerin memnuniyetinden; teklifleri büyütür.</summary>
        public float Score => state.contractScore;
        public int Completed => state.contractsCompleted;
        /// <summary>Gün içinde hesaplamada kullanılan günün geçen kesri (GameSession yazar).</summary>
        public float DayProgress { get; set; }
        float IncomeScale => state.incomeScale > 0f ? state.incomeScale : economy.IncomeScale;

        /// <summary>Şu an gelebilecek en büyük müşteri (0..3): seviye ve memnuniyet puanıyla büyür.</summary>
        public int MaxTier => Mathf.Clamp((state.level - 1) / 2 + Mathf.FloorToInt((state.contractScore - 50f) / 20f), 0, 3);
        public int MaxOffers => 3 + Mathf.Min(2, state.level / 4);

        public TransportContract GetOffer(string id) => state.contractOffers.Find(c => c.contractId == id);
        public TransportContract GetContract(string id) => state.contracts.Find(c => c.contractId == id);

        // ------------------------------------------------------------------ teklifler

        TransportContract AddOffer(System.Random rng)
        {
            string cityId = PickCity(rng);
            if (cityId == null) return null;
            int tier = PickTier(rng);
            ClientKind kind = PickKind(rng, tier);
            var offer = new TransportContract
            {
                contractId = "IH" + (++state.nextContractSerial).ToString("000"),
                clientName = ContractNames.Make(kind, tier, rng, NameTaken),
                kind = kind,
                cityId = cityId,
                tier = tier,
                expiresDay = state.currentDay + 4 + rng.Next(0, 3)
            };
            Shape(offer, rng);
            ContractSite site = SitePicker?.Invoke(offer, rng);
            if (site != null) ApplySite(offer, site);
            else
            {
                offer.km = kind == ClientKind.Factory ? 9f + (float)rng.NextDouble() * 9f : 4f + (float)rng.NextDouble() * 8f;
                offer.homeName = null;
            }
            offer.dailyPay = Mathf.Round(PayFor(offer, rng));
            state.contractOffers.Add(offer);
            OfferArrived?.Invoke(offer);
            Changed?.Invoke();
            return offer;
        }

        bool NameTaken(string name)
        {
            foreach (var c in state.contractOffers) if (c.clientName == name) return true;
            foreach (var c in state.contracts) if (c.clientName == name) return true;
            return false;
        }

        string PickCity(System.Random rng)
        {
            var cities = new List<string>();
            foreach (string id in state.ownedCityIds) if (!string.IsNullOrEmpty(id) && !cities.Contains(id)) cities.Add(id);
            if (cities.Count == 0 && !string.IsNullOrEmpty(state.startCityId)) cities.Add(state.startCityId);
            if (cities.Count == 0) return null;
            // Başlangıç şehri biraz daha sık (oyuncunun en çok aracı orada)
            if (!string.IsNullOrEmpty(state.startCityId) && cities.Contains(state.startCityId) && rng.NextDouble() < 0.4) return state.startCityId;
            return cities[rng.Next(cities.Count)];
        }

        int PickTier(System.Random rng)
        {
            int max = MaxTier;
            // Çoğunlukla en büyük açık kademe, bazen küçükler
            double roll = rng.NextDouble();
            int tier = roll < 0.55 ? max : roll < 0.85 ? max - 1 : max - 2;
            return Mathf.Clamp(tier, 0, 3);
        }

        static ClientKind PickKind(System.Random rng, int tier)
        {
            // Küçük kademede okul ve şirket; büyüdükçe fabrika ve otel (VIP)
            double roll = rng.NextDouble();
            switch (tier)
            {
                case 0: return roll < 0.45 ? ClientKind.School : roll < 0.8 ? ClientKind.Office : ClientKind.Hospital;
                case 1: return roll < 0.3 ? ClientKind.School : roll < 0.55 ? ClientKind.Factory : roll < 0.75 ? ClientKind.Office : roll < 0.9 ? ClientKind.Hospital : ClientKind.Hotel;
                default: return roll < 0.2 ? ClientKind.School : roll < 0.5 ? ClientKind.Factory : roll < 0.65 ? ClientKind.Office : roll < 0.75 ? ClientKind.Hospital : ClientKind.Hotel;
            }
        }

        static void Shape(TransportContract c, System.Random rng)
        {
            int t = c.tier;
            switch (c.kind)
            {
                case ClientKind.School:
                    c.vehicles = 1 + t + rng.Next(0, 2);
                    c.minSeats = new[] { 9, 14, 14, 17 }[t];
                    c.durationDays = 20 + rng.Next(0, 21);
                    break;
                case ClientKind.Factory:
                    c.vehicles = 2 + 2 * t + rng.Next(0, 2);
                    c.minSeats = new[] { 14, 17, 19, 19 }[t];
                    c.durationDays = 30 + rng.Next(0, 31);
                    break;
                case ClientKind.Office:
                    c.vehicles = 1 + t;
                    c.minSeats = new[] { 9, 9, 14, 14 }[t];
                    c.durationDays = 15 + rng.Next(0, 26);
                    // Büyük şirketlerin yönetim servisi VIP olur
                    if (t >= 3 && rng.NextDouble() < 0.5) { c.minComfort = 0.8f; c.minSeats = 7; }
                    break;
                case ClientKind.Hotel:
                    c.vehicles = 1 + t / 2 + rng.Next(0, 2);
                    c.minSeats = t >= 2 && rng.NextDouble() < 0.5 ? 12 : 7;
                    c.minComfort = 0.8f;
                    c.durationDays = 10 + rng.Next(0, 16);
                    break;
                default: // hastane: vardiya servisi
                    c.vehicles = 1 + t;
                    c.minSeats = new[] { 9, 14, 14, 17 }[t];
                    c.durationDays = 20 + rng.Next(0, 26);
                    break;
            }
            c.vehicles = Mathf.Clamp(c.vehicles, 1, 9);
        }

        static void ApplySite(TransportContract c, ContractSite site)
        {
            c.clientX = site.ClientX;
            c.clientY = site.ClientY;
            c.homeX = site.HomeX;
            c.homeY = site.HomeY;
            c.homeName = site.HomeName;
            c.km = Mathf.Max(1.5f, site.Km);
            c.hasSite = true;
        }

        /// <summary>Haritası yeni yüklenen şehirde yeri belli olmayan teklif ve işleri yerleştirir (ödeme değişmez).</summary>
        public void ResolveSites(string cityId, System.Random rng)
        {
            if (SitePicker == null || cityId == null) return;
            bool changed = false;
            foreach (var list in new[] { state.contractOffers, state.contracts })
                foreach (var c in list)
                {
                    if (c.hasSite || c.cityId != cityId) continue;
                    ContractSite site = SitePicker(c, rng);
                    if (site == null) continue;
                    float km = c.km;
                    ApplySite(c, site);
                    if (c.startDay > 0 || list == state.contracts) c.km = km; // süren işte mesafe (gider) değişmesin
                    changed = true;
                }
            if (changed) Changed?.Invoke();
        }

        /// <summary>İşe uyan en ucuz servis aracı (ödeme bu aracın gideri üzerinden hesaplanır).</summary>
        public BusModelData CheapestFit(TransportContract c)
        {
            BusModelData best = null;
            foreach (var model in catalog.SortedByTier)
                if (Fits(model, c) && (best == null || model.Price < best.Price)) best = model;
            return best;
        }

        public static bool Fits(BusModelData model, TransportContract c) =>
            model != null && model.IsService && model.Seats >= c.minSeats && model.Comfort + 1e-4f >= c.minComfort;

        /// <summary>Bir servis aracının bu iş için günlük gideri (yakıt, şoför, bakım, sigorta; gelir katsayısı dahil).</summary>
        public float DailyCost(BusModelData model, TransportContract c, out float fuel, out float driver, out float maintenance, out float insurance)
        {
            fuel = driver = maintenance = insurance = 0f;
            if (model == null) return 0f;
            CityData city = getCity?.Invoke(c.cityId);
            RegionData region = city != null ? city.Region : null;
            float fuelRegion = region != null ? region.FuelPriceMultiplier : 1f;
            float wageRegion = region != null ? region.WageMultiplier : 1f;
            float kmPerDay = KmPerDay(c);
            float scale = IncomeScale;
            fuel = kmPerDay * model.FuelLitersPer100Km / 100f * economy.FuelPricePerLiter * fuelRegion * scale;
            driver = (3.5f + kmPerDay / 28f) * economy.DriverWagePerHour * wageRegion * scale; // sabah ve akşam iki sefer + bekleme
            maintenance = kmPerDay * model.MaintenanceCostPerKm * scale;
            insurance = model.InsurancePerDay * scale;
            return fuel + driver + maintenance + insurance;
        }

        /// <summary>Günde gidilen yol: sabah toplama + bırakma, akşam tersi (4 yön) ve garaj yolu.</summary>
        public static float KmPerDay(TransportContract c) => 4f * Mathf.Max(1.5f, c.km) + 6f;

        float PayFor(TransportContract c, System.Random rng)
        {
            BusModelData model = CheapestFit(c) ?? catalog.GetById("SERCE_14");
            float cost = DailyCost(model, c, out _, out _, out _, out _);
            // Kâr payı: araç parası ~7-8 günde çıkar (dolmuş hattı ~6 gün; ihale daha az riskli, sabit gelir)
            float margin = (c.minSeats * 5.5f * (c.IsVip ? 2.2f : 1f) + 40f) * IncomeScale;
            float perVan = cost * 1.25f + margin;
            perVan *= 1f + 0.08f * c.tier;
            perVan *= 0.9f + (float)rng.NextDouble() * 0.22f;
            perVan *= 1f + (state.contractScore - 50f) / 250f; // memnun müşteriler daha iyi öder (kulaktan kulağa)
            return perVan * c.vehicles;
        }

        public OpResult Reject(string id)
        {
            TransportContract offer = GetOffer(id);
            if (offer == null) return OpResult.Fail("Teklif bulunamadı.");
            state.contractOffers.Remove(offer);
            Changed?.Invoke();
            return OpResult.Ok($"{offer.clientName} teklifi reddedildi.");
        }

        /// <summary>
        /// Daha fazla ister: firma ödemeyi artırır (%10–18) ya da teklifini çeker. Şans memnuniyet puanıyla artar, her
        /// istekte azalır; en çok <see cref="MaxAsks"/> kez.
        /// </summary>
        public NegotiationResult AskMore(string id, System.Random rng, out float newPay)
        {
            TransportContract offer = GetOffer(id);
            newPay = offer != null ? offer.dailyPay : 0f;
            if (offer == null || offer.asks >= MaxAsks) return NegotiationResult.NotAllowed;
            float chance = AskChance(offer);
            offer.asks++;
            if (rng.NextDouble() < chance)
            {
                offer.dailyPay = Mathf.Round(offer.dailyPay * (1.1f + (float)rng.NextDouble() * 0.08f));
                newPay = offer.dailyPay;
                Changed?.Invoke();
                return NegotiationResult.Accepted;
            }
            state.contractOffers.Remove(offer);
            OfferWithdrawn?.Invoke(offer);
            Changed?.Invoke();
            return NegotiationResult.Withdrawn;
        }

        /// <summary>"Daha fazla iste"nin kabul edilme olasılığı (0..1): arayüzde ipucu olarak gösterilir.</summary>
        public float AskChance(TransportContract offer) =>
            offer == null ? 0f : Mathf.Clamp(0.62f - 0.24f * offer.asks + (state.contractScore - 50f) * 0.006f, 0.1f, 0.85f);

        public OpResult Accept(string id)
        {
            TransportContract offer = GetOffer(id);
            if (offer == null) return OpResult.Fail("Teklif bulunamadı.");
            if (state.contracts.Count >= MaxActive) return OpResult.Fail($"Aynı anda en çok {MaxActive} iş yürütebilirsin (seviye atladıkça artar).");
            state.contractOffers.Remove(offer);
            offer.startDay = state.currentDay;
            offer.daysLeft = offer.durationDays;
            offer.satisfaction = 0.75f;
            offer.booked = Mathf.Clamp01(DayProgress);
            offer.coverageToday = 0f;
            state.contracts.Add(offer);
            ContractStarted?.Invoke(offer);
            Changed?.Invoke();
            return OpResult.Ok($"{offer.clientName} işi kabul edildi: {offer.vehicles} servis aracı ata.");
        }

        public int MaxActive => 2 + state.level / 3;

        /// <summary>Teklif gelir mi: öğretici bittiyse ya da ilk günler geçtiyse (yeni oyuncu önce dolmuşunu kursun).</summary>
        public bool OffersOpen => !state.tutorialActive || state.currentDay >= FirstOfferDay;
        public const int FirstOfferDay = 5;

        /// <summary>Oyuncu işi bırakır: müşteri memnuniyeti puanı düşer, araçlar garaja döner.</summary>
        public OpResult Abandon(string id)
        {
            TransportContract c = GetContract(id);
            if (c == null) return OpResult.Fail("İş bulunamadı.");
            Finish(c, 3);
            state.contractScore = Mathf.Clamp(state.contractScore - 10f, 0f, 100f);
            Changed?.Invoke();
            return OpResult.Ok($"{c.clientName} işi bırakıldı. Müşteri memnuniyeti düştü.");
        }

        // ------------------------------------------------------------------ araçlar

        public void GetVans(string contractId, List<BusInstance> results)
        {
            results.Clear();
            foreach (var bus in fleet.Buses)
                if (bus.assignedContractId == contractId) results.Add(bus);
        }

        public int CountVans(string contractId)
        {
            int count = 0;
            foreach (var bus in fleet.Buses) if (bus.assignedContractId == contractId) count++;
            return count;
        }

        /// <summary>Bugün çalışabilen (teslimatta/arızada olmayan) araç sayısı.</summary>
        public int CountWorkingVans(string contractId)
        {
            int count = 0;
            foreach (var bus in fleet.Buses)
                if (bus.assignedContractId == contractId && bus.IsAvailableToday && bus.Model != null) count++;
            return count;
        }

        /// <summary>Garajda bekleyen, bu işe uyan servis araçları.</summary>
        public void GetFreeFittingVans(TransportContract c, List<BusInstance> results)
        {
            results.Clear();
            foreach (var bus in fleet.Buses)
                if (!bus.IsAssigned && Fits(bus.Model, c)) results.Add(bus);
        }

        public OpResult AssignVan(string busId, string contractId)
        {
            BusInstance bus = fleet.GetBus(busId);
            TransportContract c = GetContract(contractId);
            if (bus == null || c == null) return OpResult.Fail("Araç ya da iş bulunamadı.");
            if (bus.Model == null || !bus.Model.IsService) return OpResult.Fail("İhale işlerinde yalnız servis araçları çalışır.");
            if (!Fits(bus.Model, c))
                return OpResult.Fail(c.IsVip && bus.Model.Comfort < c.minComfort
                    ? $"{c.clientName} VIP araç istiyor (lüks servis aracı)."
                    : $"{c.clientName} en az {c.minSeats} koltuklu araç istiyor.");
            if (bus.assignedContractId == contractId) return OpResult.Ok();
            if (!string.IsNullOrEmpty(bus.assignedRouteId)) return OpResult.Fail("Araç bir hatta çalışıyor.");
            if (CountVans(contractId) >= c.vehicles) return OpResult.Fail($"{c.clientName} için {c.vehicles} araç yeter.");
            fleet.SetContract(bus, contractId);
            Changed?.Invoke();
            return OpResult.Ok($"{bus.nickname} → {c.clientName}");
        }

        public OpResult UnassignVan(string busId)
        {
            BusInstance bus = fleet.GetBus(busId);
            if (bus == null || string.IsNullOrEmpty(bus.assignedContractId)) return OpResult.Fail("Araç bir işte değil.");
            fleet.SetContract(bus, null);
            Changed?.Invoke();
            return OpResult.Ok($"{bus.nickname} garaja döndü.");
        }

        /// <summary>Garajdaki uygun araçları işe atar (önce en küçük uyan araç): atanan sayı.</summary>
        public int AutoAssign(string contractId)
        {
            TransportContract c = GetContract(contractId);
            if (c == null) return 0;
            GetFreeFittingVans(c, vanBuffer);
            vanBuffer.Sort((a, b) => a.Model.Seats != b.Model.Seats ? a.Model.Seats.CompareTo(b.Model.Seats) : b.condition.CompareTo(a.condition));
            int assigned = 0;
            foreach (var bus in vanBuffer)
            {
                if (CountVans(contractId) >= c.vehicles) break;
                fleet.SetContract(bus, contractId);
                assigned++;
            }
            if (assigned > 0) Changed?.Invoke();
            return assigned;
        }

        // ------------------------------------------------------------------ gün içi akış

        /// <summary>İşlerin ödemesi ve giderleri günün geçen kesrine kadar işlenir (saniyede bir).</summary>
        public void Accrue(float dayProgress)
        {
            if (!Enabled || state.contracts.Count == 0) return;
            foreach (var c in state.contracts) AccrueContract(c, Mathf.Clamp01(dayProgress));
            Flush();
        }

        void AccrueContract(TransportContract c, float until)
        {
            float delta = until - c.booked;
            if (delta <= 0f) return;
            c.booked = until;
            GetVans(c.contractId, vanBuffer);
            int working = 0;
            foreach (var bus in vanBuffer)
            {
                if (bus.Model == null) continue;
                DailyCost(bus.Model, c, out float fuel, out float driver, out float maintenance, out float insurance);
                pending[4] += insurance * delta; // sigorta araç işte oldukça
                if (!bus.IsAvailableToday) continue;
                working++;
                pending[1] += fuel * delta;
                pending[2] += driver * delta;
                pending[3] += maintenance * delta;
                fleet.ApplyWear(bus, KmPerDay(c) * delta);
            }
            int counted = Mathf.Min(working, c.vehicles);
            double pay = c.dailyPay * (counted / (float)Mathf.Max(1, c.vehicles)) * delta;
            pending[0] += pay;
            c.earned += pay;
            c.coverageToday += counted / (float)Mathf.Max(1, c.vehicles) * delta;
        }

        void Flush()
        {
            finance.Earn(pending[0], LedgerCategory.ContractIncome);
            finance.Charge(pending[1], LedgerCategory.Fuel);
            finance.Charge(pending[2], LedgerCategory.DriverWages);
            finance.Charge(pending[3], LedgerCategory.Maintenance);
            finance.Charge(pending[4], LedgerCategory.Insurance);
            Array.Clear(pending, 0, pending.Length);
        }

        /// <summary>Bugün işin günlük ödemesinin ne kadarı alınıyor (0..1, çalışan araç / istenen).</summary>
        public float Coverage(TransportContract c) =>
            c == null ? 0f : Mathf.Min(CountWorkingVans(c.contractId), c.vehicles) / (float)Mathf.Max(1, c.vehicles);

        // ------------------------------------------------------------------ gün sonu

        /// <summary>
        /// Gün sonu: günün kalanı işlenir, memnuniyet güncellenir, biten işler kapanır, süresi geçen teklifler çekilir,
        /// yeni teklifler gelir. Gün sonu sırasında (hatlardan sonra) çağrılır.
        /// </summary>
        public void AdvanceDay(System.Random rng)
        {
            if (!Enabled) return;
            foreach (var c in state.contracts) AccrueContract(c, 1f);
            Flush();

            double passengers = 0;
            for (int i = state.contracts.Count - 1; i >= 0; i--)
            {
                TransportContract c = state.contracts[i];
                float coverage = Mathf.Clamp01(c.coverageToday);
                GetVans(c.contractId, vanBuffer);
                float condition = 0f, comfort = 0f;
                int seats = 0, count = 0;
                foreach (var bus in vanBuffer)
                {
                    if (bus.Model == null) continue;
                    condition += bus.condition;
                    comfort += bus.Model.Comfort;
                    if (bus.IsAvailableToday) seats += bus.Model.Seats;
                    count++;
                }
                if (count > 0) { condition /= count; comfort /= count; }
                passengers += Mathf.Min(seats, c.minSeats * c.vehicles) * 2f * coverage;

                bool grace = c.DaysDone < GraceDays;
                if (!grace || coverage > 0f)
                {
                    float comfortFit = Mathf.Clamp01(comfort / Mathf.Max(0.35f, c.minComfort));
                    float quality = coverage * 0.6f + (count > 0 ? condition * 0.25f + comfortFit * 0.15f : 0f);
                    // Eksik araç memnuniyeti hızla düşürür; tam ve bakımlı filo yavaşça yükseltir
                    c.satisfaction = Mathf.Clamp01(c.satisfaction + (quality - c.satisfaction) * (quality < c.satisfaction ? 0.35f : 0.2f));
                }
                c.booked = 0f;
                c.coverageToday = 0f;
                c.daysLeft--;

                if (!grace && c.satisfaction < CancelBelow)
                {
                    state.contractScore = Mathf.Clamp(state.contractScore - 8f, 0f, 100f);
                    Finish(c, 2);
                    ContractCancelled?.Invoke(c);
                    continue;
                }
                if (c.daysLeft <= 0) Complete(c);
            }
            if (passengers > 0) progression.AddPassengers(passengers);

            // Süresi geçen teklifler
            for (int i = state.contractOffers.Count - 1; i >= 0; i--)
            {
                TransportContract offer = state.contractOffers[i];
                if (offer.expiresDay > state.currentDay) continue;
                state.contractOffers.RemoveAt(i);
                OfferWithdrawn?.Invoke(offer);
            }

            // Yeni teklifler: boş yer varsa çoğu gün bir teklif. Öğretici sürerken (ilk günler) teklif gelmez.
            if (OffersOpen)
            {
                if (state.contractOffers.Count < MaxOffers && rng.NextDouble() < 0.6) AddOffer(rng);
                if (state.contractOffers.Count == 0) AddOffer(rng);
            }
            Changed?.Invoke();
        }

        void Complete(TransportContract c)
        {
            double bonus = c.satisfaction >= BonusAbove ? Math.Round(c.dailyPay * 3.0) : 0;
            if (bonus > 0)
            {
                finance.Earn(bonus, LedgerCategory.ContractIncome, c.contractId);
                c.earned += bonus;
            }
            state.contractScore = Mathf.Clamp(state.contractScore * 0.7f + c.satisfaction * 100f * 0.3f, 0f, 100f);
            state.contractsCompleted++;
            progression.AddReputation((c.satisfaction - 0.5f) * 4f * (1f + c.tier * 0.5f));
            Finish(c, 1);
            ContractCompleted?.Invoke(c, bonus);
        }

        void Finish(TransportContract c, int outcome)
        {
            GetVans(c.contractId, vanBuffer);
            foreach (var bus in vanBuffer) fleet.SetContract(bus, null);
            c.outcome = outcome;
            c.daysLeft = Mathf.Max(0, c.daysLeft);
            state.contracts.Remove(c);
            state.contractHistory.Insert(0, c);
            if (state.contractHistory.Count > HistoryLimit) state.contractHistory.RemoveAt(state.contractHistory.Count - 1);
        }
    }
}
