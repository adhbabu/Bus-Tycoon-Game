using System;
using System.Globalization;

namespace BusTycoon.Game
{
    /// <summary>Oyun arayüzünde tutarlı sayı / para biçimi (Türkçe gruplama: 12.345, kuruş: 22,50).</summary>
    public static class Fmt
    {
        static readonly CultureInfo Culture = CreateCulture();

        public static string Symbol = "$";

        static CultureInfo CreateCulture()
        {
            try { return CultureInfo.GetCultureInfo("tr-TR"); }
            catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
        }

        public static string Money(double value) =>
            (value < -0.5 ? "-" : "") + Symbol + Math.Abs(Math.Round(value)).ToString("N0", Culture);

        public static string Signed(double value) => (value >= 0 ? "+" : "") + Money(value);

        /// <summary>1.250.000 → "$1,25 Mn"; 48.200 → "$48,2 B" (bin).</summary>
        public static string Compact(double value)
        {
            double abs = Math.Abs(value);
            string sign = value < 0 ? "-" : "";
            if (abs >= 1_000_000) return sign + Symbol + (abs / 1_000_000).ToString("0.##", Culture) + " Mn";
            if (abs >= 10_000) return sign + Symbol + (abs / 1_000).ToString("0.#", Culture) + " B";
            return Money(value);
        }

        public static string Price(double value) => Symbol + value.ToString("N2", Culture);

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
