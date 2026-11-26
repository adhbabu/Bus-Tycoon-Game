using System;
using System.Globalization;
using BusTycoon.Core;

namespace BusTycoon.Presentation.UI
{
    /// <summary>Tüm ekranlarda tutarlı sayı / para / yüzde biçimi.</summary>
    public static class UIFormat
    {
        static readonly CultureInfo Culture = CreateCulture();

        static CultureInfo CreateCulture()
        {
            // Bazı platformlarda (ör. invariant globalization ile derlenen WebGL) kültür verisi bulunmayabilir.
            try { return CultureInfo.GetCultureInfo("tr-TR"); }
            catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
        }

        static string Symbol => BusTycoon.Core.Money.Symbol;

        /// <summary>12.345 → "$12.345", -800 → "-$800"</summary>
        public static string Money(double value) => BusTycoon.Core.Money.Format(value);

        /// <summary>Bilet fiyatları için kuruşlu biçim: "$23,50".</summary>
        public static string Price(double value) => BusTycoon.Core.Money.Price(value);

        /// <summary>1.250.000 → "$1,3 Mn", 12.500 → "$12,5 B" (bin); HUD gibi dar alanlar için.</summary>
        public static string MoneyCompact(double value) => BusTycoon.Core.Money.Compact(value);

        /// <summary>Değişim göstergesi: "+$120" / "-$45".</summary>
        public static string SignedMoney(double value) => (value >= 0 ? "+" : "") + Money(value);

        public static string Percent(float ratio01) => "%" + (ratio01 * 100f).ToString("0", Culture);

        public static string Number(double value) => value.ToString("N0", Culture);

        public static string Hours(float hours)
        {
            int h = (int)hours;
            int m = (int)Math.Round((hours - h) * 60f);
            if (m == 60) { h++; m = 0; }
            return m == 0 ? $"{h} sa" : $"{h} sa {m} dk";
        }

        /// <summary>0..1 konfor → "★★★☆☆"</summary>
        public static string Stars(float value01, int max = 5)
        {
            int filled = UnityEngine.Mathf.RoundToInt(UnityEngine.Mathf.Clamp01(value01) * max);
            return new string('★', filled) + new string('☆', max - filled);
        }
    }
}
