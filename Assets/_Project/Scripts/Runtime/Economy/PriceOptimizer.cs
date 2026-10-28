using System;

namespace BusTycoon.Economy
{
    /// <summary>
    /// Tek değişkenli kâr fonksiyonunun maksimumunu bulur: önce kaba tarama (yerel tepeleri kaçırmamak için),
    /// sonra en iyi örneğin etrafında altın oran araması. Kapasite, şoför sayısı gibi kırılmalar yüzünden
    /// kâr eğrisi her zaman pürüzsüz olmadığından analitik formül yerine sayısal yöntem kullanılır.
    /// </summary>
    public static class PriceOptimizer
    {
        const float InvPhi = 0.618034f;

        public static float FindBestPrice(Func<float, float> profitAtPrice, float minPrice, float maxPrice,
            int coarseSamples = 32, int refineIterations = 24)
        {
            if (profitAtPrice == null) throw new ArgumentNullException(nameof(profitAtPrice));
            if (maxPrice <= minPrice) return minPrice;
            coarseSamples = Math.Max(2, coarseSamples);

            float step = (maxPrice - minPrice) / coarseSamples;
            int bestIndex = 0;
            float bestValue = float.NegativeInfinity;
            for (int i = 0; i <= coarseSamples; i++)
            {
                float value = profitAtPrice(minPrice + step * i);
                if (value > bestValue)
                {
                    bestValue = value;
                    bestIndex = i;
                }
            }

            float lo = minPrice + step * Math.Max(0, bestIndex - 1);
            float hi = minPrice + step * Math.Min(coarseSamples, bestIndex + 1);
            float c = hi - InvPhi * (hi - lo);
            float d = lo + InvPhi * (hi - lo);
            float fc = profitAtPrice(c);
            float fd = profitAtPrice(d);

            for (int i = 0; i < refineIterations; i++)
            {
                if (fc > fd)
                {
                    hi = d; d = c; fd = fc;
                    c = hi - InvPhi * (hi - lo);
                    fc = profitAtPrice(c);
                }
                else
                {
                    lo = c; c = d; fc = fd;
                    d = lo + InvPhi * (hi - lo);
                    fd = profitAtPrice(d);
                }
            }

            float refined = 0.5f * (lo + hi);
            return profitAtPrice(refined) >= bestValue ? refined : minPrice + step * bestIndex;
        }
    }
}
