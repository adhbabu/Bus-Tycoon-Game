using System.Collections.Generic;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>Kısa süreli bildirimler (toast) ve kalıcı olay günlüğü.</summary>
    public sealed class Notifications
    {
        public struct Entry
        {
            public string Text;
            public Color Color;
            public int Day;
            public float Time;
        }

        const int LogLimit = 100;
        readonly List<Entry> log = new List<Entry>();

        public IReadOnlyList<Entry> Log => log;

        public void Push(string text, Color color, int day)
        {
            log.Add(new Entry { Text = text, Color = color, Day = day, Time = UnityEngine.Time.unscaledTime });
            if (log.Count > LogLimit) log.RemoveAt(0);
        }

        /// <summary>Son <paramref name="lifetime"/> saniyedeki en yeni <paramref name="max"/> bildirim (yeniden eskiye).</summary>
        public void GetActive(float lifetime, int max, List<Entry> results)
        {
            results.Clear();
            float now = UnityEngine.Time.unscaledTime;
            for (int i = log.Count - 1; i >= 0 && results.Count < max; i--)
            {
                if (now - log[i].Time > lifetime) break;
                results.Add(log[i]);
            }
        }

        public void Clear() => log.Clear();
    }
}
