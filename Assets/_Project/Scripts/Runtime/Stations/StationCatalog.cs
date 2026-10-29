using BusTycoon.Data;
using UnityEngine;

namespace BusTycoon.Stations
{
    /// <summary>Durağın büyüklüğü şehrin nüfusundan gelir (<see cref="CityData.MapTier"/>): küçük durak … metropol terminali.</summary>
    public enum StationTier { Stop, Terminal, Large, Metro }

    /// <summary>
    /// Durağa yapılabilecek bir geliştirme: durağın içinde görünür (StationView) ve konfor puanı kadar o duraktan
    /// geçen hatlarda talebi artırır (<see cref="StationSystem.DemandBonusPerComfort"/>). Büyük duraklarda daha çok
    /// geliştirme vardır ve fiyatlar durağın büyüklüğüyle artar.
    /// </summary>
    public sealed class StationUpgrade
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Description;
        public readonly StationTier MinTier;
        public readonly float BaseCost;
        public readonly int Comfort;

        public StationUpgrade(string id, string name, string description, StationTier minTier, float baseCost, int comfort)
        {
            Id = id;
            Name = name;
            Description = description;
            MinTier = minTier;
            BaseCost = baseCost;
            Comfort = comfort;
        }
    }

    /// <summary>Durak geliştirmeleri ve kademe bilgileri (tasarım verisi; kayıt yalnız geliştirme kimliklerini saklar).</summary>
    public static class StationCatalog
    {
        public const string Seats = "seats", Plants = "plants", Lights = "lights", Kiosk = "kiosk", Board = "board", Wifi = "wifi";
        public const string Ticket = "ticket", Cafe = "cafe", Lockers = "lockers", Taxi = "taxi";
        public const string Canopy = "canopy", Kids = "kids", Shop = "shop";
        public const string Vip = "vip", Fountain = "fountain", Clock = "clock";

        public static readonly StationUpgrade[] Upgrades =
        {
            new StationUpgrade(Seats, "Konforlu koltuklar", "Sert banklar yerine minderli, kolçaklı bekleme koltukları.", StationTier.Stop, 6000f, 2),
            new StationUpgrade(Plants, "Bitkiler ve çiçekler", "Saksı bitkileri, çiçeklikler ve gölge veren ağaçlar.", StationTier.Stop, 3000f, 1),
            new StationUpgrade(Lights, "Sıcak aydınlatma", "Sarkıt lambalar ve sokak lambaları: gece durak ışıl ışıl.", StationTier.Stop, 4500f, 1),
            new StationUpgrade(Kiosk, "Simit ve çay büfesi", "Taze simit, sıcak çay: yolcuların vazgeçilmezi.", StationTier.Stop, 7500f, 2),
            new StationUpgrade(Board, "Dijital kalkış panosu", "Hatlarınızın kalkış saatleri ve peronları canlı panoda.", StationTier.Stop, 9000f, 1),
            new StationUpgrade(Wifi, "Wi-Fi ve şarj noktası", "Ücretsiz internet ve telefon şarj istasyonu.", StationTier.Stop, 5000f, 1),
            new StationUpgrade(Ticket, "Bilet gişesi", "Şirketinizin turuncu gişesi ve kuyruk bariyerleri.", StationTier.Terminal, 10000f, 2),
            new StationUpgrade(Cafe, "Kafe", "Kahve makinesi, masalar ve sandalyeler.", StationTier.Terminal, 14000f, 2),
            new StationUpgrade(Lockers, "Emanet dolapları", "Bavullar için kilitli dolaplar.", StationTier.Terminal, 6000f, 1),
            new StationUpgrade(Taxi, "Taksi durağı", "Kapıda bekleyen sarı taksiler.", StationTier.Terminal, 9000f, 1),
            new StationUpgrade(Canopy, "Peron çatısı", "Peronlarda yağmurdan ve güneşten koruyan çatı.", StationTier.Large, 20000f, 2),
            new StationUpgrade(Kids, "Çocuk oyun alanı", "Kaydırak, top havuzu ve renkli bloklar.", StationTier.Large, 12000f, 1),
            new StationUpgrade(Shop, "Hediyelik eşya dükkânı", "Şehrin simgesini taşıyan hatıra dükkânı.", StationTier.Large, 15000f, 1),
            new StationUpgrade(Vip, "VIP bekleme salonu", "Cam bölmeli, kanepeli ve halılı ayrıcalıklı salon.", StationTier.Metro, 30000f, 3),
            new StationUpgrade(Fountain, "Süs havuzu", "Giriş meydanında fıskiyeli havuz.", StationTier.Metro, 18000f, 1),
            new StationUpgrade(Clock, "Saat kulesi", "Uzaktan görünen terminal saati.", StationTier.Metro, 25000f, 1),
        };

        static readonly string[] TierNames = { "Küçük Durak", "Otogar", "Büyük Otogar", "Metropol Terminali" };
        static readonly float[] CostMultipliers = { 1f, 1.8f, 3f, 5f };
        static readonly int[] BayCounts = { 2, 3, 5, 7 };
        static readonly int[] MaxPeople = { 10, 22, 36, 56 };
        /// <summary>Durağın "tam dolu" sayıldığı günlük yolcu (popülerlikteki trafik payı bu değerde tamamlanır).</summary>
        static readonly float[] BusyPassengers = { 150f, 500f, 1200f, 3000f };

        public static StationUpgrade Get(string id)
        {
            foreach (var upgrade in Upgrades)
                if (upgrade.Id == id) return upgrade;
            return null;
        }

        public static StationTier TierOf(CityData city) => city == null ? StationTier.Stop : (StationTier)(3 - Mathf.Clamp(city.MapTier, 0, 3));

        public static string TierName(StationTier tier) => TierNames[(int)tier];

        public static float CostMultiplier(StationTier tier) => CostMultipliers[(int)tier];

        /// <summary>Peron sayısı (durağın içinde park eden otobüsler için).</summary>
        public static int Bays(StationTier tier) => BayCounts[(int)tier];

        /// <summary>Durak en popüler olduğunda içerideki yolcu sayısı.</summary>
        public static int People(StationTier tier) => MaxPeople[(int)tier];

        public static float BusyPassengersPerDay(StationTier tier) => BusyPassengers[(int)tier];

        public static bool IsAvailable(StationUpgrade upgrade, StationTier tier) => upgrade != null && tier >= upgrade.MinTier;

        /// <summary>Kademedeki tüm geliştirmeler yapıldığında konfor puanı.</summary>
        public static int MaxComfort(StationTier tier)
        {
            int total = 0;
            foreach (var upgrade in Upgrades)
                if (IsAvailable(upgrade, tier)) total += upgrade.Comfort;
            return total;
        }

        public static int AvailableCount(StationTier tier)
        {
            int count = 0;
            foreach (var upgrade in Upgrades)
                if (IsAvailable(upgrade, tier)) count++;
            return count;
        }
    }
}
