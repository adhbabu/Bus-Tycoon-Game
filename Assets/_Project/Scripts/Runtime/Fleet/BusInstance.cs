using System;
using BusTycoon.Data;

namespace BusTycoon.Fleet
{
    /// <summary>
    /// Oyuncunun sahip olduğu tek bir otobüs (runtime durumu). Kayıt dosyasına JSON olarak yazılır;
    /// tasarım verisine (<see cref="BusModelData"/>) yalnızca <see cref="modelId"/> ile bağlanır.
    /// </summary>
    [Serializable]
    public sealed class BusInstance
    {
        public string busId;
        public string modelId;
        public string nickname;
        public int purchaseDay;
        public float purchasePrice;
        public double odometerKm;
        /// <summary>1 = yeni, 0 = hurda. Bakım maliyetini, arıza riskini ve satış değerini etkiler.</summary>
        public float condition = 1f;
        /// <summary>Boşsa otobüs garajda (atanmamış).</summary>
        public string assignedRouteId;
        /// <summary>Arıza nedeniyle seferden çekildiği kalan gün.</summary>
        public int outOfServiceDays;

        /// <summary>Yükleme sonrası <c>FleetSystem.ResolveModels</c> ile doldurulur; kaydedilmez.</summary>
        [NonSerialized] public BusModelData Model;

        public bool IsAssigned => !string.IsNullOrEmpty(assignedRouteId);
        public bool IsAvailableToday => outOfServiceDays <= 0;

        /// <summary>
        /// Filoya eklenmeyen geçici otobüs: hat açmadan önce "bu hatta 2 × Toros 46 çalıştırsam ne olur?"
        /// tahminleri için kullanılır.
        /// </summary>
        public static BusInstance CreateVirtual(BusModelData model, float condition = 1f) => new BusInstance
        {
            busId = "VIRTUAL",
            modelId = model != null ? model.Id : null,
            Model = model,
            condition = condition
        };
    }
}
