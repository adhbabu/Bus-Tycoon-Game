using System;

namespace BusTycoon.Core
{
    /// <summary>
    /// Oyun saatini yönetir. Simülasyon "gün" bazlı tick'lerle ilerler:
    /// her gün sonunda tüm hatlar tek seferde hesaplanır (ekonomi deterministik ve hızlı kalır).
    /// Görsel otobüs animasyonları bu saatten bağımsız, yalnızca kozmetiktir.
    /// </summary>
    public sealed class SimulationClock
    {
        public const int DaysPerMonth = 30;
        public const int DaysPerYear = 360;
        public static readonly int[] SpeedSteps = { 0, 1, 2, 4 };

        /// <summary>Biten günün numarasıyla çağrılır. Ekonomi simülasyonu burada çalışır.</summary>
        public event Action<int> DayEnded;
        /// <summary>Biten ayın numarasıyla (1'den başlar) çağrılır. Aylık rapor için.</summary>
        public event Action<int> MonthEnded;
        public event Action<int> SpeedChanged;

        readonly float secondsPerDay;
        float accumulator;
        int speedBeforePause = 1;

        public SimulationClock(int startDay, float secondsPerDay)
        {
            CurrentDay = Math.Max(0, startDay);
            this.secondsPerDay = Math.Max(0.05f, secondsPerDay);
            Speed = 1;
        }

        public int CurrentDay { get; private set; }
        public int Speed { get; private set; }
        public bool IsPaused => Speed == 0;
        public float DayProgress01 => accumulator / secondsPerDay;

        public void SetSpeed(int speed)
        {
            speed = Math.Max(0, speed);
            if (speed == Speed) return;
            if (speed == 0) speedBeforePause = Speed;
            Speed = speed;
            SpeedChanged?.Invoke(Speed);
        }

        public void TogglePause() => SetSpeed(IsPaused ? Math.Max(1, speedBeforePause) : 0);

        /// <summary>Her karede (gerçek zamanlı delta ile) çağrılır.</summary>
        public void Tick(float deltaSeconds)
        {
            if (Speed == 0 || deltaSeconds <= 0f) return;

            accumulator += deltaSeconds * Speed;

            // Uzun bir takılmadan (alt-tab, yükleme) sonra tek karede onlarca gün simüle etmeyelim.
            const int maxDaysPerTick = 4;
            int processed = 0;
            while (accumulator >= secondsPerDay && processed < maxDaysPerTick)
            {
                accumulator -= secondsPerDay;
                processed++;
                AdvanceOneDay();
            }

            if (accumulator >= secondsPerDay) accumulator = 0f;
        }

        /// <summary>Saati beklemeden bir gün ilerletir (debug / "günü bitir" butonu).</summary>
        public void AdvanceOneDay()
        {
            int endedDay = CurrentDay;
            DayEnded?.Invoke(endedDay);
            CurrentDay++;
            if (CurrentDay % DaysPerMonth == 0) MonthEnded?.Invoke(CurrentDay / DaysPerMonth);
        }

        public static string FormatDate(int day)
        {
            int year = day / DaysPerYear + 1;
            int month = day % DaysPerYear / DaysPerMonth + 1;
            int dayOfMonth = day % DaysPerMonth + 1;
            return $"Yıl {year} · Ay {month} · Gün {dayOfMonth}";
        }
    }
}
