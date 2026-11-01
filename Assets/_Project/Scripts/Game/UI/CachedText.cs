using System;

namespace BusTycoon.Game
{
    /// <summary>
    /// Her karede çizilen ama seyrek değişen metin (kasa, tarih, seviye…): yalnız değer değişince yeniden biçimlenir.
    /// Tarayıcıda kare başına üretilen çöp (GC) azalır; çöp toplama takılmaları seyrekleşir. Biçimleyici yakalama
    /// yapmayan bir lambda olmalı (derleyici onu bir kez üretir, her çağrıda yeni nesne oluşmaz).
    /// </summary>
    public sealed class CachedText
    {
        double a = double.NaN, b = double.NaN;
        string text = string.Empty;

        public string Get(double value, Func<double, string> format)
        {
            if (value.Equals(a)) return text;
            a = value;
            text = format(value);
            return text;
        }

        public string Get(double first, double second, Func<double, double, string> format)
        {
            if (first.Equals(a) && second.Equals(b)) return text;
            a = first;
            b = second;
            text = format(first, second);
            return text;
        }
    }
}
