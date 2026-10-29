using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BusTycoon.Multiplayer
{
    /// <summary>Çok oyunculu yarış modu: Blitz hızlı ve kısa, Normal oyunun kendi akışı.</summary>
    public enum MatchMode { Blitz, Normal }

    /// <summary>Odanın durumu (yerel oyuncunun gözünden).</summary>
    public enum RoomPhase { Idle, Connecting, Lobby, Playing, Ended, Closed }

    /// <summary>Modların kuralları: hedef kasa ve varsayılan oyun hızı.</summary>
    public static class MatchRules
    {
        /// <summary>Farklı sürümler aynı odada oynamasın.</summary>
        public const string ProtocolVersion = "1";
        public const int MaxPlayers = 8;
        public const int CodeLength = 5;
        /// <summary>Oda kodunda karıştırılmayan karakterler (0/O, 1/I yok).</summary>
        const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static double TargetCash(MatchMode mode) => mode == MatchMode.Blitz ? 1_000_000 : 10_000_000;

        /// <summary>Oyun hızı (SimulationClock.SpeedSteps'ten): Blitz 2x, Normal 1x.</summary>
        public static int DefaultSpeed(MatchMode mode) => mode == MatchMode.Blitz ? 2 : 1;

        public static string Name(MatchMode mode) => mode == MatchMode.Blitz ? "Blitz" : "Normal";

        public static string NewCode(Random random)
        {
            var code = new StringBuilder(CodeLength);
            for (int i = 0; i < CodeLength; i++) code.Append(CodeAlphabet[random.Next(CodeAlphabet.Length)]);
            return code.ToString();
        }

        /// <summary>Kullanıcının yazdığı kodu düzeltir: büyük harf, yalnız koddaki karakterler (boşluk, tire atılır).</summary>
        public static string NormalizeCode(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var code = new StringBuilder();
            foreach (char c in text.ToUpperInvariant())
                if (CodeAlphabet.IndexOf(c) >= 0 && code.Length < CodeLength) code.Append(c);
            return code.ToString();
        }
    }

    /// <summary>Odadaki bir oyuncu ve yarıştaki son durumu.</summary>
    public sealed class RoomPlayer
    {
        public string Id;
        public string Name;
        public string CityId;
        public double Cash;
        public int Day;
        public int Routes;
        public int Buses;
        public bool Connected = true;
        public bool Bankrupt;
        public bool IsHost;
    }

    /// <summary>Oyuncunun gün sonunda odaya bildirdiği durum.</summary>
    public struct PlayerStats
    {
        public int Day;
        public double Cash;
        public int Routes;
        public int Buses;
        public bool Bankrupt;
    }

    /// <summary>
    /// Ağ mesajları: "TÜR|alan|alan…" biçiminde düz metin. Ayraçlar (| ; ~) adlardan temizlenir; sayılar kültürden
    /// bağımsız yazılır. JSON yerine bu biçim Unity dışında da (testlerde) aynı çalışır.
    /// </summary>
    public static class RoomMessage
    {
        public const char Separator = '|';
        const char ListSeparator = ';', FieldSeparator = '~';

        public static string Pack(params string[] fields) => string.Join(Separator.ToString(), fields);

        public static string[] Unpack(string message) => (message ?? string.Empty).Split(Separator);

        public static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        public static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

        public static double ParseDouble(string text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0d;

        public static int ParseInt(string text) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

        /// <summary>Oyuncu adı: ayraçsız, kontrol karaktersiz, en fazla 18 karakter.</summary>
        public static string CleanName(string name)
        {
            var clean = new StringBuilder();
            foreach (char c in name ?? string.Empty)
            {
                if (char.IsControl(c) || c == Separator || c == ListSeparator || c == FieldSeparator) continue;
                clean.Append(c);
                if (clean.Length >= 18) break;
            }
            string result = clean.ToString().Trim();
            return result.Length > 0 ? result : "Oyuncu";
        }

        public static string EncodePlayers(IEnumerable<RoomPlayer> players)
        {
            var parts = new List<string>();
            foreach (var p in players)
                parts.Add(string.Join(FieldSeparator.ToString(), p.Id, CleanName(p.Name), p.CityId ?? string.Empty, Num(p.Cash), Num(p.Day),
                    Num(p.Routes), Num(p.Buses), p.Connected ? "1" : "0", p.Bankrupt ? "1" : "0", p.IsHost ? "1" : "0"));
            return string.Join(ListSeparator.ToString(), parts);
        }

        public static List<RoomPlayer> DecodePlayers(string text)
        {
            var players = new List<RoomPlayer>();
            if (string.IsNullOrEmpty(text)) return players;
            foreach (string part in text.Split(ListSeparator))
            {
                string[] f = part.Split(FieldSeparator);
                if (f.Length < 10) continue;
                players.Add(new RoomPlayer
                {
                    Id = f[0], Name = f[1], CityId = f[2], Cash = ParseDouble(f[3]), Day = ParseInt(f[4]),
                    Routes = ParseInt(f[5]), Buses = ParseInt(f[6]), Connected = f[7] == "1", Bankrupt = f[8] == "1", IsHost = f[9] == "1"
                });
            }
            return players;
        }
    }
}
