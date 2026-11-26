using System;
using System.Globalization;

namespace BusTycoon.Game
{
    /// <summary>Oyun arayüzünde tutarlı sayı / para biçimi (Türkçe gruplama: 12.345, kuruş: 22,50).</summary>
    public static class Fmt
    {
        static readonly CultureInfo Culture = CreateCulture();

        public static string Symbol => BusTycoon.Core.Money.Symbol;

        static CultureInfo CreateCulture()
        {
            try { return CultureInfo.GetCultureInfo("tr-TR"); }
            catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
        }

        /// <summary>Türkçe büyük harf: i → İ, ı → I (ToUpperInvariant "i"yi noktasız "I" yapar).</summary>
        public static string Upper(string text) =>
            string.IsNullOrEmpty(text) ? string.Empty : text.Replace('i', 'İ').Replace('ı', 'I').ToUpperInvariant();

        // Para: Türk lirası (BusTycoon.Core.Money; iç birim × 40)
        public static string Money(double value) => BusTycoon.Core.Money.Format(value);

        public static string Signed(double value) => BusTycoon.Core.Money.Signed(value);

        /// <summary>"₺1,25 Mn", "₺48,2 B" (bin), "₺1,2 Mr" (milyar).</summary>
        public static string Compact(double value) => BusTycoon.Core.Money.Compact(value);

        public static string Price(double value) => BusTycoon.Core.Money.Price(value);

        public static string Pct(float ratio01) => "%" + (ratio01 * 100f).ToString("0", Culture);

        public static string Num(double value) => Math.Round(value).ToString("N0", Culture);

        public static string Km(float km) => Num(km) + " km";

        public static string Hours(float hours)
        {
            int h = (int)hours;
            int m = (int)Math.Round((hours - h) * 60f);
            if (m == 60) { h++; m = 0; }
            return m == 0 ? $"{h} sa" : $"{h} sa {m} dk";
        }

        public static string Population(long population) =>
            population >= 1_000_000 ? (population / 1_000_000.0).ToString("0.#", Culture) + " milyon"
            : Num(population);
    }
}
