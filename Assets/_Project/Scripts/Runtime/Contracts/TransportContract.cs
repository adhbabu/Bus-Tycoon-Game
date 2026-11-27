using System;

namespace BusTycoon.Contracts
{
    /// <summary>İhale müşterisinin türü (servis işini veren kurum).</summary>
    public enum ClientKind
    {
        School = 0,
        Factory = 1,
        Office = 2,
        Hotel = 3,
        Hospital = 4
    }

    /// <summary>
    /// Taşımacılık işi (ihale): okul, fabrika, şirket, otel ya da hastane servis aracı ister. Teklif olarak gelir
    /// (<c>CompanyState.contractOffers</c>); oyuncu kabul edince sözleşme olur (<c>contracts</c>), süresi bitince
    /// geçmişe yazılır. Ödeme gün içinde saniye saniye işlenir (hatlar gibi), çalışan servis aracı sayısıyla orantılı.
    /// Servis araçları <c>BusInstance.assignedContractId</c> ile bağlanır.
    /// </summary>
    [Serializable]
    public sealed class TransportContract
    {
        public string contractId;
        public string clientName;
        public ClientKind kind;
        public string cityId;

        /// <summary>İstenen servis aracı sayısı.</summary>
        public int vehicles;
        /// <summary>Araç başına en az koltuk.</summary>
        public int minSeats;
        /// <summary>En az konfor (0..1). VIP işlerde 0.8: yalnız lüks servis araçları uyar.</summary>
        public float minComfort;
        /// <summary>Sözleşme süresi (oyun günü).</summary>
        public int durationDays;
        /// <summary>Bütün araçlar çalışınca günlük ödeme (iç birim, gelir katsayısı dahil).</summary>
        public float dailyPay;
        /// <summary>Müşterinin büyüklüğü (0..3): memnuniyet ve şirket seviyesiyle büyür.</summary>
        public int tier;

        /// <summary>Teklifin son günü (geçince geri çekilir).</summary>
        public int expiresDay;
        /// <summary>"Daha fazla iste" sayısı (en çok 2).</summary>
        public int asks;

        /// <summary>Tek yön mesafe (km): mahalle ile müşteri arası.</summary>
        public float km;
        /// <summary>Servisin toplandığı mahalle (şehir haritasındaki durak adı).</summary>
        public string homeName;
        /// <summary>Şehir haritasında konumlar (metre). <see cref="hasSite"/> false ise harita açılınca yerleştirilir.</summary>
        public float clientX, clientY, homeX, homeY;
        public bool hasSite;

        // Sözleşme (kabul edildikten sonra)
        public int startDay;
        public int daysLeft;
        /// <summary>Müşterinin bu işteki memnuniyeti (0..1), her gün güncellenir.</summary>
        public float satisfaction = 0.75f;
        /// <summary>Bu işten şimdiye kadarki gelir (iç birim).</summary>
        public double earned;
        /// <summary>Bugünün işlenen kesri (0..1).</summary>
        public float booked;
        /// <summary>Bugün çalışan araçların kapsama oranının ağırlıklı toplamı (gün sonunda memnuniyete işlenir).</summary>
        public float coverageToday;
        /// <summary>Bitiş durumu (geçmiş kayıtları): 0 sürüyor, 1 tamamlandı, 2 müşteri feshetti, 3 oyuncu bıraktı.</summary>
        public int outcome;

        public bool IsVip => minComfort >= 0.75f;
        public int DaysDone => Math.Max(0, durationDays - daysLeft);
    }

    /// <summary>Haritadaki yer (oyun katmanı şehir haritasından seçer): müşteri, mahalle ve yol mesafesi.</summary>
    public sealed class ContractSite
    {
        public float ClientX, ClientY, HomeX, HomeY;
        public string HomeName;
        public float Km;
    }

    public enum NegotiationResult
    {
        Accepted,
        Withdrawn,
        NotAllowed
    }
}
