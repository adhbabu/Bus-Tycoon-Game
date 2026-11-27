using System;

namespace BusTycoon.Contracts
{
    /// <summary>
    /// İhale müşterilerinin uydurma adları (gerçek kurum adı kullanılmaz): bir ad sözcüğü + kurum türü.
    /// Büyük müşteriler (kademe 2–3) "Holding", "Organize Sanayi" gibi daha büyük adlar alır.
    /// </summary>
    public static class ContractNames
    {
        static readonly string[] Roots =
        {
            "Kardelen", "Çınar", "Defne", "Meltem", "Sedef", "Ilgaz", "Lale", "Akasya", "Mercan", "Yakamoz", "Papatya",
            "Erguvan", "Begonya", "Sardunya", "Kumru", "Turna", "Zümrüt", "Safir", "Kehribar", "Gümüşsu", "Kartopu", "Nergis",
            "Ihlamur", "Söğüt", "Menekşe", "Badem", "Ardıç", "Gelincik", "Yaprak", "Dere", "Poyraz", "Lodos", "Kuzgun", "Simge",
            "Tomurcuk", "Mavişehir", "Gökçe", "Yamaç", "Tepeüstü", "Sahilyolu"
        };

        static readonly string[][] Small =
        {
            new[] { "Koleji", "İlkokulu", "Ortaokulu", "Anaokulu" },              // okul
            new[] { "Tekstil", "Gıda", "Ambalaj", "Mobilya Atölyesi" },          // fabrika
            new[] { "Yazılım", "Lojistik", "Muhasebe", "Reklam Ajansı" },        // şirket
            new[] { "Otel", "Butik Otel", "Pansiyon", "Konuk Evi" },             // otel
            new[] { "Tıp Merkezi", "Polikliniği", "Diş Hastanesi", "Sağlık Ocağı" } // hastane
        };

        static readonly string[][] Large =
        {
            new[] { "Anadolu Lisesi", "Fen Lisesi", "Kampüsü", "Eğitim Kurumları" },
            new[] { "Otomotiv Yan Sanayi", "Organize Sanayi", "Beyaz Eşya Fabrikası", "Kablo Fabrikası", "Tekstil Fabrikası" },
            new[] { "Holding", "Plaza", "Teknoloji Merkezi", "Sigorta Genel Müdürlüğü" },
            new[] { "Resort", "Kongre Oteli", "Holding Yönetim", "Termal Otel" },
            new[] { "Hastanesi", "Şehir Hastanesi", "Üniversite Hastanesi", "Tıp Fakültesi" }
        };

        /// <summary>Türe ve büyüklüğe uygun ad; <paramref name="taken"/> doluysa başka ad dener.</summary>
        public static string Make(ClientKind kind, int tier, Random rng, Func<string, bool> taken)
        {
            string[][] table = tier >= 2 ? Large : Small;
            string[] suffixes = table[Math.Max(0, Math.Min(table.Length - 1, (int)kind))];
            string name = null;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                name = Roots[rng.Next(Roots.Length)] + " " + suffixes[rng.Next(suffixes.Length)];
                if (taken == null || !taken(name)) return name;
            }
            return name;
        }

        public static string KindName(ClientKind kind) => kind switch
        {
            ClientKind.School => "Okul",
            ClientKind.Factory => "Fabrika",
            ClientKind.Office => "Şirket",
            ClientKind.Hotel => "Otel",
            _ => "Hastane"
        };

        /// <summary>Arayüz simgesi (Material Symbols adı).</summary>
        public static string KindSymbol(ClientKind kind) => kind switch
        {
            ClientKind.School => "school",
            ClientKind.Factory => "factory",
            ClientKind.Office => "business_center",
            ClientKind.Hotel => "apartment",
            _ => "local_hospital"
        };

        /// <summary>İşin saatleri (açıklama): okul sabah/öğle, fabrika vardiya, şirket mesai.</summary>
        public static string Schedule(ClientKind kind) => kind switch
        {
            ClientKind.School => "07:30 toplama · 15:30 dönüş",
            ClientKind.Factory => "Vardiya: 07:00 ve 19:00",
            ClientKind.Office => "08:30 mesai · 18:00 dönüş",
            ClientKind.Hotel => "Havalimanı ve şehir transferleri",
            _ => "Vardiya: 08:00 ve 20:00"
        };
    }
}
