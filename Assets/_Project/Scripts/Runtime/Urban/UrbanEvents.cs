using System.Collections.Generic;
using BusTycoon.Core;
using UnityEngine;

namespace BusTycoon.Urban
{
    /// <summary>
    /// Önemli günler: bayramda camilere, maç gününde stadyumlara, yazın turistik yerlere yolcu akını. Oyun takvimine
    /// bağlı (360 günlük yıl, 30 günlük ay, haftanın 7. günü Pazar). Etkisi dolmuş hattının yakınındaki mekânların ününe
    /// göre: ünlü bir stadyumun yanından geçen hat maç günü belirgin çok yolcu taşır, uzaktan geçen hat etkilenmez.
    /// Saf C#: hem ekonomi hem arayüz aynı takvimi okur.
    /// </summary>
    public static class UrbanEvents
    {
        public const int KindCount = 9; // LandmarkKind sayısı

        /// <summary>Bir etkinlik: hangi mekân türüne ne kadar ek yolcu (1 = ünlü mekânın yanındaki hatta +%50).</summary>
        public sealed class Event
        {
            public string Id;
            public string Title;
            public string Detail;
            /// <summary>Arayüz simgesi (Material Symbols adı).</summary>
            public string Icon;
            /// <summary>Her hafta (maç günü) ya da mevsim boyu (yaz) süren etkinlik: her gün ayrıca duyurulmaz.</summary>
            public bool Recurring;
            public readonly float[] Boost = new float[KindCount];

            internal Event Set(LandmarkKind kind, float boost)
            {
                Boost[(int)kind] = boost;
                return this;
            }
        }

        static readonly Event Ramadan = new Event { Id = "ramazan", Title = "Ramazan Bayramı", Detail = "Camilere ve çarşılara yolcu akını", Icon = "mosque" }
            .Set(LandmarkKind.Mosque, 1.4f).Set(LandmarkKind.Bazaar, 0.8f).Set(LandmarkKind.Park, 0.4f);
        static readonly Event Sacrifice = new Event { Id = "kurban", Title = "Kurban Bayramı", Detail = "Camilere yolcu akını", Icon = "mosque" }
            .Set(LandmarkKind.Mosque, 1.4f).Set(LandmarkKind.Park, 0.4f);
        static readonly Event Match = new Event { Id = "mac", Title = "Maç günü", Detail = "Stadyumlara taraftar akını", Icon = "stadium", Recurring = true }
            .Set(LandmarkKind.Stadium, 2f);
        static readonly Event Summer = new Event { Id = "yaz", Title = "Yaz sezonu", Detail = "Turistik yerler kalabalık, üniversiteler tatilde", Icon = "sunny", Recurring = true }
            .Set(LandmarkKind.Palace, 0.6f).Set(LandmarkKind.Museum, 0.6f).Set(LandmarkKind.Monument, 0.5f)
            .Set(LandmarkKind.Bazaar, 0.4f).Set(LandmarkKind.Church, 0.4f).Set(LandmarkKind.Park, 0.3f).Set(LandmarkKind.University, -0.8f);
        static readonly Event Republic = new Event { Id = "cumhuriyet", Title = "Cumhuriyet Bayramı", Detail = "Meydan ve anıtlarda kutlama", Icon = "flag" }
            .Set(LandmarkKind.Monument, 1.4f).Set(LandmarkKind.Park, 0.8f).Set(LandmarkKind.Palace, 0.4f);
        static readonly Event NewYear = new Event { Id = "yilbasi", Title = "Yılbaşı", Detail = "Meydanlar ve çarşılar kalabalık", Icon = "star" }
            .Set(LandmarkKind.Monument, 1f).Set(LandmarkKind.Bazaar, 1f).Set(LandmarkKind.Park, 0.6f);
        static readonly Event Semester = new Event { Id = "donem", Title = "Okul dönemi başlıyor", Detail = "Üniversitelere öğrenci akını", Icon = "school" }
            .Set(LandmarkKind.University, 1.6f);
        static readonly Event Museums = new Event { Id = "muze", Title = "Müzeler Haftası", Detail = "Müze ve saraylar ücretsiz", Icon = "museum" }
            .Set(LandmarkKind.Museum, 1.4f).Set(LandmarkKind.Palace, 1f);

        /// <summary>Takvimdeki bütün etkinlikler (planlayıcıda "bu hat hangi günlerde kazanır" listesi için).</summary>
        public static readonly IReadOnlyList<Event> All = new[] { Match, Ramadan, Sacrifice, Summer, Republic, NewYear, Semester, Museums };

        /// <summary>Ayın günü (1..30) ve ay (1..12).</summary>
        static void DateOf(int day, out int month, out int dayOfMonth)
        {
            int inYear = ((day % SimulationClock.DaysPerYear) + SimulationClock.DaysPerYear) % SimulationClock.DaysPerYear;
            month = inYear / SimulationClock.DaysPerMonth + 1;
            dayOfMonth = inYear % SimulationClock.DaysPerMonth + 1;
        }

        /// <summary>Haftanın günü: 0 Pazartesi … 6 Pazar (oyunun ilk günü Pazartesi).</summary>
        public static int Weekday(int day) => ((day % 7) + 7) % 7;

        /// <summary>O günkü etkinlikler (birden çok olabilir: yazın maç günü gibi).</summary>
        public static void ActiveOn(int day, List<Event> result)
        {
            result.Clear();
            DateOf(day, out int month, out int dom);
            if (month == 4 && dom >= 10 && dom <= 12) result.Add(Ramadan);
            if (month == 6 && dom >= 17 && dom <= 20) result.Add(Sacrifice);
            if (month == 10 && dom == 29) result.Add(Republic);
            if (month == 12 && dom == 30) result.Add(NewYear);
            if (month == 9 && dom >= 15 && dom <= 21) result.Add(Semester);
            if (month == 5 && dom >= 18 && dom <= 24) result.Add(Museums);
            if (month >= 6 && month <= 8) result.Add(Summer);
            // Futbol sezonu: Haziran–Temmuz dışında her Pazar
            if (Weekday(day) == 6 && month != 6 && month != 7) result.Add(Match);
        }

        /// <summary>
        /// Hattın o günkü yolcu çarpanı: 1 + Σ(tür ünü × etkinlik etkisi × 0,5). <paramref name="kindFame"/> hattın yakınındaki
        /// mekânların türlere göre ün toplamı (<see cref="KindFame"/>). Etkinlik yoksa ya da hat mekân geçmiyorsa 1.
        /// </summary>
        public static float DemandFactor(int day, float[] kindFame, List<Event> scratch)
        {
            if (kindFame == null || kindFame.Length < KindCount) return 1f;
            ActiveOn(day, scratch);
            float factor = 1f;
            foreach (Event e in scratch) factor += Factor(e, kindFame) - 1f;
            return Mathf.Clamp(factor, 0.6f, 2.5f);
        }

        /// <summary>Tek etkinliğin hatta etkisi (o gün yalnız bu etkinlik olsaydı yolcu çarpanı).</summary>
        public static float Factor(Event e, float[] kindFame)
        {
            if (e == null || kindFame == null || kindFame.Length < KindCount) return 1f;
            float extra = 0f;
            for (int k = 0; k < KindCount; k++) extra += Mathf.Min(1.5f, kindFame[k]) * e.Boost[k] * 0.5f;
            return Mathf.Clamp(1f + extra, 0.6f, 2.5f);
        }

        /// <summary>Etkinlik o gün mü başlıyor (dün yoktu): çok günlü bayram bir kez duyurulur.</summary>
        public static bool StartsOn(Event e, int day, List<Event> scratch)
        {
            ActiveOn(day - 1, scratch);
            return !scratch.Contains(e);
        }

        /// <summary>Hattın yakınındaki mekânların türlere göre ün toplamı (kayda yazılır; etkinlik etkisi buna göre).</summary>
        public static float[] KindFame(IReadOnlyList<UrbanLandmark> landmarks)
        {
            var result = new float[KindCount];
            if (landmarks == null) return result;
            foreach (UrbanLandmark landmark in landmarks)
            {
                int k = (int)landmark.Kind;
                if (k >= 0 && k < KindCount) result[k] += landmark.Fame;
            }
            return result;
        }

        /// <summary>Bugünden sonraki ilk etkinlik günü (en çok <paramref name="horizon"/> gün ileri); yoksa -1.</summary>
        public static int NextEventDay(int today, int horizon, List<Event> scratch, bool skipWeekly = true)
        {
            for (int d = today + 1; d <= today + horizon; d++)
            {
                ActiveOn(d, scratch);
                foreach (Event e in scratch)
                    if (!skipWeekly || !e.Recurring) return d;
            }
            return -1;
        }
    }
}
