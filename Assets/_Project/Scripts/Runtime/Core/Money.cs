using System;
using System.Globalization;

namespace BusTycoon.Core
{
    /// <summary>
    /// Para birimi: Türk lirası. Oyunun iç hesapları "birim" cinsinden (kayıtlar, ekonomi ayarları); ekranda her tutar
    /// <see cref="LiraPerUnit"/> ile çarpılıp ₺ olarak yazılır. Böylece eski kayıtlar ve denge aynı kalır, fiyatlar
    /// gerçekçi görünür (dolmuş ~₺880.000, dolmuş bileti ~₺40).
    /// </summary>
    public static class Money
    {
        public const double LiraPerUnit = 40.0;
        public const string Symbol = "₺";

        static readonly CultureInfo Culture = CreateCulture();

        static CultureInfo CreateCulture()
        {
            try { return CultureInfo.GetCultureInfo("tr-TR"); }
            catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
        }

        public static double ToLira(double units) => units * LiraPerUnit;

        /// <summary>Tam lira: 1.250 → "₺50.000"; eksi: "-₺800".</summary>
        public static string Format(double units)
        {
            double lira = Math.Round(units * LiraPerUnit);
            return (lira < -0.5 ? "-" : "") + Symbol + Math.Abs(lira).ToString("N0", Culture);
        }

        public static string Signed(double units) => (units >= 0 ? "+" : "") + Format(units);

        /// <summary>Dar yerler için: "₺1,25 Mn", "₺48,2 B" (bin), "₺1,2 Mr" (milyar).</summary>
        public static string Compact(double units)
        {
            double lira = units * LiraPerUnit;
            double abs = Math.Abs(lira);
            string sign = lira < 0 ? "-" : "";
            if (abs >= 1_000_000_000) return sign + Symbol + (abs / 1_000_000_000).ToString("0.##", Culture) + " Mr";
            if (abs >= 1_000_000) return sign + Symbol + (abs / 1_000_000).ToString("0.##", Culture) + " Mn";
            if (abs >= 10_000) return sign + Symbol + (abs / 1_000).ToString("0.#", Culture) + " B";
            return Format(units);
        }

        /// <summary>Bilet fiyatı: ₺10'dan küçükse kuruşlu ("₺7,50"), değilse tam lira ("₺39").</summary>
        public static string Price(double units)
        {
            double lira = units * LiraPerUnit;
            return Symbol + (Math.Abs(lira) < 10 ? lira.ToString("N2", Culture) : Math.Round(lira).ToString("N0", Culture));
        }
    }
}
