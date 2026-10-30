using System.Collections.Generic;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Kısa süreli bildirimler (toast) ve kalıcı olay günlüğü. Sessiz kayıtlar yalnız günlüğe yazılır (eylem
    /// gerektirmeyen olaylar); aynı metin kısa süre içinde tekrar gelirse yeni kayıt açılmaz, sayısı artar ("×3").
    /// </summary>
    public sealed class Notifications
    {
        public struct Entry
        {
            public string Text;
            public Color Color;
            public int Day;
            public float Time;
            /// <summary>Yalnız günlükte; ekranda bildirim olarak görünmez.</summary>
            public bool Quiet;
            /// <summary>Art arda gelen aynı bildirimin sayısı.</summary>
            public int Count;
            /// <summary>Ekranda kalma süresi (sn): oyuncunun kendi işleminin onayı kısa, uyarılar daha uzun.</summary>
            public float Duration;

            public string Display => Count > 1 ? $"{Text}  ×{Count}" : Text;
        }

        const int LogLimit = 100;
        const float MergeSeconds = 12f;
        readonly List<Entry> log = new List<Entry>();

        public IReadOnlyList<Entry> Log => log;

        public void Push(string text, Color color, int day, bool quiet = false, float duration = 5f)
        {
            float now = UnityEngine.Time.unscaledTime;
            int last = log.Count - 1;
            if (last >= 0 && log[last].Text == text && now - log[last].Time < MergeSeconds)
            {
                Entry merged = log[last];
                merged.Count++;
                merged.Day = day;
                merged.Time = now;
                merged.Quiet = merged.Quiet && quiet;
                merged.Duration = Mathf.Max(merged.Duration, duration);
                log[last] = merged;
                return;
            }
            log.Add(new Entry { Text = text, Color = color, Day = day, Time = now, Quiet = quiet, Count = 1, Duration = duration });
            if (log.Count > LogLimit) log.RemoveAt(0);
        }

        /// <summary>Süresi dolmamış en yeni <paramref name="max"/> görünür bildirim (yeniden eskiye).</summary>
        public void GetActive(int max, List<Entry> results)
        {
            results.Clear();
            float now = UnityEngine.Time.unscaledTime;
            for (int i = log.Count - 1; i >= 0 && results.Count < max; i--)
            {
                if (now - log[i].Time > MaxDuration) break; // daha eskiler kesin bitmiştir
                if (!log[i].Quiet && now - log[i].Time <= log[i].Duration) results.Add(log[i]);
            }
        }

        const float MaxDuration = 10f;

        public void Clear() => log.Clear();
    }
}
